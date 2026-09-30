using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BatteryDoctor.Models;

// File responsibility: Creates a privacy-safe diagnostic snapshot for support/debugging without user names, serial numbers, or local paths.

namespace BatteryDoctor.Services;

/// <summary>
/// Creates a privacy-safe diagnostic snapshot for support/debugging without user names, serial numbers, or local paths.
/// </summary>
public sealed class DiagnosticSnapshotExporter
{
    /// <summary>
    /// Writes a support snapshot that intentionally omits Windows username, battery serial, sample path, and unrelated application/browsing data.
    /// </summary>
    public async Task<string> ExportAsync(
        BatterySnapshot? battery,
        RecoveredTestInterruption? recovery,
        string appVersion,
        CancellationToken cancellationToken = default)
    {
        var reportDir = PortablePaths.ReportsDirectory;
        Directory.CreateDirectory(reportDir);
        var path = Path.Combine(reportDir, $"BatteryDoctor-Diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        var payload = new
        {
            schemaVersion = 2,
            reportType = "diagnostic_snapshot",
            generatedAt = DateTimeOffset.Now,
            appVersion,
            runtime = new
            {
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.OSArchitecture.ToString(),
                dotnet = Environment.Version.ToString()
            },
            battery = battery is null ? null : new
            {
                battery.Name,
                battery.Manufacturer,
                battery.Chemistry,
                designCapacityWh = battery.DesignCapacityMWh is { } d ? d / 1000.0 : (double?)null,
                fullChargeCapacityWh = battery.FullChargeCapacityMWh is { } f ? f / 1000.0 : (double?)null,
                remainingCapacityWh = battery.RemainingCapacityMWh is { } r ? r / 1000.0 : (double?)null,
                battery.EstimatedChargePercent,
                voltageV = battery.VoltageMV is { } mv ? mv / 1000.0 : (double?)null,
                battery.TemperatureC,
                battery.TemperatureSource,
                estimatedCurrentA = ElectricalDiagnosticsAnalyzer.EstimateSignedCurrentA(battery),
                battery.CycleCount,
                battery.Charging,
                battery.Discharging,
                battery.PowerOnline,
                battery.Critical
            },
            recoveredCollapse = recovery is null ? null : new
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
                recovery.VoltageSagEventCount,
                recovery.MaxVoltageSagV,
                recovery.GaugeReliabilityScore,
                recovery.BatteryReliabilityScore,
                recovery.StrongGaugeJumpDetected,
                recovery.PossibleSuddenCollapse,
                recovery.ConfidenceCode
            },
            privacy = "This diagnostic snapshot intentionally omits the Windows user name, battery serial number, journal file path, and browsing/application data."
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), cancellationToken);
        return path;
    }
}
