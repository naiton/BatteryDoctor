# Push the complete v0.9.0 source tree to GitHub

The repository `https://github.com/naiton/BatteryDoctor` has already been initialized with the public README, privacy rules, project link, version metadata, and Windows CI scaffold.

Because the ChatGPT GitHub connector cannot bulk-transfer this entire local WPF source tree in one operation, push this prepared source package once from Windows.

## Recommended

Open PowerShell in this extracted source folder and run:

```powershell
.\PUSH-TO-GITHUB.ps1
```

The script clones the existing repository into a temporary sibling folder, copies this source tree over it while excluding runtime/private data, commits the result, and pushes `main`.

Review `git status` shown by the script before confirming the push if you customize it.

## Manual equivalent

```powershell
git clone https://github.com/naiton/BatteryDoctor.git ..\BatteryDoctor-github
robocopy . ..\BatteryDoctor-github /E /XD .git bin obj Data Sessions Reports Logs artifacts
cd ..\BatteryDoctor-github
git add .
git status
git commit -m "Publish Battery Doctor 0.9.0 portable RC source"
git push origin main
```

Do not commit `Data/`, `Sessions/`, `Reports/`, or `Logs/` because they may contain machine-specific battery data.
