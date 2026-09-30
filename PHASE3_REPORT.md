# Battery Doctor — Phase 3 / v0.7.0

## Goal

Turn the proven Phase 2.x diagnostic core into a distributable Windows utility that can keep watching a risky battery while the main window is out of the way.

## Added in Phase 3

- System tray icon and tray menu
- Minimize-to-tray while preserving a real Exit/Close action
- Optional background monitoring while hidden
- Optional tray alerts
- Pre-collapse warning heuristics based on:
  - severe relative voltage sag
  - abnormal percentage/gauge drops
  - Windows critical-battery signal
  - previously recovered critical collapse evidence
- Per-alert cooldowns to reduce notification spam
- Optional Start with Windows (`HKCU` Run key, no administrator rights required)
- Settings persistence in `%LOCALAPPDATA%\BatteryDoctor\settings.json`
- Settings & About tab
- App version / GPL-3.0 / battery chemistry display
- Open local data/reports folders from the UI
- Project/support links configured through `AppLinks.json`
- Donation remains optional and unlocks no features
- Chemistry FourCC decoding (for example `LION` -> `Lithium-ion`)
- Recovered-collapse export schema bumped to v2
- Removed the misleading legacy `MeasuredUsableHealthPercent` field; the report keeps the more accurate `ObservedEnergyVsDesignPercent`
- Self-contained portable release builder
- Inno Setup per-user installer script

## Alert design

The background warning logic deliberately avoids a universal absolute voltage cutoff because laptop battery packs have different series-cell counts and firmware behavior.

Instead it looks for relative short-term behavior. A critical voltage-sag alert currently requires a high sag-severity score and a short-term drop of at least about 1.5 V relative to the rolling baseline. Gauge-collapse alerts reuse the existing percentage-drop detector. Critical alerts have cooldowns so one unstable pack does not generate a notification every polling cycle.

These alerts are screening/early-warning heuristics, not cell-level diagnosis.

## Tray behavior

- Minimize button: hides the window to the system tray when `Minimize to system tray` is enabled.
- Tray double-click / Open: restores the window.
- Close (X) / Alt+F4: exits Battery Doctor normally.
- Tray Exit: exits Battery Doctor.
- Background monitoring continues only while the process is running and the setting is enabled.

## Start with Windows

The setting writes only to the current user's:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

The startup command uses `--background`, so the installed app can start directly in the tray when background monitoring is enabled.

## Support / project links

`AppLinks.json` intentionally ships with empty URLs so the project does not invent or redirect donations to an account that does not belong to the maintainer.

Before public release, edit:

```json
{
  "ProjectUrl": "https://github.com/YOUR_ACCOUNT/BatteryDoctor",
  "SupportUrl": "https://ko-fi.com/YOUR_ACCOUNT"
}
```

Buy Me a Coffee or another HTTPS support URL can be used instead.

## Release build

```powershell
.\PUBLISH-RELEASE.ps1
```

To build the installer as well:

```powershell
winget install JRSoftware.InnoSetup
.\PUBLISH-RELEASE.ps1 -BuildInstaller
```

Outputs go to `artifacts\release`.

## Validation performed in this environment

- XAML is well-formed XML
- Thai/English JSON parses successfully
- localization keys added for Phase 3 are present in both languages
- duplicate class scan performed
- event-handler references checked statically
- release and installer files packaged

A real `dotnet build` must still be run on Windows because the current build environment does not contain the Windows .NET/WPF SDK.
