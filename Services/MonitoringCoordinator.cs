using System.Diagnostics;
using Veltrixa.Models;

namespace Veltrixa.Services;

/// <summary>One producer per source, independent of how many windows display it.</summary>
public sealed class MonitoringCoordinator : IAsyncDisposable
{
    private readonly Func<MemorySnapshot> _readMemory;
    private readonly Func<HardwareSnapshot> _readHardware;
    private readonly Func<IReadOnlyList<ProcessMemoryItem>> _readProcesses;
    private readonly Func<string> _readStorage;
    private readonly MonitoringIntervals _intervals;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim[] _signals = Enumerable.Range(0, 4).Select(_ => new SemaphoreSlim(0, 1)).ToArray();
    private readonly object _sync = new();
    private Task[] _workers = [];
    private bool _widgetVisible, _dashboardVisible, _processesVisible, _started, _disposed;

    public event Action<MemorySnapshot>? MemoryUpdated;
    public event Action<HardwareSnapshot>? HardwareUpdated;
    public event Action<IReadOnlyList<ProcessMemoryItem>>? ProcessesUpdated;
    public event Action<string>? StorageUpdated;
    public event Action<string, string>? ReadFailed;
    public event Action<string, double>? SampleMeasured;

    public MonitoringCoordinator(Func<MemorySnapshot> readMemory, Func<HardwareSnapshot> readHardware,
        Func<IReadOnlyList<ProcessMemoryItem>> readProcesses, Func<string> readStorage,
        MonitoringIntervals? intervals = null)
    {
        _readMemory = readMemory;
        _readHardware = readHardware;
        _readProcesses = readProcesses;
        _readStorage = readStorage;
        _intervals = intervals ?? new();
    }

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started) return;
            _started = true;
            _workers =
            [
                Task.Run(() => RunAsync(0, "Memoria", _readMemory, value => MemoryUpdated?.Invoke(value))),
                Task.Run(() => RunAsync(1, "Sensores", _readHardware, value => HardwareUpdated?.Invoke(value))),
                Task.Run(() => RunAsync(2, "Procesos", _readProcesses, value => ProcessesUpdated?.Invoke(value))),
                Task.Run(() => RunAsync(3, "Almacenamiento", _readStorage, value => StorageUpdated?.Invoke(value)))
            ];
        }
    }

    public void SetWidgetVisible(bool visible)
    {
        bool wake;
        lock (_sync)
        {
            wake = visible && !_widgetVisible && !_dashboardVisible;
            _widgetVisible = visible;
        }
        if (wake) { Signal(0); Signal(1); }
    }

    public void SetDashboardState(bool visible, bool showProcesses)
    {
        bool wakeFast, wakeProcesses, wakeStorage;
        lock (_sync)
        {
            wakeFast = visible && !_dashboardVisible && !_widgetVisible;
            wakeProcesses = visible && showProcesses && !(_dashboardVisible && _processesVisible);
            wakeStorage = visible && !_dashboardVisible;
            _dashboardVisible = visible;
            _processesVisible = showProcesses;
        }
        if (wakeFast) { Signal(0); Signal(1); }
        if (wakeProcesses) Signal(2);
        if (wakeStorage) Signal(3);
    }

    public void RefreshMemory() => Signal(0);

    private TimeSpan? Interval(int lane)
    {
        lock (_sync)
        {
            var visible = _widgetVisible || _dashboardVisible;
            return lane switch
            {
                0 => visible ? _intervals.Memory : _intervals.Background,
                1 => visible ? _intervals.Hardware : _intervals.Background,
                2 => _dashboardVisible && _processesVisible ? _intervals.Processes : null,
                3 => _dashboardVisible ? _intervals.Storage : null,
                _ => null
            };
        }
    }

    private async Task RunAsync<T>(int lane, string name, Func<T> read, Action<T> publish)
    {
        var token = _stop.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                while (_signals[lane].Wait(0)) { }
                if (Interval(lane) is null)
                {
                    await _signals[lane].WaitAsync(token).ConfigureAwait(false);
                    continue;
                }
                var watch = Stopwatch.StartNew();
                try
                {
                    // Each lane is a worker; slow sensors never hold up RAM or the UI.
                    var value = read();
                    if (!token.IsCancellationRequested) publish(value);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested) ReadFailed?.Invoke(name, ex.Message);
                }
                finally { SampleMeasured?.Invoke(name, watch.Elapsed.TotalMilliseconds); }

                var delay = Interval(lane);
                if (delay.HasValue)
                    await _signals[lane].WaitAsync(delay.Value, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Signal(int lane)
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_signals[lane].CurrentCount == 0) _signals[lane].Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task[] workers;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            workers = _workers;
            _stop.Cancel();
        }
        await Task.WhenAll(workers).ConfigureAwait(false);
        foreach (var signal in _signals) signal.Dispose();
        _stop.Dispose();
    }
}

public sealed record MonitoringIntervals
{
    public TimeSpan Memory { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan Hardware { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan Processes { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan Storage { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan Background { get; init; } = TimeSpan.FromSeconds(30);
}
