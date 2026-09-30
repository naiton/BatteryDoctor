# Apply Battery Doctor Phase 2.8

## Recommended: clean full-project folder

Extract the full project to a new folder, for example:

```powershell
D:\Projects\myProject\BatteryDoctor_Phase2_8
```

Then run:

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase2_8

dotnet restore
dotnet clean
dotnet build
dotnet run
```

The existing Battery Doctor database and collapse journals remain under:

```text
%LOCALAPPDATA%\BatteryDoctor
```

so the recovered collapse from Phase 2.6/2.7 should still be available in Phase 2.8.

## Patch an existing Phase 2.7 folder

Extract `BatteryDoctor_Phase2_8_patch.zip` directly over the Phase 2.7 project root. Do **not** create a nested patch folder inside the project.

The patch contains only changed/new source files and documentation.

Then run:

```powershell
dotnet clean
dotnet build
dotnet run
```

## Verify Phase 2.8

After launch, a previously recovered collapse should show:

1. Diagnostic Summary
2. Capacity / Gauge / Voltage / Shutdown / Overall reliability
3. A recommendation card
4. An **Export collapse report** button
5. A capacity note that clearly identifies the health figure as the current firmware/BMS estimate

Click **Export collapse report**. The app should create HTML + JSON under:

```text
Documents\BatteryDoctor\Reports
```

The HTML report should include the saved timeline from the recovered Collapse Watch session.
