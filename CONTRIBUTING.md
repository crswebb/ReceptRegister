# Contributing to ReceptRegister

Welcome! This project favors a simple, disciplined workflow to keep changes readable and easy to ship. The two most common policy failures are: (1) missing `Closes #<issue>` lines in the PR body and (2) merge commits in a feature branch. This guide makes both unambiguous so they stop happening.

## Ground rules (high level)
- Postback-first UI; progressive enhancement with small vanilla ESM modules.
- Hand-written CSS; no external JS/CSS libraries, no inline scripts/styles.
- Two apps: Razor Pages Frontend and Minimal API. DB lives in API; Frontend calls API.
- Keep PRs small and focused. Prefer clarity over cleverness.

See `.copilot/copilot-instructions.md` for full guardrails, commit message recipe, and persona.

## Workflow at a glance
Feature development is always done in a short‑lived feature branch. Promotion to deployment happens via a release branch.

1) Pick or open an issue describing the change (never start work without an issue).  
2) Create a feature branch from `main` using the naming pattern below.  
3) Open a Draft PR to `main` immediately and include at least one closure line (e.g. `Closes #41`).  
4) Push incremental commits; keep your branch up to date by rebasing onto `main` (never merge `main` into your branch).  
5) Keep the PR body accurate as scope evolves (add more `Closes #` lines if you intentionally finish additional issues).  
6) When ready, mark PR “Ready for review”, ensure checks are green, and squash‑merge into `main`.  
7) Release promotion: open a PR from `main` to the active `release/preview-*` branch (or fast‑forward / reset the release branch) to deploy. No feature work ever lands first in a release branch.  
8) Cut a new release/preview branch from `main` only when you intend to stabilize & deploy a new preview (naming: `release/preview-N`).

If an urgent production fix is needed: create `hotfix/<issue>-<slug>` from `main`, PR → `main`, then immediately promote `main` into the release branch.

## Branch naming
Pattern: `type/scope-<issueNumber>-<short-slug>`
- type: `feat` | `fix` | `chore` | `docs` | `test` | `refactor` | `perf` | `build` | `ci` | `hotfix`
- scope: `api` | `frontend` | `infra` | `docs` (optional but encouraged)

Examples:
- `feat/frontend-41-css-suite`
- `fix/api-30-recipes-put-validation`
- `hotfix/99-login-nullref`

## Why open a Draft PR early?
- Visibility: teammates can see intent and progress; avoids duplicated work.  
- Early feedback: catch design/product concerns before code hardens.  
- CI signal: lint/build/tests run on every push to surface issues sooner.  
- Traceability: link the PR to its issue (“Closes #…”) from day one; keeps context together.  
- Smaller, safer changes: nudges us to ship in increments rather than large, risky drops.

## Rebase-first branch hygiene (why and how)
Why rebase?
- Clean, linear history that’s easy to read, review, and bisect.  
- Avoids merge-commit noise and “octopus graphs.”  
- Keeps release notes and blame more meaningful.

How (typical loop):
```powershell
# Update local refs
git fetch origin

# Rebase your branch onto latest main
git rebase origin/main

# If conflicts occur, resolve them, then continue
git add -A
git rebase --continue

# Push with lease after history rewrite
git push --force-with-lease
```
Notes:
- Only rebase your own feature branches. Don’t rebase shared branches others base work on.  
- Use `--force-with-lease` (not `--force`) to avoid clobbering others’ updates.

Absolutely NO merge commits in feature branches. If you accidentally merged `main`:
```powershell
# Option A: Soft reset before anyone else pulled
git reset --soft HEAD~1
git commit -m "<recreate your intended commit message>"

# Option B: Interactive rebase to drop the merge commit
git rebase -i origin/main
# Mark the merge commit line as 'd' (drop), save & continue
git push --force-with-lease
```
If in doubt, ask or open a draft PR comment—history cleanliness beats speed.

## Squash-merge by default (why and how)
Why squash?
- Keeps `main` history focused on change sets, not WIP commits.  
- Easier to revert a feature (one commit) if needed.  
- PR title/body become the canonical “what/why”, improving traceability.

How:
- Ensure the PR title is an imperative summary (<= 72 chars).  
- In the PR body, describe what changed and why; add one or more lines each beginning with `Closes #<issueNumber>` (exact casing and spacing).  
- Choose “Squash and merge” when merging.

Multiple issues? Use one line per issue. Example:
```
Closes #41
Closes #57
Closes #60
```
GitHub only parses closure keywords when they start a line (or appear in a sentence); the safest pattern here is one keyword per line near the top under an `Issue Linkages:` heading (the PR template provides it).

Trade-offs and exceptions:
- Squash loses WIP granularity in `main` (you still have it in the PR). If a feature truly needs multiple logical commits preserved, discuss and consider a standard merge.  
- Rebase rewrites history—only do it on your branch; never on `main`.

## Commit messages (short guide)
- Title: imperative, <= 72 chars.  
- Body: what changed, why, risks/rollbacks, and links to issues.  
- Example: `feat(frontend): add base layout and skip link (Closes #33)`

## PR checklist (quick)
- PR body contains at least one `Closes #<issue>` line (each on its own line).  
- Small, focused diff (<~300 LOC where possible).  
- No external JS/CSS libraries; no inline scripts/styles.  
- Progressive enhancement parity (works without JS).  
- Tests added/updated for core logic and 1 edge case.  
- Rebased onto latest `main` (no merge commits); CI is green.  
- Ready for review (undraft) and prepared for squash merge.

If the PR already exists and you forgot to add a closure line, just edit the PR description—no commit rewrite needed.

### Quick remediation examples

Missing closure line policy failure:
1. Edit PR body.
2. Add:
	```
	Issue Linkages:
	Closes #41
	```
3. Save; re-run (or wait for) the policy check.

Merge commit detected in branch:
1. Ensure work tree clean.
2. `git fetch origin`
3. `git rebase origin/main`
4. Resolve conflicts (`git add -A && git rebase --continue`) until done.
5. `git push --force-with-lease`.

Generated or build artifacts committed (e.g. `obj/` / `bin/`):
1. Remove them: `git rm -r obj bin` (adjust paths).  
2. Add to `.gitignore` if missing.  
3. Commit and push.

## Hotfixes
- Branch from `main` as `hotfix/<issueNumber>-<slug>`.  
- Keep the fix minimal; open PR quickly; squash-merge after checks pass.

## Release branch promotion model
Release branches exist only for deployment/stabilization. They should **never** contain work not already merged into `main`.

Principles:
- `main` is the single integration trunk; it must stay green (tests passing) at all times.
- A release branch (`release/preview-N`) is cut from `main` when you decide to produce/iterate on a deployable preview.
- All feature branches target `main` only. After a feature merges, you *promote* by updating the release branch with the current `main` (PR `main` → `release/preview-N`, or fast‑forward/reset if acceptable).
- No cherry-picks into the release branch except: (1) critical hotfix already merged to `main` or (2) a temporary revert while stabilizing (followed by a proper fix to `main`).
- If divergence occurs (a commit appears only on the release branch), fix it immediately by either merging it properly through `main` or reverting it from the release branch.

Promotion workflow (normal case):
```text
feat/*  -> PR -> main (squash merge)
main    -> PR -> release/preview-N (deploy)
```

Updating the release branch:
```powershell
# Option A (PR based – preserves review / CI):
git checkout main
git fetch origin
git pull --ff-only origin main
git checkout -b promote/main-to-preview-N
git merge --ff-only origin/release/preview-N || echo "(If diverged, prefer reset approach)"
# (Usually empty merge; branch contains only main commits.)
# Open PR: base=release/preview-N compare=promote/main-to-preview-N

# Option B (fast-forward the release branch) – only if policy allows direct push:
git checkout release/preview-N
git fetch origin
git reset --hard origin/main
git push --force-with-lease origin release/preview-N
```

Cutting a new preview branch:
```powershell
git checkout main
git fetch origin
git pull --ff-only origin main
git checkout -b release/preview-3
git push -u origin release/preview-3
```

Changelog & tagging:
- Update `CHANGELOG.md` in `main` just before cutting the new branch (move entries from `[Unreleased]` to a new `[preview-N]` section).
- After branch cut, update `[Unreleased]` diff links to compare from the new release branch.
- Tag (optional) if you want immutable refs: `git tag -a v0.0.0-preview.N <sha>` then `git push origin v0.0.0-preview.N`.

Hotfix on current preview:
1. Branch from `main` (not the release branch).  
2. Implement & merge via PR to `main`.  
3. Promote `main` to release branch (PR or fast-forward).  
4. Deploy.

Rationale:
- Ensures a single source of truth (main) for all accepted work.
- Avoids “mystery” fixes living only in release branches.
- Simplifies auditing & rollback (one linear main history).


Thank you for keeping the dough smooth and the history clean! 🍞

---

## Branch cleanup / pruning policy
To keep the repository tidy and reduce accidental work off stale code, we prune merged feature branches promptly.

Policy:
- Delete a feature / chore / fix branch (local + remote) within 24h after its PR is squash‑merged.
- Keep only long‑lived protected branches (currently just `main`).
- Large multi‑PR efforts should still use one branch per issue; do NOT create an umbrella long‑lived branch unless explicitly agreed.
- Temporary linearization / backup branches (e.g. `*-linear`, `backup/*`) must be deleted immediately after the target branch is updated or the PR is merged.
- If you need a historical snapshot, create an annotated tag (`git tag -a archive/<slug> <sha> -m "Pre‑cleanup snapshot"`) before deleting the branch.

Automatic safety checks before deletion:
1. Ensure the branch tip is reachable from `origin/main`:
	 ```powershell
	 git fetch origin
	 git merge-base --is-ancestor origin/<branch> origin/main
	 ```
	 Exit code `0` = merged (safe to delete).
2. Confirm no open PR still references the branch.
3. Tag if you genuinely need an archive.

Quick prune helper (PowerShell):
```powershell
git fetch origin
$protected = @('main')
$excludeActive = @('feat/frontend-5-ui-foundation')  # update if there are active PR branches
$merged = git for-each-ref --format='%(refname:short)' refs/remotes/origin | Where-Object { $_ -notmatch 'origin/(HEAD|main)$' } | ForEach-Object {
	$b = ($_ -replace '^origin/')
	if ($protected -contains $b) { return }
	if ($excludeActive -contains $b) { return }
	git merge-base --is-ancestor origin/$b origin/main; if ($LASTEXITCODE -eq 0) { $b }
}
if ($merged) {
	Write-Host "Deleting merged branches: $merged" -ForegroundColor Yellow
	foreach ($b in $merged) { git push origin --delete $b; if (git show-ref --verify --quiet refs/heads/$b) { git branch -D $b } }
} else { Write-Host 'No merged branches to prune.' }
```

Rationale:
- Minimizes noise in branch pickers and CI
- Prevents accidental branching from stale tips
- Keeps security surface (protected ref rules) simple

If in doubt about deleting a branch, open an issue or convert it into a tag first.

---

## Copilot / AI Assistant Guidance (meta)
When asking Copilot (the PR reviewer or chat assistant) for help:
- Always specify the issue number(s) up front so it can include `Closes #…` lines early.
- Ask it to perform a rebase rather than merge when updating a branch.
- Request it to check for and remove any accidental merge commits (`git log --oneline --decorate --graph -n 15`).
- Have it update the PR body instead of adding a new commit just to fix linkage lines.
- If tests fail after a rebase, ask it to run only the failing test(s) first before a full suite.

The `.github/pull_request_template.md` plus this guide should minimize policy noise.

If you see repeatable friction not covered here, open a docs issue so we can refine the guardrails.

### Automated PR check
A GitHub Action (`pr-issue-linkage check`) validates that every referenced issue `#<n>` in the PR body either:
- Has a closure keyword (`Closes #n`, `Fixes #n`, etc.), or
- Is already closed, or
- Is intentionally exempt by having `[no-close]` on the same line.

If it fails, add the missing `Closes #n` lines near the top (preferred) or append `[no-close]` if the reference is informational only.