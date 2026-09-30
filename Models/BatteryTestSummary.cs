// File responsibility: Derived summary of a completed discharge test and its quality/confidence metrics.

namespace BatteryDoctor.Models;

/// <summary>
/// Derived summary of a completed discharge test and its quality/confidence metrics.
/// </summary>
public sealed record BatteryTestSummary(
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int? StartChargePercent,
    int? EndChargePercent,
    double? StartRemainingWh,
    double? EndRemainingWh,
    double? EstimatedEnergyUsedWh,
    double? AveragePowerW,
    double? AverageVoltageV,
    double? MinVoltageV,
    double? MaxVoltageV,
    int SampleCount,
    int ValidPowerSampleCount,
    double? BatteryHealthPercent,
    string VerdictCode,
    IReadOnlyList<AnomalyFinding> Anomalies,
    string ConfidenceCode,
    double? EnergyAgreementPercent,
    string DataConsistencyCode,
    double? ExtrapolatedUsableCapacityWh,
    double? ExtrapolatedUsableHealthPercent,
    int VoltageSagSeverityScore,
    string VoltageSagSeverityCode,
    int VoltageSagEventCount,
    double? MaxVoltageSagV,
    double? AverageTemperatureC,
    double? MinTemperatureC,
    double? MaxTemperatureC,
    double? TemperatureRiseC,
    double? AverageEstimatedCurrentA,
    double? DynamicResistanceOhm,
    int DynamicResistanceSampleCount,
    double? PowerTemperatureCorrelation)
{
    public TimeSpan Duration => EndedAt - StartedAt;

    public int? ChargeDropPercent =>
        StartChargePercent is { } start && EndChargePercent is { } end
            ? Math.Max(0, Math.Clamp(start, 0, 100) - Math.Clamp(end, 0, 100))
            : null;

    public double? CapacityDeltaWh =>
        StartRemainingWh is { } start && EndRemainingWh is { } end
            ? Math.Max(0, start - end)
            : null;
}

/// <summary>
/// Data/application type used by BatteryTestSummary.
/// </summary>
public sealed record ReportObservation(string Severity, string Title, string Detail);

/// <summary>
/// File paths produced by a report export.
/// </summary>
public sealed record ExportResult(string HtmlPath, string JsonPath);
