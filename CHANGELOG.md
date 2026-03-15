# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

### Added

- (reserved)

## [1.0.0] - 2026-03-15

### Added

- Public release assets:
  - `docs/release/public-release-runbook.md`
  - `docs/release/getting-started.md`
  - `docs/release/hotfix-response-plan.md`
  - `docs/release/post-release-72h-monitoring.md`
  - `docs/release/post-release-72h-report-v1.0.0.md`
  - `docs/release/RELEASE_NOTES_1.0.0.md`
  - `.github/ISSUE_TEMPLATE/hotfix-incident.yml`

### Changed

- Release workflow can create GitHub Release entries for stable tags.
- Stable release `v1.0.0` published with MSI, ZIP and SHA256 assets.

## [1.0.0-rc1] - 2026-03-15

### Added

- RC process assets:
  - `docs/release/feature-freeze-policy.md`
  - `docs/release/rc-verification-checklist.md`
  - `docs/release/KNOWN_ISSUES.md`
  - `docs/release/user-guide.md`
  - `scripts/release/New-RcTag.ps1`
- Closed beta operational package for phase 6.1:
  - recruitment, triage, metrics and issue templates.

### Changed

- Release workflow now validates MSI lifecycle and upgrade paths on Windows.
- Release readiness documentation aligned with RC stabilization goals.

## [0.1.6] - 2026-03-15

### Fixed

- Release artifact script uses absolute output paths, fixing MSI discovery in CI baseline build.

## [0.1.5] - 2026-03-15

### Fixed

- Restore manager closes temporary file streams before atomic move, fixing Windows lock regression.

## [0.1.4] - 2026-03-15

### Changed

- Restore flow error messages include exception detail for faster CI diagnosis.

## [0.1.3] - 2026-03-15

### Changed

- Backup recovery integration tests now emit richer diagnostics on assertion failures.

## [0.1.2] - 2026-03-15

### Fixed

- Test-annotation step in release workflow fixed for PowerShell variable interpolation.

## [0.1.1] - 2026-03-15

### Changed

- Release workflow now uploads TRX and emits failed-test annotations for quicker triage.

## [0.1.0] - 2026-03-15

### Added

- Initial project scaffold and core implementation for phases 0-5.
