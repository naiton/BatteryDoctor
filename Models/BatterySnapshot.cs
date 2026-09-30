// File responsibility: Normalized point-in-time battery telemetry read from Windows/firmware.

namespace BatteryDoctor.Models;

/// <summary>
/// Normalized point-in-time battery telemetry read from Windows/firmware.
/// </summary>
public sealed class BatterySnapshot
{
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public string? Name { get; init; }
    public string? Manufacturer { get; init; }
    public string? SerialNumber { get; init; }
    public string? Chemistry { get; init; }
    public uint? DesignCapacityMWh { get; init; }
    public uint? FullChargeCapacityMWh { get; init; }
    public uint? RemainingCapacityMWh { get; init; }
    public uint? CycleCount { get; init; }
    public uint? VoltageMV { get; init; }
    public double? TemperatureC { get; init; }
    public string? TemperatureSource { get; init; }
    public int? ChargeRateMW { get; init; }
    public int? DischargeRateMW { get; init; }
    public int? EstimatedChargePercent { get; init; }
    public int? EstimatedRuntimeMinutes { get; init; }
    public bool? PowerOnline { get; init; }
    public bool? Charging { get; init; }
    public bool? Discharging { get; init; }
    public bool? Critical { get; init; }

    /// <summary>
    /// Firmware-reported capacity health: FullChargeCapacity / DesignCapacity. This is a
    /// capacity estimate only; reliability/sudden-collapse diagnostics are calculated separately.
    /// </summary>
    public double? HealthPercent =>
        DesignCapacityMWh is > 0 && FullChargeCapacityMWh is > 0
            ? Math.Clamp(FullChargeCapacityMWh.Value * 100.0 / DesignCapacityMWh.Value, 0, 120)
            : null;

    /// <summary>
    /// Complement of firmware-reported health for display. The value is clamped to avoid
    /// nonsensical negative wear when firmware temporarily reports capacity above design.
    /// </summary>
    public double? WearPercent => HealthPercent is { } h ? Math.Max(0, 100 - h) : null;
}
