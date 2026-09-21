using System.Collections.ObjectModel;
using Veltrixa.Models;

namespace Veltrixa.Services;

public static class ProcessListUpdater
{
    /// <summary>Preserve unchanged items instead of resetting the visible list.</summary>
    public static void Update(ObservableCollection<ProcessMemoryItem> items, IReadOnlyList<ProcessMemoryItem> desired)
    {
        var pids = desired.Select(p => p.Pid).ToHashSet();
        for (var i = items.Count - 1; i >= 0; i--)
            if (!pids.Contains(items[i].Pid)) items.RemoveAt(i);
        for (var i = 0; i < desired.Count; i++)
        {
            var target = desired[i];
            var existing = -1;
            for (var j = i; j < items.Count; j++)
                if (items[j].Pid == target.Pid) { existing = j; break; }
            if (existing < 0) items.Insert(i, target);
            else
            {
                if (existing != i) items.Move(existing, i);
                if (items[i] != target) items[i] = target;
            }
        }
    }
}
