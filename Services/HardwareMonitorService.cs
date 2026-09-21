using LibreHardwareMonitor.Hardware;
using Veltrixa.Models;

namespace Veltrixa.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private static readonly string[] CpuTemperatureNames = ["Tctl/Tdie", "Tdie", "CPU Package", "Package", "Core", "CCD"];
    private static readonly string[] CpuLoadNames = ["CPU Total", "Total"];
    private static readonly string[] CpuFanNames = ["CPU"];
    private static readonly string[] BoardCpuTemperatureNames = ["CPU", "Package"];
    private static readonly string[] GpuTemperatureNames = ["GPU Core", "GPU Edge", "Edge", "Core"];
    private static readonly string[] HotspotNames = ["Hot Spot", "Hotspot", "Junction"];
    private static readonly string[] GpuLoadNames = ["GPU Core", "Core", "GPU D3D"];
    private static readonly string[] GpuUsedMemoryNames = ["GPU Memory Used", "Memory Used", "D3D Dedicated Memory Used"];
    private static readonly string[] GpuTotalMemoryNames = ["GPU Memory Total", "Memory Total", "D3D Dedicated Memory Total"];
    private static readonly string[] GpuFanNames = ["GPU"];

    private readonly object _sync = new();
    private Computer? _computer;
    private DeviceEntry? _cpu;
    private DeviceEntry? _gpu;
    private DeviceEntry? _motherboard;
    private bool _disposed;
    private int _startFailures;
    private long _retryStartAt;
    private string? _startupError;

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_computer is not null || Environment.TickCount64 < _retryStartAt) return;

            Computer? computer;
            string? warning = null;
            if (!TryOpen(includeMotherboard: true, out computer, out var fullError))
            {
                if (!TryOpen(includeMotherboard: false, out computer, out var basicError))
                {
                    ScheduleStartRetry($"Sensores no disponibles: {basicError}");
                    return;
                }
                warning = $"Sensores de placa no disponibles: {fullError}";
            }

            _computer = computer;
            try
            {
                var hardware = _computer!.Hardware;
                var cpu = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                var gpu = hardware.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                    .OrderByDescending(GpuPreference)
                    .ThenBy(h => h.Identifier.ToString(), StringComparer.Ordinal)
                    .FirstOrDefault();
                var board = hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);

                _cpu = cpu is null ? null : new DeviceEntry(cpu);
                _gpu = gpu is null ? null : new DeviceEntry(gpu);
                _motherboard = board is null ? null : new DeviceEntry(board);
                _startupError = warning;
                _startFailures = 0;
            }
            catch (Exception ex)
            {
                CloseComputer();
                ScheduleStartRetry($"No se pudo preparar el inventario de sensores: {ex.Message}");
            }
        }
    }

    public HardwareSnapshot ReadSnapshot()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_computer is null) Start();
            if (_computer is null)
                return EmptySnapshot(_startupError ?? "Sensores no disponibles.");

            var errors = new List<string>(4);
            if (_startupError is not null) errors.Add(_startupError);
            var cpuSensors = ReadSensors(_cpu, errors);
            var gpuSensors = ReadSensors(_gpu, errors);
            var boardSensors = ReadSensors(_motherboard, errors);

            var cpuTemp = FindTemperature(cpuSensors, CpuTemperatureNames, allowFallback: true)
                ?? FindTemperature(boardSensors, BoardCpuTemperatureNames);
            var gpuTemp = FindTemperature(gpuSensors, GpuTemperatureNames);
            // A core/edge temperature is never substituted for a missing hotspot sensor.
            var gpuHotspot = FindTemperature(gpuSensors, HotspotNames);
            var cpuLoad = FindSensor(cpuSensors, SensorType.Load, CpuLoadNames);
            var gpuLoad = FindSensor(gpuSensors, SensorType.Load, GpuLoadNames);
            var cpuFan = FindSensor(cpuSensors, SensorType.Fan, CpuFanNames)
                ?? FindSensor(boardSensors, SensorType.Fan, CpuFanNames);
            var gpuFan = FindSensor(gpuSensors, SensorType.Fan, GpuFanNames);
            var gpuMemUsed = FindSensor(gpuSensors, SensorType.SmallData, GpuUsedMemoryNames);
            var gpuMemTotal = FindSensor(gpuSensors, SensorType.SmallData, GpuTotalMemoryNames);

            if (_cpu is null) errors.Add("CPU no detectada.");
            else if (cpuTemp is null) errors.Add("Temperatura CPU no disponible.");
            if (_gpu is null) errors.Add("GPU no detectada.");
            else if (gpuTemp is null) errors.Add("Temperatura GPU no disponible.");

            return new HardwareSnapshot(cpuTemp, gpuTemp, gpuHotspot, cpuLoad, gpuLoad,
                gpuMemUsed, gpuMemTotal, cpuFan, gpuFan, _cpu?.Name ?? "CPU", _gpu?.Name ?? "GPU", DateTimeOffset.Now)
            {
                Error = errors.Count == 0 ? null : string.Join(" ", errors)
            };
        }
    }

    private static bool TryOpen(bool includeMotherboard, out Computer? computer, out string? error)
    {
        var candidate = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = false, // RAM comes from MemoryService; do not sample it twice.
            IsMotherboardEnabled = includeMotherboard,
            IsControllerEnabled = false
        };
        try
        {
            candidate.Open();
            computer = candidate;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            try { candidate.Close(); }
            catch { /* Opening may have left only part of the driver tree initialized. */ }
            computer = null;
            error = ex.Message;
            return false;
        }
    }

    private void ScheduleStartRetry(string error)
    {
        _startupError = error;
        _startFailures = Math.Min(_startFailures + 1, 5);
        _retryStartAt = Environment.TickCount64 + RetryDelay(_startFailures);
    }

    // LibreHardwareMonitor has no common IsDiscrete flag. Prefer clearly identified
    // discrete families; always keep every metric tied to the same selected adapter.
    private static int GpuPreference(IHardware hardware)
    {
        if (hardware.HardwareType == HardwareType.GpuNvidia) return 2;
        string[] discreteFamilies = ["Radeon RX", "Radeon Pro", "FirePro", "Arc A", "Arc B", "Arc Pro"];
        return discreteFamilies.Any(family => hardware.Name.Contains(family, StringComparison.OrdinalIgnoreCase)) ? 2 : 1;
    }

    private static ISensor[] ReadSensors(DeviceEntry? entry, List<string> errors)
    {
        if (entry is null) return [];
        if (entry.TryUpdate(out var error)) return entry.Sensors;
        if (error is not null) errors.Add(error);
        return [];
    }

    private static double? FindTemperature(ISensor[] sensors, string[] preferredNames, bool allowFallback = false)
    {
        var preferred = FindSensor(sensors, SensorType.Temperature, preferredNames);
        if (preferred.HasValue || !allowFallback) return preferred;

        double? maximum = null;
        foreach (var sensor in sensors)
        {
            if (sensor.SensorType == SensorType.Temperature && ReadValue(sensor) is double value)
                maximum = maximum.HasValue ? Math.Max(maximum.Value, value) : value;
        }
        return maximum;
    }

    private static double? FindSensor(ISensor[] sensors, SensorType type, string[] names)
    {
        foreach (var name in names)
        foreach (var sensor in sensors)
        {
            if (sensor.SensorType == type && sensor.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                && ReadValue(sensor) is double value)
                return value;
        }
        return null;
    }

    private static double? ReadValue(ISensor sensor)
    {
        if (sensor.Value is not float value || !float.IsFinite(value)) return null;
        return sensor.SensorType switch
        {
            SensorType.Temperature when value is > 0 and < 150 => value,
            SensorType.Load when value is >= 0 and <= 100 => value,
            SensorType.Fan or SensorType.SmallData when value >= 0 => value,
            _ => null
        };
    }

    private static long RetryDelay(int failures) => Math.Min(60_000, 5_000L << (Math.Clamp(failures, 1, 5) - 1));

    private static HardwareSnapshot EmptySnapshot(string error) =>
        new(null, null, null, null, null, null, null, null, null, "CPU", "GPU", DateTimeOffset.Now) { Error = error };

    private void CloseComputer()
    {
        _cpu?.Dispose();
        _gpu?.Dispose();
        _motherboard?.Dispose();
        _cpu = null;
        _gpu = null;
        _motherboard = null;
        var computer = _computer;
        _computer = null;
        try { computer?.Close(); }
        catch { /* Shutdown must still release the remaining app resources. */ }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            CloseComputer();
        }
    }

    // Cache the device tree and sensor objects. Activation/removal events invalidate
    // the sensor array, so sensors that become available after the first poll appear.
    private sealed class DeviceEntry : IDisposable
    {
        private readonly IHardware[] _hardware;
        private int _inventoryDirty = 1;
        private int _failures;
        private long _retryAt;
        private string? _error;

        public string Name { get; }
        public ISensor[] Sensors { get; private set; } = [];

        public DeviceEntry(IHardware root)
        {
            Name = root.Name;
            _hardware = EnumerateHardware(root).ToArray();
            foreach (var hardware in _hardware)
            {
                hardware.SensorAdded += SensorInventoryChanged;
                hardware.SensorRemoved += SensorInventoryChanged;
            }
        }

        public bool TryUpdate(out string? error)
        {
            if (Environment.TickCount64 < _retryAt)
            {
                error = _error;
                return false;
            }
            try
            {
                foreach (var hardware in _hardware) hardware.Update();
                if (Interlocked.Exchange(ref _inventoryDirty, 0) == 1)
                    Sensors = _hardware.SelectMany(h => h.Sensors).ToArray();
                _failures = 0;
                _error = null;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _inventoryDirty, 1);
                _failures = Math.Min(_failures + 1, 5);
                _retryAt = Environment.TickCount64 + RetryDelay(_failures);
                _error = $"No se pudo leer {Name}: {ex.Message}";
                error = _error;
                return false;
            }
        }

        private void SensorInventoryChanged(ISensor _) => Interlocked.Exchange(ref _inventoryDirty, 1);

        private static IEnumerable<IHardware> EnumerateHardware(IHardware hardware)
        {
            yield return hardware;
            foreach (var child in hardware.SubHardware)
            foreach (var descendant in EnumerateHardware(child))
                yield return descendant;
        }

        public void Dispose()
        {
            foreach (var hardware in _hardware)
            {
                hardware.SensorAdded -= SensorInventoryChanged;
                hardware.SensorRemoved -= SensorInventoryChanged;
            }
            Sensors = [];
        }
    }
}
