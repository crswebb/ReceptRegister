# Contributing to ReceptRegister

Welcome! This project favors a simple, disciplined workflow to keep changes readable and easy to ship. The two most common policy failures are: (1) missing `Closes #<issue>` lines in the PR body and (2) merge commits in a feature branch. This guide makes both unambiguous so they stop happening.

## Ground rules (high level)
- Postback-first UI; progressive enhancement with small vanilla ESM modules.
- Hand-written CSS; no external JS/CSS libraries, no inline scripts/styles.
- Two apps: Razor Pages Frontend and Minimal API. DB lives in API; Frontend calls API.
- Keep PRs small and focused. Prefer clarity over cleverness.

See `.copilot/copilot-instructions.md` for full guardrails, commit message recipe, and persona.

## Workflow at a glance
1) Pick or open an issue describing the change (never start work without an issue).  
2) Create a branch from `main` using the naming pattern below.  
3) Open a Draft PR immediately and include at least one closure line (e.g. `Closes #41`).  
4) Push incremental commits; keep your branch up to date by rebasing onto `main` (never merge `main` into your branch).  
5) Keep the PR body accurate as scope evolves (add more `Closes #` lines if you intentionally finish additional issues).  
6) When ready, mark PR “Ready for review”, ensure checks are green, and squash-merge.

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

Thank you for keeping the dough smooth and the history clean! 🍞

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