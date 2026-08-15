using CommunityToolkit.Mvvm.ComponentModel;
using StandbyMemoryManager.Models;
using StandbyMemoryManager.Services;
using System.Collections.ObjectModel;

namespace StandbyMemoryManager.ViewModels;

public partial class MonitorViewModel : ObservableObject
{
    private readonly MemoryService _memory;
    private readonly HardwareMonitorService _hardware;
    private readonly ProcessMemoryService? _processes;
    private int _refreshing;

    public ObservableCollection<ProcessMemoryItem> TopProcesses { get; } = new();

    [ObservableProperty] private string totalRam = "—";
    [ObservableProperty] private string usedRam = "—";
    [ObservableProperty] private string availableRam = "—";
    [ObservableProperty] private string standby = "—";
    [ObservableProperty] private string standbyPercent = "—";
    [ObservableProperty] private double standbyProgress;

    [ObservableProperty] private string cpuTemperature = "—";
    [ObservableProperty] private string gpuTemperature = "—";
    [ObservableProperty] private string gpuHotspot = "—";
    [ObservableProperty] private string cpuLoad = "—";
    [ObservableProperty] private string gpuLoad = "—";
    [ObservableProperty] private string gpuVram = "—";
    [ObservableProperty] private string cpuFan = "—";
    [ObservableProperty] private string gpuFan = "—";
    [ObservableProperty] private string cpuName = "CPU";
    [ObservableProperty] private string gpuName = "GPU";

    [ObservableProperty] private string statusMessage = "Monitorizando";
    [ObservableProperty] private string lastClean = "Todavía no";
    [ObservableProperty] private string released = "0 GB";
    [ObservableProperty] private bool isCleaning;

    public MonitorViewModel(MemoryService memory, HardwareMonitorService hardware, ProcessMemoryService? processes = null)
    {
        _memory = memory;
        _hardware = hardware;
        _processes = processes;
    }

    public async Task RefreshAsync(bool includeHardware = true, bool includeProcesses = false)
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;

        try
        {
            var memTask = Task.Run(_memory.ReadSnapshot);
            Task<HardwareSnapshot?> hwTask = includeHardware
                ? Task.Run<HardwareSnapshot?>(() => _hardware.ReadSnapshot())
                : Task.FromResult<HardwareSnapshot?>(null);
            Task<IReadOnlyList<ProcessMemoryItem>?> processTask = includeProcesses && _processes is not null
                ? Task.Run<IReadOnlyList<ProcessMemoryItem>?>(() => _processes.GetTopProcesses())
                : Task.FromResult<IReadOnlyList<ProcessMemoryItem>?>(null);

            var mem = await memTask;
            var hw = await hwTask;
            var processes = await processTask;

            TotalRam = $"{mem.TotalGb:F1} GB";
            UsedRam = $"{mem.UsedGb:F1} GB";
            AvailableRam = $"{mem.AvailableGb:F1} GB";
            Standby = $"{mem.StandbyGb:F1} GB";
            StandbyPercent = $"{mem.StandbyPercent:F0}%";
            StandbyProgress = Math.Clamp(mem.StandbyPercent, 0, 100);

            if (hw is not null)
            {
                CpuTemperature = Temp(hw.CpuTemperatureC);
                GpuTemperature = Temp(hw.GpuTemperatureC);
                GpuHotspot = Temp(hw.GpuHotspotC);
                CpuLoad = Percent(hw.CpuLoadPercent);
                GpuLoad = Percent(hw.GpuLoadPercent);
                CpuFan = Rpm(hw.CpuFanRpm);
                GpuFan = Rpm(hw.GpuFanRpm);
                CpuName = hw.CpuName;
                GpuName = hw.GpuName;
                GpuVram = FormatVram(hw.GpuMemoryUsedMb, hw.GpuMemoryTotalMb);
            }

            if (processes is not null)
            {
                TopProcesses.Clear();
                foreach (var item in processes)
                    TopProcesses.Add(item);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error de monitorización: " + ex.Message;
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    public async Task CleanAsync()
    {
        if (IsCleaning) return;
        IsCleaning = true;
        StatusMessage = "Limpiando Standby Memory…";

        try
        {
            var result = await Task.Run(_memory.PurgeStandby);
            Released = $"{result.ReleasedGb:F2} GB";
            LastClean = DateTime.Now.ToString("HH:mm:ss");
            StatusMessage = result.Message;
            await RefreshAsync(includeHardware: false);
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo limpiar: " + ex.Message;
        }
        finally
        {
            IsCleaning = false;
        }
    }

    private static string Temp(double? value) => value.HasValue ? $"{value:F0} °C" : "—";
    private static string Percent(double? value) => value.HasValue ? $"{value:F0}%" : "—";
    private static string Rpm(double? value) => value.HasValue ? $"{value:F0} RPM" : "—";
    private static string FormatVram(double? used, double? total)
    {
        if (!used.HasValue && !total.HasValue) return "—";
        if (used.HasValue && total.HasValue) return $"{used:F0} / {total:F0} MB";
        return used.HasValue ? $"{used:F0} MB usadas" : $"{total:F0} MB total";
    }
}
