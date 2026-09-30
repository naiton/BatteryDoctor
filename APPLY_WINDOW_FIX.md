# Battery Doctor Phase 2 - Window Controls Fix

This patch adds visible app-level window controls so Battery Doctor can always be minimized, maximized/restored, moved, resized, and closed even when the normal Windows title bar is not visible.

## Files in this patch

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Languages/en-US.json`
- `Languages/th-TH.json`

## Apply

Copy these files over the same paths in your existing `BatteryDoctor_Phase2` project. Do **not** extract this patch into a nested subfolder under the project.

Then run:

```powershell
dotnet clean
dotnet build
dotnet run
```

## Added behavior

- `—` minimizes
- `□` maximizes; changes to `❐` while maximized and restores the window when clicked again
- `✕` closes Battery Doctor
- Double-click the Battery Doctor title area to maximize/restore
- Drag the title area to move the window
- Window edges remain resizable through WPF `WindowChrome`
- Thai/English tooltips are included
