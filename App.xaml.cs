using Microsoft.UI.Xaml;
using StandbyMemoryManager.Services;
using StandbyMemoryManager.Views;

namespace StandbyMemoryManager;

public partial class App : Application
{
    public MemoryService MemoryService { get; } = new();
    public HardwareMonitorService HardwareMonitorService { get; } = new();
    public ProcessMemoryService ProcessMemoryService { get; } = new();

    private WidgetWindow? _widget;
    private DashboardWindow? _dashboard;

    public App()
    {
        InitializeComponent();
        UnhandledException += (sender, e) =>
        {
            e.Handled = true;
            try
            {
                var dir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "MemoraX");
                System.IO.Directory.CreateDirectory(dir);
                var logPath = System.IO.Path.Combine(dir, "crash.log");
                System.IO.File.AppendAllText(logPath, $"[{System.DateTime.Now}] Exception: {e.Exception}\n");
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            HardwareMonitorService.Start();
        }
        catch { }

        _widget = new WidgetWindow(MemoryService, HardwareMonitorService, ShowDashboard);
        _widget.Activate();
    }

    private void ShowDashboard(DashboardSection section)
    {
        if (_dashboard is null)
        {
            _dashboard = new DashboardWindow(MemoryService, HardwareMonitorService, ProcessMemoryService);
            _dashboard.Closed += (_, _) => _dashboard = null;
        }

        _dashboard.ShowSection(section);
        _dashboard.Activate();
    }
}
