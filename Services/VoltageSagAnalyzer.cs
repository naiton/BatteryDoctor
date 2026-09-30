using BatteryDoctor.Models;

// File responsibility: Detects transient pack-voltage sag relative to a rolling local baseline and converts it to a severity score.

namespace BatteryDoctor.Services;

/// <summary>
/// Looks for rapid battery-voltage drops relative to a short rolling baseline.
/// The score is heuristic observation data: higher means more severe/repeated sag.
/// It is not a cell-level diagnosis.
/// </summary>
public sealed class VoltageSagAnalyzer
{
    private const double SagThresholdV = 0.8;
    private static readonly TimeSpan EventSeparation = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Compares each voltage sample with a short rolling median baseline, groups nearby sags into events, and converts sag depth/frequency/terminal sag into a 0-100 severity score.
    /// </summary>
    public VoltageSagAssessment Analyze(IReadOnlyList<BatteryTestSample> samples)
    {
        var valid = samples
            .Where(x => x.VoltageV is > 0)
            .OrderBy(x => x.CapturedAt)
            .ToList();

        if (valid.Count < 4)
            return new VoltageSagAssessment(0, "unknown", 0, null, null, null,
                valid.Count == 0 ? null : valid.Min(x => x.VoltageV!.Value),
                valid.Count == 0 ? null : valid[^1].VoltageV,
                null);

        var eventCount = 0;
        DateTimeOffset? lastEventAt = null;
        double maxSag = 0;
        double? worstBaseline = null;
        double? worstVoltage = null;

        // Use a rolling median rather than the immediately previous sample. The median is less
        // sensitive to one noisy WMI voltage reading and represents the local loaded baseline.
        for (var i = 3; i < valid.Count; i++)
        {
            var start = Math.Max(0, i - 5);
            var baselineValues = valid
                .Skip(start)
                .Take(i - start)
                .Select(x => x.VoltageV!.Value)
                .OrderBy(x => x)
                .ToList();
            if (baselineValues.Count < 3) continue;

            var baseline = Median(baselineValues);
            var voltage = valid[i].VoltageV!.Value;
            var sag = baseline - voltage;
            if (sag < SagThresholdV) continue;

            if (lastEventAt is null || valid[i].CapturedAt - lastEventAt.Value >= EventSeparation)
            {
                eventCount++;
                lastEventAt = valid[i].CapturedAt;
            }

            if (sag > maxSag)
            {
                maxSag = sag;
                worstBaseline = baseline;
                worstVoltage = voltage;
            }
        }

        double? terminalSag = null;
        if (valid.Count >= 4)
        {
            var previous = valid.TakeLast(Math.Min(6, valid.Count)).Take(Math.Min(5, valid.Count - 1))
                .Select(x => x.VoltageV!.Value).OrderBy(x => x).ToList();
            if (previous.Count >= 3)
            {
                var baseline = Median(previous);
                var drop = baseline - valid[^1].VoltageV!.Value;
                if (drop >= SagThresholdV) terminalSag = drop;
            }
        }

        // Severity combines sag depth, recurrence, and whether the final sample is itself in sag.
        // The score is diagnostic evidence, not a direct measurement of individual cell health.
        var score = 0.0;
        if (maxSag > 0)
            score += Math.Min(60.0, maxSag / 2.5 * 60.0);
        score += Math.Min(25.0, eventCount * 3.0);
        if (terminalSag is > 0.8) score += 15.0;
        var severityScore = (int)Math.Round(Math.Clamp(score, 0, 100));

        var code = severityScore switch
        {
            >= 75 => "critical",
            >= 50 => "high",
            >= 25 => "moderate",
            > 0 => "low",
            _ => "none"
        };

        return new VoltageSagAssessment(
            severityScore,
            code,
            eventCount,
            maxSag > 0 ? maxSag : null,
            worstBaseline,
            worstVoltage,
            valid.Min(x => x.VoltageV!.Value),
            valid[^1].VoltageV,
            terminalSag);
    }

    /// <summary>
    /// Returns the median of an already sorted local baseline window, reducing sensitivity to one noisy voltage sample.
    /// </summary>
    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }
}
