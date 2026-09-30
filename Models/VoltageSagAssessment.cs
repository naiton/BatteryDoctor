// File responsibility: Result of voltage-sag analysis including severity, event count, and worst sag details.

namespace BatteryDoctor.Models;

/// <summary>
/// Result of voltage-sag analysis including severity, event count, and worst sag details.
/// </summary>
public sealed record VoltageSagAssessment(
    int SeverityScore,
    string SeverityCode,
    int EventCount,
    double? MaxSagV,
    double? WorstBaselineV,
    double? WorstVoltageV,
    double? MinVoltageV,
    double? LastVoltageV,
    double? TerminalSagV);
