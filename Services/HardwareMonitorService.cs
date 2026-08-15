using LibreHardwareMonitor.Hardware;
using StandbyMemoryManager.Models;

namespace StandbyMemoryManager.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly object _sync = new();
    private Computer? _computer;

    public void Start()
    {
        lock (_sync)
        {
            if (_computer is not null) return;

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            _computer.Open();
        }
    }

    public HardwareSnapshot ReadSnapshot()
    {
        lock (_sync)
        {
            if (_computer is null) Start();

            double? cpuTemp = null, gpuTemp = null, gpuHotspot = null;
            double? cpuLoad = null, gpuLoad = null;
            double? gpuMemUsed = null, gpuMemTotal = null;
            double? cpuFan = null, gpuFan = null;
            string cpuName = "CPU", gpuName = "GPU";

            foreach (var hardware in _computer!.Hardware)
            {
                UpdateHardwareTree(hardware);

                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    cpuName = hardware.Name;
                    cpuTemp = FindPreferredTemperature(hardware, "CPU Package", "Package", "Core") ?? cpuTemp;
                    cpuLoad = FindSensor(hardware, SensorType.Load, "CPU Total", "Total") ?? cpuLoad;
                    cpuFan = FindSensor(hardware, SensorType.Fan, "CPU") ?? cpuFan;
                }
                else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                {
                    gpuName = hardware.Name;
                    gpuTemp = FindPreferredTemperature(hardware, "GPU Core", "Core") ?? gpuTemp;
                    gpuHotspot = FindPreferredTemperature(hardware, "Hot Spot", "Hotspot") ?? gpuHotspot;
                    gpuLoad = FindSensor(hardware, SensorType.Load, "GPU Core", "Core") ?? gpuLoad;
                    gpuMemUsed = FindSensor(hardware, SensorType.SmallData, "GPU Memory Used", "Memory Used") ?? gpuMemUsed;
                    gpuMemTotal = FindSensor(hardware, SensorType.SmallData, "GPU Memory Total", "Memory Total") ?? gpuMemTotal;
                    gpuFan = FindSensor(hardware, SensorType.Fan, "GPU") ?? gpuFan;
                }

                if (cpuFan is null && hardware.HardwareType == HardwareType.Motherboard)
                    cpuFan = FindSensorRecursive(hardware, SensorType.Fan, "CPU", "Fan #1") ?? cpuFan;
            }

            return new HardwareSnapshot(cpuTemp, gpuTemp, gpuHotspot, cpuLoad, gpuLoad,
                gpuMemUsed, gpuMemTotal, cpuFan, gpuFan, cpuName, gpuName, DateTimeOffset.Now);
        }
    }

    private static void UpdateHardwareTree(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            UpdateHardwareTree(sub);
    }

    private static double? FindPreferredTemperature(IHardware hardware, params string[] preferredNames)
    {
        foreach (var preferred in preferredNames)
        {
            var sensor = EnumerateSensors(hardware)
                .FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value.HasValue &&
                                     s.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase));
            if (sensor?.Value is float value) return value;
        }

        var temperatures = EnumerateSensors(hardware)
            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
            .Select(s => (double)s.Value!.Value)
            .ToArray();

        return temperatures.Length == 0 ? null : temperatures.Max();
    }

    private static double? FindSensor(IHardware hardware, SensorType type, params string[] names) =>
        FindSensorRecursive(hardware, type, names);

    private static double? FindSensorRecursive(IHardware hardware, SensorType type, params string[] names)
    {
        foreach (var name in names)
        {
            var sensor = EnumerateSensors(hardware)
                .FirstOrDefault(s => s.SensorType == type && s.Value.HasValue &&
                                     s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (sensor?.Value is float value) return value;
        }
        return null;
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
            yield return sensor;

        foreach (var sub in hardware.SubHardware)
        foreach (var sensor in EnumerateSensors(sub))
            yield return sensor;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _computer?.Close();
            _computer = null;
        }
    }
}
