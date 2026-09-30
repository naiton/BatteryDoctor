// File responsibility: Timestamp/value pair consumed by TrendChart.

namespace BatteryDoctor.Models;

/// <summary>
/// Timestamp/value pair consumed by TrendChart.
/// </summary>
public sealed record TrendPoint(DateTimeOffset CapturedAt, double Value);
