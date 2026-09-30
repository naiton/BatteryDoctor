# Changelog

## 0.8.0 - Portable RC

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
