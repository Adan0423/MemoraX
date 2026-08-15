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
                list.Add(new ProcessMemoryItem(
                    process.Id,
                    process.ProcessName + ".exe",
                    process.WorkingSet64,
                    process.PrivateMemorySize64,
                    process.Responding ? "Activo" : "Sin responder"));
            }
            catch
            {
                // Procesos protegidos pueden negar acceso. Se omiten sin bloquear el monitor.
            }
            finally
            {
                process.Dispose();
            }
        }

        return list.OrderByDescending(p => p.WorkingSetBytes).Take(take).ToArray();
    }
}
