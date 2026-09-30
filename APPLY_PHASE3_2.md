# Apply / test Phase 3.2 (v0.9.0)

## Recommended: use the full project in a new folder

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase3_2
dotnet restore
dotnet clean
dotnet build
dotnet run
```

## What to verify first

1. Open **Dashboard** and **Live Monitor**.
2. Look at **Battery temperature**.
   - If a value such as `34.5 °C` appears, note the sensor source shown underneath.
   - If `เครื่องไม่รายงาน / Not reported` appears, the laptop battery driver does not expose this optional sensor. Do not treat this as an application failure.
3. Unplug AC power and create a small load change (normal browsing/video is enough; do not intentionally stress an unsafe battery).
4. Check **Estimated pack current** and **Dynamic resistance est.**. Dynamic resistance stays `—` until enough clean load-current steps exist.
5. The temperature/current charts should collect data only when their source values are available.

## Battery Test

Quick/Standard/Deep/Collapse Watch now persist optional `TemperatureC` in each JSONL sample. Test summaries and exported reports include thermal/electrical metrics when available.

There is no need to repeat a destructive collapse test solely to validate temperature support. For the known unreliable battery, ordinary discharge observation is sufficient.

## If build fails

Send the full `dotnet build` output. Phase 3.2 adds native P/Invoke code, so any compile error around `BatteryTemperatureProvider.cs` is especially useful to capture exactly.
