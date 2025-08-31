# Security

This document consolidates security-related guidance extracted from the README.

## Contents
- [Password Hashing](#password-hashing)
- [Session Management](#session-management)
- [Password Recovery & Rotation](#password-recovery--rotation)
- [Manual Recovery (SQL Quick Steps)](#manual-recovery-sql-quick-steps)
- [Disaster Recovery Checklist](#disaster-recovery-checklist)
- [Environment Variables](#environment-variables)
- [Threat Reminders](#threat-reminders)

## Password Hashing
PBKDF2 (SHA-256) with per-user 32-byte random salt, configurable iteration count (env `RECEPT_PBKDF2_ITERATIONS`, default 150,000), optional global pepper `RECEPT_PEPPER` concatenated before hashing.

Strength meter (server authoritative) scores 0–6 across: length >=8, length >=12, lowercase, uppercase, digit, symbol. Minimum accepted score = 3.

Pepper guidance:
- DO set a long random pepper in production
- DO keep it outside source control (secret store / env)
- DON’T rotate casually (rotation invalidates existing hashes)

## Session Management
Endpoints:
- `POST /auth/set-password` – one time setup
- `POST /auth/login` – returns `{ expiresAt, csrf }` + creates `rr_session` HttpOnly cookie
- `POST /auth/change-password` – requires current password + CSRF
- `POST /auth/refresh` – prolong current session
- `GET /auth/status` – status shape: `{ hasPassword, authenticated, expiresAt?, csrf? }`

Include `X-CSRF-TOKEN` for any state-changing method (POST/PUT/PATCH/DELETE). Read-only GETs do not need it.

Rate limiting (defaults): 5 failed logins / 5 minute sliding window per IP (configurable with `RECEPT_LOGIN_MAX_ATTEMPTS` / `RECEPT_LOGIN_WINDOW_SECONDS`).

Sessions are in-memory (single process). Restart invalidates them—acceptable for personal scope.

## Password Recovery & Rotation
Lost password -> force setup mode:
1. Stop process.
2. Delete row from `AuthConfig` table (see manual recovery below).
3. Start app, set a new password.

Pepper rotation procedure:
1. Maintenance window (read-only).
2. Delete `AuthConfig` row.
3. Set new `RECEPT_PEPPER` env.
4. Start app, set password anew.

Iteration increases: safe anytime; hashes rehashed lazily on successful login.

## Manual Recovery (SQL Quick Steps)
```sql
DELETE FROM AuthConfig WHERE Id=1;
VACUUM; -- optional
```
Then restart the app and set a new password.

## Disaster Recovery Checklist
- Backup: copy `App_Data/receptregister.db` while stopped
- Restore: replace file, ensure file permissions
- Integrity check: `SELECT COUNT(*) FROM Recipes;`

## Environment Variables
| Variable | Purpose | Recommendation |
|----------|---------|---------------|
| `RECEPT_PBKDF2_ITERATIONS` | Hash iteration count | >=150k, raise gradually |
| `RECEPT_PEPPER` | Global pepper secret | Long random, prod only |
| `RECEPT_SESSION_MINUTES` | Session lifetime | 120 default |
| `RECEPT_LOGIN_MAX_ATTEMPTS` | Max failed logins | 5 default |
| `RECEPT_LOGIN_WINDOW_SECONDS` | Rate limit window | 300 |

## Threat Reminders
- Keep pepper secret; never log it.
- Monitor iteration performance (< ~250ms target per hash).
- Limit brute force via rate limiting.
- Use HTTPS in production (reverse proxy or Kestrel with cert) so session cookie confidentiality is preserved.

---
← Previous: (none) | Next: [Data Migration](./data-migration.md) →
