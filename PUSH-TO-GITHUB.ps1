$ErrorActionPreference = "Stop"

$Repo = "https://github.com/naiton/BatteryDoctor.git"
$Source = $PSScriptRoot
$Target = Join-Path (Split-Path $Source -Parent) "BatteryDoctor-github"

if (Test-Path $Target) {
    throw "Target already exists: $Target`nRemove or rename it first so this script never overwrites an existing checkout."
}

Write-Host "Cloning $Repo ..." -ForegroundColor Cyan
git clone $Repo $Target
if ($LASTEXITCODE -ne 0) { throw "git clone failed" }

Write-Host "Copying prepared portable source ..." -ForegroundColor Cyan
$excludeDirs = @(".git", "bin", "obj", "Data", "Sessions", "Reports", "Logs", "artifacts")
$xd = $excludeDirs | ForEach-Object { Join-Path $Source $_ }
& robocopy $Source $Target /E /NFL /NDL /NJH /NJS /NP /XD $xd | Out-Null
if ($LASTEXITCODE -gt 7) { throw "robocopy failed with exit code $LASTEXITCODE" }

Push-Location $Target
try {
    git add .
    Write-Host "`nFiles ready to publish:" -ForegroundColor Yellow
    git status --short

    git commit -m "Publish Battery Doctor 0.9.0 portable RC source with developer documentation"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Nothing new to commit, or git needs your name/email configured." -ForegroundColor Yellow
    }

    git push origin main
    if ($LASTEXITCODE -ne 0) { throw "git push failed" }

    Write-Host "`nPublished: https://github.com/naiton/BatteryDoctor" -ForegroundColor Green
}
finally {
    Pop-Location
}
