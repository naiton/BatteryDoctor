using BatteryDoctor.Models;

// File responsibility: Derives current, dynamic-resistance and thermal/load metrics from recent pack-level telemetry.

namespace BatteryDoctor.Services;

/// <summary>
/// Calculates electrical/thermal estimates from a short series of BatterySnapshot values.
/// These metrics are diagnostic indicators only; they are not a substitute for cell-level
/// impedance measurement equipment.
/// </summary>
public sealed class ElectricalDiagnosticsAnalyzer
{
    /// <summary>
    /// Analyzes the recent live window and returns current, dynamic resistance, temperature trend,
    /// and simple power/temperature correlation metrics when the required source data exists.
    /// </summary>
    public ElectricalDiagnosticsAssessment Analyze(IReadOnlyList<BatterySnapshot> samples)
    {
        if (samples.Count == 0)
            return new ElectricalDiagnosticsAssessment(null, null, 0, null, null, 0, null, null, null);

        var latest = samples[^1];
        var estimatedCurrent = EstimateSignedCurrentA(latest);
        var resistanceCandidates = EstimateDynamicResistanceCandidates(samples);
        double? dynamicResistance = resistanceCandidates.Count == 0 ? null : Median(resistanceCandidates);

        var temperatureSamples = samples
            .Where(x => x.TemperatureC is not null)
            .OrderBy(x => x.CapturedAt)
            .ToList();

        double? temperatureDelta = null;
        double? temperatureRate = null;
        if (temperatureSamples.Count >= 2)
        {
            var lastTemperature = temperatureSamples[^1];
            var cutoff = lastTemperature.CapturedAt - TimeSpan.FromMinutes(5);
            var firstTemperature = temperatureSamples.FirstOrDefault(x => x.CapturedAt >= cutoff) ?? temperatureSamples[0];
            var elapsedMinutes = (lastTemperature.CapturedAt - firstTemperature.CapturedAt).TotalMinutes;
            if (elapsedMinutes >= 0.5 &&
                firstTemperature.TemperatureC is { } firstC &&
                lastTemperature.TemperatureC is { } lastC)
            {
                temperatureDelta = lastC - firstC;
                temperatureRate = temperatureDelta.Value / elapsedMinutes;
            }
        }

        var correlation = CalculatePowerTemperatureCorrelation(samples);
        return new ElectricalDiagnosticsAssessment(
            estimatedCurrent,
            dynamicResistance,
            resistanceCandidates.Count,
            latest.TemperatureC,
            latest.TemperatureSource,
            temperatureSamples.Count,
            temperatureDelta,
            temperatureRate,
            correlation);
    }

    /// <summary>
    /// Estimates signed pack current from pack power and pack voltage using I = P / V.
    /// Positive values mean charging and negative values mean discharging.
    /// </summary>
    public static double? EstimateSignedCurrentA(BatterySnapshot snapshot)
    {
        if (snapshot.VoltageMV is not > 0) return null;
        var voltageV = snapshot.VoltageMV.Value / 1000.0;
        if (voltageV <= 0) return null;

        if (snapshot.Discharging == true && snapshot.DischargeRateMW is { } discharge && discharge != 0)
            return -Math.Abs(discharge) / 1000.0 / voltageV;
        if (snapshot.Charging == true && snapshot.ChargeRateMW is { } charge && charge != 0)
            return Math.Abs(charge) / 1000.0 / voltageV;
        return null;
    }

    /// <summary>
    /// Uses short load-current steps to estimate pack dynamic resistance from |ΔV / ΔI|.
    /// Pairs are accepted only when current and voltage move in opposite directions, which is
    /// the expected response of a battery under a changed discharge load.
    /// </summary>
    private static List<double> EstimateDynamicResistanceCandidates(IReadOnlyList<BatterySnapshot> samples)
    {
        var candidates = new List<double>();
        for (var i = 1; i < samples.Count; i++)
        {
            var previous = samples[i - 1];
            var current = samples[i];
            var elapsed = current.CapturedAt - previous.CapturedAt;
            if (elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(20)) continue;
            if (previous.Discharging != true || current.Discharging != true) continue;
            if (previous.VoltageMV is not > 0 || current.VoltageMV is not > 0) continue;

            var previousCurrent = EstimateDischargeCurrentA(previous);
            var currentCurrent = EstimateDischargeCurrentA(current);
            if (previousCurrent is not { } i1 || currentCurrent is not { } i2) continue;

            var deltaI = i2 - i1;
            if (Math.Abs(deltaI) < 0.12) continue;

            var deltaV = current.VoltageMV.Value / 1000.0 - previous.VoltageMV.Value / 1000.0;
            if (deltaV * deltaI >= 0) continue;

            var resistance = Math.Abs(deltaV / deltaI);
            if (resistance is >= 0.01 and <= 10.0)
                candidates.Add(resistance);
        }

        return candidates.TakeLast(20).ToList();
    }

    /// <summary>
    /// Calculates positive discharge-current magnitude for dynamic-resistance estimation.
    /// </summary>
    private static double? EstimateDischargeCurrentA(BatterySnapshot snapshot)
    {
        if (snapshot.Discharging != true || snapshot.DischargeRateMW is not { } powerMw || snapshot.VoltageMV is not > 0)
            return null;
        var voltageV = snapshot.VoltageMV.Value / 1000.0;
        return voltageV > 0 ? Math.Abs(powerMw) / 1000.0 / voltageV : null;
    }

    /// <summary>
    /// Computes a Pearson correlation between absolute pack power and measured battery temperature.
    /// It is shown only when enough variation exists; battery thermal response can lag load, so
    /// the coefficient is descriptive rather than a fault verdict.
    /// </summary>
    private static double? CalculatePowerTemperatureCorrelation(IReadOnlyList<BatterySnapshot> samples)
    {
        var pairs = samples
            .Select(x => new
            {
                Power = SignedPowerW(x) is { } p ? Math.Abs(p) : (double?)null,
                Temperature = x.TemperatureC
            })
            .Where(x => x.Power is not null && x.Temperature is not null)
            .Select(x => (Power: x.Power!.Value, Temperature: x.Temperature!.Value))
            .ToList();

        if (pairs.Count < 12) return null;
        if (pairs.Max(x => x.Power) - pairs.Min(x => x.Power) < 1.0) return null;
        if (pairs.Max(x => x.Temperature) - pairs.Min(x => x.Temperature) < 0.2) return null;

        var meanPower = pairs.Average(x => x.Power);
        var meanTemperature = pairs.Average(x => x.Temperature);
        double numerator = 0;
        double sumPower = 0;
        double sumTemperature = 0;
        foreach (var pair in pairs)
        {
            var dp = pair.Power - meanPower;
            var dt = pair.Temperature - meanTemperature;
            numerator += dp * dt;
            sumPower += dp * dp;
            sumTemperature += dt * dt;
        }

        var denominator = Math.Sqrt(sumPower * sumTemperature);
        return denominator <= 0 ? null : Math.Clamp(numerator / denominator, -1, 1);
    }

    /// <summary>
    /// Returns signed pack power in watts using the same convention as the live chart.
    /// </summary>
    private static double? SignedPowerW(BatterySnapshot snapshot)
    {
        if (snapshot.Discharging == true && snapshot.DischargeRateMW is { } discharge)
            return -Math.Abs(discharge) / 1000.0;
        if (snapshot.Charging == true && snapshot.ChargeRateMW is { } charge)
            return Math.Abs(charge) / 1000.0;
        return null;
    }

    /// <summary>
    /// Returns the median so a single noisy current/voltage step cannot dominate resistance display.
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
