# QA Assets (Phase 5)

This folder contains QA documentation and datasets for milestone 5.x.

## Files

- `hardware-matrix.csv` - machine-readable platform matrix.
- `hardware-validation-checklist.md` - full validation procedure.
- `asus-z490p-superio-write-checklist.md` - P1 checklist for safe Super I/O (Nuvoton) write enablement on ASUS Z490-P.
- `vendor-conflict-test-matrix.md` - vendor conflict scenarios.
- `hardware-test-report-template.md` - per-platform report template.
- `performance-soak-playbook.md` - soak/performance runbook.
- `performance-baselines.csv` - baseline metrics registry.
- `performance-run-template.md` - report template for each perf run.
- `phase4-ux-system-integration-checklist.md` - sign-off checklist for milestone 4.x (tray/onboarding/settings).
- `phase5-hardware-performance-checklist.md` - sign-off checklist for compatibility matrix + performance baseline.

## Matrix Validation Script

Use PowerShell:

```powershell
pwsh -NoProfile -File scripts/compatibility/Validate-HardwareMatrix.ps1
```

Optional strict mode (requires minimum validated rows):

```powershell
pwsh -NoProfile -File scripts/compatibility/Validate-HardwareMatrix.ps1 -RequireValidated
```

## Soak Script (Phase 5.2)

```powershell
pwsh -NoProfile -File scripts/performance/Run-Phase52Soak.ps1 `
  -ProjectPath src/FanControlPro.Presentation/FanControlPro.Presentation.csproj `
  -DurationHours 24 `
  -SampleIntervalSeconds 30 `
  -OutputDirectory artifacts/perf
```

## Phase 4 Readiness Script

```powershell
pwsh -NoProfile -File scripts/qa/Validate-Phase4Readiness.ps1
```

## Phase 5 Readiness Script

```powershell
pwsh -NoProfile -File scripts/qa/Validate-Phase5Readiness.ps1
```

Strict matrix gate:

```powershell
pwsh -NoProfile -File scripts/qa/Validate-Phase5Readiness.ps1 -RequireValidatedMatrix
```
