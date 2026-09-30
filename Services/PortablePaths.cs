using System.IO;

// File responsibility: Central definition of portable Data/Sessions/Reports/Logs paths plus legacy-data migration helpers.

namespace BatteryDoctor.Services;

/// <summary>
/// Centralized application storage paths. The public/portable build ships with
/// portable.flag, so settings, history, sessions, reports and logs stay beside
/// BatteryDoctor.exe instead of being written into the Windows user profile.
/// </summary>
public static class PortablePaths
{
    private const string MigrationMarkerName = ".legacy-migration-v1-complete";

    public static string RootDirectory { get; } =
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));

    public static string DataDirectory => Path.Combine(RootDirectory, "Data");
    public static string SessionsDirectory => Path.Combine(RootDirectory, "Sessions");
    public static string ReportsDirectory => Path.Combine(RootDirectory, "Reports");
    public static string LogsDirectory => Path.Combine(RootDirectory, "Logs");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public static string DatabasePath => Path.Combine(DataDirectory, "battery-doctor.db");
    public static string PortableFlagPath => Path.Combine(RootDirectory, "portable.flag");

    /// <summary>
    /// Creates all portable runtime folders, verifies they are writable, and performs a one-time best-effort legacy-data copy.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(SessionsDirectory);
        Directory.CreateDirectory(ReportsDirectory);
        Directory.CreateDirectory(LogsDirectory);

        VerifyWritable();
        TryMigrateLegacyUserData();
    }

    /// <summary>
    /// Resolves a stored session path from either an existing legacy absolute path or a portable relative/sample filename.
    /// </summary>
    public static string ResolveSessionPath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;

        if (Path.IsPathRooted(storedPath) && File.Exists(storedPath))
            return storedPath;

        var fileName = Path.GetFileName(storedPath);
        return string.IsNullOrWhiteSpace(fileName)
            ? string.Empty
            : Path.Combine(SessionsDirectory, fileName);
    }

    /// <summary>
    /// Converts a path under the portable application root to a share-safe relative path; external paths are reduced to filename only.
    /// </summary>
    public static string ToPortableRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            var full = Path.GetFullPath(path);
            if (full.StartsWith(RootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Path.GetRelativePath(RootDirectory, full);
        }
        catch
        {
            // Fall through to filename-only sanitization.
        }

        return Path.GetFileName(path);
    }

    /// <summary>
    /// Performs a real write/delete probe and gives a clear error when the portable app is placed in a protected/read-only directory.
    /// </summary>
    private static void VerifyWritable()
    {
        var probe = Path.Combine(DataDirectory, $".write-test-{Environment.ProcessId}.tmp");
        try
        {
            File.WriteAllText(probe, "Battery Doctor portable write test");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"Battery Doctor cannot write to its portable folder: {RootDirectory}. " +
                "Move the BatteryDoctor folder to a writable location such as D:\\Apps, C:\\Tools, Desktop, or a USB drive.",
                ex);
        }
    }

    /// <summary>
    /// Copies legacy AppData/Documents Battery Doctor files into the portable structure once, never deleting the originals.
    /// </summary>
    private static void TryMigrateLegacyUserData()
    {
        var marker = Path.Combine(DataDirectory, MigrationMarkerName);
        if (File.Exists(marker)) return;

        try
        {
            var legacyRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BatteryDoctor");

            // Migration is COPY-ONLY. We never delete legacy AppData/Documents content, so a
            // user can safely fall back to an older Battery Doctor build if needed.
            CopyFileIfMissing(Path.Combine(legacyRoot, "settings.json"), SettingsPath);
            CopyFileIfMissing(Path.Combine(legacyRoot, "battery-doctor.db"), DatabasePath);
            CopyFileIfMissing(Path.Combine(legacyRoot, "battery-doctor.db-shm"), DatabasePath + "-shm");
            CopyFileIfMissing(Path.Combine(legacyRoot, "battery-doctor.db-wal"), DatabasePath + "-wal");
            CopyDirectoryIfPresent(Path.Combine(legacyRoot, "sessions"), SessionsDirectory);
            CopyDirectoryIfPresent(Path.Combine(legacyRoot, "logs"), LogsDirectory);

            var legacyDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(legacyDocuments))
                CopyDirectoryIfPresent(Path.Combine(legacyDocuments, "BatteryDoctor", "Reports"), ReportsDirectory);

            File.WriteAllText(marker,
                $"Migrated/copy-checked legacy Battery Doctor user-profile data at {DateTimeOffset.Now:O}.{Environment.NewLine}" +
                "Legacy files were not deleted.");
        }
        catch
        {
            // Migration is best effort. Portable storage still works even if an
            // old profile folder is unavailable or partially locked.
        }
    }

    /// <summary>
    /// Recursively copies legacy files that do not already exist in the portable destination.
    /// </summary>
    private static void CopyDirectoryIfPresent(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
            CopyFileIfMissing(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var child in Directory.EnumerateDirectories(source))
            CopyDirectoryIfPresent(child, Path.Combine(destination, Path.GetFileName(child)));
    }

    /// <summary>
    /// Copies one legacy file only when the source exists and the destination has not already been created.
    /// </summary>
    private static void CopyFileIfMissing(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination)) return;
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        File.Copy(source, destination, overwrite: false);
    }
}
