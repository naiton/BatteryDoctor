using System.IO;
using System.Windows;
using System.Windows.Threading;

// File responsibility: Application bootstrap and last-resort exception handling. Initializes portable storage before the main window is created.

namespace BatteryDoctor;

/// <summary>
/// WPF application entry point for Battery Doctor.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Initializes portable folders and global exception hooks, then creates the main window. The --background switch starts the window hidden when background monitoring is requested.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            BatteryDoctor.Services.PortablePaths.Initialize();
            var startHidden = e.Args.Any(x => string.Equals(x, "--background", StringComparison.OrdinalIgnoreCase));
            var window = new BatteryDoctor.MainWindow(startHidden);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            var logPath = WriteCrashLog("startup", ex);
            System.Windows.MessageBox.Show(
                $"Battery Doctor could not start.\n\n{ex.GetType().Name}: {ex.Message}\n\nCrash log:\n{logPath}",
                "Battery Doctor - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>
    /// Handles otherwise-unhandled WPF UI exceptions, writes a crash log, informs the user, and marks the exception handled so the diagnostic message can be seen.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = WriteCrashLog("ui", e.Exception);
        System.Windows.MessageBox.Show(
            $"Battery Doctor encountered an unexpected error.\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\nCrash log:\n{logPath}",
            "Battery Doctor - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>
    /// Records process/AppDomain exceptions that bypass the WPF dispatcher.
    /// </summary>
    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteCrashLog("domain", ex);
    }

    /// <summary>
    /// Records unobserved Task exceptions and marks them observed to avoid escalation during finalization.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("task", e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// Writes diagnostic exception details to the portable Logs folder and returns the created path (or a safe fallback message if logging itself fails).
    /// </summary>
    private static string WriteCrashLog(string area, Exception ex)
    {
        try
        {
            var dir = BatteryDoctor.Services.PortablePaths.LogsDirectory;
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{area}.log");
            File.WriteAllText(path,
                $"Battery Doctor crash log{Environment.NewLine}" +
                $"Time: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                $"Area: {area}{Environment.NewLine}" +
                $"OS: {Environment.OSVersion}{Environment.NewLine}" +
                $".NET: {Environment.Version}{Environment.NewLine}{Environment.NewLine}" +
                ex);
            return path;
        }
        catch
        {
            return "(unable to write crash log)";
        }
    }
}
