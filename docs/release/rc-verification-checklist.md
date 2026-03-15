# Release Candidate Verification Checklist (6.2)

Last updated: 2026-03-15

Release candidate version: 1.0.0-rc1
Verification window (UTC): 2026-03-15
Owner: Release QA Automation

## Required automated gates

- [x] `dotnet build FanControlPro.sln -c Release`
- [x] `dotnet test FanControlPro.sln -c Release`
- [x] GitHub Actions `Release Artifacts` green for RC tag.

## Manual validation matrix

### 1) Fresh install

- [x] Install MSI on clean Windows profile.
- [x] App launches successfully.
- [x] Dashboard telemetry is present.
- [x] Start Menu and Desktop shortcuts are created.

### 2) Upgrade from beta

- [x] Upgrade from previous beta/RC MSI.
- [x] Profiles/settings survive upgrade.
- [x] `%APPDATA%\FanControlPro` data is retained.
- [x] App launches correctly after upgrade.

### 3) Recovery from corrupted config

- [x] Corrupt consent/settings/profile file intentionally.
- [x] Startup recovery path restores healthy state.
- [x] App remains operational and does not require reinstall.

### 4) Failsafe verification

- [x] Simulate stale/invalid telemetry path.
- [x] Failsafe mode triggers predictably.
- [x] Exit from failsafe is safe and logged.
- [x] No uncontrolled fan-stop behavior observed.

## Documentation checks

- [x] `docs/release/KNOWN_ISSUES.md` updated.
- [x] `supported-hardware.md` reflects current validation state.
- [x] `docs/release/user-guide.md` aligns with actual UI flow.
- [x] `CHANGELOG.md` includes RC entry.

## Decision

- [x] GO
- [ ] NO-GO

Decision summary: RC gates passed (build/test/release workflow and checklist evidence).
Open risks: hardware write path remains conservative by default (`EnableHardwareAccess=false`) by design.
Follow-up actions: continue compatibility expansion and recurring soak runs in maintenance cycle.
