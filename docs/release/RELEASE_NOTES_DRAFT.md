# FanControl Pro Release Notes (Draft)

Version: 1.0.0-rc1  
Date: 2026-03-15

## Highlights

- Hardware monitoring and support-level classification pipeline.
- Manual control, fan curves, and profile switching.
- Safety monitor with failsafe and recovery workflows.
- Tray integration, onboarding flow, and application settings.
- Automated Windows release pipeline with MSI lifecycle + upgrade validation.

## RC Readiness Updates

- Feature freeze policy documented.
- RC verification checklist prepared.
- Known issues and user guide drafted.
- CHANGELOG initialized for RC series.

## Known Limitations

- Full compatibility matrix still requires hardware-in-the-loop validation.
- 24h soak results are pending on target Windows hardware.
- Some OEM platforms remain monitoring-only due to missing safe write path.

## Upgrade Notes

- User data is stored under `%APPDATA%\FanControlPro`.
- Upgrades should preserve profiles and settings.

## Support

- Include support bundle when reporting issues.
- Beta/RC issue template requires bundle attachment by default.
