// File responsibility: Historical sample containing capacity, charge, voltage and signed power for trend charts.

namespace BatteryDoctor.Models;

/// <summary>
/// Historical sample containing capacity, charge, voltage and signed power for trend charts.
/// </summary>
public sealed record HistorySamplePoint(
    DateTimeOffset CapturedAt,
    double? HealthPercent,
    int? ChargePercent,
    double? FullChargeWh,
    double? VoltageV,
    double? PowerW,
    double? TemperatureC,
    double? EstimatedCurrentA);
