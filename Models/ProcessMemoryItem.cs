namespace StandbyMemoryManager.Models;

public sealed record ProcessMemoryItem(
    int Pid,
    string Name,
    long WorkingSetBytes,
    long PrivateBytes,
    string Status)
{
    public double WorkingSetMb => WorkingSetBytes / 1024d / 1024d;
    public double PrivateMb => PrivateBytes / 1024d / 1024d;
    public string WorkingSetDisplay => $"{WorkingSetMb:F0} MB";
    public string PrivateDisplay => $"{PrivateMb:F0} MB";
}
