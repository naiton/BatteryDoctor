# Battery Doctor Phase 2.8 — Diagnostic Summary & Recovery Report

Version: **0.6.0**

Phase 2.8 focuses on presentation and actionable interpretation after a recovered sudden-collapse event. It does not replace Phase 2.7 scoring; it makes that evidence easier to understand and export.

## Added in Phase 2.8

- **Recovered-collapse report export** from both Dashboard and Battery Test.
  - Exports HTML + JSON to `Documents\BatteryDoctor\Reports`.
  - Includes the crash-recovered session, current battery/firmware state, diagnostic scores, recommendation text, and the saved JSONL sample timeline.
  - Export remains available after restart; the user does not have to perform another discharge test.
- **Diagnostic Summary** with five separate dimensions:
  - Capacity condition
  - Gauge reliability
  - Voltage stability
  - Shutdown reliability
  - Overall battery reliability
- **Recommendation engine** that prioritizes recovered sudden-cutoff evidence over a simple capacity percentage.
- **Percentage trust warning** when a recovered collapse shows that the charge percentage could not predict the actual cutoff.
- **Current firmware/BMS estimate labeling** so a post-collapse Full Charge Capacity re-estimation is not presented as a direct cell measurement.
- Updated Thai and English localization for the new diagnostics and reports.

## Recovery report interpretation

The exported collapse report deliberately distinguishes:

- what Windows/firmware reported,
- what Battery Doctor observed during the discharge session,
- heuristic reliability scores,
- and recommendations based on observed behavior.

The report does **not** claim which cell group failed. Windows typically exposes pack-level battery data, so Battery Doctor can flag voltage sag, gauge discontinuities, controller re-estimation, and sudden cutoff behavior without making a cell-level diagnosis.

## Diagnostic summary logic

### Capacity condition

Uses the current capacity-based health estimate. If a large firmware/BMS re-estimation was detected after restart, the UI explicitly says that the current value is a controller estimate that changed after the collapse.

### Gauge reliability

Reuses the Phase 2.7 gauge score. Strong before/after-restart percentage jumps and cutoff while reported energy remained can drive the score to critical.

### Voltage stability

Displayed as `100 - Voltage Sag Severity`. A high sag-severity score therefore produces a low voltage-stability score.

### Shutdown reliability

A recovered strong sudden collapse is treated conservatively as 0/100. A possible/interrupted collapse is shown as 20/100 until stronger evidence is available.

### Overall battery reliability

Uses the Phase 2.7 combined reliability score, which already incorporates capacity, gauge behavior, voltage sag, and collapse evidence.

## Recommendation behavior

A strong recovered collapse or overall reliability <= 20 takes priority and produces a critical recommendation to avoid relying on the displayed percentage and to plan battery replacement/service. Lower-severity evidence produces a caution/testing recommendation. Capacity wear alone can still produce a replacement consideration when practical runtime is too short.

## Safety wording

The recommendation also tells users to stop using the battery and seek service if the pack is swollen, unusually hot, smells abnormal, or deforms the laptop case.

## Build gate

The generation environment does not include the Windows .NET/WPF SDK. The source was statically checked for:

- well-formed WPF XAML,
- valid Thai/English JSON,
- static localization-key coverage,
- duplicate source/type structure,
- balanced C# delimiters and source layout.

Run `dotnet build` on Windows as the authoritative compile gate.
