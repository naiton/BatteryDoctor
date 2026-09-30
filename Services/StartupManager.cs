// File responsibility: Portable-build startup policy. Deliberately avoids creating persistent Windows startup state.

namespace BatteryDoctor.Services;

/// <summary>
/// Portable RC intentionally does not register itself in the Windows user
/// profile or registry. Startup integration can be added later as a separate,
/// explicit opt-in distribution feature.
/// </summary>
public static class StartupManager
{
    /// <summary>
    /// Portable mode intentionally rejects enabling Windows auto-start so the app leaves no persistent per-user registry state.
    /// </summary>
    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        if (!enabled)
        {
            error = null;
            return true;
        }

        error = "Start with Windows is disabled in the portable build to keep it self-contained.";
        return false;
    }

    /// <summary>
    /// Always reports auto-start disabled for the portable build.
    /// </summary>
    public static bool IsEnabled() => false;
}
