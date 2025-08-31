param(
    [int]$MaxLines = ${env:README_MAX_LINES} -as [int] -or 350,
    [switch]$FailAbove = [bool](${env:README_FAIL_ABOVE} -as [int])
)

$readmePath = Join-Path $PSScriptRoot '..' 'README.md'
if (-not (Test-Path $readmePath)) {
    Write-Host "README.md not found at $readmePath" -ForegroundColor Red
    exit 2
}
$count = (Get-Content $readmePath | Measure-Object -Line).Lines
if ($count -le $MaxLines) {
    Write-Host "README line count: $count (<= $MaxLines) : OK" -ForegroundColor Green
    exit 0
}
$msg = "README line count $count exceeds limit $MaxLines. Consider extracting content into docs/."
if ($FailAbove) {
    Write-Host $msg -ForegroundColor Red
    exit 1
} else {
    Write-Host $msg -ForegroundColor Yellow
    exit 0
}
