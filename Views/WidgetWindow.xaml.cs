using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Veltrixa.Interop;
using Veltrixa.Services;
using Veltrixa.ViewModels;
using Windows.Graphics;

namespace Veltrixa.Views;

public sealed partial class WidgetWindow : Window
{
    private readonly Action<DashboardSection> _showDashboard;
    private readonly MonitoringCoordinator _monitor;
    public MonitorViewModel ViewModel { get; }

    public WidgetWindow(MonitoringCoordinator monitor, Action<DashboardSection> showDashboard)
    {
        _monitor = monitor;
        ViewModel = new MonitorViewModel(((App)Application.Current).MemoryService, monitor, Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
        InitializeComponent();
        _showDashboard = showDashboard;

        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(RootGrid_PointerPressed), handledEventsToo: true);

        ConfigureWindow();
        _monitor.SetWidgetVisible(true);
        Closed += (_, _) => { _monitor.SetWidgetVisible(false); ViewModel.Dispose(); };
    }

    private void ConfigureWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Changed += (_, _) =>
        {
            var visible = appWindow.Presenter is not OverlappedPresenter presenter ||
                          presenter.State != OverlappedPresenterState.Minimized;
            _monitor.SetWidgetVisible(visible);
        };
        appWindow.Resize(new SizeInt32(190, 82));
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
            appWindow.Move(new PointInt32(work.X + work.Width - 350, work.Y + 32));
        }
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(sender as UIElement).Properties.IsLeftButtonPressed)
        {
            if (IsButtonClick(e.OriginalSource as DependencyObject))
                return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
            e.Handled = true;
        }
    }

    private static bool IsButtonClick(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void WidgetDragRegion_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = false;
    }

    private async void Clean_Click(object sender, RoutedEventArgs e) => await ViewModel.CleanAsync();
    private async void Optimize_Click(object sender, RoutedEventArgs e) => await ViewModel.CleanAsync();
    private void Details_Click(object sender, RoutedEventArgs e) => _showDashboard(DashboardSection.Overview);
    private void Processes_Click(object sender, RoutedEventArgs e) => _showDashboard(DashboardSection.Processes);

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
