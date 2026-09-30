using System.Management;
using System.Text;
using BatteryDoctor.Models;

// File responsibility: Windows telemetry provider. Merges ROOT/WMI, Win32_Battery and powercfg fallback data into BatterySnapshot.

namespace BatteryDoctor.Services;

/// <summary>
/// Windows telemetry provider. Merges ROOT/WMI, Win32_Battery and powercfg fallback data into BatterySnapshot.
/// </summary>
public sealed class WindowsBatteryProvider : IBatteryProvider
{
    private static readonly object FallbackGate = new();
    private static BatteryReportFallback? _fallbackCache;
    private static DateTimeOffset _fallbackReadAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Reads Windows battery telemetry on a worker thread so WMI/powercfg work never blocks the WPF UI thread.
    /// </summary>
    public Task<BatterySnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(ReadInternal, cancellationToken);
    }

    /// <summary>
    /// Queries Windows battery providers, merges their fields, applies cached powercfg fallback data, normalizes chemistry, and builds one BatterySnapshot.
    /// </summary>
    private static BatterySnapshot ReadInternal()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Battery Doctor supports Windows only.");

        // No single Windows provider exposes every field on every laptop. Read several sources
        // and merge them, using powercfg only for values WMI did not provide.
        var staticData = First(@"root\WMI", "SELECT * FROM BatteryStaticData");
        var fullData = First(@"root\WMI", "SELECT * FROM BatteryFullChargedCapacity");
        var cycleData = First(@"root\WMI", "SELECT * FROM BatteryCycleCount");
        var statusData = First(@"root\WMI", "SELECT * FROM BatteryStatus");
        var win32 = First(@"root\CIMV2", "SELECT * FROM Win32_Battery");

        var design = UInt(staticData, "DesignedCapacity") ?? UInt(win32, "DesignCapacity");
        var full = UInt(fullData, "FullChargedCapacity");
        var cycle = UInt(cycleData, "CycleCount");
        BatteryReportFallback? report = null;
        if (design is null or 0 || full is null or 0 || cycle is null or 0)
            report = GetBatteryReportFallback();
        design = NonZero(design) ?? report?.DesignCapacityMWh;
        full = NonZero(full) ?? report?.FullChargeCapacityMWh;
        cycle = NonZero(cycle) ?? report?.CycleCount;
        var voltage = UInt(statusData, "Voltage") ?? UInt64AsUInt(win32, "DesignVoltage");
        var remaining = UInt(statusData, "RemainingCapacity");

        var chargeRate = Int(statusData, "ChargeRate");
        var dischargeRate = Int(statusData, "DischargeRate");
        if (dischargeRate is 0) dischargeRate = null;
        if (chargeRate is 0) chargeRate = null;

        // Battery temperature is optional. Use the battery class interface rather than generic
        // ACPI/CPU thermal zones so a displayed temperature always belongs to the battery pack.
        var temperature = BatteryTemperatureProvider.TryRead();

        return new BatterySnapshot
        {
            CapturedAt = DateTimeOffset.Now,
            Name = Text(win32, "Name") ?? Text(staticData, "DeviceName") ?? report?.Name,
            Manufacturer = Text(staticData, "ManufactureName") ?? Text(win32, "Manufacturer") ?? report?.Manufacturer,
            SerialNumber = Text(staticData, "SerialNumber") ?? report?.SerialNumber,
            Chemistry = ChemistryText(staticData),
            DesignCapacityMWh = NonZero(design),
            FullChargeCapacityMWh = NonZero(full),
            RemainingCapacityMWh = NonZero(remaining),
            CycleCount = NonZero(cycle),
            VoltageMV = NonZero(voltage),
            TemperatureC = temperature?.Celsius,
            TemperatureSource = temperature?.Source,
            ChargeRateMW = chargeRate,
            DischargeRateMW = dischargeRate,
            EstimatedChargePercent = Int(win32, "EstimatedChargeRemaining"),
            EstimatedRuntimeMinutes = NormalizeRuntime(Int(win32, "EstimatedRunTime")),
            PowerOnline = Bool(statusData, "PowerOnline"),
            Charging = Bool(statusData, "Charging"),
            Discharging = Bool(statusData, "Discharging"),
            Critical = Bool(statusData, "Critical")
        };
    }


    /// <summary>
    /// Caches expensive powercfg battery-report fallback data for 30 minutes.
    /// </summary>
    private static BatteryReportFallback? GetBatteryReportFallback()
    {
        lock (FallbackGate)
        {
            if (DateTimeOffset.Now - _fallbackReadAt < TimeSpan.FromMinutes(30))
                return _fallbackCache;

            _fallbackCache = PowerCfgBatteryReportReader.TryRead();
            _fallbackReadAt = DateTimeOffset.Now;
            return _fallbackCache;
        }
    }

    /// <summary>
    /// Executes a WMI query and returns the first object while treating provider/permission failures as missing optional data.
    /// </summary>
    private static ManagementObject? First(string scope, string query)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject item in searcher.Get())
                return item;
        }
        catch (ManagementException)
        {
            // Some OEM firmware does not expose every battery WMI class.
        }
        catch (UnauthorizedAccessException)
        {
        }
        return null;
    }


    /// <summary>
    /// Decodes WMI chemistry values, including packed four-character codes such as LION, into human-readable chemistry names.
    /// </summary>
    private static string? ChemistryText(ManagementBaseObject? obj)
    {
        var raw = obj?["Chemistry"];
        if (raw is null) return null;

        try
        {
            var value = Convert.ToUInt32(raw);
            var bytes = BitConverter.GetBytes(value);
            var code = Encoding.ASCII.GetString(bytes).Trim('\0', ' ').ToUpperInvariant();
            if (code.Length > 0 && code.All(ch => ch >= 32 && ch <= 126))
            {
                return code switch
                {
                    "LION" => "Lithium-ion",
                    "LIPO" => "Lithium polymer",
                    "NIMH" => "Nickel-metal hydride",
                    "NICD" => "Nickel-cadmium",
                    "PBAC" => "Lead-acid",
                    _ => code
                };
            }

            return value switch
            {
                1 => "Other",
                2 => "Unknown",
                3 => "Lead-acid",
                4 => "Nickel-cadmium",
                5 => "Nickel-metal hydride",
                6 => "Lithium-ion",
                7 => "Zinc-air",
                8 => "Lithium polymer",
                _ => "Unknown"
            };
        }
        catch
        {
            var text = raw.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }

    /// <summary>
    /// Reads a trimmed string property from a WMI object.
    /// </summary>
    private static string? Text(ManagementBaseObject? obj, string key)
        => obj?[key]?.ToString()?.Trim() is { Length: > 0 } s ? s : null;

    /// <summary>
    /// Converts a WMI property to nullable UInt32 without propagating conversion/provider errors.
    /// </summary>
    private static uint? UInt(ManagementBaseObject? obj, string key)
    {
        try { return obj?[key] is null ? null : Convert.ToUInt32(obj[key]); }
        catch { return null; }
    }

    /// <summary>
    /// Converts an unsigned 64-bit WMI value only when it fits safely into UInt32.
    /// </summary>
    private static uint? UInt64AsUInt(ManagementBaseObject? obj, string key)
    {
        try
        {
            if (obj?[key] is null) return null;
            var value = Convert.ToUInt64(obj[key]);
            return value <= uint.MaxValue ? (uint)value : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Converts a WMI property to nullable Int32 without propagating conversion/provider errors.
    /// </summary>
    private static int? Int(ManagementBaseObject? obj, string key)
    {
        try { return obj?[key] is null ? null : Convert.ToInt32(obj[key]); }
        catch { return null; }
    }

    /// <summary>
    /// Converts a WMI property to nullable Boolean without propagating conversion/provider errors.
    /// </summary>
    private static bool? Bool(ManagementBaseObject? obj, string key)
    {
        try { return obj?[key] is null ? null : Convert.ToBoolean(obj[key]); }
        catch { return null; }
    }

    /// <summary>
    /// Normalizes zero-valued firmware fields to null because zero commonly means unavailable.
    /// </summary>
    private static uint? NonZero(uint? value) => value is > 0 ? value : null;

    /// <summary>
    /// Rejects invalid/sentinel firmware runtime values and returns plausible minutes only.
    /// </summary>
    private static int? NormalizeRuntime(int? minutes)
    {
        // WMI commonly uses 71582788 to mean unknown/unlimited.
        return minutes is > 0 and < 14400 ? minutes : null;
    }
}
