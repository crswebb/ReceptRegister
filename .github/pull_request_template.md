# Pull Request

## Summary
Explain the change in 1–3 sentences. Keep this aligned with the eventual squash commit title.

## Issue Linkages
List one closure keyword per line (add more as scope intentionally expands):
```
Closes #<issue-number>
```

## What / Why
- What was added/changed
- Why this approach
- Trade-offs or follow-ups (link issues if already opened)

## Validation
- [ ] Manual run locally (brief notes)
- [ ] Tests added/updated (list)
- [ ] Works without JS (progressive enhancement parity)
- [ ] Accessibility basics verified (focus order, labels, aria-live where dynamic)

## Checklist
- [ ] Small, focused diff (≈ <300 LOC or justified)
- [ ] No external JS/CSS libs; no inline scripts/styles
- [ ] Rebased onto latest `main` (no merge commits)
- [ ] PR body has all necessary `Closes #` lines
- [ ] Commit message(s) follow convention (will squash to title)
- [ ] Ready for squash merge

## Screenshots (optional)
Add before/after or key UI states if relevant.

## Notes
Anything reviewers should pay special attention to (risks, rollback, perf, data migrations).
