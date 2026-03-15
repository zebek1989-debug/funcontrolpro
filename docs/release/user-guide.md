# FanControl Pro User Guide (RC)

Last updated: 2026-03-15

## 1. Installation

1. Download MSI or portable ZIP from release artifacts.
2. For MSI: run installer as administrator when prompted.
3. Launch app from Start Menu shortcut.

## 2. First launch and onboarding

1. Complete onboarding wizard.
2. Confirm hardware classification (`Full Control` or `Monitoring Only`).
3. If enabling control mode, explicitly accept risk confirmation.

## 3. Dashboard basics

- Temperatures: CPU/GPU/motherboard sensors.
- Fan speeds: RPM per channel.
- System load: CPU/GPU/RAM indicators.
- Safety status: normal/failsafe/alerts.

## 4. Fan control modes

- `Monitoring Only`: no control writes, telemetry only.
- `Manual`: direct channel percentage control.
- `Curve`: temperature-based dynamic control.
- Profiles: quick switch between saved setups.

## 5. Safety and recovery

- App monitors sensor freshness and validity.
- On unsafe conditions, failsafe path is activated.
- Backup/recovery protects config files from corruption scenarios.

## 6. Diagnostics and support

1. Export support bundle from diagnostics panel.
2. Attach bundle when reporting issues.
3. Include app version/tag and hardware summary.

## 7. Upgrade behavior

- Upgrades are designed to preserve `%APPDATA%\FanControlPro` data.
- Existing profiles/settings should remain available after MSI upgrade.

## 8. Known limitations

See `docs/release/KNOWN_ISSUES.md` and `supported-hardware.md`.
