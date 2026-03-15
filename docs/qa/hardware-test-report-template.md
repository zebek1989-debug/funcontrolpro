# Hardware Validation Report Template

Use one report per tested configuration.

## Metadata

- Configuration ID:
- Date (UTC):
- Tester:
- Branch/Commit:
- Build version:

## Platform

- Motherboard:
- BIOS version:
- CPU:
- GPU:
- RAM:
- OS version:

## Support Classification Result

- Expected support target:
- Observed support level(s):
- Classification verdict:

## Telemetry Cross-Check (FanControl Pro vs HWiNFO64)

| Metric | FanControl Pro | HWiNFO64 | Delta | Pass/Fail |
|---|---:|---:|---:|---|
| CPU Temp |  |  |  |  |
| GPU Temp |  |  |  |  |
| Fan RPM |  |  |  |  |

## Control Validation (Full Control only)

| Channel | Setpoint | Observed RPM delta | Response Time | Pass/Fail | Notes |
|---|---:|---:|---:|---|---|
|  | 30% |  |  |  |  |
|  | 50% |  |  |  |  |
|  | 70% |  |  |  |  |
|  | 100% |  |  |  |  |

## Safety / Recovery Checks

- Sensor fault simulation:
- Failsafe entered:
- Recovery behavior:
- Logs/support bundle attached:

## Vendor Conflict Checks

- Tool(s) active during test:
- Warning shown:
- Recommendation shown:
- Diagnostics event present:

## Final Verdict

- Result: `Validated` / `MonitoringOnlyValidated` / `UnsupportedValidated` / `Rejected`
- Required follow-ups:
- Known issues:
