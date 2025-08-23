<#!
.SYNOPSIS
Runs API and Frontend simultaneously with file watching.
.MILESTONE
Supports Milestone 1 Issue #9 (single action dev orchestration).
#>

$ErrorActionPreference = 'Stop'

$api = Start-Process powershell -PassThru -ArgumentList '-NoLogo','-NoExit','-Command','dotnet watch run --project ReceptRegister.Api'
$frontend = Start-Process powershell -PassThru -ArgumentList '-NoLogo','-NoExit','-Command','dotnet watch run --project ReceptRegister.Frontend'

Write-Host "Started API (PID=$($api.Id)) and Frontend (PID=$($frontend.Id)). Press Enter to stop both..." -ForegroundColor Green
[Console]::ReadLine() | Out-Null

foreach ($p in @($api,$frontend)) { if (!$p.HasExited) { $p.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 500; if (!$p.HasExited) { $p | Stop-Process -Force } } }

Write-Host "Stopped both applications." -ForegroundColor Yellow
