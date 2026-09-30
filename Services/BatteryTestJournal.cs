using System.IO;
using System.Text;
using System.Text.Json;
using BatteryDoctor.Models;

// File responsibility: Crash-safe test journal. Persists every collapse-watch sample and reconstructs interrupted sessions after reboot.

namespace BatteryDoctor.Services;

/// <summary>
/// Crash/power-loss tolerant journal for a running battery test.
/// Each sample is appended and flushed to disk immediately so an unexpected
/// battery cutoff can be analyzed after Windows starts again.
/// </summary>
public sealed class BatteryTestJournal
{
    private readonly string _sessionDirectory;
    private readonly string _activePath;
    private TestJournalState? _active;
    private readonly VoltageSagAnalyzer _voltageSagAnalyzer = new();

    /// <summary>
    /// Binds the journal to the portable Sessions folder and defines active-test.json as the recoverable session marker.
    /// </summary>
    public BatteryTestJournal()
    {
        _sessionDirectory = PortablePaths.SessionsDirectory;
        Directory.CreateDirectory(_sessionDirectory);
        _activePath = Path.Combine(_sessionDirectory, "active-test.json");
    }

    public string SessionDirectory => _sessionDirectory;

    /// <summary>
    /// Creates a new active session state and stores a relative JSONL sample filename so the whole portable folder can be moved safely.
    /// </summary>
    public void StartSession(BatterySnapshot snapshot, string profileCode)
    {
        var id = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var samplePath = $"test-{id}.jsonl";
        _active = new TestJournalState
        {
            SchemaVersion = 1,
            SessionId = id,
            Status = "active",
            ProfileCode = profileCode,
            StartedAt = snapshot.CapturedAt,
            BatteryName = snapshot.Name,
            StartChargePercent = ClampPercent(snapshot.EstimatedChargePercent),
            DesignCapacityWh = snapshot.DesignCapacityMWh is { } design ? design / 1000.0 : null,
            FullChargeCapacityWh = snapshot.FullChargeCapacityMWh is { } full ? full / 1000.0 : (double?)null,
            SamplePath = samplePath
        };

        WriteState(_activePath, _active);
    }

    /// <summary>
    /// Appends one JSONL sample using write-through and an explicit disk flush so useful evidence survives a sudden battery cutoff.
    /// </summary>
    public void AppendSample(BatteryTestSample sample)
    {
        _active ??= ReadState(_activePath);
        if (_active is null || !string.Equals(_active.Status, "active", StringComparison.OrdinalIgnoreCase))
            return;

        var line = JsonSerializer.Serialize(sample) + Environment.NewLine;
        using var stream = new FileStream(
            PortablePaths.ResolveSessionPath(_active.SamplePath),
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true);
        writer.Write(line);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Marks a normal test completion, archives its metadata, removes the active marker, and clears in-memory journal state.
    /// </summary>
    public void CompleteSession(string reason)
    {
        _active ??= ReadState(_activePath);
        if (_active is null) return;

        _active.Status = reason;
        _active.EndedAt = DateTimeOffset.Now;
        var completedPath = Path.Combine(_sessionDirectory, $"session-{_active.SessionId}-{Sanitize(reason)}.json");
        WriteState(completedPath, _active);
        TryDelete(_activePath);
        _active = null;
    }

    /// <summary>
    /// On startup, determines whether an active journal ended close to a real Windows reboot; if so, reconstructs collapse evidence from the last persisted samples.
    /// </summary>
    public RecoveredTestInterruption? RecoverInterruptedSession(BatterySnapshot currentSnapshot)
    {
        var state = ReadState(_activePath);
        if (state is null || !string.Equals(state.Status, "active", StringComparison.OrdinalIgnoreCase))
            return null;

        var samples = ReadSamples(PortablePaths.ResolveSessionPath(state.SamplePath));
        if (samples.Count == 0)
        {
            ArchiveRecoveredState(state, "interrupted_no_samples");
            return null;
        }

        var last = samples[^1];
        // Environment.TickCount64 is monotonic since the current Windows boot. Comparing the
        // reconstructed boot time with the last flushed sample helps distinguish a real reboot
        // from the user simply closing/reopening Battery Doctor in the same boot.
        var bootAt = DateTimeOffset.Now - TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64));
        var systemRestarted = bootAt > last.CapturedAt.AddSeconds(3);
        var restartGap = bootAt - last.CapturedAt;
        var restartCloseToLastSample = systemRestarted && restartGap <= TimeSpan.FromMinutes(5);

        // A same-boot app close/crash is not enough evidence of a battery collapse.
        if (!restartCloseToLastSample)
        {
            ArchiveRecoveredState(state, systemRestarted ? "interrupted_stale_reboot" : "interrupted_same_boot");
            return null;
        }

        var result = AnalyzeRecoveredSession(
            state,
            samples,
            ClampPercent(currentSnapshot.EstimatedChargePercent),
            currentSnapshot.FullChargeCapacityMWh is { } currentFull ? currentFull / 1000.0 : (double?)null,
            systemRestarted);

        var recoveryPath = Path.Combine(_sessionDirectory, $"recovered-{state.SessionId}.json");
        File.WriteAllText(recoveryPath, JsonSerializer.Serialize(result, JsonOptions()), new UTF8Encoding(false));
        ArchiveRecoveredState(state, result.PossibleSuddenCollapse ? "recovered_possible_collapse" : "recovered_restart");
        return result;
    }

    /// <summary>
    /// Re-opens the most recent saved recovery result so Phase 2.7 can enrich a
    /// collapse that was already captured by Phase 2.6. Missing Phase 2.7 fields
    /// are recalculated from the crash-safe JSONL sample file.
    /// </summary>
    public RecoveredTestInterruption? LoadLatestRecoveredSession(BatterySnapshot currentSnapshot)
    {
        var file = Directory.EnumerateFiles(_sessionDirectory, "recovered-*.json")
            .Select(path => new FileInfo(path))
            .Where(info => info.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-7))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .FirstOrDefault();
        if (file is null) return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file.FullName));
            var root = document.RootElement;
            var storedSamplePath = JsonString(root, "SamplePath");
            if (string.IsNullOrWhiteSpace(storedSamplePath)) return null;
            var samplePath = PortablePaths.ResolveSessionPath(storedSamplePath);
            if (!File.Exists(samplePath))
            {
                var sessionId = JsonString(root, "SessionId") ?? Path.GetFileNameWithoutExtension(file.Name).Replace("recovered-", "");
                samplePath = Path.Combine(_sessionDirectory, $"test-{sessionId}.jsonl");
            }
            var samples = ReadSamples(samplePath);
            if (samples.Count == 0) return null;

            var state = new TestJournalState
            {
                SchemaVersion = 1,
                SessionId = JsonString(root, "SessionId") ?? Path.GetFileNameWithoutExtension(file.Name).Replace("recovered-", ""),
                Status = "recovered_possible_collapse",
                ProfileCode = "collapse",
                StartedAt = JsonDate(root, "StartedAt") ?? samples[0].CapturedAt,
                BatteryName = currentSnapshot.Name,
                StartChargePercent = JsonInt(root, "StartChargePercent") ?? ClampPercent(samples[0].ChargePercent),
                DesignCapacityWh = JsonDouble(root, "DesignCapacityWh"),
                FullChargeCapacityWh = JsonDouble(root, "ReportedFullChargeCapacityWh"),
                SamplePath = Path.GetFileName(samplePath)
            };

            var currentAtRecovery = JsonInt(root, "CurrentChargePercent");
            var postFullWh = JsonDouble(root, "FullChargeCapacityAfterRestartWh") ??
                             (currentSnapshot.FullChargeCapacityMWh is { } full ? full / 1000.0 : (double?)null);
            var result = AnalyzeRecoveredSession(state, samples, currentAtRecovery, postFullWh, systemRestarted: true);

            // Upgrade the saved recovery JSON in place so subsequent starts do not
            // need to infer Phase 2.7 fields again.
            File.WriteAllText(file.FullName, JsonSerializer.Serialize(result, JsonOptions()), new UTF8Encoding(false));
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Derives gauge jump, observed energy, firmware recalibration, voltage sag, gauge reliability, overall battery reliability, and confidence from a recovered interruption.
    /// </summary>
    private RecoveredTestInterruption AnalyzeRecoveredSession(
        TestJournalState state,
        IReadOnlyList<BatteryTestSample> samples,
        int? currentPct,
        double? postFullWh,
        bool systemRestarted)
    {
        var last = samples[^1];
        var lastPct = ClampPercent(last.ChargePercent);
        currentPct = ClampPercent(currentPct);
        var strongGaugeJump = lastPct is { } before && currentPct is { } after &&
                              before >= 20 && after <= 10 && before - after >= 20;
        var possibleCollapse = lastPct is { } reported && reported >= 20;
        var energyWh = EstimateEnergyWh(samples);
        var designWh = state.DesignCapacityWh;
        var preFullWh = state.FullChargeCapacityWh;
        var lastRemainingWh = last.RemainingCapacityMWh is { } remaining ? remaining / 1000.0 : (double?)null;

        double? observedEnergyVsDesign = null;
        if (designWh is > 0 && energyWh is > 0)
            observedEnergyVsDesign = Math.Clamp(energyWh.Value * 100.0 / designWh.Value, 0, 150);

        double? observedVsReportedAvailable = null;
        if (energyWh is > 0 && preFullWh is > 0 && state.StartChargePercent is { } startPct && startPct > 0)
        {
            var reportedAvailableAtStart = preFullWh.Value * Math.Clamp(startPct, 0, 100) / 100.0;
            if (reportedAvailableAtStart > 0)
                observedVsReportedAvailable = Math.Clamp(energyWh.Value * 100.0 / reportedAvailableAtStart, 0, 200);
        }

        double? fullChargeChangePercent = null;
        var firmwareRecalibrationDetected = false;
        if (preFullWh is > 0 && postFullWh is > 0)
        {
            fullChargeChangePercent = (postFullWh.Value - preFullWh.Value) * 100.0 / preFullWh.Value;
            firmwareRecalibrationDetected = Math.Abs(fullChargeChangePercent.Value) >= 20.0;
        }

        var sag = _voltageSagAnalyzer.Analyze(samples);

        // Reliability scores are intentionally conservative: a large post-reboot gauge jump,
        // firmware capacity re-estimation, or claimed remaining Wh at cutoff each reduce trust.
        var gaugeScore = 100;
        if (strongGaugeJump) gaugeScore -= 55;
        else if (possibleCollapse) gaugeScore -= 25;
        if (firmwareRecalibrationDetected) gaugeScore -= 20;
        if (possibleCollapse && lastRemainingWh is > 2.0) gaugeScore -= 15;
        if (observedVsReportedAvailable is < 60) gaugeScore -= 10;
        gaugeScore = Math.Clamp(gaugeScore, 0, 100);
        var gaugeCode = ScoreToReliabilityCode(gaugeScore);

        var capacityScore = designWh is > 0 && preFullWh is > 0
            ? Math.Clamp(preFullWh.Value * 100.0 / designWh.Value, 0, 100)
            : 50.0;
        var voltageStabilityScore = 100 - sag.SeverityScore;
        var reliabilityScore = (int)Math.Round(
            Math.Clamp(capacityScore * 0.35 + gaugeScore * 0.35 + voltageStabilityScore * 0.30, 0, 100));
        if (possibleCollapse) reliabilityScore = Math.Min(reliabilityScore, 20);
        if (strongGaugeJump) reliabilityScore = Math.Min(reliabilityScore, 10);
        var reliabilityCode = ScoreToReliabilityCode(reliabilityScore);

        var confidence = strongGaugeJump && (sag.SeverityScore >= 50 || firmwareRecalibrationDetected)
            ? "very_high"
            : strongGaugeJump
                ? "high"
                : possibleCollapse && lastPct is >= 50
                    ? "medium"
                    : "low";

        return new RecoveredTestInterruption(
            state.SessionId,
            state.StartedAt,
            last.CapturedAt,
            state.StartChargePercent,
            lastPct,
            currentPct,
            energyWh,
            designWh,
            preFullWh,
            observedEnergyVsDesign,
            observedVsReportedAvailable,
            lastRemainingWh,
            last.VoltageV,
            postFullWh,
            fullChargeChangePercent,
            firmwareRecalibrationDetected,
            sag.SeverityScore,
            sag.SeverityCode,
            sag.EventCount,
            sag.MaxSagV,
            sag.WorstBaselineV,
            sag.WorstVoltageV,
            gaugeScore,
            gaugeCode,
            reliabilityScore,
            reliabilityCode,
            systemRestarted,
            strongGaugeJump,
            possibleCollapse,
            confidence,
            Path.GetFileName(state.SamplePath));
    }

    /// <summary>
    /// Maps a numeric reliability score to good/fair/poor/critical.
    /// </summary>
    private static string ScoreToReliabilityCode(int score) => score switch
    {
        >= 80 => "good",
        >= 60 => "fair",
        >= 35 => "poor",
        _ => "critical"
    };

    /// <summary>
    /// Safely reads a nullable string property from recovery JSON.
    /// </summary>
    private static string? JsonString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// Safely reads a nullable integer property from recovery JSON.
    /// </summary>
    private static int? JsonInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;

    /// <summary>
    /// Safely reads a nullable double property from recovery JSON.
    /// </summary>
    private static double? JsonDouble(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : null;

    /// <summary>
    /// Safely reads a nullable DateTimeOffset property from recovery JSON.
    /// </summary>
    private static DateTimeOffset? JsonDate(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
           DateTimeOffset.TryParse(value.GetString(), out var result) ? result : null;

    /// <summary>
    /// Archives the interrupted session metadata with a status describing how recovery classified it.
    /// </summary>
    private void ArchiveRecoveredState(TestJournalState state, string status)
    {
        state.Status = status;
        state.EndedAt = DateTimeOffset.Now;
        var path = Path.Combine(_sessionDirectory, $"session-{state.SessionId}-{Sanitize(status)}.json");
        WriteState(path, state);
        TryDelete(_activePath);
        _active = null;
    }

    /// <summary>
    /// Reads the crash-safe JSONL sample stream, tolerating a truncated final line that may result from abrupt power loss.
    /// </summary>
    private static List<BatteryTestSample> ReadSamples(string path)
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
                // A hard cutoff can leave the final JSON line incomplete.
                // Earlier fully flushed lines remain usable.
            }
        }
        return result.OrderBy(x => x.CapturedAt).ToList();
    }

    /// <summary>
    /// Integrates recovered power samples with the trapezoidal rule to estimate energy actually delivered before interruption.
    /// </summary>
    private static double? EstimateEnergyWh(IReadOnlyList<BatteryTestSample> samples)
    {
        if (samples.Count < 2) return null;
        double totalWh = 0;
        var intervals = 0;

        for (var i = 1; i < samples.Count; i++)
        {
            var a = samples[i - 1];
            var b = samples[i];
            if (a.PowerW is not > 0 || b.PowerW is not > 0) continue;

            var hours = (b.CapturedAt - a.CapturedAt).TotalHours;
            if (hours is <= 0 or > 0.1) continue;

            totalWh += ((a.PowerW.Value + b.PowerW.Value) / 2.0) * hours;
            intervals++;
        }

        return intervals == 0 ? null : totalWh;
    }

    /// <summary>
    /// Clamps firmware charge values to the user-visible 0-100 percent range.
    /// </summary>
    private static int? ClampPercent(int? value)
        => value is { } pct ? Math.Clamp(pct, 0, 100) : null;

    /// <summary>
    /// Persists journal state via a temporary file followed by replace/move so metadata updates are as atomic as practical.
    /// </summary>
    private static void WriteState(string path, TestJournalState state)
    {
        var tmp = path + ".tmp";
        var bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(state, JsonOptions()));
        using (var stream = new FileStream(
                   tmp,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   4096,
                   FileOptions.WriteThrough))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>
    /// Reads and deserializes journal metadata; malformed state is treated as unavailable instead of crashing startup.
    /// </summary>
    private static TestJournalState? ReadState(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<TestJournalState>(File.ReadAllText(path));
        }
        catch (Exception) when (File.Exists(path))
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the consistent indented serializer settings used for journal/recovery metadata.
    /// </summary>
    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };

    /// <summary>
    /// Replaces characters that are unsafe in an archive filename.
    /// </summary>
    private static string Sanitize(string value)
    {
        foreach (var ch in Path.GetInvalidFileNameChars())
            value = value.Replace(ch, '_');
        return value;
    }

    /// <summary>
    /// Best-effort deletion used for journal cleanup; cleanup failure must never crash battery monitoring.
    /// </summary>
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private sealed class TestJournalState
    {
        public int SchemaVersion { get; set; }
        public string SessionId { get; set; } = "";
        public string Status { get; set; } = "active";
        public string ProfileCode { get; set; } = "standard";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public string? BatteryName { get; set; }
        public int? StartChargePercent { get; set; }
        public double? DesignCapacityWh { get; set; }
        public double? FullChargeCapacityWh { get; set; }
        public string SamplePath { get; set; } = "";
    }
}
