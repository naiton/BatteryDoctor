// File responsibility: Persisted user preferences for tray/background behavior.

namespace BatteryDoctor.Models;

/// <summary>
/// Persisted user preferences for tray/background behavior.
/// </summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool BackgroundMonitoring { get; set; } = true;
    public bool TrayNotifications { get; set; } = true;
}
