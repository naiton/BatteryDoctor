$ErrorActionPreference = "Continue"

Write-Host "=== Battery Doctor data probe ===" -ForegroundColor Cyan
Write-Host "Win32_Battery" -ForegroundColor Yellow
Get-CimInstance -ClassName Win32_Battery | Format-List Name,DeviceID,DesignCapacity,DesignVoltage,EstimatedChargeRemaining,EstimatedRunTime,BatteryStatus

Write-Host "BatteryStaticData" -ForegroundColor Yellow
Get-CimInstance -Namespace ROOT/WMI -ClassName BatteryStaticData | Format-List InstanceName,DesignedCapacity,DeviceName,ManufactureName,SerialNumber,Chemistry

Write-Host "BatteryFullChargedCapacity" -ForegroundColor Yellow
Get-CimInstance -Namespace ROOT/WMI -ClassName BatteryFullChargedCapacity | Format-List InstanceName,FullChargedCapacity

Write-Host "BatteryCycleCount" -ForegroundColor Yellow
Get-CimInstance -Namespace ROOT/WMI -ClassName BatteryCycleCount | Format-List InstanceName,CycleCount

Write-Host "BatteryStatus" -ForegroundColor Yellow
Get-CimInstance -Namespace ROOT/WMI -ClassName BatteryStatus | Format-List InstanceName,RemainingCapacity,ChargeRate,DischargeRate,Voltage,PowerOnline,Charging,Discharging,Critical

$report = Join-Path $env:TEMP "battery-doctor-probe.xml"
powercfg /batteryreport /output $report /xml | Out-Null
Write-Host "Battery report XML: $report" -ForegroundColor Green
