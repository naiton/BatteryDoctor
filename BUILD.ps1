$ErrorActionPreference = "Stop"

Write-Host "Battery Doctor - Restore" -ForegroundColor Cyan
dotnet restore

Write-Host "Battery Doctor - Build Release" -ForegroundColor Cyan
dotnet build -c Release

Write-Host "Done. Run with: dotnet run -c Release" -ForegroundColor Green
