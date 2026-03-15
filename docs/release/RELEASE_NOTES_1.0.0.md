# FanControl Pro 1.0.0 Release Notes

Date: 2026-03-15

## Highlights

- Hardware monitoring pipeline with support-level classification.
- Manual fan control, curve-based automation and profile switching.
- Safety monitor with failsafe and recovery flow.
- Backup/recovery and diagnostic support bundle export.
- Windows release pipeline with automated MSI lifecycle + upgrade validation.

## Included artifacts

- MSI installer (`win-x64`)
- Portable ZIP (`win-x64`)
- SHA256 checksum manifest

## Documentation

- Getting Started: `docs/release/getting-started.md`
- Compatibility list: `supported-hardware.md`
- Known issues: `docs/release/KNOWN_ISSUES.md`
- Changelog: `CHANGELOG.md`

## Notes

- Administrator rights are required for hardware control mode.
- Running vendor fan tools in parallel may cause undefined behavior.
- User data is preserved in `%APPDATA%\\FanControlPro` during upgrade.

## Support

- Open issue with support bundle attached.
- Use hotfix incident template for critical post-release problems.
