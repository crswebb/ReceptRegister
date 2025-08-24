<#!
.SYNOPSIS
    Archive (tag) and delete merged or stale feature branches.
.DESCRIPTION
    Finds remote branches (excluding protected set) whose tips are ancestors of origin/main OR
    whose diff to origin/main is empty (identical tree). Archives each via annotated tag
    then deletes remote and local refs.
.PARAMETER Protected
    Array of protected branch names (default: main)
.PARAMETER Keep
    Array of branch names to skip even if merged (e.g., active PR branches)
.EXAMPLE
    ./tools/prune-merged.ps1 -Keep 'feat/active-99-something'
#>
param(
  [string[]]$Protected = @('main'),
  [string[]]$Keep = @()
)

Write-Host "[prune] Fetching refs..." -ForegroundColor Cyan
& git fetch origin --prune | Out-Null

$current = (& git rev-parse --abbrev-ref HEAD).Trim()
$branches = & git for-each-ref --format='%(refname:short)' refs/remotes/origin |
  Where-Object { $_ -notmatch 'origin/(HEAD|main)$' } |
  ForEach-Object { $_ -replace '^origin/', '' }

if (-not $branches) { Write-Host '[prune] No remote branches found.'; exit 0 }

$mainSha = (& git rev-parse origin/main).Trim()
$pruned = @()
$skipped = @()

foreach ($b in $branches) {
  if ($Protected -contains $b) { $skipped += "$b (protected)"; continue }
  if ($Keep -contains $b) { $skipped += "$b (keep list)"; continue }
  if ($b -eq $current) { $skipped += "$b (current)"; continue }
  $remoteRef = "origin/$b"
  $exists = & git show-ref --verify --quiet refs/remotes/origin/$b; if ($LASTEXITCODE -ne 0) { $skipped += "$b (no remote)"; continue }
  $tipSha = (& git rev-parse $remoteRef).Trim()
  # Condition 1: branch tip is ancestor of main
  & git merge-base --is-ancestor $tipSha $mainSha
  $isAncestor = ($LASTEXITCODE -eq 0)
  # Condition 2: identical tree (no diff)
  $diff = & git diff --name-only $tipSha $mainSha
  $identical = -not $diff
  if (-not ($isAncestor -or $identical)) { $skipped += "$b (not merged)"; continue }
  $tag = "archive/" + ($b -replace '/','-') + "-" + $tipSha.Substring(0,8)
  Write-Host "[prune] Archiving $b as $tag" -ForegroundColor Yellow
  & git tag -a $tag $tipSha -m "Archive snapshot before pruning $b" 2>$null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "[prune] ERROR: Failed to create tag $tag for $b" -ForegroundColor Red
    $skipped += "$b (tag create failed)"
    continue
  }
  & git push origin $tag | Out-Null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "[prune] ERROR: Failed to push tag $tag for $b" -ForegroundColor Red
    $skipped += "$b (tag push failed)"
    continue
  }
  & git push origin --delete $b | Out-Null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "[prune] ERROR: Failed to delete remote branch $b" -ForegroundColor Red
    $skipped += "$b (remote delete failed)"
    continue
  }
  & git show-ref --verify --quiet refs/heads/$b; if ($LASTEXITCODE -eq 0) { & git branch -D $b | Out-Null }
  $pruned += $b
}

Write-Host "[prune] Done." -ForegroundColor Cyan
Write-Host "Pruned:  $($pruned -join ', ' )"
Write-Host "Skipped: $($skipped -join ', ')"
