# Release Candidate Verification Checklist (6.2)

Last updated: 2026-03-15

Release candidate version:
Verification window (UTC):
Owner:

## Required automated gates

- [ ] `dotnet build FanControlPro.sln -c Release`
- [ ] `dotnet test FanControlPro.sln -c Release`
- [ ] GitHub Actions `Release Artifacts` green for RC tag.

## Manual validation matrix

### 1) Fresh install

- [ ] Install MSI on clean Windows profile.
- [ ] App launches successfully.
- [ ] Dashboard telemetry is present.
- [ ] Start Menu and Desktop shortcuts are created.

### 2) Upgrade from beta

- [ ] Upgrade from previous beta/RC MSI.
- [ ] Profiles/settings survive upgrade.
- [ ] `%APPDATA%\FanControlPro` data is retained.
- [ ] App launches correctly after upgrade.

### 3) Recovery from corrupted config

- [ ] Corrupt consent/settings/profile file intentionally.
- [ ] Startup recovery path restores healthy state.
- [ ] App remains operational and does not require reinstall.

### 4) Failsafe verification

- [ ] Simulate stale/invalid telemetry path.
- [ ] Failsafe mode triggers predictably.
- [ ] Exit from failsafe is safe and logged.
- [ ] No uncontrolled fan-stop behavior observed.

## Documentation checks

- [ ] `docs/release/KNOWN_ISSUES.md` updated.
- [ ] `supported-hardware.md` reflects current validation state.
- [ ] `docs/release/user-guide.md` aligns with actual UI flow.
- [ ] `CHANGELOG.md` includes RC entry.

## Decision

- [ ] GO
- [ ] NO-GO

Decision summary:
Open risks:
Follow-up actions:
