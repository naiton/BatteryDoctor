// File responsibility: Configuration model for the project and optional donation/support URLs.

namespace BatteryDoctor.Models;

/// <summary>
/// Configuration model for the project and optional donation/support URLs.
/// </summary>
public sealed class AppLinks
{
    public string? ProjectUrl { get; set; }
    public string? SupportUrl { get; set; }
}
