using System.ComponentModel;
using System.Runtime.InteropServices;
using StandbyMemoryManager.Interop;
using StandbyMemoryManager.Models;

namespace StandbyMemoryManager.Services;

public sealed class MemoryService : IDisposable
{
    private const uint ERROR_SUCCESS = 0;
    private const uint PDH_FMT_LARGE = 0x00000400;

    private readonly object _sync = new();
    private nint _query;
    private readonly List<nint> _counters = new();
    private bool _pdhReady;

    public MemoryService()
    {
        InitializePdh();
    }

    public MemorySnapshot ReadSnapshot()
    {
        var memory = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref memory))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        ulong installedBytes = memory.ullTotalPhys;
        if (NativeMethods.GetPhysicallyInstalledSystemMemory(out var installedKb))
        {
            installedBytes = installedKb * 1024;
        }

        var total = memory.ullTotalPhys;
        var available = memory.ullAvailPhys;
        var used = total > available ? total - available : 0;
        var standby = ReadStandbyBytes();

        return new MemorySnapshot(installedBytes, total, used, available, standby, DateTimeOffset.Now);
    }

    public CleanResult PurgeStandby()
    {
        var before = ReadSnapshot();

        if (!TryEnablePrivilege("SeProfileSingleProcessPrivilege", out var privilegeError))
            return new CleanResult(false, before.StandbyBytes, before.StandbyBytes, privilegeError);

        var command = NativeMethods.MemoryPurgeStandbyList;
        var status = NativeMethods.NtSetSystemInformation(
            NativeMethods.SystemMemoryListInformation,
            ref command,
            sizeof(int));

        Thread.Sleep(180);
        var after = ReadSnapshot();

        if (status != 0)
        {
            return new CleanResult(false, before.StandbyBytes, after.StandbyBytes,
                $"NtSetSystemInformation devolvió NTSTATUS 0x{status:X8}. Ejecuta la aplicación como administrador.");
        }

        return new CleanResult(true, before.StandbyBytes, after.StandbyBytes, "Standby Memory liberada.");
    }

    private void InitializePdh()
    {
        if (NativeMethods.PdhOpenQuery(null, 0, out _query) != ERROR_SUCCESS)
            return;

        var paths = new[]
        {
            @"\Memory\Standby Cache Reserve Bytes",
            @"\Memory\Standby Cache Normal Priority Bytes",
            @"\Memory\Standby Cache Core Bytes"
        };

        foreach (var path in paths)
        {
            if (NativeMethods.PdhAddEnglishCounter(_query, path, 0, out var counter) == ERROR_SUCCESS)
            {
                _counters.Add(counter);
            }
        }

        _pdhReady = _counters.Count > 0;
        if (_pdhReady)
            NativeMethods.PdhCollectQueryData(_query);
    }

    private ulong ReadStandbyBytes()
    {
        lock (_sync)
        {
            if (!_pdhReady)
                return 0;

            if (NativeMethods.PdhCollectQueryData(_query) != ERROR_SUCCESS)
                return 0;

            ulong total = 0;
            foreach (var counter in _counters)
            {
                total += ReadCounter(counter);
            }
            return total;
        }
    }

    private static ulong ReadCounter(nint counter)
    {
        if (NativeMethods.PdhGetFormattedCounterValue(counter, PDH_FMT_LARGE, out _, out var value) != ERROR_SUCCESS)
            return 0;
        return value.longValue <= 0 ? 0 : (ulong)value.longValue;
    }

    private static bool TryEnablePrivilege(string privilegeName, out string error)
    {
        error = string.Empty;
        if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY, out var token))
        {
            error = $"No se pudo abrir el token del proceso. Win32: {Marshal.GetLastWin32Error()}";
            return false;
        }

        try
        {
            if (!NativeMethods.LookupPrivilegeValue(null, privilegeName, out var luid))
            {
                error = $"No se encontró el privilegio {privilegeName}. Win32: {Marshal.GetLastWin32Error()}";
                return false;
            }

            var tp = new NativeMethods.TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new NativeMethods.LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = NativeMethods.SE_PRIVILEGE_ENABLED
                }
            };

            if (!NativeMethods.AdjustTokenPrivileges(token, false, ref tp, 0, 0, 0))
            {
                error = $"No se pudo habilitar {privilegeName}. Win32: {Marshal.GetLastWin32Error()}";
                return false;
            }

            var lastError = Marshal.GetLastWin32Error();
            if (lastError != 0)
            {
                error = $"El privilegio no está disponible para este proceso. Win32: {lastError}.";
                return false;
            }

            return true;
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    public void Dispose()
    {
        if (_query != 0)
        {
            NativeMethods.PdhCloseQuery(_query);
            _query = 0;
        }
    }
}

public sealed record CleanResult(bool Success, ulong BeforeBytes, ulong AfterBytes, string Message)
{
    public ulong ReleasedBytes => BeforeBytes > AfterBytes ? BeforeBytes - AfterBytes : 0;
    public double ReleasedGb => ReleasedBytes / 1024d / 1024d / 1024d;
}
