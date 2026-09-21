using Microsoft.UI.Xaml;
using Veltrixa.Services;
using Veltrixa.Views;

namespace Veltrixa;

public partial class App : Application
{
    public MemoryService MemoryService { get; } = new();
    public HardwareMonitorService HardwareMonitorService { get; } = new();
    public ProcessMemoryService ProcessMemoryService { get; } = new();
    public MonitoringCoordinator Monitor { get; }

    private WidgetWindow? _widget;
    private DashboardWindow? _dashboard;

    public App()
    {
        InitializeComponent();
        Monitor = new MonitoringCoordinator(
            MemoryService.ReadSnapshot,
            HardwareMonitorService.ReadSnapshot,
            () => ProcessMemoryService.GetTopProcesses(),
            StorageService.ReadSummary);
        UnhandledException += (sender, e) =>
        {
            e.Handled = true;
            try
            {
                var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Veltrixa");
                System.IO.Directory.CreateDirectory(dir);
                var logPath = System.IO.Path.Combine(dir, "crash.log");
                System.IO.File.AppendAllText(logPath, $"[{System.DateTime.Now}] Exception: {e.Exception}\n");
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Monitor.Start();
        _widget = new WidgetWindow(Monitor, ShowDashboard);
        _widget.Activate();
    }

    private void ShowDashboard(DashboardSection section)
    {
        if (_dashboard is null)
        {
            _dashboard = new DashboardWindow(Monitor);
            _dashboard.Closed += (_, _) => _dashboard = null;
        }

        _dashboard.ShowSection(section);
        _dashboard.Activate();
    }

}
