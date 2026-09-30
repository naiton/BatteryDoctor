// File responsibility: Immutable description of one anomaly detected in recent telemetry.

namespace BatteryDoctor.Models;

/// <summary>
/// Immutable description of one anomaly detected in recent telemetry.
/// </summary>
public sealed record AnomalyFinding(
    string Severity,
    string TitleKey,
    string DetailKey,
    DateTimeOffset CapturedAt,
    double? Value = null);
