# Changelog

## 0.9.0 - Phase 3.2 Sensor Diagnostics

- Added real battery-pack temperature query through the Windows battery-class IOCTL with a battery-specific WMI fallback.
- Never substitutes CPU/GPU/generic ACPI thermal-zone temperature for battery temperature.
- Added estimated pack current from pack power/voltage.
- Added median dynamic-resistance estimate from short discharge load steps (`|ΔV / ΔI|`).
- Added 5-minute temperature change/rate and descriptive power↔temperature correlation.
- Added live temperature/current charts and thermal/electrical metrics to Battery Test.
- Test JSON/HTML reports now include temperature/current/dynamic-resistance metrics when available.
- SQLite history schema now stores optional temperature and estimated current fields.
- Preserves backward compatibility with older collapse JSONL sessions whose temperature field is absent.
- Added contributor documentation for sensor provenance and conservative interpretation.

## 0.8.0 - Portable RC

- Added function-level XML documentation across the maintained C# source plus architecture, function-reference, and commenting guides for contributors.

- Portable-first storage: settings, SQLite history, collapse sessions, reports and logs stay beside the application.
- One-time best-effort copy of legacy Battery Doctor data from AppData/Documents; originals are left untouched.
- Session journals store move-safe relative sample filenames.
- Shared HTML/JSON reports no longer expose an absolute Windows user path or battery serial number.
- GitHub-ready build and tag-based portable release workflows.
- Project link points to `naiton/BatteryDoctor`; donation link remains intentionally blank until configured by the maintainer.
- Includes the Phase 3 WPF/WinForms namespace-collision fixes.

## 0.7.0 - Phase 3

- System tray and background battery monitoring.
- Early warning notifications for unstable battery behavior.
- Start-with-Windows option.
- Settings & About page and privacy-safe diagnostic snapshot export.
