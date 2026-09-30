# Apply Phase 2.5 Combined Patch

Recommended target:

`D:\Projects\myProject\BatteryDoctor_Phase2`

1. Close Battery Doctor.
2. Back up the project folder if desired.
3. Extract `BatteryDoctor_Phase2_5_combined_patch.zip` directly into the project root, with overwrite enabled.
4. Do **not** extract the patch into a new subfolder inside the project.
5. Run:

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase2

dotnet clean
dotnet build
dotnet run
```

The existing SQLite history remains in `%LOCALAPPDATA%\BatteryDoctor\battery-doctor.db` and is not replaced by this patch.

After a Battery Test is stopped, the **Export report** button writes HTML + JSON files under:

`Documents\BatteryDoctor\Reports`
