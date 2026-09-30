// File responsibility: Evidence reconstructed after an unexpected reboot/power loss during a test.

namespace BatteryDoctor.Models;

/// <summary>
/// Evidence reconstructed after an unexpected reboot/power loss during a test.
/// </summary>
public sealed record RecoveredTestInterruption(
    string SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset LastSampleAt,
    int? StartChargePercent,
    int? LastReportedChargePercent,
    int? CurrentChargePercent,
    double? EnergyRecordedWh,
    double? DesignCapacityWh,
    double? ReportedFullChargeCapacityWh,
    double? ObservedEnergyVsDesignPercent,
    double? ObservedEnergyVsReportedAvailablePercent,
    double? LastRemainingWh,
    double? LastVoltageV,
    double? FullChargeCapacityAfterRestartWh,
    double? FullChargeCapacityChangePercent,
    bool FirmwareRecalibrationDetected,
    int VoltageSagSeverityScore,
    string VoltageSagSeverityCode,
    int VoltageSagEventCount,
    double? MaxVoltageSagV,
    double? WorstSagBaselineV,
    double? WorstSagVoltageV,
    int GaugeReliabilityScore,
    string GaugeReliabilityCode,
    int BatteryReliabilityScore,
    string BatteryReliabilityCode,
    bool SystemRestartDetected,
    bool StrongGaugeJumpDetected,
    bool PossibleSuddenCollapse,
    string ConfidenceCode,
    string SamplePath);
