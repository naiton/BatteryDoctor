// File responsibility: Derived electrical/thermal metrics calculated from recent live battery samples.

namespace BatteryDoctor.Models;

/// <summary>
/// Derived electrical and thermal diagnostics for the recent live-monitor window.
/// Values are intentionally marked as estimates because Windows exposes pack-level telemetry,
/// not laboratory-grade cell impedance measurements.
/// </summary>
public sealed record ElectricalDiagnosticsAssessment(
    double? EstimatedCurrentA,
    double? DynamicResistanceOhm,
    int DynamicResistanceSampleCount,
    double? TemperatureC,
    string? TemperatureSource,
    int ValidTemperatureSampleCount,
    double? TemperatureDelta5MinC,
    double? TemperatureRateCPerMinute,
    double? PowerTemperatureCorrelation);
