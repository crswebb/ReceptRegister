# Changelog

All notable changes to this project will be documented in this file.

The format loosely follows Keep a Changelog, with unreleased changes collected under the `[Unreleased]` section. Versions / previews are cut from `main` into `release/*` branches.

## [Unreleased]
### Added
- Mobile-friendly collapsible navigation menu (feature #139) with animated expand/collapse.
- Debug page exposing runtime settings (cultures, database provider/connection, environment) (#163).

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
[preview-2]: https://github.com/crswebb/ReceptRegister/compare/release/preview-1...release/preview-2
