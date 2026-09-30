using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using BatteryDoctor.Models;

// File responsibility: Exports recovered sudden-collapse evidence, reliability scores, and raw samples to JSON/HTML.

namespace BatteryDoctor.Services;

/// <summary>
/// Exports a crash-recovered Collapse Watch session. The report deliberately
/// separates firmware-reported capacity from observed delivered energy and
/// does not claim cell-level diagnosis.
/// </summary>
public sealed class RecoveredCollapseReportExporter
{
    /// <summary>
    /// Exports recovered collapse evidence and its raw crash-safe samples to privacy-conscious JSON and standalone HTML.
    /// </summary>
    public async Task<ExportResult> ExportAsync(
        RecoveredTestInterruption recovery,
        BatterySnapshot currentBattery,
        IReadOnlyDictionary<string, string> text,
        string language,
        string recommendationTitle,
        string recommendationDetail,
        CancellationToken cancellationToken = default)
    {
        var samples = ReadSamples(PortablePaths.ResolveSessionPath(recovery.SamplePath));
        var reportDir = PortablePaths.ReportsDirectory;
        Directory.CreateDirectory(reportDir);

        var stamp = recovery.LastSampleAt.LocalDateTime.ToString("yyyyMMdd-HHmmss");
        var baseName = $"BatteryDoctor-Collapse-{stamp}";
        var jsonPath = Path.Combine(reportDir, baseName + ".json");
        var htmlPath = Path.Combine(reportDir, baseName + ".html");

        var currentHealth = currentBattery.HealthPercent;
        var currentWear = currentHealth is { } h ? Math.Clamp(100 - h, 0, 100) : (double?)null;
        var voltageStabilityScore = Math.Clamp(100 - recovery.VoltageSagSeverityScore, 0, 100);
        var shutdownReliabilityScore = recovery.StrongGaugeJumpDetected ? 0 : recovery.PossibleSuddenCollapse ? 20 : 100;

        var payload = new
        {
            schemaVersion = 3,
            reportType = "recovered_sudden_collapse",
            generatedAt = DateTimeOffset.Now,
            language,
            recovery = new
            {
                recovery.SessionId,
                recovery.StartedAt,
                recovery.LastSampleAt,
                recovery.StartChargePercent,
                recovery.LastReportedChargePercent,
                recovery.CurrentChargePercent,
                recovery.EnergyRecordedWh,
                recovery.DesignCapacityWh,
                recovery.ReportedFullChargeCapacityWh,
                recovery.ObservedEnergyVsDesignPercent,
                recovery.ObservedEnergyVsReportedAvailablePercent,
                recovery.LastRemainingWh,
                recovery.LastVoltageV,
                recovery.FullChargeCapacityAfterRestartWh,
                recovery.FullChargeCapacityChangePercent,
                recovery.FirmwareRecalibrationDetected,
                recovery.VoltageSagSeverityScore,
                recovery.VoltageSagSeverityCode,
                recovery.VoltageSagEventCount,
                recovery.MaxVoltageSagV,
                recovery.WorstSagBaselineV,
                recovery.WorstSagVoltageV,
                recovery.GaugeReliabilityScore,
                recovery.GaugeReliabilityCode,
                recovery.BatteryReliabilityScore,
                recovery.BatteryReliabilityCode,
                recovery.SystemRestartDetected,
                recovery.StrongGaugeJumpDetected,
                recovery.PossibleSuddenCollapse,
                recovery.ConfidenceCode,
                sampleFile = PortablePaths.ToPortableRelativePath(PortablePaths.ResolveSessionPath(recovery.SamplePath))
            },
            diagnostics = new
            {
                currentFirmwareHealthPercent = currentHealth,
                currentFirmwareWearPercent = currentWear,
                gaugeReliabilityScore = recovery.GaugeReliabilityScore,
                voltageStabilityScore,
                shutdownReliabilityScore,
                batteryReliabilityScore = recovery.BatteryReliabilityScore,
                recommendationTitle,
                recommendationDetail
            },
            currentBattery = new
            {
                currentBattery.Name,
                currentBattery.Manufacturer,
                currentBattery.Chemistry,
                designCapacityWh = currentBattery.DesignCapacityMWh is { } design ? design / 1000.0 : (double?)null,
                fullChargeCapacityWh = currentBattery.FullChargeCapacityMWh is { } full ? full / 1000.0 : (double?)null,
                remainingCapacityWh = currentBattery.RemainingCapacityMWh is { } remaining ? remaining / 1000.0 : (double?)null,
                currentBattery.EstimatedChargePercent,
                voltageV = currentBattery.VoltageMV is { } mv ? mv / 1000.0 : (double?)null,
                currentBattery.TemperatureC,
                currentBattery.TemperatureSource,
                estimatedCurrentA = ElectricalDiagnosticsAnalyzer.EstimateSignedCurrentA(currentBattery),
                currentBattery.CycleCount,
                currentBattery.Charging,
                currentBattery.Discharging,
                currentBattery.PowerOnline
            },
            samples
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, new UTF8Encoding(false), cancellationToken);

        var html = BuildHtml(recovery, currentBattery, samples, text, recommendationTitle, recommendationDetail);
        await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(false), cancellationToken);

        return new ExportResult(htmlPath, jsonPath);
    }

    /// <summary>
    /// Builds the standalone HTML report from already-derived metrics and localized labels; this method performs presentation only and does not change diagnostic results.
    /// </summary>
    private static string BuildHtml(
        RecoveredTestInterruption recovery,
        BatterySnapshot battery,
        IReadOnlyList<BatteryTestSample> samples,
        IReadOnlyDictionary<string, string> text,
        string recommendationTitle,
        string recommendationDetail)
    {
        // Local report helpers:
        // T = localized text lookup, E = HTML encoding, F = nullable numeric formatting,
        // P = safe percentage formatting. Keeping them local prevents presentation concerns from
        // leaking into the analysis/domain services.
        string T(string key) => text.TryGetValue(key, out var value) ? value : key;
        string E(string? value) => WebUtility.HtmlEncode(value ?? "—");
        string F(double? value, string format, string suffix = "") => value is { } v ? v.ToString(format) + suffix : "—";
        string P(int? value) => value is { } v ? $"{Math.Clamp(v, 0, 100)}%" : "—";
        string Reliability(int score) => $"{score}/100 · {T("Reliability_" + ReliabilityCode(score))}";

        var currentHealth = battery.HealthPercent;
        var currentWear = currentHealth is { } h ? Math.Clamp(100 - h, 0, 100) : (double?)null;
        var voltageStability = Math.Clamp(100 - recovery.VoltageSagSeverityScore, 0, 100);
        var shutdownReliability = recovery.StrongGaugeJumpDetected ? 0 : recovery.PossibleSuddenCollapse ? 20 : 100;

        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.AppendLine("<title>Battery Doctor</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Tahoma,sans-serif;background:#f6f8fb;color:#0f172a;margin:0;padding:28px}");
        sb.AppendLine(".wrap{max-width:1100px;margin:auto}.card{background:white;border-radius:16px;padding:22px;margin:14px 0;box-shadow:0 1px 3px #0001}");
        sb.AppendLine(".danger{background:#fef2f2;border:1px solid #fca5a5}.advice{background:#fff7ed;border:1px solid #fdba74}.info{background:#eff6ff}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px}.metric{background:#f8fafc;border-radius:12px;padding:14px}");
        sb.AppendLine(".v{font-size:22px;font-weight:700}.l{font-size:12px;color:#64748b;margin-top:4px}.muted{color:#64748b}.critical{color:#991b1b}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;font-size:12px}th,td{padding:7px;border-bottom:1px solid #e5e7eb;text-align:right}");
        sb.AppendLine("th:first-child,td:first-child{text-align:left}h1,h2{margin-top:0}");
        sb.AppendLine("</style></head><body><div class='wrap'>");

        sb.AppendLine($"<h1>{E(T("CollapseReportTitle"))}</h1>");
        sb.AppendLine($"<div class='muted'>{E(T("GeneratedAt"))}: {E(DateTimeOffset.Now.LocalDateTime.ToString("g"))}</div>");

        sb.AppendLine("<div class='card danger'>");
        var recoveryTitleKey = recovery.StrongGaugeJumpDetected ? "RecoveredCollapseHighTitle" : "RecoveredCollapsePossibleTitle";
        sb.AppendLine($"<h2 class='critical'>{E(T(recoveryTitleKey))}</h2>");
        sb.AppendLine($"<p>{E(BuildRecoveryDetail(recovery, T))}</p>");
        sb.AppendLine("<div class='grid'>");
        Metric(sb, P(recovery.LastReportedChargePercent), E(T("RecoveredLastCharge")));
        Metric(sb, P(recovery.CurrentChargePercent), E(T("RecoveredCurrentCharge")));
        Metric(sb, F(recovery.EnergyRecordedWh, "0.00", " Wh"), E(T("RecoveredEnergy")));
        Metric(sb, F(recovery.LastRemainingWh, "0.00", " Wh"), E(T("RecoveredLastRemaining")));
        Metric(sb, F(recovery.LastVoltageV, "0.00", " V"), E(T("RecoveredLastVoltage")));
        Metric(sb, F(recovery.ObservedEnergyVsDesignPercent, "0.0", "%"), E(T("RecoveredObservedEnergyVsDesign")));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class='card info'>");
        sb.AppendLine($"<h2>{E(T("DiagnosticSummary"))}</h2>");
        sb.AppendLine("<div class='grid'>");
        Metric(sb, F(currentHealth, "0.0", "%"), E(T("DiagnosticCapacity")));
        Metric(sb, Reliability(recovery.GaugeReliabilityScore), E(T("DiagnosticGauge")));
        Metric(sb, Reliability(voltageStability), E(T("DiagnosticVoltage")));
        Metric(sb, Reliability(shutdownReliability), E(T("DiagnosticShutdown")));
        Metric(sb, Reliability(recovery.BatteryReliabilityScore), E(T("DiagnosticOverall")));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class='card advice'>");
        sb.AppendLine($"<h2>{E(recommendationTitle)}</h2>");
        sb.AppendLine($"<p>{E(recommendationDetail)}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("CollapseEvidenceDetails"))}</h2>");
        sb.AppendLine("<div class='grid'>");
        Metric(sb, Reliability(recovery.BatteryReliabilityScore), E(T("RecoveredBatteryReliability")));
        Metric(sb, Reliability(recovery.GaugeReliabilityScore), E(T("RecoveredGaugeReliability")));
        Metric(sb, $"{recovery.VoltageSagSeverityScore}/100 · {E(T("SagSeverity_" + recovery.VoltageSagSeverityCode))}", E(T("RecoveredVoltageSagScore")));
        Metric(sb, recovery.VoltageSagEventCount.ToString("N0"), E(T("RecoveredSagEvents")));
        Metric(sb, F(recovery.MaxVoltageSagV, "0.00", " V"), E(T("RecoveredMaxVoltageSag")));
        Metric(sb, FormatFirmwareChange(recovery), E(T("RecoveredFirmwareChange")));
        Metric(sb, F(recovery.ObservedEnergyVsReportedAvailablePercent, "0.0", "%"), E(T("RecoveredObservedVsReportedAvailable")));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("BatteryInformation"))}</h2>");
        sb.AppendLine("<div class='grid'>");
        Metric(sb, E(battery.Name), E(T("BatteryNameLabel")));
        Metric(sb, F(battery.DesignCapacityMWh is { } d ? d / 1000.0 : (double?)null, "0.0", " Wh"), E(T("DesignCapacity")));
        Metric(sb, F(battery.FullChargeCapacityMWh is { } f ? f / 1000.0 : (double?)null, "0.0", " Wh"), E(T("FullChargeCapacity")));
        Metric(sb, F(currentHealth, "0.0", "%"), E(T("CurrentFirmwareHealth")));
        Metric(sb, F(currentWear, "0.0", "%"), E(T("Wear")));
        Metric(sb, battery.CycleCount?.ToString("N0") ?? "—", E(T("CycleCount")));
        Metric(sb, F(battery.TemperatureC, "0.0", " °C"), E(T("BatteryTemperature")));
        Metric(sb, F(ElectricalDiagnosticsAnalyzer.EstimateSignedCurrentA(battery) is { } current ? Math.Abs(current) : (double?)null, "0.00", " A"), E(T("EstimatedCurrent")));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("ReportSamples"))}</h2>");
        sb.AppendLine("<table><thead><tr>");
        foreach (var header in new[] { "ReportTime", "ReportCharge", "Voltage", "CurrentPower", "ReportEstimatedCurrent", "ReportTemperature", "ReportRemainingWh" })
            sb.AppendLine($"<th>{E(T(header))}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var sample in samples)
        {
            var remainingWh = sample.RemainingCapacityMWh is { } remaining ? $"{remaining / 1000.0:0.00} Wh" : "—";
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{E(sample.CapturedAt.LocalDateTime.ToString("G"))}</td>");
            sb.AppendLine($"<td>{E(P(sample.ChargePercent))}</td>");
            sb.AppendLine($"<td>{E(F(sample.VoltageV, "0.00", " V"))}</td>");
            sb.AppendLine($"<td>{E(F(sample.PowerW, "0.0", " W"))}</td>");
            var sampleCurrent = sample.PowerW is > 0 && sample.VoltageV is > 0 ? sample.PowerW.Value / sample.VoltageV.Value : (double?)null;
            sb.AppendLine($"<td>{E(F(sampleCurrent, "0.00", " A"))}</td>");
            sb.AppendLine($"<td>{E(F(sample.TemperatureC, "0.0", " °C"))}</td>");
            sb.AppendLine($"<td>{E(remainingWh)}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table></div>");
        sb.AppendLine($"<div class='card muted'>{E(T("CollapseReportDisclaimer"))}</div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Expands the localized recovered-collapse narrative template with the recorded percentages and energy evidence.
    /// </summary>
    private static string BuildRecoveryDetail(RecoveredTestInterruption recovery, Func<string, string> text)
    {
        var key = recovery.StrongGaugeJumpDetected ? "RecoveredCollapseHighDetail" : "RecoveredCollapsePossibleDetail";
        return text(key)
            .Replace("{last}", recovery.LastReportedChargePercent is { } last ? $"{last}%" : "—")
            .Replace("{current}", recovery.CurrentChargePercent is { } current ? $"{current}%" : "—")
            .Replace("{energy}", recovery.EnergyRecordedWh?.ToString("0.00") ?? "—")
            .Replace("{observed}", recovery.ObservedEnergyVsDesignPercent?.ToString("0.0") ?? "—")
            .Replace("{remaining}", recovery.LastRemainingWh?.ToString("0.00") ?? "—");
    }

    /// <summary>
    /// Formats the pre/post-restart FullChargeCapacity change that indicates a large firmware/BMS re-estimation.
    /// </summary>
    private static string FormatFirmwareChange(RecoveredTestInterruption recovery)
    {
        if (recovery.ReportedFullChargeCapacityWh is not { } before || recovery.FullChargeCapacityAfterRestartWh is not { } after)
            return "—";
        var suffix = recovery.FullChargeCapacityChangePercent is { } delta ? $" ({delta:+0.0;-0.0;0.0}%)" : "";
        return $"{before:0.0} → {after:0.0} Wh{suffix}";
    }

    /// <summary>
    /// Maps a numeric reliability score to the localization code used by reports.
    /// </summary>
    private static string ReliabilityCode(int score) => score switch
    {
        >= 80 => "good",
        >= 60 => "fair",
        >= 35 => "poor",
        _ => "critical"
    };

    /// <summary>
    /// Reads the JSONL sample file and ignores an incomplete final record that can be left by abrupt power loss.
    /// </summary>
    private static IReadOnlyList<BatteryTestSample> ReadSamples(string path)
    {
        var result = new List<BatteryTestSample>();
        if (!File.Exists(path)) return result;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var sample = JsonSerializer.Deserialize<BatteryTestSample>(line);
                if (sample is not null) result.Add(sample);
            }
            catch (JsonException)
            {
                // A hard cutoff may leave an incomplete final JSON line.
            }
        }

        return result.OrderBy(x => x.CapturedAt).ToList();
    }

    /// <summary>
    /// Appends one value/label metric card to the generated HTML.
    /// </summary>
    private static void Metric(StringBuilder sb, string value, string label)
    {
        sb.AppendLine("<div class='metric'>");
        sb.AppendLine($"<div class='v'>{value}</div>");
        sb.AppendLine($"<div class='l'>{label}</div>");
        sb.AppendLine("</div>");
    }
}
