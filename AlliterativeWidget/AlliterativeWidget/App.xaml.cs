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
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace AlliterativeWidget;

public partial class App : Application
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private Window? _window;
    private WidgetViewModel? _viewModel;
    private GymViewModel? _gymViewModel;
    private GymService? _gymService;
    private WidgetContent? _content;
    private TaskbarIcon? _trayIcon;
    private bool _isWidgetVisible = true;
    private WidgetConfig? _config;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize services
        var persistenceService = new PersistenceService();
        _config = persistenceService.LoadConfig();

        // Register for auto-start if configured
        if (_config.Ui.AutoStart && !StartupManager.IsRegistered())
        {
            StartupManager.RegisterStartup();
        }

        // Create services
        var contentEngine = new ContentEngine(persistenceService);

        // Create gym service if enabled
        if (_config.Gym.Enabled)
        {
            _gymService = new GymService(_config.Gym);
            _gymViewModel = new GymViewModel(_gymService, _config.Gym);
        }

        // Create window
        _window = new Window();
        _window.SystemBackdrop = null;

        // Create and set content wrapped in a background grid to eliminate white edges
        _content = new WidgetContent();
        _content.WidgetLeftClicked += OnWidgetLeftClicked;
        _content.WidgetRightClicked += OnWidgetRightClicked;
        _content.GymRefreshRequested += OnGymRefreshRequested;

        var rootGrid = new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 26, 26, 26)) // #1A1A1A
        };
        rootGrid.Children.Add(_content);
        _window.Content = rootGrid;

        // Set gym view model if enabled
        if (_gymViewModel != null)
        {
            _content.SetGymViewModel(_gymViewModel);
        }

        // Configure window after it's created
        ConfigureWindow(_config);

        // Set window icon
        SetWindowIcon();

        // Setup system tray icon
        SetupTrayIcon();

        // Create scheduler with dispatcher queue
        var schedulerService = new SchedulerService(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

        // Create ViewModel
        _viewModel = new WidgetViewModel(contentEngine, schedulerService);
        _viewModel.ContentRefreshed += OnContentRefreshed;

        // Activate window and initialize for all days (including weekends)
        _window.Activate();
        _viewModel.Initialize();

        // Initialize gym data
        if (_gymViewModel != null)
        {
            await _gymViewModel.InitializeAsync();
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void SetWindowIcon()
    {
        if (_window == null) return;
        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void SetupTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Alliterative Widget",
            ContextMenuMode = ContextMenuMode.PopupMenu
        };
        if (File.Exists(iconPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);
        }

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

    private async void OnGymRefreshRequested(object? sender, EventArgs e)
    {
        // Force refresh gym data when left-clicked on gym tile
        if (_gymViewModel != null)
        {
            await _gymViewModel.ForceRefreshAsync();
        }
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _viewModel?.Cleanup();
        _content?.Cleanup();
        _gymService?.Dispose();
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

        // Calculate height based on gym enabled state
        // Base height: 140px for alliterative tile only
        // With gym: compact layout ~292px (280 base + 12 for cycling row + gap)
        var height = config.Gym.Enabled ? 292 : config.Ui.Height;

        // Scale to physical pixels — AppWindow.Resize expects device pixels, not DIPs
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi / 96.0;

        // Set window size
        appWindow.Resize(new Windows.Graphics.SizeInt32(
            (int)(config.Ui.Width * scale),
            (int)(height * scale)));

        // Position window in top-right corner
        var positionService = new WindowPositionService();
        var (x, y) = positionService.CalculateTopRightPosition(
            hwnd,
            config.Ui.Width,
            height,
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
