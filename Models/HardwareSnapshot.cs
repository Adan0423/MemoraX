namespace StandbyMemoryManager.Models;

public sealed record HardwareSnapshot(
    double? CpuTemperatureC,
    double? GpuTemperatureC,
    double? GpuHotspotC,
    double? CpuLoadPercent,
    double? GpuLoadPercent,
    double? GpuMemoryUsedMb,
    double? GpuMemoryTotalMb,
    double? CpuFanRpm,
    double? GpuFanRpm,
    string CpuName,
    string GpuName,
    DateTimeOffset Timestamp);
