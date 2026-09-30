// File responsibility: Compact sample persisted during a discharge or collapse-watch test.

namespace BatteryDoctor.Models;

/// <summary>
/// Compact sample persisted during a discharge or collapse-watch test.
/// </summary>
public sealed record BatteryTestSample(
    DateTimeOffset CapturedAt,
    int? ChargePercent,
    double? VoltageV,
    double? PowerW,
    uint? RemainingCapacityMWh,
    double? TemperatureC = null);
