# Phase 5 Hardware/Performance Checklist

Use this checklist to sign off milestone 5.1 and 5.2.

## 5.1 Compatibility Matrix

- [ ] `hardware-matrix.csv` includes at least 3 `FullControl` targets.
- [ ] `hardware-matrix.csv` includes at least 10 `MonitoringOnly` targets.
- [ ] No duplicated `ConfigurationId` values.
- [ ] Each executed target has evidence package (screenshots + support bundle + notes).
- [ ] `ValidationStatus` is updated after each run (`Validated`, `MonitoringOnlyValidated`, etc.).

## 5.1 Vendor Conflict Scenarios

- [ ] Scenarios from `vendor-conflict-test-matrix.md` executed at least once.
- [ ] Conflict warning appears in onboarding/runtime.
- [ ] Conflict event appears in diagnostics logs.
- [ ] No unsafe write attempts during conflict mode.

## 5.2 Performance/Stress

- [ ] `ProfileSwitchStressTests` pass.
- [ ] Integration tests pass.
- [ ] At least one soak run artifact exists in `artifacts/perf/<timestamp>/`.
- [ ] `summary.json` from soak run is within budget (RAM/error-line limits).
- [ ] `performance-baselines.csv` updated with latest run.

## Automated Readiness Check

```powershell
pwsh -NoProfile -File scripts/qa/Validate-Phase5Readiness.ps1
```

Strict mode (requires validated matrix rows):

```powershell
pwsh -NoProfile -File scripts/qa/Validate-Phase5Readiness.ps1 -RequireValidatedMatrix
```

Generated report:

- `artifacts/qa/phase5-readiness-report.md`
