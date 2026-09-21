using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Veltrixa.Models;
using Veltrixa.Services;
using System.Collections.ObjectModel;

namespace Veltrixa.ViewModels;

/// <summary>Shared session state. All binding notifications run on the UI thread.</summary>
public partial class MonitorViewModel : ObservableObject, IDisposable
{
    private readonly MemoryService _memory;
    private readonly MonitoringCoordinator _monitor;
    private readonly DispatcherQueue _dispatcher;
    private IReadOnlyList<ProcessMemoryItem> _processes = [];
    private string _filter = "";
    private bool _disposed, _memoryAvailable;
    private Task? _cleanTask;

    public ObservableCollection<ProcessMemoryItem> TopProcesses { get; } = new();
    [ObservableProperty] public partial string InstalledRam { get; set; } = "—";
    [ObservableProperty] public partial string TotalRam { get; set; } = "—";
    [ObservableProperty] public partial string UsedRam { get; set; } = "—";
    [ObservableProperty] public partial string AvailableRam { get; set; } = "—";
    [ObservableProperty] public partial string Standby { get; set; } = "—";
    [ObservableProperty] public partial string StandbyPercent { get; set; } = "Sin lectura";
    [ObservableProperty] public partial string StandbyPercentShort { get; set; } = "";
    [ObservableProperty] public partial double StandbyProgress { get; set; }
    [ObservableProperty] public partial double UsedPercent { get; set; }
    [ObservableProperty] public partial double AvailablePercent { get; set; }
    [ObservableProperty] public partial string MemorySummary { get; set; } = "Leyendo memoria de Windows…";
    [ObservableProperty] public partial string CpuTemperature { get; set; } = "—";
    [ObservableProperty] public partial string GpuTemperature { get; set; } = "—";
    [ObservableProperty] public partial bool HasCpuTemperature { get; set; }
    [ObservableProperty] public partial bool HasGpuTemperature { get; set; }
    [ObservableProperty] public partial bool HasStandby { get; set; }
    [ObservableProperty] public partial string GpuHotspot { get; set; } = "—";
    [ObservableProperty] public partial string CpuLoad { get; set; } = "—";
    [ObservableProperty] public partial string GpuLoad { get; set; } = "—";
    [ObservableProperty] public partial string GpuVram { get; set; } = "—";
    [ObservableProperty] public partial string CpuFan { get; set; } = "—";
    [ObservableProperty] public partial string GpuFan { get; set; } = "—";
    [ObservableProperty] public partial string CpuName { get; set; } = "Procesador";
    [ObservableProperty] public partial string GpuName { get; set; } = "Gráfica";
    [ObservableProperty] public partial string StorageInfo { get; set; } = "Leyendo unidad del sistema…";
    [ObservableProperty] public partial string SensorStatus { get; set; } = "Detectando sensores…";
    [ObservableProperty] public partial string LastUpdated { get; set; } = "Esperando primera lectura";
    [ObservableProperty] public partial string MonitoringStatus { get; set; } = "Iniciando monitoreo";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial bool HasStatus { get; set; }
    [ObservableProperty] public partial string LastClean { get; set; } = "Sin limpiezas en esta sesión";
    [ObservableProperty] public partial string Released { get; set; } = "—";
    [ObservableProperty] public partial bool IsCleaning { get; set; }
    [ObservableProperty] public partial bool CanClean { get; set; }
    [ObservableProperty] public partial bool IsRefreshingProcesses { get; set; } = true;
    [ObservableProperty] public partial bool HasProcesses { get; set; }
    [ObservableProperty] public partial bool IsProcessListEmpty { get; set; } = true;
    [ObservableProperty] public partial string ProcessEmptyMessage { get; set; } = "Leyendo procesos…";

    public MonitorViewModel(MemoryService memory, MonitoringCoordinator monitor, DispatcherQueue dispatcher)
    {
        _memory = memory;
        _monitor = monitor;
        _dispatcher = dispatcher;
        monitor.MemoryUpdated += OnMemory;
        monitor.HardwareUpdated += OnHardware;
        monitor.ProcessesUpdated += OnProcesses;
        monitor.StorageUpdated += OnStorage;
        monitor.ReadFailed += OnFailure;
    }

    private void Dispatch(Action update)
    {
        if (!_disposed) _dispatcher.TryEnqueue(() => { if (!_disposed) update(); });
    }

    private void OnMemory(MemorySnapshot value) => Dispatch(() =>
    {
        InstalledRam = $"{value.InstalledGb:F1} GB";
        TotalRam = $"{value.TotalGb:F1} GB";
        UsedRam = $"{value.UsedGb:F1} GB";
        AvailableRam = $"{value.AvailableGb:F1} GB";
        Standby = value.StandbyGb.HasValue ? $"{value.StandbyGb:F1} GB" : "—";
        HasStandby = value.StandbyGb.HasValue;
        StandbyPercent = value.StandbyPercent.HasValue ? $"{value.StandbyPercent:F0}% de la RAM utilizable" : "Lectura no disponible";
        StandbyPercentShort = value.StandbyPercent.HasValue ? $"{value.StandbyPercent:F0}%" : "";
        StandbyProgress = Math.Clamp(value.StandbyPercent ?? 0, 0, 100);
        UsedPercent = value.TotalBytes == 0 ? 0 : value.UsedBytes * 100d / value.TotalBytes;
        AvailablePercent = value.TotalBytes == 0 ? 0 : value.AvailableBytes * 100d / value.TotalBytes;
        MemorySummary = $"{UsedPercent:F0}% en uso · {value.AvailableGb:F1} GB disponibles";
        _memoryAvailable = value.StandbyBytes.HasValue;
        CanClean = _memoryAvailable && !IsCleaning;
        LastUpdated = $"Actualizado a las {value.Timestamp:HH:mm:ss}";
        MonitoringStatus = value.Error ?? "Monitoreo activo · RAM 2 s / sensores 3 s";
    });

    private void OnHardware(HardwareSnapshot value) => Dispatch(() =>
    {
        HasCpuTemperature = value.CpuTemperatureC.HasValue;
        HasGpuTemperature = value.GpuTemperatureC.HasValue;
        CpuTemperature = Temperature(value.CpuTemperatureC);
        GpuTemperature = Temperature(value.GpuTemperatureC);
        GpuHotspot = Temperature(value.GpuHotspotC);
        CpuLoad = Percent(value.CpuLoadPercent);
        GpuLoad = Percent(value.GpuLoadPercent);
        CpuFan = value.CpuFanRpm.HasValue ? $"{value.CpuFanRpm:F0} RPM" : "—";
        GpuFan = value.GpuFanRpm.HasValue ? $"{value.GpuFanRpm:F0} RPM" : "—";
        CpuName = value.CpuName;
        GpuName = value.GpuName;
        GpuVram = value.GpuMemoryUsedMb.HasValue && value.GpuMemoryTotalMb.HasValue
            ? $"{value.GpuMemoryUsedMb / 1024:F1} / {value.GpuMemoryTotalMb / 1024:F1} GB"
            : value.GpuMemoryUsedMb.HasValue ? $"{value.GpuMemoryUsedMb:F0} MB usadas" : "—";
        SensorStatus = value.Error ?? "Sensores activos · — indica un sensor no disponible en este equipo";
    });

    private void OnProcesses(IReadOnlyList<ProcessMemoryItem> values) => Dispatch(() =>
    {
        _processes = values;
        IsRefreshingProcesses = false;
        ApplyFilter();
    });

    private void OnStorage(string value) => Dispatch(() => StorageInfo = value);

    private void OnFailure(string source, string error) => Dispatch(() =>
    {
        switch (source)
        {
            case "Memoria":
                _memoryAvailable = false;
                CanClean = false;
                HasStandby = false;
                Standby = "—";
                StandbyProgress = 0;
                StandbyPercent = "Lectura no disponible";
                StandbyPercentShort = "";
                MonitoringStatus = "Memoria no disponible: " + error;
                break;
            case "Sensores": SensorStatus = "Sensores no disponibles: " + error; break;
            case "Almacenamiento": StorageInfo = "Unidad no disponible"; break;
            case "Procesos":
                IsRefreshingProcesses = false;
                _processes = [];
                TopProcesses.Clear();
                HasProcesses = false;
                IsProcessListEmpty = true;
                ProcessEmptyMessage = "No se pudieron leer los procesos: " + error;
                break;
        }
    });

    public void FilterProcesses(string text)
    {
        _filter = text.Trim();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var desired = _processes.Where(p => _filter.Length == 0 ||
            p.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
            p.Pid.ToString().Contains(_filter, StringComparison.Ordinal)).ToArray();
        ProcessListUpdater.Update(TopProcesses, desired);
        HasProcesses = desired.Length > 0;
        IsProcessListEmpty = !HasProcesses;
        ProcessEmptyMessage = IsRefreshingProcesses ? "Leyendo procesos…" :
            _filter.Length > 0 ? "No hay procesos que coincidan con la búsqueda." : "No hay procesos accesibles en esta lectura.";
    }

    public Task CleanAsync()
    {
        if (IsCleaning) return _cleanTask ?? Task.CompletedTask;
        if (!CanClean) return Task.CompletedTask;
        _cleanTask = CleanCoreAsync();
        return _cleanTask;
    }

    private async Task CleanCoreAsync()
    {
        IsCleaning = true;
        CanClean = false;
        HasStatus = true;
        StatusMessage = "Limpiando caché standby…";
        try
        {
            var result = await Task.Run(_memory.PurgeStandby);
            StatusMessage = result.Message;
            if (result.Success)
            {
                Released = result.ReleasedGb.HasValue ? $"{result.ReleasedGb:F2} GB" : "Sin medición fiable";
                LastClean = DateTime.Now.ToString("HH:mm:ss");
            }
            _monitor.RefreshMemory();
        }
        catch (Exception ex) { StatusMessage = "No se pudo limpiar: " + ex.Message; }
        finally
        {
            IsCleaning = false;
            CanClean = _memoryAvailable;
        }
    }

    public Task WaitForCleanAsync() => _cleanTask ?? Task.CompletedTask;
    private static string Temperature(double? value) => value.HasValue ? $"{value:F0} °C" : "Sin datos";
    private static string Percent(double? value) => value.HasValue ? $"{value:F0}%" : "Sin datos";

    public void Dispose()
    {
        _disposed = true;
        _monitor.MemoryUpdated -= OnMemory;
        _monitor.HardwareUpdated -= OnHardware;
        _monitor.ProcessesUpdated -= OnProcesses;
        _monitor.StorageUpdated -= OnStorage;
        _monitor.ReadFailed -= OnFailure;
    }
}
