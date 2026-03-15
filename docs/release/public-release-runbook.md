# Public Release Runbook (6.3)

Last updated: 2026-03-15

## Objective

Publish FanControl Pro 1.0 with complete artifacts, documentation and rollback readiness.

## Preconditions

- RC verification checklist complete (`docs/release/rc-verification-checklist.md`).
- Go decision approved.
- `CHANGELOG.md` and `docs/release/RELEASE_NOTES_1.0.0.md` updated.
- No open P0 issues.

## Release steps

1. Create final release tag:
   - `git tag -a v1.0.0 -m "release: v1.0.0"`
   - `git push origin v1.0.0`
2. Wait for `Release Artifacts` workflow to complete.
3. Verify generated assets:
   - MSI (`win-x64`)
   - Portable ZIP (`win-x64`)
   - `SHA256SUMS.txt`
4. Confirm GitHub Release entry is created for stable tag.
5. Validate release notes/changelog references.
6. Publish announcement with links to:
   - Getting Started
   - Compatibility matrix
   - Known issues

## Documentation links to publish

- `docs/release/getting-started.md`
- `supported-hardware.md`
- `docs/release/KNOWN_ISSUES.md`
- `CHANGELOG.md`

## Sign-off

- Release manager:
- QA:
- Security:
- Date (UTC):
