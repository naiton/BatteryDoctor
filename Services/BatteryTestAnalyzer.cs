using BatteryDoctor.Models;

// File responsibility: Calculates discharge-test metrics, energy integration, confidence, consistency, sag severity, and verdict codes.

namespace BatteryDoctor.Services;

/// <summary>
/// Calculates discharge-test metrics, energy integration, confidence, consistency, sag severity, and verdict codes.
/// </summary>
public sealed class BatteryTestAnalyzer
{
    private readonly BatteryAnomalyDetector _anomalyDetector = new();
    private readonly VoltageSagAnalyzer _voltageSagAnalyzer = new();

    /// <summary>
    /// Turns recorded test samples into a summary: integrated energy, average power/voltage, charge drop, confidence, gauge agreement, extrapolated capacity, sag metrics, anomalies, and verdict.
    /// </summary>
    public BatteryTestSummary? Analyze(
        IReadOnlyList<BatteryTestSample> samples,
        IReadOnlyList<BatterySnapshot> rawSamples,
        double? batteryHealthPercent,
        double? designCapacityWh = null)
    {
        if (samples.Count == 0) return null;

        var first = samples[0];
        var last = samples[^1];
        var powers = samples.Where(x => x.PowerW is > 0).Select(x => x.PowerW!.Value).ToList();
        var voltages = samples.Where(x => x.VoltageV is > 0).Select(x => x.VoltageV!.Value).ToList();
        var temperatures = samples.Where(x => x.TemperatureC is not null).Select(x => x.TemperatureC!.Value).ToList();
        var currents = samples
            .Where(x => x.PowerW is > 0 && x.VoltageV is > 0)
            .Select(x => x.PowerW!.Value / x.VoltageV!.Value)
            .Where(x => x is > 0 and < 100)
            .ToList();
        var resistanceCandidates = EstimateDynamicResistanceCandidates(samples);
        var anomalies = _anomalyDetector.Analyze(rawSamples);
        var sag = _voltageSagAnalyzer.Analyze(samples);

        var energyWh = EstimateEnergyWh(samples);
        var duration = last.CapturedAt - first.CapturedAt;
        int? startPct = samples.FirstOrDefault(x => x.ChargePercent is not null)?.ChargePercent is { } sp ? Math.Clamp(sp, 0, 100) : null;
        int? endPct = samples.LastOrDefault(x => x.ChargePercent is not null)?.ChargePercent is { } ep ? Math.Clamp(ep, 0, 100) : null;
        var chargeDrop = startPct is { } start && endPct is { } end ? Math.Max(0, start - end) : (int?)null;
        var startRemainingWh = samples.FirstOrDefault(x => x.RemainingCapacityMWh is not null)?.RemainingCapacityMWh / 1000.0;
        var endRemainingWh = samples.LastOrDefault(x => x.RemainingCapacityMWh is not null)?.RemainingCapacityMWh / 1000.0;
        var capacityDeltaWh = startRemainingWh is { } sr && endRemainingWh is { } er ? Math.Max(0, sr - er) : (double?)null;
        var agreement = CalculateAgreementPercent(energyWh, capacityDeltaWh);
        var consistencyCode = agreement switch
        {
            >= 85 => "good",
            >= 70 => "fair",
            null => "unknown",
            _ => "poor"
        };

        var firstTemperature = samples.FirstOrDefault(x => x.TemperatureC is not null)?.TemperatureC;
        var lastTemperature = samples.LastOrDefault(x => x.TemperatureC is not null)?.TemperatureC;
        var temperatureRise = firstTemperature is { } firstC && lastTemperature is { } lastC
            ? lastC - firstC
            : (double?)null;
        var powerTemperatureCorrelation = CalculatePowerTemperatureCorrelation(samples);

        double? extrapolatedUsableWh = null;
        double? extrapolatedHealth = null;
        if (energyWh is > 0 && chargeDrop is >= 10)
        {
            extrapolatedUsableWh = energyWh.Value * 100.0 / chargeDrop.Value;
            if (designCapacityWh is > 0)
                extrapolatedHealth = Math.Clamp(extrapolatedUsableWh.Value * 100.0 / designCapacityWh.Value, 0, 150);
        }

        var confidenceCode = GetConfidenceCode(duration, chargeDrop, powers.Count);
        var hasBad = anomalies.Any(x => string.Equals(x.Severity, "bad", StringComparison.OrdinalIgnoreCase));
        var hasWarn = anomalies.Any(x => string.Equals(x.Severity, "warn", StringComparison.OrdinalIgnoreCase));

        string verdictCode;
        if (samples.Count < 2 || duration < TimeSpan.FromMinutes(1))
            verdictCode = "insufficient";
        else if (hasBad)
            verdictCode = "anomaly";
        else if (duration < TimeSpan.FromMinutes(5))
            verdictCode = "short";
        else if (hasWarn)
            verdictCode = "caution";
        else if (batteryHealthPercent is < 60)
            verdictCode = "capacity_worn_stable";
        else
            verdictCode = "stable";

        return new BatteryTestSummary(
            first.CapturedAt,
            last.CapturedAt,
            startPct,
            endPct,
            startRemainingWh,
            endRemainingWh,
            energyWh,
            powers.Count == 0 ? null : powers.Average(),
            voltages.Count == 0 ? null : voltages.Average(),
            voltages.Count == 0 ? null : voltages.Min(),
            voltages.Count == 0 ? null : voltages.Max(),
            samples.Count,
            powers.Count,
            batteryHealthPercent,
            verdictCode,
            anomalies,
            confidenceCode,
            agreement,
            consistencyCode,
            extrapolatedUsableWh,
            extrapolatedHealth,
            sag.SeverityScore,
            sag.SeverityCode,
            sag.EventCount,
            sag.MaxSagV,
            temperatures.Count == 0 ? null : temperatures.Average(),
            temperatures.Count == 0 ? null : temperatures.Min(),
            temperatures.Count == 0 ? null : temperatures.Max(),
            temperatureRise,
            currents.Count == 0 ? null : currents.Average(),
            resistanceCandidates.Count == 0 ? null : Median(resistanceCandidates),
            resistanceCandidates.Count,
            powerTemperatureCorrelation);
    }

    /// <summary>
    /// Assigns a confidence level from test duration, observed charge drop, and count of valid power samples.
    /// </summary>
    private static string GetConfidenceCode(TimeSpan duration, int? chargeDrop, int validPowerSamples)
    {
        if (duration >= TimeSpan.FromMinutes(20) || chargeDrop is >= 20)
            return "high";
        if (duration >= TimeSpan.FromMinutes(10) || chargeDrop is >= 10)
            return "medium";
        if (duration >= TimeSpan.FromMinutes(5) && chargeDrop is >= 3 && validPowerSamples >= 30)
            return "low";
        return "very_low";
    }

    /// <summary>
    /// Compares watt-time integrated energy against the firmware remaining-capacity delta; high agreement means the two independent estimates are consistent.
    /// </summary>
    private static double? CalculateAgreementPercent(double? integratedEnergyWh, double? capacityDeltaWh)
    {
        if (integratedEnergyWh is not > 0 || capacityDeltaWh is not > 0) return null;
        var average = (integratedEnergyWh.Value + capacityDeltaWh.Value) / 2.0;
        if (average <= 0) return null;
        var differencePercent = Math.Abs(integratedEnergyWh.Value - capacityDeltaWh.Value) / average * 100.0;
        return Math.Clamp(100.0 - differencePercent, 0, 100);
    }

    /// <summary>
    /// Integrates discharge power over time with the trapezoidal rule while ignoring invalid or excessively large sample gaps.
    /// </summary>
    private static double? EstimateEnergyWh(IReadOnlyList<BatteryTestSample> samples)
    {
        if (samples.Count < 2) return null;

        double totalWh = 0;
        var validIntervals = 0;
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            if (previous.PowerW is not > 0 || current.PowerW is not > 0) continue;

            var hours = (current.CapturedAt - previous.CapturedAt).TotalHours;
            if (hours is <= 0 or > 0.1) continue;

            totalWh += ((previous.PowerW.Value + current.PowerW.Value) / 2.0) * hours;
            validIntervals++;
        }

        return validIntervals == 0 ? null : totalWh;
    }

    /// <summary>
    /// Estimates pack dynamic resistance from short discharge-load steps using |ΔV / ΔI|.
    /// Only opposite-direction voltage/current changes are accepted to reduce noise-driven false values.
    /// </summary>
    private static List<double> EstimateDynamicResistanceCandidates(IReadOnlyList<BatteryTestSample> samples)
    {
        var candidates = new List<double>();
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            var elapsed = current.CapturedAt - previous.CapturedAt;
            if (elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(20)) continue;
            if (previous.PowerW is not > 0 || current.PowerW is not > 0 ||
                previous.VoltageV is not > 0 || current.VoltageV is not > 0)
                continue;

            var previousCurrent = previous.PowerW.Value / previous.VoltageV.Value;
            var currentCurrent = current.PowerW.Value / current.VoltageV.Value;
            var deltaI = currentCurrent - previousCurrent;
            if (Math.Abs(deltaI) < 0.12) continue;

            var deltaV = current.VoltageV.Value - previous.VoltageV.Value;
            if (deltaV * deltaI >= 0) continue;

            var resistance = Math.Abs(deltaV / deltaI);
            if (resistance is >= 0.01 and <= 10.0)
                candidates.Add(resistance);
        }

        return candidates.TakeLast(20).ToList();
    }

    /// <summary>
    /// Calculates Pearson correlation between discharge power and measured battery temperature.
    /// The coefficient is descriptive only because thermal response can lag electrical load.
    /// </summary>
    private static double? CalculatePowerTemperatureCorrelation(IReadOnlyList<BatteryTestSample> samples)
    {
        var pairs = samples
            .Where(x => x.PowerW is > 0 && x.TemperatureC is not null)
            .Select(x => (Power: x.PowerW!.Value, Temperature: x.TemperatureC!.Value))
            .ToList();

        if (pairs.Count < 12) return null;
        if (pairs.Max(x => x.Power) - pairs.Min(x => x.Power) < 1.0) return null;
        if (pairs.Max(x => x.Temperature) - pairs.Min(x => x.Temperature) < 0.2) return null;

        var meanPower = pairs.Average(x => x.Power);
        var meanTemperature = pairs.Average(x => x.Temperature);
        double numerator = 0;
        double powerVariance = 0;
        double temperatureVariance = 0;
        foreach (var pair in pairs)
        {
            var dp = pair.Power - meanPower;
            var dt = pair.Temperature - meanTemperature;
            numerator += dp * dt;
            powerVariance += dp * dp;
            temperatureVariance += dt * dt;
        }

        var denominator = Math.Sqrt(powerVariance * temperatureVariance);
        return denominator <= 0 ? null : Math.Clamp(numerator / denominator, -1, 1);
    }

    /// <summary>
    /// Returns a median estimate to suppress one-off telemetry spikes.
    /// </summary>
    private static double Median(IReadOnlyList<double> values)
    {
        var ordered = values.OrderBy(x => x).ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }

}
