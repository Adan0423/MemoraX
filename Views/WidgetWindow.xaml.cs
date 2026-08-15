using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using StandbyMemoryManager.Interop;
using StandbyMemoryManager.Services;
using StandbyMemoryManager.ViewModels;
using Windows.Graphics;

namespace StandbyMemoryManager.Views;

public sealed partial class WidgetWindow : Window
{
    private readonly Action<DashboardSection> _showDashboard;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    public MonitorViewModel ViewModel { get; }

    public WidgetWindow(MemoryService memory, HardwareMonitorService hardware, Action<DashboardSection> showDashboard)
    {
        InitializeComponent();
        ViewModel = new MonitorViewModel(memory, hardware);
        _showDashboard = showDashboard;

        ConfigureWindow();
        _timer.Tick += async (_, _) => await ViewModel.RefreshAsync(includeHardware: true);
        _timer.Start();
        _ = ViewModel.RefreshAsync(includeHardware: true);
        Closed += (_, _) => _timer.Stop();
    }

    private void ConfigureWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(360, 220));
        appWindow.SetIcon("Assets/app_icon.ico");

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
            presenter.SetBorderAndTitleBar(true, false);
        }

        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        if (area is not null)
        {
            var work = area.WorkArea;
            appWindow.Move(new PointInt32(work.X + work.Width - 384, work.Y + 32));
        }
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as UIElement);
        if (point.Properties.IsLeftButtonPressed)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
        }
    }

    private async void Clean_Click(object sender, RoutedEventArgs e) => await ViewModel.CleanAsync();
    private void Details_Click(object sender, RoutedEventArgs e) => _showDashboard(DashboardSection.Overview);
    private void Processes_Click(object sender, RoutedEventArgs e) => _showDashboard(DashboardSection.Processes);
    private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
