# FanControl Pro - Supported Hardware Matrix

Last updated: 2026-03-15

## Scope

This document is the public compatibility list for FanControl Pro 1.0.
Support level is assigned per tested platform and validated with the QA procedure from `docs/qa/hardware-validation-checklist.md`.

Support levels:
- `Full Control` - monitoring + validated PWM/DC write path.
- `Monitoring Only` - telemetry only, control path blocked or not validated.
- `Unsupported` - no stable telemetry path or unsafe behavior.

## Validation Policy

- Hardware is published as `Validated` only after full checklist pass.
- Results are tracked in `docs/qa/hardware-matrix.csv`.
- Vendor conflict behavior is tracked in `docs/qa/vendor-conflict-test-matrix.md`.

## Full Control (Validated)

| Platform ID | Motherboard | Chipset | CPU | GPU | Controller | OS | BIOS | Status | Last Validated | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| FC-001 | ASUS Z490-P | Z490 | Intel Core i7-10700K | NVIDIA RTX 3070 | ASUS EC | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Full control path validated in phase 5 matrix. |
| FC-002 | MSI B550 Tomahawk | B550 | AMD Ryzen 7 5800X | AMD RX 6700 XT | MSI EC | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Full control path validated in phase 5 matrix. |
| FC-003 | Gigabyte Z690 AORUS Elite | Z690 | Intel Core i5-12600K | NVIDIA RTX 4060 | Gigabyte EC | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Full control path validated in phase 5 matrix. |

## Monitoring Only (Validated)

| Platform ID | Motherboard | Chipset | CPU | GPU | Controller | OS | BIOS | Status | Last Validated | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| MO-001 | ASUS PRIME B450M-A | B450 | AMD Ryzen 5 3600 | NVIDIA GTX 1660 | SuperIO | Windows 10 22H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-002 | ASUS TUF GAMING B660-PLUS | B660 | Intel Core i5-12400 | NVIDIA RTX 3060 | SuperIO | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-003 | MSI PRO B660M-A | B660 | Intel Core i3-12100F | NVIDIA RTX 2060 | SuperIO | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-004 | MSI B450M PRO-VDH MAX | B450 | AMD Ryzen 5 5600 | AMD RX 6600 | SuperIO | Windows 10 22H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-005 | Gigabyte B550M DS3H | B550 | AMD Ryzen 5 5600X | NVIDIA RTX 3060 Ti | SuperIO | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-006 | Gigabyte H610M S2H | H610 | Intel Core i3-13100 | Intel UHD | SuperIO | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-007 | ASRock B560 Steel Legend | B560 | Intel Core i7-11700 | NVIDIA RTX 3070 | SuperIO | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-008 | ASRock B550 Pro4 | B550 | AMD Ryzen 7 5700X | AMD RX 6800 | SuperIO | Windows 11 23H2 | TBD | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-009 | Dell XPS 8940 OEM Board | OEM | Intel Core i7-10700 | NVIDIA GTX 1650 | OEM EC | Windows 11 23H2 | OEM | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |
| MO-010 | HP OMEN 30L OEM Board | OEM | AMD Ryzen 7 5800X | NVIDIA RTX 3080 | OEM EC | Windows 11 23H2 | OEM | Validated | 2026-03-15 | Monitoring-only telemetry validated in phase 5 matrix. |

## Unsupported / Blocked

| Platform ID | Motherboard | Reason | Status | Notes |
|---|---|---|---|---|
| UNSUP-001 | TBD | Not enough stable sensors or unsafe control path | Deferred | No blocking unsupported target in current 1.0 validation set. |

## Known Limitations

- `Full Control` requires Administrator rights and explicit user consent.
- Running vendor fan software in parallel may cause undefined behavior.
- Some OEM boards expose partial telemetry with no writable channels.

## Known Vendor Conflicts

- ASUS Armoury Crate / AI Suite
- MSI Center / Dragon Center
- Gigabyte Control Center / AORUS Engine
- Corsair iCUE (sensor/control overlap)
- NZXT CAM (sensor/control overlap)

See detailed matrix in `docs/qa/vendor-conflict-test-matrix.md`.

## How To Request Validation

Create an issue with:
- full hardware specs (motherboard, CPU, GPU, BIOS version),
- exported support bundle,
- whether vendor fan tools are installed/running.
