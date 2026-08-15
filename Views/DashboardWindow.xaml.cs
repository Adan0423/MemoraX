using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using StandbyMemoryManager.Services;
using StandbyMemoryManager.ViewModels;
using Windows.Graphics;

namespace StandbyMemoryManager.Views;

public sealed partial class DashboardWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    public MonitorViewModel ViewModel { get; }

    public DashboardWindow(MemoryService memory, HardwareMonitorService hardware, ProcessMemoryService processes)
    {
        InitializeComponent();
        ViewModel = new MonitorViewModel(memory, hardware, processes);
        ConfigureWindow();

        _timer.Tick += async (_, _) =>
        {
            var includeProcesses = MainTabs.SelectedIndex == 2;
            await ViewModel.RefreshAsync(includeHardware: true, includeProcesses: includeProcesses);
        };
        _timer.Start();
        _ = ViewModel.RefreshAsync(includeHardware: true, includeProcesses: false);
        Closed += (_, _) => _timer.Stop();
    }

    public void ShowSection(DashboardSection section)
    {
        MainTabs.SelectedIndex = section switch
        {
            DashboardSection.Overview => 0,
            DashboardSection.Hardware => 1,
            DashboardSection.Processes => 2,
            _ => 0
        };

        _ = ViewModel.RefreshAsync(includeHardware: true, includeProcesses: section == DashboardSection.Processes);
    }

    private void ConfigureWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(960, 480));
        appWindow.Title = "MemoraX";
        try
        {
            var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "app_icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Ignorar errores al cargar el icono
        }
    }

    private async void Clean_Click(object sender, RoutedEventArgs e) => await ViewModel.CleanAsync();
}
