using System.Diagnostics;
using StandbyMemoryManager.Models;

namespace StandbyMemoryManager.Services;

public sealed class ProcessMemoryService
{
    public IReadOnlyList<ProcessMemoryItem> GetTopProcesses(int take = 30)
    {
        var list = new List<ProcessMemoryItem>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var rawName = process.ProcessName;
                var name = string.IsNullOrWhiteSpace(rawName)
                    ? "Sistema"
                    : (rawName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? rawName : rawName + ".exe");

                list.Add(new ProcessMemoryItem(
                    process.Id,
                    name,
                    process.WorkingSet64,
                    process.PrivateMemorySize64,
                    process.Responding ? "Activo" : "Sin responder"));
            }
            catch
            {
                // Procesos del sistema protegidos por Windows (System, Registry, etc.) se omiten de forma segura.
            }
            finally
            {
                process.Dispose();
            }
        }

        return list.OrderByDescending(p => p.WorkingSetBytes).Take(take).ToArray();
    }
}
