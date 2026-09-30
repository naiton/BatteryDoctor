param(
    [string]$Runtime = "win-x64",
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$version = "0.9.0"
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish\$Runtime"
$release = Join-Path $artifacts "release"

Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publish, $release | Out-Null

Write-Host "Publishing Battery Doctor $version ($Runtime, self-contained)..." -ForegroundColor Cyan
dotnet publish (Join-Path $root "BatteryDoctor.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publish

$portableZip = Join-Path $release "BatteryDoctor-v$version-$Runtime-portable.zip"
Remove-Item $portableZip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $portableZip -CompressionLevel Optimal
Write-Host "Portable ZIP: $portableZip" -ForegroundColor Green

if ($BuildInstaller) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $iscc = $candidates | Select-Object -First 1
    if (-not $iscc) {
        Write-Warning "Inno Setup 6 was not found. Install it, then rerun with -BuildInstaller."
        Write-Host "winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
    & $iscc (Join-Path $root "installer\BatteryDoctor.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }
    Write-Host "Installer build complete. See artifacts\release." -ForegroundColor Green
}
