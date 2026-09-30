// File responsibility: Minimal historical health/charge point used by simple history views.

namespace BatteryDoctor.Models;

/// <summary>
/// Minimal historical health/charge point used by simple history views.
/// </summary>
public sealed record HistoryPoint(DateTimeOffset CapturedAt, double? HealthPercent, int? ChargePercent);
