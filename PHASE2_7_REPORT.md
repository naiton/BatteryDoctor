# Battery Doctor Phase 2.7 — Reliability Diagnostics

Version: **0.5.0**

Phase 2.7 is focused on batteries that still report a high percentage but then cut power unexpectedly. It builds on the Phase 2.6 crash-safe Collapse Watch journal.

## Added in Phase 2.7

- **Voltage Sag Severity Score (0–100)** — higher means larger and/or repeated short-term voltage drops relative to a rolling local baseline.
- **Gauge Reliability Score (0–100)** — lower scores reflect large before/after-restart percentage jumps, unexpected cutoff while firmware still reports stored energy, large controller re-estimation, and poor agreement with observed delivered energy.
- **Battery Reliability Score (0–100)** — combines reported capacity condition, gauge behavior, voltage-sag evidence, and recovered sudden-collapse evidence. A confirmed/strong collapse caps the reliability rating at a critical level.
- **Firmware/BMS re-estimation event detection** — compares Full Charge Capacity before the collapse with the value reported after restart. A change of 20% or more is surfaced explicitly.
- **Observed energy vs design capacity** — renamed in recovery UI so the app does not overstate a partial collapse session as a laboratory-grade “measured health” result.
- **Observed delivered energy vs firmware-reported available energy** — shows how much energy was actually delivered relative to what the controller said was available at test start.
- **Last remaining Wh and last voltage before cutoff** — useful for identifying cases where Windows still believed substantial energy remained immediately before power loss.
- **Prior Phase 2.6 recovery migration** — if a collapse was already recovered, Phase 2.7 can reopen the latest recovery JSON and re-analyze its JSONL samples instead of requiring another destructive test.
- Standard/Quick/Deep test reports now include voltage-sag severity, event count, and largest sag.
- Fixed the Wear formatting bug where values such as `66.7%` could appear as `671%`.

## Heuristic interpretation

These scores are screening/diagnostic aids, not cell-level measurements. Windows normally exposes pack-level information, not the voltage of each individual cell group. Battery Doctor therefore reports evidence such as voltage sag, controller/gauge inconsistency, and cutoff behavior without claiming which cell is defective.

### Voltage sag score

The analyzer compares each voltage sample with the median of several preceding samples. A local drop of approximately 0.8 V or more is treated as a sag candidate. Nearby candidates are grouped so one physical event is not counted many times. The score combines the largest sag, repeated events, and a sag at the final pre-cutoff sample.

### Gauge reliability

The score is reduced by evidence such as:

- large percentage discontinuity after restart,
- power loss while the last reported charge was still high,
- substantial firmware-reported Wh remaining before cutoff,
- major Full Charge Capacity re-estimation after restart,
- much less observed energy delivered than the controller implied was available.

### Battery reliability

This is a conservative combined score. A recovered high-confidence sudden-collapse event takes precedence over a superficially reasonable capacity percentage.

## Recorded collapse case used to tune this phase

The supplied Collapse Watch journal contained 669 samples. The session began near full charge and the final saved sample still reported 63% before the machine lost power. On restart Windows reported 5%. The journal integrated about 5.45 Wh before interruption while the controller had still reported substantial remaining capacity. The Phase 2.7 sag heuristic also finds repeated short voltage drops, including a largest local sag of roughly 2.33 V in the supplied session.

This is exactly the type of case Phase 2.7 is intended to flag as a **reliability/cutoff problem**, not merely “capacity wear.”

## Build gate

The project was statically checked in the generation environment for:

- well-formed WPF XAML,
- valid language JSON,
- duplicate C# type declarations,
- localization key coverage for static bindings,
- source-tree structure.

The generation environment does not contain the Windows .NET/WPF SDK, so `dotnet build` on Windows remains the authoritative compile gate.
