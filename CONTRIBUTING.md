# Contributing to ReceptRegister

Welcome! This project favors a simple, disciplined workflow to keep changes readable and easy to ship.

## Ground rules (high level)
- Postback-first UI; progressive enhancement with small vanilla ESM modules.
- Hand-written CSS; no external JS/CSS libraries, no inline scripts/styles.
- Two apps: Razor Pages Frontend and Minimal API. DB lives in API; Frontend calls API.
- Keep PRs small and focused. Prefer clarity over cleverness.

See `.copilot/copilot-instructions.md` for full guardrails, commit message recipe, and persona.

## Workflow at a glance
1) Pick or open an issue describing the change.  
2) Create a branch from `main` using the naming pattern below.  
3) Open a Draft PR immediately and push incremental commits.  
4) Keep your branch up to date by rebasing onto `main`.  
5) When ready, mark PR “Ready for review”, ensure checks are green, and squash-merge.

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

## Squash-merge by default (why and how)
Why squash?
- Keeps `main` history focused on change sets, not WIP commits.  
- Easier to revert a feature (one commit) if needed.  
- PR title/body become the canonical “what/why”, improving traceability.

How:
- Ensure the PR title is an imperative summary (<= 72 chars).  
- In the PR body, describe what changed and why; add “Closes #<issueNumber>”.  
- Choose “Squash and merge” when merging.

Trade-offs and exceptions:
- Squash loses WIP granularity in `main` (you still have it in the PR). If a feature truly needs multiple logical commits preserved, discuss and consider a standard merge.  
- Rebase rewrites history—only do it on your branch; never on `main`.

## Commit messages (short guide)
- Title: imperative, <= 72 chars.  
- Body: what changed, why, risks/rollbacks, and links to issues.  
- Example: `feat(frontend): add base layout and skip link (Closes #33)`

## PR checklist (quick)
- Linked the issue with `Closes #…`.  
- Small, focused diff (<~300 LOC where possible).  
- No external JS/CSS libraries; no inline scripts/styles.  
- Progressive enhancement parity (works without JS).  
- Tests added/updated for core logic and 1 edge case.  
- Rebased onto latest `main`; CI is green.  
- Ready for review (undraft) and prepared for squash merge.

## Hotfixes
- Branch from `main` as `hotfix/<issueNumber>-<slug>`.  
- Keep the fix minimal; open PR quickly; squash-merge after checks pass.

Thank you for keeping the dough smooth and the history clean! 🍞
