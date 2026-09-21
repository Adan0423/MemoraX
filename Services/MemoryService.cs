using System.ComponentModel;
using System.Runtime.InteropServices;
using Veltrixa.Interop;
using Veltrixa.Models;

namespace Veltrixa.Services;

public sealed class MemoryService : IDisposable
{
    private const uint ErrorSuccess = 0;
    private const uint PdhFmtLarge = 0x00000400;
    private const uint PdhValidData = 0;
    private const uint PdhNewData = 1;
    private static readonly object PurgeSync = new();
    private static readonly string[] StandbyCounterPaths =
    [
        @"\Memory\Standby Cache Reserve Bytes",
        @"\Memory\Standby Cache Normal Priority Bytes",
        @"\Memory\Standby Cache Core Bytes"
    ];

    private readonly object _sync = new();
    private readonly List<nint> _counters = new(3);
    private nint _query;
    private bool _disposed;
    private string? _pdhError;
    private long _retryPdhAt;
    private int _pdhFailures;
    private ulong? _installedBytes;

    public MemorySnapshot ReadSnapshot()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var memory = new NativeMethods.MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
            };

            if (!NativeMethods.GlobalMemoryStatusEx(ref memory))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!_installedBytes.HasValue && NativeMethods.GetPhysicallyInstalledSystemMemory(out var installedKb))
                _installedBytes = installedKb * 1024;

            var used = memory.ullTotalPhys > memory.ullAvailPhys ? memory.ullTotalPhys - memory.ullAvailPhys : 0;
            var standby = ReadStandbyBytes();

            return new MemorySnapshot(_installedBytes ?? memory.ullTotalPhys, memory.ullTotalPhys,
                used, memory.ullAvailPhys, standby, DateTimeOffset.Now) { Error = _pdhError };
        }
    }

    // Serialize the complete operation across all windows and service instances. No automatic purges.
    public CleanResult PurgeStandby()
    {
        lock (PurgeSync)
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var before = ReadSnapshot();
            if (!TryEnablePrivilege("SeProfileSingleProcessPrivilege", out var privilege, out var error))
                return new CleanResult(false, before.StandbyBytes, before.StandbyBytes, error);

            int status;
            string? restoreError = null;
            try
            {
                var command = NativeMethods.MemoryPurgeStandbyList;
                status = NativeMethods.NtSetSystemInformation(
                    NativeMethods.SystemMemoryListInformation, ref command, sizeof(int));
            }
            finally
            {
                restoreError = privilege!.RestoreAndClose();
            }

            if (status != 0)
                return new CleanResult(false, before.StandbyBytes, null,
                    $"No se pudo vaciar Standby (NTSTATUS 0x{status:X8}). Comprueba los permisos de administrador." + restoreError);

            Thread.Sleep(180);
            try
            {
                var after = ReadSnapshot();
                var message = before.StandbyBytes.HasValue && after.StandbyBytes.HasValue
                    ? "Caché Standby vaciada."
                    : "Caché Standby vaciada; no se pudo medir la variación de memoria.";
                return new CleanResult(true, before.StandbyBytes, after.StandbyBytes, message + restoreError);
            }
            catch (Win32Exception)
            {
                return new CleanResult(true, before.StandbyBytes, null,
                    "Caché Standby vaciada; no se pudo leer la memoria posterior." + restoreError);
            }
        }
    }

    // All PDH operations run under _sync, including close and retries.
    private bool InitializePdh()
    {
        if (Environment.TickCount64 < _retryPdhAt) return false;

        var status = NativeMethods.PdhOpenQuery(null, 0, out _query);
        if (status != ErrorSuccess)
        {
            FailPdh($"Standby no disponible: no se pudo abrir PDH (0x{status:X8}).");
            return false;
        }

        foreach (var path in StandbyCounterPaths)
        {
            status = NativeMethods.PdhAddEnglishCounter(_query, path, 0, out var counter);
            if (status != ErrorSuccess)
            {
                FailPdh($"Standby no disponible: faltan contadores de Windows (0x{status:X8}).");
                return false;
            }
            _counters.Add(counter);
        }
        return true;
    }

    private ulong? ReadStandbyBytes()
    {
        if (_query == 0 && !InitializePdh()) return null;
        if (_counters.Count != StandbyCounterPaths.Length)
        {
            FailPdh("Standby no disponible: el conjunto de contadores está incompleto.");
            return null;
        }

        var status = NativeMethods.PdhCollectQueryData(_query);
        if (status != ErrorSuccess)
        {
            FailPdh($"Standby no disponible: error de lectura PDH (0x{status:X8}).");
            return null;
        }

        ulong total = 0;
        foreach (var counter in _counters)
        {
            status = NativeMethods.PdhGetFormattedCounterValue(counter, PdhFmtLarge, out _, out var value);
            if (status != ErrorSuccess || (value.CStatus != PdhValidData && value.CStatus != PdhNewData) || value.longValue < 0)
            {
                FailPdh($"Standby no disponible: contador no válido (PDH 0x{status:X8}; estado 0x{value.CStatus:X8}).");
                return null;
            }
            total += (ulong)value.longValue;
        }

        _pdhError = null;
        _pdhFailures = 0;
        return total;
    }

    private void FailPdh(string error)
    {
        _pdhError = error;
        _pdhFailures = Math.Min(_pdhFailures + 1, 5);
        _retryPdhAt = Environment.TickCount64 + Math.Min(60_000, 5_000L << (_pdhFailures - 1));
        ClosePdh();
    }

    private void ClosePdh()
    {
        if (_query != 0) NativeMethods.PdhCloseQuery(_query);
        _query = 0;
        _counters.Clear();
    }

    private static bool TryEnablePrivilege(string privilegeName, out PrivilegeLease? privilege, out string error)
    {
        privilege = null;
        error = string.Empty;
        if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(),
                NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY, out var token))
        {
            error = $"No se pudo abrir el token del proceso. Win32: {Marshal.GetLastWin32Error()}.";
            return false;
        }

        nint buffer = 0;
        try
        {
            if (!NativeMethods.LookupPrivilegeValue(null, privilegeName, out var luid))
            {
                error = $"No se encontró el privilegio de limpieza. Win32: {Marshal.GetLastWin32Error()}.";
                return false;
            }

            var state = new NativeMethods.TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new NativeMethods.LUID_AND_ATTRIBUTES { Luid = luid, Attributes = NativeMethods.SE_PRIVILEGE_ENABLED }
            };
            var size = Marshal.SizeOf<NativeMethods.TOKEN_PRIVILEGES>();
            buffer = Marshal.AllocHGlobal(size + sizeof(uint));
            Marshal.StructureToPtr(default(NativeMethods.TOKEN_PRIVILEGES), buffer, false);
            if (!NativeMethods.AdjustTokenPrivileges(token, false, ref state, (uint)size, buffer, buffer + size))
            {
                error = $"No se pudo habilitar la limpieza. Win32: {Marshal.GetLastWin32Error()}.";
                return false;
            }

            var lastError = Marshal.GetLastWin32Error();
            var previousState = Marshal.PtrToStructure<NativeMethods.TOKEN_PRIVILEGES>(buffer);
            var lease = new PrivilegeLease(token, previousState);
            token = 0; // The lease now owns the token, including failure restoration.
            if (lastError != 0)
            {
                error = $"La limpieza necesita permisos de administrador. Win32: {lastError}." + lease.RestoreAndClose();
                return false;
            }
            privilege = lease;
            return true;
        }
        finally
        {
            if (buffer != 0) Marshal.FreeHGlobal(buffer);
            if (token != 0) NativeMethods.CloseHandle(token);
        }
    }

    private sealed class PrivilegeLease(nint token, NativeMethods.TOKEN_PRIVILEGES previousState)
    {
        private nint _token = token;
        private NativeMethods.TOKEN_PRIVILEGES _previousState = previousState;

        public string? RestoreAndClose()
        {
            if (_token == 0) return null;
            try
            {
                if (_previousState.PrivilegeCount == 0) return null;
                var success = NativeMethods.AdjustTokenPrivileges(_token, false, ref _previousState, 0, 0, 0);
                var error = Marshal.GetLastWin32Error();
                return success && error == 0 ? null : $" No se pudo restaurar el privilegio previo (Win32: {error}).";
            }
            finally
            {
                NativeMethods.CloseHandle(_token);
                _token = 0;
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            ClosePdh();
        }
    }
}

public sealed record CleanResult(bool Success, ulong? BeforeBytes, ulong? AfterBytes, string Message)
{
    public ulong? ReleasedBytes => Success && BeforeBytes is ulong before && AfterBytes is ulong after
        ? (before > after ? before - after : 0)
        : null;
    public double? ReleasedGb => ReleasedBytes / 1024d / 1024d / 1024d;
}
