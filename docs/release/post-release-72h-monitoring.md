# Post-Release Monitoring (First 72h)

Last updated: 2026-03-15

## Monitoring window

- Start: release publish timestamp (UTC).
- End: +72 hours.

## Cadence

- Hour 0-12: checks every 2 hours.
- Hour 12-48: checks every 6 hours.
- Hour 48-72: checks every 12 hours.

## What to track

1. New critical (`P0`) issues.
2. Frequency of failsafe incidents.
3. Installer/upgrade failures.
4. Top recurring user reports.
5. Any pattern tied to specific hardware/vendor tools.

## Triage actions

- Escalate all P0 immediately to hotfix path.
- Cluster duplicates and keep one canonical issue per symptom.
- Update known issues doc when workaround exists.
- Refresh beta/release metrics snapshot daily.

## Decision at 72h

- `Stable`: no unresolved P0 and no rising major regression trend.
- `Patch 1.0.1 needed`: recurring P1 or unresolved P0/P1.

## Output report template

- Release tag:
- Observation period:
- P0 count:
- P1 count:
- Top 5 issues:
- Recommended action:
