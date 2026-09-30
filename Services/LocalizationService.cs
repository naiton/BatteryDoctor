using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

// File responsibility: Loads language JSON files and provides dictionary-style localized string lookup.

namespace BatteryDoctor.Services;

/// <summary>
/// Loads language JSON files and provides dictionary-style localized string lookup.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
    private string _language = "en-US";

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en-US", "English"),
        new("th-TH", "ไทย")
    ];

    public string CurrentLanguage
    {
        get => _language;
        set
        {
            if (string.Equals(_language, value, StringComparison.OrdinalIgnoreCase)) return;
            Load(value);
            OnPropertyChanged();
        }
    }

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    /// <summary>
    /// Chooses Thai or English from the current Windows UI culture and loads that language at startup.
    /// </summary>
    public LocalizationService()
    {
        var preferred = CultureInfo.CurrentUICulture.Name.StartsWith("th", StringComparison.OrdinalIgnoreCase)
            ? "th-TH"
            : "en-US";
        Load(preferred);
    }

    /// <summary>
    /// Loads one language JSON dictionary, falling back to English if the requested file is missing, then notifies all bindings.
    /// </summary>
    public void Load(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Languages", $"{language}.json");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Languages", "en-US.json");

        var json = File.ReadAllText(path);
        _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        _language = language;
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentLanguage));
    }

    /// <summary>
    /// Raises INotifyPropertyChanged so WPF refreshes localization-dependent bindings.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Data/application type used by LocalizationService.
/// </summary>
public sealed record LanguageOption(string Code, string DisplayName);
