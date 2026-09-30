# Apply Battery Doctor Phase 2

## Recommended: test as a clean sibling project

Extract `BatteryDoctor_Phase2.zip` so the folder is:

`D:\Projects\myProject\BatteryDoctor_Phase2`

Then:

```powershell
cd D:\Projects\myProject\BatteryDoctor_Phase2
dotnet restore
dotnet clean
dotnet build
dotnet run
```

The Phase 1 history is preserved because the database is outside the source folder at `%LOCALAPPDATA%\BatteryDoctor\battery-doctor.db`.

## Patch the existing Phase 1 folder

From anywhere in PowerShell:

```powershell
Expand-Archive -Path .\BatteryDoctor_Phase2_patch.zip `
  -DestinationPath D:\Projects\myProject\BatteryDoctor_Phase1 `
  -Force

cd D:\Projects\myProject\BatteryDoctor_Phase1
dotnet clean
dotnet build
dotnet run
```

Do not extract the patch into a nested folder inside `BatteryDoctor_Phase1`, because the SDK automatically compiles nested `*.cs` files and duplicate class definitions can result.
