namespace Veltrixa.Models;

public sealed record MemorySnapshot(
    ulong InstalledBytes,
    ulong TotalBytes,
    ulong UsedBytes,
    ulong AvailableBytes,
    ulong? StandbyBytes,
    DateTimeOffset Timestamp)
{
    public string? Error { get; init; }

    public double InstalledGb => InstalledBytes / 1024d / 1024d / 1024d;
    public double TotalGb => TotalBytes / 1024d / 1024d / 1024d;
    public double UsedGb => UsedBytes / 1024d / 1024d / 1024d;
    public double AvailableGb => AvailableBytes / 1024d / 1024d / 1024d;
    public double? StandbyGb => StandbyBytes / 1024d / 1024d / 1024d;
    public double? StandbyPercent => TotalBytes == 0 ? null : StandbyBytes * 100d / TotalBytes;
}
