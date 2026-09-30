# Battery Doctor architecture

This document explains how the project is divided and how data moves through the application. It is intended for contributors who are opening the source for the first time.

## 1. Design goals

Battery Doctor is a Windows laptop battery diagnostic tool with four deliberate constraints:

1. **Offline first** — battery data is processed locally; no account, cloud service, or telemetry is required.
2. **Portable first** — runtime state stays beside the application in `Data/`, `Sessions/`, `Reports/`, and `Logs/`.
3. **Do not trust one number** — firmware `Health %` is useful, but the application also evaluates voltage sag, gauge reliability, energy delivered, firmware recalibration, and sudden cutoff behavior.
4. **Crash-safe evidence** — Collapse Watch samples are flushed to disk so an unexpected battery cutoff can be analyzed after reboot.

## 2. High-level flow

```text
Windows / firmware
      |
      v
WindowsBatteryProvider
      |
      v
BatterySnapshot  <-------------------------------+
      |                                           |
      +--> BatteryHealthAnalyzer                  |
      +--> BatteryAnomalyDetector                 |
      +--> ElectricalDiagnosticsAnalyzer          |
      +--> MainViewModel live window              |
      +--> HistoryRepository (SQLite)             |
      +--> BatteryTestJournal --> JSONL samples --+
      |                    |
      |                    +--> reboot recovery
      |                              |
      |                              v
      |                   RecoveredTestInterruption
      |                              |
      +--> BatteryTestAnalyzer       +--> RecoveredCollapseReportExporter
      |          |
      |          +--> VoltageSagAnalyzer
      |
      +--> MainViewModel --> WPF bindings --> MainWindow / TrendChart
                                      |
                                      +--> NotifyIcon / tray warnings
```

## 3. Project areas

### `Models/`

Pure data contracts. These classes/records should contain very little application logic. Examples:

- `BatterySnapshot` — one normalized point-in-time reading.
- `BatteryTestSample` — compact test/journal sample.
- `BatteryTestSummary` — derived metrics for a completed/in-progress discharge test.
- `RecoveredTestInterruption` — reconstructed evidence after a reboot interrupted a test.
- `VoltageSagAssessment` — output of voltage-sag analysis.

When adding a new diagnostic metric, prefer adding it to an appropriate model rather than passing loose tuples/dictionaries through the codebase.

### `Services/WindowsBatteryProvider.cs`

The hardware/OS boundary. Windows exposes battery fields through several providers and not every laptop supports every field. The provider therefore merges:

- `ROOT\WMI` battery classes,
- `Win32_Battery`, and
- a cached `powercfg /batteryreport /xml` fallback.

Provider failures are normally treated as **missing optional data**, not fatal application errors.

### `Services/BatteryTemperatureProvider.cs`

Reads a **real battery-pack temperature** from the Windows battery class interface. The preferred path is:

1. enumerate battery device interfaces with SetupAPI,
2. acquire the current battery tag,
3. request `BatteryTemperature` with `IOCTL_BATTERY_QUERY_INFORMATION`, and
4. convert tenths Kelvin to Celsius.

If the native battery driver rejects that optional query, Battery Doctor may try the battery-specific `ROOT\WMI` `BatteryTemperature` block.

It intentionally does **not** read generic ACPI thermal zones or CPU/GPU sensors and label them as battery temperature. If the OEM does not expose a battery sensor, the value remains unavailable.

### `Services/ElectricalDiagnosticsAnalyzer.cs`

Derives secondary pack-level metrics from recent telemetry:

- **Estimated current**: `I ≈ P / V`, signed positive while charging and negative while discharging.
- **Dynamic resistance estimate**: median of valid short load steps using `|ΔV / ΔI|`.
- **Temperature delta/rate**: change over the recent live window.
- **Power↔temperature correlation**: descriptive Pearson correlation when both signals have enough samples and variation.

Dynamic resistance and correlation are trend indicators, not laboratory measurements. Battery thermal response can lag electrical load and Windows normally exposes pack-level rather than per-cell telemetry.

### `Services/BatteryHealthAnalyzer.cs`

Interprets firmware capacity health. It intentionally does **not** claim that a battery is electrically stable just because Full Charge Capacity looks acceptable.

### `Services/BatteryAnomalyDetector.cs`

Looks for short-term behavior that is suspicious independently of long-term capacity health, such as abrupt percentage loss and voltage movement.

### `Services/VoltageSagAnalyzer.cs`

Builds a rolling local median voltage baseline and measures short-duration drops below it. A median baseline is used instead of a single previous sample because WMI readings can be noisy.

The sag score is **diagnostic evidence**, not cell-level diagnosis. Windows normally does not expose each individual cell-group voltage.

### `Services/BatteryTestAnalyzer.cs`

Turns test samples into derived metrics. Important calculations include:

- trapezoidal integration of power over time (`Wh`),
- firmware remaining-capacity delta,
- energy-agreement/consistency,
- test confidence,
- extrapolated usable capacity (only when enough charge drop exists),
- voltage-sag score,
- battery temperature range/change when a real sensor is available,
- average estimated current,
- dynamic-resistance estimate,
- descriptive power↔temperature correlation, and
- final verdict code.

Do not silently turn a short partial test into a laboratory-quality full-capacity measurement. Confidence and wording must remain conservative.

### `Services/BatteryTestJournal.cs`

This is the critical path for sudden-collapse detection.

When a test starts:

1. `active-test.json` stores session metadata.
2. Each test sample is appended to `test-<session>.jsonl`.
3. The stream uses write-through plus an explicit disk flush.

After a real reboot, `RecoverInterruptedSession` checks whether the last flushed sample was close to the reconstructed Windows boot time. This helps distinguish:

- actual reboot/power loss, from
- the user merely closing and reopening Battery Doctor in the same Windows boot.

Recovered evidence is then scored for gauge jump, firmware capacity recalibration, voltage sag, claimed remaining Wh at cutoff, and overall reliability.

### `Services/HistoryRepository.cs`

SQLite history stored at `Data/battery-doctor.db`. Writes are intentionally throttled (currently every few minutes) instead of saving every live poll. Trend queries select representative daily samples so charts remain lightweight.

### `Services/PortablePaths.cs`

The single source of truth for writable runtime locations. New code must use this class instead of hard-coding `%LOCALAPPDATA%`, Documents, or an absolute user path.

Legacy migration is **copy only**: existing user-profile data is not deleted.

### Report exporters

- `BatteryReportExporter` — normal discharge-test report.
- `RecoveredCollapseReportExporter` — sudden-collapse report.
- `DiagnosticSnapshotExporter` — privacy-safe support/debug snapshot.

Shared reports must not unnecessarily expose:

- Windows user names,
- absolute local paths,
- battery serial numbers, or
- unrelated machine/application data.

### `ViewModels/MainViewModel.cs`

The orchestration layer. It owns application workflows but not the physical window. Its refresh cycle is roughly:

1. read one `BatterySnapshot`,
2. recover prior interrupted test once per startup,
3. add live telemetry,
4. evaluate background alerts,
5. update health assessment,
6. update an active test,
7. refresh findings/anomalies,
8. periodically persist history,
9. raise WPF binding notifications.

All calculations during one refresh should derive from the **same snapshot** to avoid mixing telemetry from different polling moments.

### `MainWindow.xaml.cs`

Window lifecycle and tray shell only. It owns:

- monitor timer,
- custom title-bar behavior,
- minimize-to-tray/restore,
- `NotifyIcon`, and
- tray balloon rendering.

Business/diagnostic logic should remain in services/view model rather than moving into the window code-behind.

## 4. Polling cadence

- Normal monitoring: about 5 seconds.
- Active battery test: about 2 seconds.
- Long-term history: throttled separately (minutes, not seconds).

A faster test sample rate improves collapse evidence, while the slower normal rate reduces unnecessary WMI work.

## 5. Reliability vs health

Keep these concepts separate in new features:

- **Firmware Health**: `FullChargeCapacity / DesignCapacity`.
- **Capacity condition**: interpretation of firmware health.
- **Gauge reliability**: how trustworthy the displayed percentage/remaining energy appears.
- **Voltage stability**: derived from sag behavior.
- **Shutdown reliability**: whether the pack can unexpectedly cut power while Windows still reports substantial charge.
- **Overall battery reliability**: conservative combined diagnostic score.

A battery can have a plausible capacity estimate yet still be unsafe/unreliable for untethered work.

## 6. Threshold changes

Diagnostic thresholds are intentionally visible in source code. When changing one:

1. explain the reason in the PR/commit,
2. keep the threshold relative where hardware diversity makes absolute values unsafe,
3. test against known normal and collapse sessions,
4. update comments/documentation, and
5. avoid wording that claims a specific failed cell unless cell-level telemetry actually exists.

## 7. Error-handling philosophy

- Battery telemetry source missing a field: return `null` and continue where possible.
- History database failure: show a non-fatal warning; live diagnostics should continue.
- Report export failure: report the error; do not stop monitoring.
- Journal sample flush failure: surface prominently because collapse evidence may be lost.
- Application-level unexpected exception: write to portable `Logs/` and present the log location.

## 8. Where to start when contributing

For UI behavior, start at `MainWindow.xaml` + `MainViewModel.cs`.

For battery fields, start at `WindowsBatteryProvider.cs` and `BatteryTemperatureProvider.cs`.

For derived electrical/thermal metrics, start at `ElectricalDiagnosticsAnalyzer.cs`.

For diagnostic rules, start at `BatteryHealthAnalyzer.cs`, `BatteryAnomalyDetector.cs`, `VoltageSagAnalyzer.cs`, and `BatteryTestAnalyzer.cs`.

For sudden shutdown/reboot recovery, start at `BatteryTestJournal.cs`.

For exported evidence/privacy, start at the three exporter services.

See [`FUNCTION_REFERENCE.md`](FUNCTION_REFERENCE.md) for a file-by-file function reference.
