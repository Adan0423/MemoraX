using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Veltrixa.Services;
using Veltrixa.ViewModels;
using Windows.Graphics;

namespace Veltrixa.Views;

public sealed partial class DashboardWindow : Window
{
    private readonly MonitoringCoordinator _monitor;
    public MonitorViewModel ViewModel { get; }

    public DashboardWindow(MonitoringCoordinator monitor)
    {
        _monitor = monitor;
        ViewModel = new MonitorViewModel(((App)Application.Current).MemoryService, monitor, Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
        InitializeComponent();
        ConfigureWindow();
        MainTabs.SelectionChanged += (_, _) => _monitor.SetDashboardState(true, MainTabs.SelectedIndex == 2);
        _monitor.SetDashboardState(true, false);
        Closed += (_, _) => { _monitor.SetDashboardState(false, false); ViewModel.Dispose(); };
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

        _monitor.SetDashboardState(true, section == DashboardSection.Processes);
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
            _monitor.SetDashboardState(visible, visible && MainTabs.SelectedIndex == 2);
        };
        appWindow.Resize(new SizeInt32(1080, 720));
        appWindow.Title = "Veltrixa";
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

    private void ProcessSearch_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e) =>
        ViewModel.FilterProcesses(((Microsoft.UI.Xaml.Controls.TextBox)sender).Text);

    private void MainTabs_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e) =>
        _monitor.SetDashboardState(true, MainTabs.SelectedIndex == 2);

    private void ThemeSelector_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is not Microsoft.UI.Xaml.Controls.ComboBoxItem item) return;
        RootGrid.RequestedTheme = item.Tag?.ToString() switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}
