# Hotfix Response Plan (Post 1.0)

Last updated: 2026-03-15

## Intake channel

- Primary: GitHub Issues (`hotfix` label).
- Critical incidents: immediate escalation in maintainer channel + incident issue.

## Severity and SLA

- `P0` (critical safety/security): acknowledge within 1h, mitigation/workaround within 24h.
- `P1` (major regression): acknowledge within 1 business day, fix target in next patch.
- `P2` (minor): triage and schedule during regular maintenance.

## Hotfix release flow

1. Open incident issue (`severity:p0` or `severity:p1`, `hotfix` label).
2. Reproduce and isolate in minimal scenario.
3. Implement fix in hotfix branch.
4. Run release workflow + targeted regression checks.
5. Tag patch release (`v1.0.1`, `v1.0.2`, ...).
6. Publish short advisory with impact, workaround and fix version.

## Rollback policy

- If hotfix fails validation, pause rollout and keep previous stable tag active.
- Revert problematic release notes/status with clear communication.
- Keep MSI + ZIP checksums for previous stable build available.

## Required postmortem for P0

- Root cause.
- Detection gap.
- Corrective action.
- Preventive automation/test updates.
