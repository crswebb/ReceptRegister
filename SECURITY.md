# Security Notes

This document summarizes authentication, session, and CSRF defenses for ReceptRegister (Issue #59 hardening pass).

## Authentication Flow
1. First run: `GET /auth/status` returns `hasPassword=false`.
2. Admin sets password via `POST /auth/set-password` (one-time until reset).
3. Login via `POST /auth/login` -> sets HttpOnly session cookie + returns `expiresAt` + `csrf` token.
4. Client stores CSRF token (meta tag + progressive enhancement JS) and attaches it to unsafe requests.
5. Optional refresh: `POST /auth/refresh` extends expiry (client auto-invokes when ~25% lifetime remains).

## Session & CSRF
Cookies:
- `rr_session`: HttpOnly, Secure (non-development), SameSite=Strict (configurable), path `/`.
- `rr_csrf`: Non-HttpOnly; tied to session expiry; renewed on refresh/login.

Protection:
- Server validates `X-CSRF-TOKEN` header on unsafe methods if authenticated.
- Token entropy: 128 bits (GUID or similar implementation in service layer).
- Token stored only server-side (session store) + sent in meta tag when page prerenders for authenticated user.

## Password Storage
- PBKDF2-SHA256 with per-user 32-byte salt.
- Iterations configurable (default 150k) via `RECEPT_PBKDF2_ITERATIONS`.
- Optional application pepper (`RECEPT_PEPPER`). Changing pepper requires password reset.

## Threat Model (High Level)
| Threat | Mitigation |
|--------|------------|
| Offline hash cracking | High iteration PBKDF2 + optional pepper |
| Session theft via XSS | HttpOnly session cookie + no dynamic inline scripts for session token |
| CSRF on state change | Validates `X-CSRF-TOKEN` header bound per-session |
| Session fixation | New token issued on login & password set; refresh keeps same token but rotates CSRF only |
| Brute force login | Rate limiter (configurable window / attempts) |
| Password reset abuse | Single admin model; reset requires DB row deletion (physical access) |
| Persistent session abuse | Short session (120m default) + optional remember TTL + refresh threshold |

## Environment Variables (Auth Related)
| Variable | Purpose |
|----------|---------|
| `RECEPT_SESSION_MINUTES` | Session lifetime (short) |
| `RECEPT_SESSION_REMEMBER_MINUTES` | Extended remember-me lifetime |
| `RECEPT_SESSION_SAMESITE_STRICT` | Toggle SameSite Strict vs Lax |
| `RECEPT_SESSION_COOKIE` | Override session cookie name |
| `RECEPT_CSRF_COOKIE` | Override CSRF cookie name |
| `RECEPT_PBKDF2_ITERATIONS` | PBKDF2 iteration count |
| `RECEPT_PEPPER` | Global pepper secret |

## Operational Guidelines
- Always run behind HTTPS in production; cookies are Secure outside Development.
- Rotate pepper only during planned maintenance with forced password reset.
- Monitor authentication logs for repeated 401/429 patterns.

For vulnerabilities please open a private issue or contact the maintainer before disclosure.
