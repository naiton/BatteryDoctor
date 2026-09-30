# Battery Doctor

Free, offline and privacy-friendly battery diagnostics for Windows laptops.

Battery Doctor goes beyond a single Battery Health percentage. It combines firmware-reported capacity with live discharge behavior, voltage sag, gauge reliability, historical trends and crash-safe recovery after an unexpected battery cutoff.

## Highlights

- Battery capacity health and wear
- Live charge/discharge power and runtime estimates
- Quick / Standard / Deep / Collapse Watch tests
- Crash-safe session journal for sudden-cutoff investigation
- Battery temperature when exposed by the battery driver
- Estimated pack current and dynamic-resistance trend
- Thermal/load correlation and temperature trend
- Voltage Sag Severity
- Gauge Reliability
- Battery Reliability
- Firmware/BMS capacity re-estimation detection
- HTML + JSON reports
- System tray and background monitoring
- Early warning tray notifications
- Optional Start with Windows
- Thai and English UI
- Free, offline, no account and no telemetry

## Portable-first storage

The public build is portable. Runtime data stays inside the Battery Doctor folder:

```text
BatteryDoctor/
├─ BatteryDoctor.exe
├─ Data/       # settings + SQLite history
├─ Sessions/   # crash-safe battery test journals
├─ Reports/    # exported HTML/JSON reports
└─ Logs/       # crash logs
```

Do not place the portable build in a read-only location such as `C:\Program Files`. Good locations include `D:\Apps\BatteryDoctor`, `C:\Tools\BatteryDoctor`, Desktop, or a USB drive.

On first portable run, Battery Doctor performs a best-effort **copy** of legacy data previously stored in `%LOCALAPPDATA%\BatteryDoctor` and `Documents\BatteryDoctor\Reports`. Legacy files are not deleted.

## Privacy

Battery Doctor runs locally. It does not require a server, account, cloud sync or telemetry.

Shared diagnostic/collapse exports are designed not to expose the Windows username, absolute local journal path, or battery serial number. Always review a report before posting it publicly.

## Download

Stable/preview portable builds are published under [GitHub Releases](https://github.com/naiton/BatteryDoctor/releases).

## Build from source

Requirements: Windows 10/11 x64 and the .NET 10 SDK.

```powershell
git clone https://github.com/naiton/BatteryDoctor.git
cd BatteryDoctor
dotnet restore
dotnet build
dotnet run
```

## Developer documentation

For contributors, start with:

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — component responsibilities and end-to-end data flow.
- [`docs/FUNCTION_REFERENCE.md`](docs/FUNCTION_REFERENCE.md) — function-by-function responsibility reference generated from source XML summaries.
- [`docs/COMMENTING_GUIDE.md`](docs/COMMENTING_GUIDE.md) — commenting/documentation conventions for future changes.

The maintained C# source also includes `/// <summary>` documentation on methods/constructors and inline comments around non-obvious diagnostic, crash-recovery, privacy, and migration logic.

## Create a portable release locally

```powershell
.\PUBLISH-RELEASE.ps1
```

The GitHub workflow also builds a portable ZIP automatically when a tag such as `v0.9.0` is pushed.

## Support development

Battery Doctor is free and does not lock features behind donations. The in-app support button remains disabled until the maintainer configures `SupportUrl` in `AppLinks.json`.

For GitHub's Sponsor button, copy `.github/FUNDING.example.yml` to `.github/FUNDING.yml` and add the chosen Ko-fi, Buy Me a Coffee, GitHub Sponsors, or custom donation URL.

## License

GPL-3.0-only. See [LICENSE](LICENSE).

## Scope

Battery Doctor observes pack-level information exposed by Windows and firmware. It can flag patterns consistent with severe wear, unreliable gauge behavior, voltage sag or sudden cutoff, but it does not claim to identify an individual failed cell unless the hardware exposes cell-level telemetry.

### Temperature and electrical estimates

Battery Doctor requests battery temperature from the Windows battery-class interface (`BatteryTemperature`) and only falls back to the battery-specific WMI temperature block. It **does not** substitute CPU, GPU, motherboard, or generic ACPI thermal-zone temperatures.

Not every laptop exposes battery temperature. In that case the UI shows that the sensor is unavailable.

Pack current is estimated from `Power / Voltage`, and dynamic resistance is estimated from short load steps using `|ΔV / ΔI|`. These are trend/diagnostic estimates, not laboratory cell-impedance measurements.
