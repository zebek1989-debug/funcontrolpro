# Release Candidate Feature Freeze Policy (6.2)

Last updated: 2026-03-15

## Objective

Protect RC stability by allowing only low-risk changes after freeze starts.

## Freeze window

- Start: when RC branch/tag candidate is selected.
- End: when Go/No-Go decision for public 1.0 is made.

## Allowed changes

- Bugfixes with reproducible issue and clear verification steps.
- Security fixes.
- Documentation corrections required for release clarity.
- Test-only changes improving confidence and observability.

## Blocked changes

- New end-user features.
- New dependencies unless required for critical fix.
- Breaking behavior changes without explicit release manager approval.
- Config schema changes.

## Config schema freeze

- `settings.json5`, profile payloads, onboarding/consent and backup metadata schemas are frozen.
- If schema change is unavoidable:
  1. Add migration plan.
  2. Validate upgrade + rollback.
  3. Obtain release manager sign-off.

## Approval flow

1. Open issue with `rc-change` label.
2. Mark risk level: low/medium/high.
3. Add verification checklist and rollback notes.
4. Require at least one reviewer approval before merge.

## Exit criteria

- No unreviewed RC changes.
- No pending schema changes.
- All P0/P1 issues resolved or accepted workaround documented.
