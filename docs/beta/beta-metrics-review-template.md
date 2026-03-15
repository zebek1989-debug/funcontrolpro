# Beta Metrics Review Template

Last updated: 2026-03-15

## Input sources

- `docs/beta/beta-metrics-log.csv`
- Open `beta` issues with severity labels.
- Crash and failsafe telemetry from diagnostics/support bundles.

## Snapshot window

- From (UTC):
- To (UTC):
- Evaluated build/tag:

## Core metrics

- Sessions total:
- Sessions crash-free:
- Crash-free sessions %:
- Failsafe events:
- Open P0:
- Open P1:
- Open P1 with accepted workaround:
- Open P2:

## Top recurring problems (Top 10)

1.
2.
3.
4.
5.
6.
7.
8.
9.
10.

## Decision gates

- Crash-free sessions >= 99.5%: `PASS/FAIL`
- Open P0 == 0: `PASS/FAIL`
- P1 closed or workaround accepted: `PASS/FAIL`
- Top recurring problems addressed: `PASS/FAIL`

## Recommendation

- `GO` / `NO-GO`
- Summary:
- Required follow-up actions:
