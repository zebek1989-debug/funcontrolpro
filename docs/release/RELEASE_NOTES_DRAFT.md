# FanControl Pro Release Notes (Draft)

Version: 1.0.0-rc1 (draft)  
Date: 2026-03-15

## Highlights

- Hardware monitoring and support-level classification pipeline.
- Manual control, fan curves, and profile switching.
- Safety monitor with failsafe and recovery workflows.
- Tray integration, onboarding flow, and application settings.

## Phase 5 Updates (In Progress)

- Public compatibility matrix and QA validation assets added.
- Performance/soak playbook and stress test harness added.
- MSI installer scaffold (WiX v4) and release artifact workflow added.

## Known Limitations

- Full compatibility matrix still requires hardware-in-the-loop validation.
- 24h soak results are pending on target Windows hardware.
- Installer flow requires Windows build runner for MSI generation.

## Upgrade Notes

- User data is stored under `%APPDATA%\FanControlPro`.
- Upgrades should preserve profiles and settings.

## Support

- Include support bundle when reporting issues.
