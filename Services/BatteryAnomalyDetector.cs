using BatteryDoctor.Models;

// File responsibility: Detects short-term charge, voltage, and power anomalies from recent battery telemetry.

namespace BatteryDoctor.Services;

/// <summary>
/// Detects short-term charge, voltage, and power anomalies from recent battery telemetry.
/// </summary>
public sealed class BatteryAnomalyDetector
{
    /// <summary>
    /// Scans recent snapshots for sudden percentage drops, rapid collapse patterns, large short-term voltage swings, and unusual power spikes.
    /// </summary>
    public IReadOnlyList<AnomalyFinding> Analyze(IReadOnlyList<BatterySnapshot> samples)
    {
        var findings = new List<AnomalyFinding>();
        if (samples.Count < 2) return findings;

        var newest = samples[^1];
        var shortDropDetected = false;

        // Fast percentage collapse: useful for gauge jumps such as 90% -> 0%.
        // First pass: catch an abrupt percentage drop inside 90 seconds. This is deliberately
        // short so ordinary long discharges do not look like a gauge failure.
        for (var i = samples.Count - 2; i >= 0; i--)
        {
            var older = samples[i];
            var elapsed = newest.CapturedAt - older.CapturedAt;
            if (elapsed > TimeSpan.FromSeconds(90)) break;

            if (newest.Discharging == true &&
                older.EstimatedChargePercent is { } oldPct &&
                newest.EstimatedChargePercent is { } newPct &&
                oldPct - newPct >= 4)
            {
                findings.Add(new AnomalyFinding(
                    "bad",
                    "AnomalySuddenDropTitle",
                    "AnomalySuddenDropDetail",
                    newest.CapturedAt,
                    oldPct - newPct));
                shortDropDetected = true;
                break;
            }
        }

        // Slower but still implausibly large collapse. This catches the real-world
        // symptom where a laptop reports ~90% and reaches cutoff within <15 min,
        // including resume/hibernate gaps where a 90-second detector would miss it.
        // Second pass: a much larger fall over up to 30 minutes is treated as a rapid-collapse
        // pattern even when no single 90-second jump crossed the short-drop threshold.
        if (!shortDropDetected && newest.Discharging == true)
        {
            for (var i = samples.Count - 2; i >= 0; i--)
            {
                var older = samples[i];
                var elapsed = newest.CapturedAt - older.CapturedAt;
                if (elapsed > TimeSpan.FromMinutes(30)) break;

                if (older.EstimatedChargePercent is { } oldPct &&
                    newest.EstimatedChargePercent is { } newPct &&
                    oldPct - newPct >= 25)
                {
                    findings.Add(new AnomalyFinding(
                        "bad",
                        "AnomalyRapidCollapseTitle",
                        "AnomalyRapidCollapseDetail",
                        newest.CapturedAt,
                        oldPct - newPct));
                    break;
                }
            }
        }

        // Surface a large voltage movement as an observation, not a diagnosis.
        var recentVoltage = samples
            .Where(x => newest.Discharging == true && x.Discharging == true && x.VoltageMV is > 0 && newest.CapturedAt - x.CapturedAt <= TimeSpan.FromMinutes(2))
            .Select(x => (double)x.VoltageMV!.Value / 1000.0)
            .ToList();
        if (recentVoltage.Count >= 2)
        {
            var swing = recentVoltage.Max() - recentVoltage.Min();
            if (swing >= 1.5)
            {
                findings.Add(new AnomalyFinding(
                    "warn",
                    "AnomalyVoltageSwingTitle",
                    "AnomalyVoltageSwingDetail",
                    newest.CapturedAt,
                    swing));
            }
        }

        // Detect an unusually large power spike relative to the recent median.
        var recentPower = samples
            .Where(x => x.Discharging == true && x.DischargeRateMW is not null &&
                        newest.CapturedAt - x.CapturedAt <= TimeSpan.FromMinutes(5))
            .Select(x => Math.Abs(x.DischargeRateMW!.Value) / 1000.0)
            .Where(x => x > 0)
            .OrderBy(x => x)
            .ToList();
        if (recentPower.Count >= 6 && newest.Discharging == true && newest.DischargeRateMW is { } latestMw)
        {
            var median = recentPower[recentPower.Count / 2];
            var latest = Math.Abs(latestMw) / 1000.0;
            if (median > 0 && latest >= 15 && latest >= median * 2.5)
            {
                findings.Add(new AnomalyFinding(
                    "info",
                    "AnomalyPowerSpikeTitle",
                    "AnomalyPowerSpikeDetail",
                    newest.CapturedAt,
                    latest));
            }
        }

        return findings;
    }
}
