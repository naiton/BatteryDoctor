# Battery Doctor — Phase 1 implementation report

## Implemented

- WPF desktop shell targeting .NET 10 on Windows.
- Battery data provider using Windows WMI battery classes.
- Fallback reader using `powercfg /batteryreport /xml` when firmware omits key capacity/cycle values from WMI.
- Capacity-based health and wear calculation.
- Human-readable condition assessment without inventing unsupported sensor values.
- Current charge, voltage, power state, charge/discharge rate, runtime when exposed by the device.
- SQLite history stored under `%LOCALAPPDATA%\BatteryDoctor\battery-doctor.db`.
- Thai and English JSON localization packs with runtime language switching.
- Offline/privacy-first architecture; no network client, login, telemetry, or backend.
- Build and publish PowerShell scripts.
- Standalone PowerShell battery-data probe for diagnosing OEM/WMI compatibility.

## Health score policy

Phase 1 intentionally uses only the defensible capacity ratio for the headline score:

`FullChargeCapacity / DesignCapacity × 100`

Cycle count and voltage are supporting diagnostics. They are not blended into an arbitrary score because cycle lifetime differs by battery chemistry, OEM policy, thermal history, and charging behavior.

## Validation completed in this environment

- XAML/XML files parse successfully as XML.
- Both language JSON files parse successfully and contain matching keys.
- Project source layout and local persistence paths were checked.

## Validation still required on Windows

This development environment is Linux and does not contain the Windows WPF/.NET toolchain, so the project has **not yet been compiled or hardware-tested here**. The next gate should be run on a Windows 10/11 notebook:

1. Run `tools\Test-BatteryData.ps1` and save its output.
2. Run `BUILD.ps1` with the .NET 10 SDK installed.
3. Launch the app and compare displayed capacities/cycle count against `powercfg /batteryreport`.
4. Test both plugged-in and battery-powered states.
5. Test Thai/English switching and SQLite history after several refreshes.

## Next phase

- Continuous diagnostic session with configurable sample interval.
- Detect sudden percentage drops and unstable discharge behavior.
- Trend chart and 30/90-day degradation rate.
- Exportable user report.
- Donation/support page and community translation workflow.
