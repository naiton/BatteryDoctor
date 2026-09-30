// File responsibility: Event payload for a user-visible background/tray battery warning.

namespace BatteryDoctor.Models;

/// <summary>
/// Event payload for a user-visible background/tray battery warning.
/// </summary>
public sealed class BackgroundAlertEventArgs : EventArgs
{
    /// <summary>
    /// Initializes background alert event args.
    /// </summary>
    public BackgroundAlertEventArgs(string severity, string title, string detail, DateTimeOffset capturedAt)
    {
        Severity = severity;
        Title = title;
        Detail = detail;
        CapturedAt = capturedAt;
    }

    public string Severity { get; }
    public string Title { get; }
    public string Detail { get; }
    public DateTimeOffset CapturedAt { get; }
}
