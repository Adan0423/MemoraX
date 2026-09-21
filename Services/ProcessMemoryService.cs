using System.Diagnostics;
using Veltrixa.Models;

namespace Veltrixa.Services;

public sealed class ProcessMemoryService
{
    public IReadOnlyList<ProcessMemoryItem> GetTopProcesses(int take = 30)
    {
        if (take <= 0) return [];

        // Responding is a comparatively expensive cross-process query. Keep the
        // handles only for the candidates that will actually be shown and ask for
        // that status after sorting by Working Set.
        var candidates = new List<(Process Process, string Name, long WorkingSet, long PrivateMemory)>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var rawName = process.ProcessName;
                var name = string.IsNullOrWhiteSpace(rawName)
                    ? "Sistema"
                    : (rawName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? rawName : rawName + ".exe");

                candidates.Add((process, name, process.WorkingSet64, process.PrivateMemorySize64));
                continue;
            }
            catch
            {
                // Procesos del sistema protegidos por Windows (System, Registry, etc.) se omiten de forma segura.
            }
            process.Dispose();
        }

        var top = candidates.OrderByDescending(p => p.WorkingSet).Take(take).ToArray();
        var result = new List<ProcessMemoryItem>(top.Length);
        var selected = top.Select(candidate => candidate.Process).ToHashSet();

        foreach (var candidate in candidates)
        {
            if (!selected.Contains(candidate.Process))
            {
                candidate.Process.Dispose();
                continue;
            }

            try
            {
                result.Add(new ProcessMemoryItem(
                    candidate.Process.Id,
                    candidate.Name,
                    candidate.WorkingSet,
                    candidate.PrivateMemory,
                    candidate.Process.Responding ? "Activo" : "Sin responder"));
            }
            catch
            {
                // A process may exit between enumeration and inspection.
            }
            finally
            {
                candidate.Process.Dispose();
            }
        }

        return result.OrderByDescending(p => p.WorkingSetBytes).ToArray();
    }
}
