namespace Veltrixa.Services;

public static class StorageService
{
    public static string ReadSummary()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrEmpty(root)) return "Unidad del sistema no disponible";
        var drive = new DriveInfo(root);
        if (!drive.IsReady) return "Unidad del sistema no disponible";
        var total = drive.TotalSize / 1073741824d;
        var free = drive.AvailableFreeSpace / 1073741824d;
        return $"{drive.Name}  ·  {free:F0} GB libres de {total:F0} GB";
    }
}
