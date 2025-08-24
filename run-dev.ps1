<#!
.SYNOPSIS
Developer convenience launcher (watch mode) for ReceptRegister.

.DESCRIPTION
As of Milestone 5 the Frontend hosts the API endpoints (single process). This script now defaults to launching only the
frontend project in `dotnet watch` mode. A legacy two‑process mode is still available via -Legacy if you want the API and
Frontend as separate processes (e.g., to debug cross-origin behavior or future split hosting).

.EXAMPLE
  ./run-dev.ps1                      # Single unified process (watch)
  ./run-dev.ps1 -Https               # Single process using HTTPS launch profile (if configured)
  ./run-dev.ps1 -Legacy              # Legacy: run API + Frontend separately (both watch)
  ./run-dev.ps1 -Legacy -ApiPort 5232 -FrontendPort 5034  # Legacy with port guidance messages

.PARAMETER Legacy
Run both API and Frontend separately (pre-unification behavior).

.PARAMETER Https
Attempt to run the frontend HTTPS profile (requires dev cert). Falls back to http profile if unavailable.

.PARAMETER FrontendPort / ApiPort
Purely informational; echoed in startup summary (actual ports are from launchSettings unless you modify those files or set ASPNETCORE_URLS).

.NOTES
Ctrl+C or Enter stops processes. If a process doesn’t close gracefully it is force-stopped.

Updated for unified hosting (Milestone 5+).
#>

param(
	[switch]$Legacy,
	[switch]$Https,
	[int]$FrontendPort,
	[int]$ApiPort
)

$ErrorActionPreference = 'Stop'

function Write-Section($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

function Ensure-DevCert {
	if (-not $Https) { return }
	Write-Host "(HTTPS) Ensuring dev certificate (dotnet dev-certs https) is trusted..." -ForegroundColor DarkCyan
	dotnet dev-certs https --trust | Out-Null
}

function Start-Unified {
	Ensure-DevCert
	$profile = if ($Https) { 'https' } else { 'http' }
	Write-Section "Unified mode"
	Write-Host "Launching Frontend (hosts API) with profile '$profile'..." -ForegroundColor Green
	Write-Host "(Stop with Ctrl+C or press Enter in this window)" -ForegroundColor Yellow
	$env:ASPNETCORE_ENVIRONMENT = 'Development'
	$psi = "dotnet watch run --project ReceptRegister.Frontend --launch-profile $profile"
	Write-Host "> $psi" -ForegroundColor DarkGray
	$proc = Start-Process powershell -PassThru -ArgumentList '-NoLogo','-NoExit','-Command',$psi
	return ,$proc
}

function Start-Legacy {
	Ensure-DevCert
	Write-Section "Legacy split mode"
	Write-Host "Launching API and Frontend as separate processes..." -ForegroundColor Green
	if ($FrontendPort) { Write-Host "(FrontendPort hint: $FrontendPort)" -ForegroundColor DarkGray }
	if ($ApiPort) { Write-Host "(ApiPort hint: $ApiPort)" -ForegroundColor DarkGray }
	$apiProfile = if ($Https) { 'https' } else { 'http' }
	$frontProfile = if ($Https) { 'https' } else { 'http' }
	$apiCmd = "dotnet watch run --project ReceptRegister.Api --launch-profile $apiProfile"
	$frontCmd = "dotnet watch run --project ReceptRegister.Frontend --launch-profile $frontProfile"
	Write-Host "> $apiCmd" -ForegroundColor DarkGray
	Write-Host "> $frontCmd" -ForegroundColor DarkGray
	$api = Start-Process powershell -PassThru -ArgumentList '-NoLogo','-NoExit','-Command',$apiCmd
	$frontend = Start-Process powershell -PassThru -ArgumentList '-NoLogo','-NoExit','-Command',$frontCmd
	return @($api,$frontend)
}

$procs = if ($Legacy) { Start-Legacy } else { Start-Unified }

# Collect process IDs safely (ForEach-Object Id was invalid syntax).
$procIds = ($procs | Where-Object { $_ -and -not $_.HasExited } | ForEach-Object { $_.Id }) -join ', '
Write-Host "`nStarted process id(s): $procIds" -ForegroundColor Green
Write-Host "Press Enter to stop all..." -ForegroundColor Yellow
[Console]::ReadLine() | Out-Null

Write-Section "Stopping"
foreach ($p in $procs) {
	if (-not $p) { continue }
	if ($p.HasExited) { continue }
	Write-Host "Stopping PID $($p.Id)..." -ForegroundColor DarkYellow
	$p.CloseMainWindow() | Out-Null
	Start-Sleep -Milliseconds 500
	if (-not $p.HasExited) { $p | Stop-Process -Force }
}
Write-Host "All processes stopped." -ForegroundColor Yellow
