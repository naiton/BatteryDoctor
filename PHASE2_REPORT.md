# Battery Doctor — Phase 2 implementation report

Version: 0.2.0
Target: Windows 10/11 x64, .NET 10 WPF

## Implemented

- Live battery sampling every 5 seconds while the app is open.
- Signed power display: down arrow for discharge, up arrow for charge.
- Rolling 1-minute and 5-minute discharge averages.
- Runtime estimates from remaining energy and rolling average power.
- Live 5-minute power trend chart without a third-party chart package.
- Controlled discharge test with start/stop, duration, charge drop, average power, voltage range, sample count and test power chart.
- Conservative anomaly observations for sudden percentage drop, large short-term voltage movement, and power spikes. These are observations, not cell-level diagnoses.
- Long-term SQLite history throttled to about one sample every 5 minutes while the app is running.
- 90-day history queries and trend charts for battery-health percentage and full-charge capacity.
- Existing Phase 1 SQLite database is migrated in place by adding rate/power columns when needed.
- `powercfg /batteryreport` fallback is cached for 30 minutes so Live Monitor does not repeatedly launch powercfg on hardware that lacks WMI capacity/cycle fields.
- Thai and English text added for all Phase 2 screens.

## Safety / interpretation

Battery Doctor does not claim to identify an individual failed cell from Windows telemetry alone. A sudden percentage drop or voltage movement is shown as a symptom to repeat in a controlled test. If a battery is swollen, unusually hot, leaking, or physically damaged, stop using it and follow the laptop/battery manufacturer's safety guidance.

## History behavior

The database remains at:

`%LOCALAPPDATA%\BatteryDoctor\battery-doctor.db`

Live Monitor samples are kept in memory every 5 seconds for smoothing and charts. Long-term history is written only about every 5 minutes, avoiding a database row every live tick.

## Build

```powershell
dotnet restore
dotnet clean
dotnet build
dotnet run
```

## Validation status

The source/XAML/JSON structure was checked in the generation environment, but WPF cannot be compiled or executed there because the environment is Linux and has no .NET SDK/WPF runtime. The next release gate is a Windows `dotnet build` followed by a 15–30 minute discharge test on the real laptop.
