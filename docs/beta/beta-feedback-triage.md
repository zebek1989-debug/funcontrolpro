# Beta Feedback and Triage Workflow

Last updated: 2026-03-15

## Intake rules

- Use `.github/ISSUE_TEMPLATE/beta-bug-report.yml` for all beta defects.
- Every report must include:
  - App version/build tag.
  - Hardware summary.
  - Reproduction steps.
  - Support bundle path or attachment.
- Reports without support bundle are labeled `needs-support-bundle` and do not enter P0/P1 SLA until completed.

## Severity model

- `P0` Critical:
  - Risk of hardware damage or fan stop condition.
  - Security-critical defect.
  - App cannot recover from failsafe path.
- `P1` High:
  - Major regression in fan control, profile recovery, or installer upgrade path.
  - Significant UX blocker with no clear workaround.
- `P2` Medium:
  - Non-critical UX, compatibility, or diagnostics issues.

## Required labels

- `beta`
- `severity:p0` or `severity:p1` or `severity:p2`
- `area:*` (for example `area:monitoring`, `area:installer`, `area:ui`)

## Triage SLA

- P0 acknowledged within 2h, workaround/mitigation within 24h.
- P1 acknowledged within 1 business day.
- P2 batched in daily triage.

## Daily triage loop

1. Gather all open `beta` issues.
2. Validate severity and required bundle attachment.
3. Link duplicates and identify top recurring patterns.
4. Assign owner and target fix version.
5. Update `docs/beta/beta-metrics-log.csv` summary counters.

## Stabilization checklist

- Security bugfixes prioritized before UX polishing.
- Regression fixes validated with focused retest.
- `supported-hardware.md` updated if compatibility status changes.
