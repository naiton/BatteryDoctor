# Apply / test Phase 3

The safest route is to use the Phase 3 full project in a new folder.

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase3

dotnet restore
dotnet clean
dotnet build
dotnet run
```

Your existing history and collapse evidence remain under `%LOCALAPPDATA%\BatteryDoctor`, so creating a new source folder does not erase them.

## First checks

1. Confirm the previous recovered collapse still appears.
2. Confirm Wear is formatted correctly.
3. Open **Settings & About**.
4. Enable background monitoring and tray notifications.
5. Click Minimize; Battery Doctor should disappear from the taskbar and remain in the system tray.
6. Double-click the tray icon to restore it.
7. Confirm X still exits the program.
8. Toggle `Start with Windows`, then verify Task Manager > Startup apps or the current-user Run key if desired.
9. Check battery chemistry; the prior numeric `1313818956` case should now display `Lithium-ion` when the firmware reports the `LION` FourCC.

## Background-alert regression test without another hard shutdown

The previous collapse journal remains valid regression evidence. You do **not** need to intentionally hard-cut the battery again just to validate Phase 3.

For a real early-warning test later, minimize Battery Doctor to the tray and use the machine normally on battery. If a new severe short-term voltage sag or abnormal gauge drop occurs, a tray warning should appear. Save important work before any battery-on-load testing.

## Configure project / coffee links before public distribution

Edit `AppLinks.json` and replace the blank URLs with links you own. Until configured, the buttons remain disabled.
