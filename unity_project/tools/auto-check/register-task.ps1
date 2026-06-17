#requires -Version 5.1
<#
  Register the auto-check schedule - daily at given time, run as current user (must be logged on).
  Unregister: .\register-task.ps1 -Unregister
  Pause:      create tools/auto-check/DISABLED, or Disable-ScheduledTask -TaskName <name>

  NOTE: ASCII-only on purpose (Windows PowerShell 5.1 parses BOM-less .ps1 as ANSI).
#>
[CmdletBinding()]
param(
  [string]$Time = '04:00',
  [string]$TaskName = 'LoveAlgo-AutoCheck',
  [switch]$Unregister
)

if ($Unregister) {
  Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
  Write-Host "Unregistered: '$TaskName'"
  return
}

$runner = Join-Path $PSScriptRoot 'run-check.ps1'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
  -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$runner`""
$trigger = New-ScheduledTaskTrigger -Daily -At $Time
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable `
  -ExecutionTimeLimit (New-TimeSpan -Hours 1)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
  -Settings $settings -Principal $principal -Force | Out-Null

Write-Host "Registered: '$TaskName' - daily at $Time (current user, runs when logged on; missed runs start when available)."
Write-Host "Pause: Disable-ScheduledTask -TaskName $TaskName    Unregister: .\register-task.ps1 -Unregister"
