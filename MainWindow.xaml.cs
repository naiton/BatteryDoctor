using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BatteryDoctor.Models;
using BatteryDoctor.ViewModels;
using Forms = System.Windows.Forms;

// File responsibility: WPF window shell. Owns window chrome, the polling timer, system-tray integration, and forwards UI lifecycle events to MainViewModel.

namespace BatteryDoctor;

/// <summary>
/// Top-level window and system-tray host for Battery Doctor.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _monitorTimer;
    private readonly bool _startHidden;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ContextMenuStrip _trayMenu;
    private readonly Forms.ToolStripMenuItem _trayOpenItem;
    private readonly Forms.ToolStripMenuItem _trayRefreshItem;
    private readonly Forms.ToolStripMenuItem _trayHideItem;
    private readonly Forms.ToolStripMenuItem _trayExitItem;
    private bool _trayMinimizeNoticeShown;

    /// <summary>
    /// Builds the WPF shell, binds MainViewModel, configures the monitor timer, and creates the WinForms NotifyIcon/context menu used by system-tray mode.
    /// </summary>
    public MainWindow(bool startHidden = false)
    {
        InitializeComponent();
        _startHidden = startHidden;
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _monitorTimer.Tick += OnMonitorTick;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.BackgroundAlertRaised += OnBackgroundAlertRaised;

        _trayMenu = new Forms.ContextMenuStrip();
        _trayOpenItem = new Forms.ToolStripMenuItem();
        _trayRefreshItem = new Forms.ToolStripMenuItem();
        _trayHideItem = new Forms.ToolStripMenuItem();
        _trayExitItem = new Forms.ToolStripMenuItem();
        _trayOpenItem.Click += (_, _) => Dispatcher.Invoke(RestoreFromTray);
        _trayRefreshItem.Click += (_, _) => Dispatcher.Invoke(() => _viewModel.RefreshCommand.Execute(null));
        _trayHideItem.Click += (_, _) => Dispatcher.Invoke(HideToTray);
        _trayExitItem.Click += (_, _) => Dispatcher.Invoke(Close);
        _trayMenu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _trayOpenItem,
            _trayRefreshItem,
            _trayHideItem,
            new Forms.ToolStripSeparator(),
            _trayExitItem
        });

        _trayIcon = new Forms.NotifyIcon
        {
            Visible = true,
            Icon = System.Drawing.SystemIcons.Information,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);
        UpdateTrayLocalization();
        UpdateTrayText();

        Loaded += OnLoaded;
        Closed += OnClosed;
        StateChanged += OnWindowStateChanged;
    }

    /// <summary>
    /// Performs one-time asynchronous view-model initialization, starts periodic monitoring, and optionally hides to tray for background startup.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
        UpdateMonitorInterval();
        UpdateTrayText();
        _monitorTimer.Start();

        if (_startHidden && _viewModel.BackgroundMonitoring)
            HideToTray();
    }

    /// <summary>
    /// Runs one periodic monitor cycle. Hidden windows skip polling when background monitoring is disabled.
    /// </summary>
    private async void OnMonitorTick(object? sender, EventArgs e)
    {
        UpdateMonitorInterval();
        if (!IsVisible && !_viewModel.BackgroundMonitoring)
            return;

        await _viewModel.MonitorTickAsync();
        UpdateTrayText();
    }

    /// <summary>
    /// Keeps tray text/localization and polling cadence synchronized with view-model state changes.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsTestRunning))
            UpdateMonitorInterval();

        if (e.PropertyName is nameof(MainViewModel.SelectedLanguage) or
            nameof(MainViewModel.BackgroundMonitoring) or
            nameof(MainViewModel.TrayNotifications) or
            nameof(MainViewModel.CurrentCharge) or
            nameof(MainViewModel.PowerState) or
            nameof(MainViewModel.DiagnosticOverall))
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTrayLocalization();
                UpdateTrayText();
            });
        }
    }

    /// <summary>
    /// Uses a faster 2-second poll while a test is running and a lower-overhead 5-second interval during ordinary monitoring.
    /// </summary>
    private void UpdateMonitorInterval()
    {
        _monitorTimer.Interval = _viewModel.IsTestRunning
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Stops timers, detaches handlers, and disposes WinForms tray resources to prevent process/resource leaks.
    /// </summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _monitorTimer.Stop();
        _monitorTimer.Tick -= OnMonitorTick;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.BackgroundAlertRaised -= OnBackgroundAlertRaised;
        StateChanged -= OnWindowStateChanged;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();
    }

    /// <summary>
    /// Handles the minimize click event.
    /// </summary>
    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Handles the maximize restore click event.
    /// </summary>
    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    /// <summary>
    /// Handles the close click event.
    /// </summary>
    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Implements custom-title-bar drag and double-click maximize/restore behavior.
    /// </summary>
    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed && WindowState != WindowState.Maximized)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
    }

    /// <summary>
    /// Switches between maximized and normal window states.
    /// </summary>
    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>
    /// Updates the custom maximize icon and moves minimized windows to the tray when that preference is enabled.
    /// </summary>
    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeRestoreButton is not null)
            MaximizeRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";

        if (WindowState == WindowState.Minimized && _viewModel.MinimizeToTray)
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(HideToTray));
    }

    /// <summary>
    /// Hides the WPF window from the taskbar while keeping the process and background monitor alive.
    /// </summary>
    private void HideToTray()
    {
        if (!IsVisible) return;
        ShowInTaskbar = false;
        Hide();

        if (!_trayMinimizeNoticeShown && _viewModel.TrayNotifications)
        {
            _trayMinimizeNoticeShown = true;
            ShowTrayBalloon(
                _viewModel.Loc["TrayMinimizedTitle"],
                _viewModel.Loc["TrayMinimizedDetail"],
                Forms.ToolTipIcon.Info);
        }
    }

    /// <summary>
    /// Restores, activates, and focuses the WPF window after a tray interaction.
    /// </summary>
    private void RestoreFromTray()
    {
        if (!IsVisible) Show();
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    /// <summary>
    /// Refreshes tray-menu labels from the currently selected localization dictionary.
    /// </summary>
    private void UpdateTrayLocalization()
    {
        _trayOpenItem.Text = _viewModel.Loc["TrayOpen"];
        _trayRefreshItem.Text = _viewModel.Loc["TrayRefresh"];
        _trayHideItem.Text = _viewModel.Loc["TrayHide"];
        _trayExitItem.Text = _viewModel.Loc["TrayExit"];
    }

    /// <summary>
    /// Builds the short NotifyIcon tooltip from current charge and power state, respecting Windows tooltip length limits.
    /// </summary>
    private void UpdateTrayText()
    {
        var text = $"Battery Doctor · {_viewModel.CurrentCharge} · {_viewModel.PowerState}";
        _trayIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    /// <summary>
    /// Maps a diagnostic alert severity to a Windows tray balloon icon and displays the alert on the UI dispatcher.
    /// </summary>
    private void OnBackgroundAlertRaised(object? sender, BackgroundAlertEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var icon = e.Severity == "critical"
                ? Forms.ToolTipIcon.Error
                : e.Severity == "warn" ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info;
            ShowTrayBalloon(e.Title, e.Detail, icon);
        });
    }

    /// <summary>
    /// Displays one tray balloon notification with the supplied title, detail text, and severity icon.
    /// </summary>
    private void ShowTrayBalloon(string title, string detail, Forms.ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = detail;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(7000);
    }
}
