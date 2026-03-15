# Hardware Validation Checklist (Milestone 5.1)

## Objective

Provide repeatable validation for:
- support level classification (`Full Control`, `Monitoring Only`, `Unsupported`),
- telemetry quality vs HWiNFO64,
- RPM response and safety behavior,
- vendor conflict detection.

## Pre-Conditions

- Build under test: Release build from current main branch.
- OS: Windows 10/11 x64 fully updated.
- BIOS defaults loaded unless scenario explicitly needs custom fan setup.
- No overclocking utilities active unless part of a conflict test.
- Tester has local admin rights.

## Tools

- FanControl Pro build under test.
- HWiNFO64 (reference telemetry).
- Optional: OCCT / Cinebench / FurMark for load generation.
- Stopwatch or timestamped logging for response-time checks.

## Validation Flow

### 1. Environment Capture

- Record platform ID and full hardware specification.
- Record OS build and BIOS version.
- Record installed vendor fan utilities.

### 2. Detection And Classification

- Launch app and complete onboarding.
- Capture detected components + support level per channel.
- Verify unsupported channels explain reason in UI.
- Save support bundle.

Pass criteria:
- every channel has explicit support level,
- no channel marked `Full Control` without writable validation path.

### 3. Telemetry Cross-Check vs HWiNFO64

Sample for at least 5 minutes, 1-second interval:
- CPU temperature,
- GPU temperature,
- at least one fan RPM channel.

Pass criteria:
- temperature deviation within +/-5 C for stable phases,
- fan RPM deviation within +/-50 RPM,
- no repeated invalid/missing readings on healthy sensors.

### 4. Full Control Write Validation (Only for Full Control candidates)

For each writable channel:
- apply 30%, 50%, 70%, 100% manual setpoint (respecting CPU_FAN minimum),
- observe resulting RPM and response delay.

Pass criteria:
- write command acknowledged,
- RPM trend follows expected direction,
- visible response in <=1 second,
- `Reset` and `Full Speed` actions work.

### 5. Safety And Recovery Checks

- Simulate sensor fault or stale telemetry path.
- Verify app enters caution/emergency state.
- Verify failsafe applies high speed or safe fallback.
- Verify event is visible in UI/tray and logs.

Pass criteria:
- no uncontrolled fan stop,
- failsafe event is logged and user-visible,
- profile/config state remains consistent after recovery.

### 6. Vendor Conflict Check

Execute scenarios from `vendor-conflict-test-matrix.md`.

Pass criteria:
- conflict warning shown,
- app recommends Monitoring Only when conflict is active,
- event appears in diagnostics log.

### 7. Final Classification Decision

- Mark platform result: `Validated`, `MonitoringOnlyValidated`, `UnsupportedValidated`, or `Rejected`.
- Update `docs/qa/hardware-matrix.csv`.
- Update public list `supported-hardware.md`.

## Evidence To Attach Per Platform

- screenshots: dashboard + support level + warnings,
- support bundle zip path,
- short telemetry comparison table (FanControl Pro vs HWiNFO64),
- command/log excerpts for failsafe or conflict behavior,
- final verdict with tester/date.
