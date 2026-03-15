# Release Packaging Checklist (Milestone 5.3)

Last updated: 2026-03-15

## Artifacts

- MSI installer (`win-x64`).
- Portable ZIP package (`win-x64`).
- SHA256 checksums for each distributed artifact.

## Pre-Release Build Validation

- `dotnet build FanControlPro.sln -c Release`
- `dotnet test FanControlPro.sln -c Release`
- Release artifact workflow green on `windows-latest`.

## Installer Validation

- Fresh install works on clean Windows machine.
- Start Menu shortcut created.
- Desktop shortcut created.
- App launches after install.
- Automated lifecycle script passes: `scripts/release/Validate-MsiLifecycle.ps1`.
- Automated upgrade script passes: `scripts/release/Validate-MsiUpgrade.ps1`.
- `release-artifacts` workflow validates both lifecycle and upgrade on `windows-latest`.

## Upgrade Validation

- Upgrade from previous build keeps user data in `%APPDATA%\FanControlPro`.
- Existing profiles/settings still load.
- Uninstall/reinstall path remains stable.

## Uninstall Validation

- Application binaries removed from install folder.
- Shortcuts removed.
- User data retained unless explicitly removed by user choice.

## Release Metadata

- `LICENSE` present (MIT).
- `THIRD-PARTY-NOTICES.md` present and updated.
- `docs/release/RELEASE_NOTES_DRAFT.md` updated.
- Checksums published with artifacts.

## Sign-Off

- Release manager:
- QA sign-off:
- Date (UTC):
