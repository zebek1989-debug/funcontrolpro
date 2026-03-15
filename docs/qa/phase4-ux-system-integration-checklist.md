# Phase 4 UX/System Integration Checklist

Use this checklist to sign off milestone 4.1, 4.2, and 4.3.

## 4.1 System Tray and Autostart

- [ ] App can start with `--start-minimized` and remain available from tray.
- [ ] Tray actions work: `Show`, `Hide`, `Emergency Full Speed`, `Exit`.
- [ ] Tray profile switch changes active profile in dashboard.
- [ ] Closing window with `MinimizeToTrayOnClose=true` hides app to tray.
- [ ] Task Scheduler autostart task can be created and removed without error.
- [ ] Start delay is honored when configured (`StartupDelaySeconds`).

## 4.2 Onboarding and Hardware Classification

- [ ] First run shows onboarding flow instead of dashboard.
- [ ] Risk consent is required only when Full Control is possible.
- [ ] Without consent, Full Control path cannot be activated.
- [ ] Revoking consent returns app to Monitoring Only behavior.
- [ ] Empty states explain missing admin rights/vendor conflicts clearly.

## 4.3 Application Settings

- [ ] Saving valid settings applies immediately without restart.
- [ ] Invalid settings are rejected with readable messages.
- [ ] Reset restores defaults and keeps app stable.
- [ ] Theme `System` follows OS theme behavior.
- [ ] Settings persist after app restart.

## Automated Validation (local)

Run:

```powershell
pwsh -NoProfile -File scripts/qa/Validate-Phase4Readiness.ps1
```

Generated report:

- `artifacts/qa/phase4-readiness-report.md`

## Manual Windows Validation

Run elevated app launcher:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\run\Run-PresentationAdmin.ps1" -DisableHardwareAccess
```

Then validate tray, onboarding, and settings behavior against the checklist above.
