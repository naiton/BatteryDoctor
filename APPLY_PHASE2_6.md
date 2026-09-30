# Apply Battery Doctor Phase 2.6

## Recommended: clean folder

Extract the full project into a new folder, for example:

```text
D:\Projects\myProject\BatteryDoctor_Phase2_6
```

Then run:

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase2_6

dotnet restore
dotnet clean
dotnet build
dotnet run
```

The existing Battery Doctor SQLite history remains available because it is stored under `%LOCALAPPDATA%\BatteryDoctor`, not inside the source folder.

## Patch an existing Phase 2.5.2 folder

Extract the patch directly over the project root. Do not create a nested source folder inside the project.

After copying:

```powershell
dotnet clean
dotnet build
dotnet run
```

## Recommended collapse test

1. Save and close important work.
2. Charge the laptop near full.
3. Unplug the adapter and wait until Battery Doctor shows Discharging.
4. Open Battery Test.
5. Select `Collapse Watch`.
6. Press Start test.
7. Use the laptop normally.
8. If the laptop suddenly powers off, reconnect power if needed and boot Windows normally.
9. Open Battery Doctor again.

If the previous test ended close to a system restart, Battery Doctor will recover the flushed journal and show an interruption/collapse assessment.

Do not repeatedly force a failing battery to hard power-off if the pack is swollen, unusually hot, physically damaged, or otherwise unsafe.
