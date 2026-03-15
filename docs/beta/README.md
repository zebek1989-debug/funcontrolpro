# Closed Beta Ops (Phase 6.1)

This folder contains operational assets for Milestone 6.1 (Closed Beta).

## Goal

Run a controlled beta cycle and make a release/no-release decision for 1.0 based on measurable quality gates.

## Scope of this package

- Tester recruitment and roster tracking.
- Standardized beta issue intake with required support bundle.
- P0/P1/P2 triage rules and stabilization workflow.
- Beta metrics capture and readiness review template.

## Quick links

- Recruitment plan: `docs/beta/tester-recruitment-plan.md`
- Tester roster: `docs/beta/tester-roster.csv`
- Feedback + triage: `docs/beta/beta-feedback-triage.md`
- Metrics log: `docs/beta/beta-metrics-log.csv`
- Metrics review: `docs/beta/beta-metrics-review-template.md`
- Readiness calculator: `scripts/beta/Calculate-BetaReadiness.ps1`
- GitHub template: `.github/ISSUE_TEMPLATE/beta-bug-report.yml`

## Minimum readiness gates (from plan)

- Crash-free sessions >= 99.5%.
- No open critical security issues (P0).
- P1 issues closed or covered by accepted workaround.
- Top 10 recurring user issues addressed.
