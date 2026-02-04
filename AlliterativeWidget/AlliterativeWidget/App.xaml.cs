using AlliterativeWidget.Helpers;
using AlliterativeWidget.Models;
using AlliterativeWidget.Services;
using AlliterativeWidget.ViewModels;
using AlliterativeWidget.Views;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace AlliterativeWidget;

public partial class App : Application
{
    private Window? _window;
    private WidgetViewModel? _viewModel;
    private WidgetContent? _content;
    private TaskbarIcon? _trayIcon;
    private bool _isWidgetVisible = true;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize services
        var persistenceService = new PersistenceService();
        var config = persistenceService.LoadConfig();

        // Register for auto-start if configured
        if (config.Ui.AutoStart && !StartupManager.IsRegistered())
        {
            StartupManager.RegisterStartup();
        }

        // Create services
        var contentEngine = new ContentEngine(persistenceService);

        // Create window
        _window = new Window();

        // Create and set content
        _content = new WidgetContent();
        _content.WidgetLeftClicked += OnWidgetLeftClicked;
        _content.WidgetRightClicked += OnWidgetRightClicked;
        _window.Content = _content;

        // Configure window after it's created
        ConfigureWindow(config);

        // Setup system tray icon
        SetupTrayIcon();

        // Create scheduler with dispatcher queue
        var schedulerService = new SchedulerService(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

        // Create ViewModel
        _viewModel = new WidgetViewModel(contentEngine, schedulerService);
        _viewModel.ContentRefreshed += OnContentRefreshed;

        // Check if today is a workday
        if (DateTimeHelper.IsWorkday())
        {
            _window.Activate();
            _viewModel.Initialize();
        }
        else
        {
            // Weekend - hide window but keep app running for scheduler
            _viewModel.Initialize();
            _isWidgetVisible = false;
            HideWindow();
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Alliterative Widget",
            ContextMenuMode = ContextMenuMode.PopupMenu
        };

        // Create context menu
        var contextMenu = new MenuFlyout();

        var showHideItem = new MenuFlyoutItem
        {
            Text = "Hide Widget"
        };
        showHideItem.Click += (s, e) => ToggleWidgetVisibility();
        contextMenu.Items.Add(showHideItem);

        contextMenu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem
        {
            Text = "Exit"
        };
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextFlyout = contextMenu;

        // Left-click to toggle visibility
        _trayIcon.LeftClickCommand = new RelayCommand(ToggleWidgetVisibility);

        _trayIcon.ForceCreate();
    }

    private void ToggleWidgetVisibility()
    {
        if (_isWidgetVisible)
        {
            HideWindow();
            _isWidgetVisible = false;
            UpdateTrayMenuText("Show Widget");
        }
        else
        {
            ShowWindow();
            _isWidgetVisible = true;
            UpdateTrayMenuText("Hide Widget");
        }
    }

    private void UpdateTrayMenuText(string text)
    {
        if (_trayIcon?.ContextFlyout is MenuFlyout menu && menu.Items.Count > 0)
        {
            if (menu.Items[0] is MenuFlyoutItem item)
            {
                item.Text = text;
            }
        }
    }

    private void OnWidgetLeftClicked(object? sender, EventArgs e)
    {
        // Hide widget when left-clicked
        HideWindow();
        _isWidgetVisible = false;
        UpdateTrayMenuText("Show Widget");
    }

    private void OnWidgetRightClicked(object? sender, EventArgs e)
    {
        // Regenerate content when right-clicked
        _viewModel?.RefreshContent();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _viewModel?.Cleanup();
        _window?.Close();
        Environment.Exit(0);
    }

    private void ConfigureWindow(WidgetConfig config)
    {
        if (_window == null) return;

        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Configure presenter - no resize, no titlebar, NOT always-on-top (desktop widget style)
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = false;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        // Set window size
        appWindow.Resize(new Windows.Graphics.SizeInt32(config.Ui.Width, config.Ui.Height));

        // Position window in top-right corner
        var positionService = new WindowPositionService();
        var (x, y) = positionService.CalculateTopRightPosition(
            hwnd,
            config.Ui.Width,
            config.Ui.Height,
            config.Ui.Padding);
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private async void OnContentRefreshed(object? sender, DailyContent content)
    {
        if (_content != null)
        {
            await _content.UpdateContentAsync(content);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WidgetViewModel.IsVisible))
        {
            if (_viewModel?.IsVisible == true)
            {
                ShowWindow();
                _isWidgetVisible = true;
                UpdateTrayMenuText("Hide Widget");
            }
            else
            {
                HideWindow();
                _isWidgetVisible = false;
                UpdateTrayMenuText("Show Widget");
            }
        }
    }

    private void ShowWindow()
    {
        if (_window == null) return;
        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Show();
    }

    private void HideWindow()
    {
        if (_window == null) return;
        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Hide();
    }
}
