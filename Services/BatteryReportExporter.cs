using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using BatteryDoctor.Models;

// File responsibility: Exports a completed discharge test to privacy-conscious JSON and human-readable HTML.

namespace BatteryDoctor.Services;

/// <summary>
/// Exports a completed discharge test to privacy-conscious JSON and human-readable HTML.
/// </summary>
public sealed class BatteryReportExporter
{
    /// <summary>
    /// Exports the supplied diagnostic/test data to JSON and a standalone human-readable HTML report in the portable Reports folder.
    /// </summary>
    public async Task<ExportResult> ExportAsync(
        BatteryTestSummary summary,
        IReadOnlyList<BatteryTestSample> samples,
        BatterySnapshot battery,
        IReadOnlyList<ReportObservation> observations,
        IReadOnlyDictionary<string, string> text,
        string language,
        CancellationToken cancellationToken = default)
    {
        var reportDir = PortablePaths.ReportsDirectory;
        Directory.CreateDirectory(reportDir);

        var stamp = summary.EndedAt.LocalDateTime.ToString("yyyyMMdd-HHmmss");
        var baseName = $"BatteryDoctor-Test-{stamp}";
        var jsonPath = Path.Combine(reportDir, baseName + ".json");
        var htmlPath = Path.Combine(reportDir, baseName + ".html");

        var payload = new
        {
            schemaVersion = 3,
            generatedAt = DateTimeOffset.Now,
            language,
            battery = new
            {
                battery.Name,
                battery.Manufacturer,
                battery.Chemistry,
                battery.TemperatureC,
                battery.TemperatureSource,
                estimatedCurrentA = ElectricalDiagnosticsAnalyzer.EstimateSignedCurrentA(battery),
                designCapacityWh = battery.DesignCapacityMWh / 1000.0,
                fullChargeCapacityWh = battery.FullChargeCapacityMWh / 1000.0,
                healthPercent = summary.BatteryHealthPercent,
                battery.CycleCount
            },
            test = summary,
            observations,
            samples
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(jsonPath, json, new UTF8Encoding(false), cancellationToken);

        var html = BuildHtml(summary, samples, battery, observations, text);
        await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(false), cancellationToken);

        return new ExportResult(htmlPath, jsonPath);
    }

    /// <summary>
    /// Builds the standalone HTML report from already-derived metrics and localized labels; this method performs presentation only and does not change diagnostic results.
    /// </summary>
    private static string BuildHtml(
        BatteryTestSummary summary,
        IReadOnlyList<BatteryTestSample> samples,
        BatterySnapshot battery,
        IReadOnlyList<ReportObservation> observations,
        IReadOnlyDictionary<string, string> text)
    {
        // Local report helpers:
        // T = localized text lookup, E = HTML encoding, F = nullable numeric formatting,
        // P = safe percentage formatting. Keeping them local prevents presentation concerns from
        // leaking into the analysis/domain services.
        string T(string key) => text.TryGetValue(key, out var value) ? value : key;
        string E(string? value) => WebUtility.HtmlEncode(value ?? "—");
        string F(double? value, string format, string suffix = "") =>
            value is { } v ? v.ToString(format) + suffix : "—";
        string P(int? value) => value is { } v ? v + "%" : "—";

        var sb = new StringBuilder();

        // Use single quotes for HTML attributes so the C# source does not need
        // escaped double quotes. This intentionally avoids the quoting problem
        // that caused the Phase 2.5 exporter build failure.
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html><head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width,initial-scale=1'>");
        sb.AppendLine("<title>Battery Doctor</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Tahoma,sans-serif;background:#f6f8fb;color:#0f172a;margin:0;padding:28px}");
        sb.AppendLine(".wrap{max-width:980px;margin:auto}.card{background:white;border-radius:16px;padding:22px;margin:14px 0;box-shadow:0 1px 3px #0001}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px}.metric{background:#f8fafc;border-radius:12px;padding:14px}");
        sb.AppendLine(".v{font-size:22px;font-weight:700}.l{font-size:12px;color:#64748b;margin-top:4px}.verdict{background:#eff6ff}.muted{color:#64748b}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;font-size:12px}th,td{padding:7px;border-bottom:1px solid #e5e7eb;text-align:right}");
        sb.AppendLine("th:first-child,td:first-child{text-align:left}h1,h2{margin-top:0}");
        sb.AppendLine("</style></head><body><div class='wrap'>");

        sb.AppendLine($"<h1>{E(T("ReportTitle"))}</h1>");
        sb.AppendLine($"<div class='muted'>{E(T("GeneratedAt"))}: {E(DateTimeOffset.Now.LocalDateTime.ToString("g"))}</div>");

        sb.AppendLine("<div class='card verdict'>");
        sb.AppendLine($"<h2>{E(T("TestVerdictHeading"))}: {E(T("VerdictTitle"))}</h2>");
        sb.AppendLine($"<p>{E(T("VerdictDetail"))}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("BatteryInformation"))}</h2>");
        sb.AppendLine("<div class='grid'>");
        Metric(sb, E(battery.Name), E(T("BatteryNameLabel")));
        Metric(sb, F(summary.BatteryHealthPercent, "0.0", "%"), E(T("HealthScore")));
        Metric(sb, F(battery.DesignCapacityMWh / 1000.0, "0.0", " Wh"), E(T("DesignCapacity")));
        Metric(sb, F(battery.FullChargeCapacityMWh / 1000.0, "0.0", " Wh"), E(T("FullChargeCapacity")));
        Metric(sb, battery.CycleCount?.ToString("N0") ?? "—", E(T("CycleCount")));
        Metric(sb, F(battery.TemperatureC, "0.0", " °C"), E(T("BatteryTemperature")));
        Metric(sb, F(ElectricalDiagnosticsAnalyzer.EstimateSignedCurrentA(battery) is { } current ? Math.Abs(current) : (double?)null, "0.00", " A"), E(T("EstimatedCurrent")));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("BatteryTest"))}</h2>");
        sb.AppendLine("<div class='grid'>");
        Metric(sb, E(FormatDuration(summary.Duration)), E(T("TestDuration")));
        Metric(sb, P(summary.StartChargePercent) + " → " + P(summary.EndChargePercent), E(T("TestChargeStartEnd")));
        Metric(sb, P(summary.ChargeDropPercent), E(T("TestChargeDrop")));
        Metric(sb, F(summary.AveragePowerW, "0.0", " W"), E(T("TestAveragePower")));
        Metric(sb, F(summary.EstimatedEnergyUsedWh, "0.00", " Wh"), E(T("TestEnergyUsed")));
        Metric(sb, F(summary.AverageVoltageV, "0.00", " V"), E(T("TestAverageVoltage")));

        var voltageRange = summary.MinVoltageV is { } min && summary.MaxVoltageV is { } max
            ? $"{min:0.00}–{max:0.00} V"
            : "—";
        Metric(sb, voltageRange, E(T("TestVoltageRange")));
        Metric(sb, summary.ValidPowerSampleCount.ToString("N0"), E(T("TestValidPowerSamples")));
        Metric(sb, E(T($"TestConfidence_{summary.ConfidenceCode}")), E(T("TestConfidence")));
        Metric(sb, F(summary.EnergyAgreementPercent, "0.0", "%"), E(T("TestEnergyAgreement")));
        Metric(sb, E(T($"DataConsistency_{summary.DataConsistencyCode}")), E(T("TestDataConsistency")));
        Metric(sb, F(summary.ExtrapolatedUsableCapacityWh, "0.0", " Wh"), E(T("TestEstimatedUsableCapacity")));
        Metric(sb, F(summary.ExtrapolatedUsableHealthPercent, "0.0", "%"), E(T("TestEstimatedUsableHealth")));
        var sagLabel = E(T("SagSeverity_" + summary.VoltageSagSeverityCode));
        Metric(sb, $"{summary.VoltageSagSeverityScore}/100 · {sagLabel}", E(T("TestVoltageSagScore")));
        Metric(sb, summary.VoltageSagEventCount.ToString("N0"), E(T("TestVoltageSagEvents")));
        Metric(sb, F(summary.MaxVoltageSagV, "0.00", " V"), E(T("TestMaxVoltageSag")));
        Metric(sb, F(summary.AverageTemperatureC, "0.0", " °C"), E(T("TestAverageTemperature")));
        var temperatureRange = summary.MinTemperatureC is { } minT && summary.MaxTemperatureC is { } maxT
            ? $"{minT:0.0}–{maxT:0.0} °C"
            : "—";
        Metric(sb, temperatureRange, E(T("TestTemperatureRange")));
        Metric(sb, F(summary.TemperatureRiseC, "+0.0;-0.0;0.0", " °C"), E(T("TestTemperatureRise")));
        Metric(sb, F(summary.AverageEstimatedCurrentA, "0.00", " A"), E(T("TestAverageCurrent")));
        Metric(sb, F(summary.DynamicResistanceOhm, "0.000", " Ω"), E(T("TestDynamicResistance")));
        Metric(sb, F(summary.PowerTemperatureCorrelation, "+0.00;-0.00;0.00"), E(T("TestPowerTemperatureCorrelation")));
        sb.AppendLine("</div></div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("TestObservations"))}</h2>");
        foreach (var observation in observations)
        {
            sb.AppendLine($"<p><strong>{E(observation.Title)}</strong><br><span class='muted'>{E(observation.Detail)}</span></p>");
        }
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='card'>");
        sb.AppendLine($"<h2>{E(T("ReportSamples"))}</h2>");
        sb.AppendLine("<table><thead><tr>");
        foreach (var header in new[] { "ReportTime", "ReportCharge", "Voltage", "CurrentPower", "ReportEstimatedCurrent", "ReportTemperature", "ReportRemainingWh" })
        {
            sb.AppendLine($"<th>{E(T(header))}</th>");
        }
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var sample in samples)
        {
            var remainingWh = sample.RemainingCapacityMWh is { } remaining
                ? $"{remaining / 1000.0:0.00} Wh"
                : "—";

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
        sb.AppendLine($"<div class='card muted'>{E(T("ReportDisclaimer"))}</div>");
        sb.AppendLine("</div></body></html>");

        return sb.ToString();
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

    /// <summary>
    /// Formats the test duration for the exported report.
    /// </summary>
    private static string FormatDuration(TimeSpan span)
    {
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:0}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }
}
