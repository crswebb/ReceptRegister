# Changelog

All notable changes to this project will be documented in this file.

The format loosely follows Keep a Changelog, with unreleased changes collected under the `[Unreleased]` section. Versions / previews are cut from `main` into `release/*` branches.

## [Unreleased]
### Added
- (placeholder)

## [preview-6] - 2025-09-14
### Added
- Detailed startup error diagnostics: `/api/health?details` gated extended info and `/api/startup-error` plaintext endpoint (health visibility improvements).
- Resilient health status states (starting | ok | error) and improved `/api/health` JSON structure.
- Database connectivity probe endpoint `/api/db-ping` (lightweight open + SELECT timing).
- Structured schema migrations system (migration history table + initial migration) replacing ad-hoc schema setup.
- Debug page exposing runtime settings (cultures, database provider/connection, environment) (#163 / #162).
- Environment-driven database provider overrides & documentation (`RECEPT_DB_PROVIDER`, `RECEPT_DB_CONNECTIONSTRING`) (#144).
- Environment culture overrides (`RECEPT_DEFAULT_CULTURE`, `RECEPT_SUPPORTED_CULTURES`) (#137).
- Pepper support integrated into infrastructure deployment & App Service settings automation (#169, #166, #167, #170).

### Changed
- CI policy relaxed to allow `promote/*` and alternate promotion patterns (improves release ergonomics) (#173 / #175).
- Portable smoke test script (removed reliance on external `seq` / `head` utilities) (#188 / #189 backport).
- App settings loading hardened (robust JSON settings handling and culture parsing).
- README & docs restructured: extracted security, localization, theming, data migration docs (issues #141, #146–#154).

### Fixed
- Startup health endpoint now consistently reports initialization state and surfaces failures safely (multiple incremental fixes #180–#184, #186).
- Removed leftover merge markers in health diagnostics backport branch.
- Frontend empty recipes state localization separation (#138).

### Security
- Pepper (RECEPT_PEPPER) deployment & configuration added; guidance documented.

### Documentation
- Removed outdated architecture diagram section & simplified diagram references (#141).
- Added detailed data migration, localization, theming, and security guides.

### CI/CD
- Added migrations & health diagnostics backport automation; improved promotion workflow clarity.

### Diagnostics
- Plaintext startup error endpoint for fast copy/paste during incident response (gated exposure rules shared with detailed health view).

### Infrastructure
- Automated App Service settings provisioning (pepper + culture overrides) (#166, #167, #169, #170).

### Notes
- This preview consolidates multiple backports that landed in `release/preview-5` via cherry-picks; cutting directly from `main` aligns future promotions.

## [preview-2] - 2025-08-30
### Added
- Release workflow: build & test on PRs to `release/**`; publish & deploy only on push (policy codified in #124).
- Changelog file introduced.

### Changed
- README sanitized: removed instance-specific deployment details; added neutral deployment note (#125).

### Removed
- Azure instance-specific instructions from README (moved to neutral guidance).

### Security
- No security-affecting changes.

## [preview-1] - 2025-08-22
### Added
- Initial preview scaffolding and baseline features (see commit history prior to `preview-2`).

[Unreleased]: https://github.com/crswebb/ReceptRegister/compare/release/preview-2...HEAD
[preview-6]: https://github.com/crswebb/ReceptRegister/compare/release/preview-5...release/preview-6
[preview-2]: https://github.com/crswebb/ReceptRegister/compare/release/preview-1...release/preview-2
