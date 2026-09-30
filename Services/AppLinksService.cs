using System.IO;
using System.Text.Json;
using BatteryDoctor.Models;

// File responsibility: Loads optional project/support URLs shipped beside the executable.

namespace BatteryDoctor.Services;

/// <summary>
/// Loads optional project/support URLs shipped beside the executable.
/// </summary>
public sealed class AppLinksService
{
    /// <summary>
    /// Reads AppLinks.json from the application directory. Invalid or missing configuration falls back to empty links rather than preventing startup.
    /// </summary>
    public AppLinks Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "AppLinks.json");
            if (!File.Exists(path)) return new AppLinks();
            return JsonSerializer.Deserialize<AppLinks>(File.ReadAllText(path)) ?? new AppLinks();
        }
        catch
        {
            return new AppLinks();
        }
    }
}
