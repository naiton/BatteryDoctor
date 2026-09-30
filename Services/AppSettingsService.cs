using System.IO;
using System.Text.Json;
using BatteryDoctor.Models;

// File responsibility: Loads and atomically saves user preferences in the portable Data folder.

namespace BatteryDoctor.Services;

/// <summary>
/// Loads and atomically saves user preferences in the portable Data folder.
/// </summary>
public sealed class AppSettingsService
{
    private readonly string _settingsPath;

    /// <summary>
    /// Initializes the portable settings location under Data.
    /// </summary>
    public AppSettingsService()
    {
        Directory.CreateDirectory(PortablePaths.DataDirectory);
        _settingsPath = PortablePaths.SettingsPath;
    }

    public string SettingsPath => _settingsPath;

    /// <summary>
    /// Deserializes settings.json; corrupt/missing settings safely fall back to defaults.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Serializes preferences to a temporary file and atomically replaces settings.json to reduce corruption risk.
    /// </summary>
    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var temp = _settingsPath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _settingsPath, overwrite: true);
    }
}
