using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

// File responsibility: Fallback reader that invokes powercfg /batteryreport when WMI omits design/full capacity or cycle count.

namespace BatteryDoctor.Services;

internal sealed record BatteryReportFallback(
    uint? DesignCapacityMWh,
    uint? FullChargeCapacityMWh,
    uint? CycleCount,
    string? Manufacturer,
    string? SerialNumber,
    string? Name);

internal static class PowerCfgBatteryReportReader
{
    /// <summary>
    /// Runs powercfg /batteryreport to a temporary XML file and extracts capacity/cycle metadata used when WMI providers omit those values.
    /// </summary>
    public static BatteryReportFallback? TryRead()
    {
        if (!OperatingSystem.IsWindows()) return null;

        var temp = Path.Combine(Path.GetTempPath(), $"battery-doctor-{Guid.NewGuid():N}.xml");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = $"/batteryreport /output \"{temp}\" /xml",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null) return null;
            if (!process.WaitForExit(8000))
            {
                try { process.Kill(true); } catch { }
                return null;
            }
            if (process.ExitCode != 0 || !File.Exists(temp)) return null;

            var doc = XDocument.Load(temp);
            var battery = doc.Descendants().FirstOrDefault(x =>
                x.Name.LocalName.Equals("Battery", StringComparison.OrdinalIgnoreCase));
            if (battery is null) return null;

            return new BatteryReportFallback(
                Number(battery, "DesignCapacity"),
                Number(battery, "FullChargeCapacity"),
                Number(battery, "CycleCount"),
                Text(battery, "Manufacturer"),
                Text(battery, "SerialNumber"),
                Text(battery, "Id") ?? Text(battery, "Name"));
        }
        catch
        {
            return null;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    /// <summary>
    /// Finds a child XML element by local name regardless of namespace.
    /// </summary>
    private static XElement? Child(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(x =>
            x.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads and trims a battery-report XML element as text.
    /// </summary>
    private static string? Text(XElement parent, string localName)
        => Child(parent, localName)?.Value.Trim() is { Length: > 0 } s ? s : null;

    /// <summary>
    /// Extracts a positive integer from a battery-report XML field that may contain formatting text.
    /// </summary>
    private static uint? Number(XElement parent, string localName)
    {
        var raw = Text(parent, localName);
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Battery report XML values can be plain numbers or formatted strings.
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return uint.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;
    }
}
