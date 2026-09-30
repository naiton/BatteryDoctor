# Apply / Test Phase 2.7

## Recommended: clean folder

Extract the full archive into a **new** folder, for example:

```powershell
D:\Projects\myProject\BatteryDoctor_Phase2_7
```

Then:

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase2_7

dotnet restore
dotnet clean
dotnet build
dotnet run
```

Do not extract another full Battery Doctor source tree *inside* the project directory, because SDK-style .NET projects automatically compile nested `.cs` files and duplicate classes will result.

## Existing Phase 2.6 collapse

Phase 2.7 can reuse the last recovered collapse stored under:

```text
%LOCALAPPDATA%\BatteryDoctor\sessions
```

On first start it will try to reopen the latest `recovered-*.json` (up to 7 days old), read its referenced `test-*.jsonl`, and calculate the new sag/gauge/reliability metrics. You should not need to repeat the sudden-shutdown test just to get the new Phase 2.7 analysis.

## What to inspect after launch

The red recovered-collapse panel should now show, when data are available:

- last charge before interruption,
- charge after restart,
- energy delivered before interruption,
- observed energy vs design capacity,
- firmware-reported Wh remaining before cutoff,
- last voltage before cutoff,
- voltage sag severity,
- largest short-term sag,
- sag event count,
- gauge reliability,
- overall battery reliability,
- Full Charge Capacity before → after restart and percentage change.

The Dashboard Wear value should also display a normal decimal percentage such as `88.3%`, not `881%`.
