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
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        HardwareMonitorService.Start();
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
