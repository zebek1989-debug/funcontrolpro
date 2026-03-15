# Vendor Conflict Test Matrix (Milestone 5.1)

Last updated: 2026-03-15

## Goal

Verify that FanControl Pro detects active vendor fan-control tools, warns the user, and preserves safe behavior.

## Expected Global Behavior

- Conflict warning visible in onboarding or runtime notification path.
- Recommendation to stay in `Monitoring Only`.
- Conflict event written to diagnostics log/support bundle.
- No uncontrolled write attempts while conflict is active.

## Test Matrix

| Scenario ID | Vendor Tool | Version | Service/Process Token | Test Steps | Expected Result | Status | Notes |
|---|---|---|---|---|---|---|---|
| VC-001 | ASUS Armoury Crate | TBD | `armourycrate` | Start tool, launch FanControl Pro, open onboarding/dashboard | Conflict warning displayed, control path not silently forced | Planned |  |
| VC-002 | ASUS AI Suite / FanXpert | TBD | `aisuite` / `fanxpert` | Start tool, apply fan change there, observe FanControl Pro | Warning + diagnostics event, telemetry still stable | Planned |  |
| VC-003 | MSI Center | TBD | `msicenter` | Start tool, launch app | Warning shown, recommendation to Monitoring Only | Planned |  |
| VC-004 | MSI Dragon Center | TBD | `dragoncenter` | Same as VC-003 | Warning + log entry | Planned |  |
| VC-005 | Gigabyte Control Center | TBD | `gigabytecontrolcenter` | Start tool, launch app | Warning + log entry | Planned |  |
| VC-006 | AORUS Engine | TBD | `aorus` | Start tool, launch app | Warning + log entry | Planned |  |
| VC-007 | Corsair iCUE | TBD | `icue` | Start tool, launch app | Warning for potential overlap | Planned |  |
| VC-008 | NZXT CAM | TBD | `nzxtcam` | Start tool, launch app | Warning for potential overlap | Planned |  |
| VC-009 | Multiple tools active | Mixed | multiple | Run 2+ tools simultaneously | Single deduplicated warning + clear details | Planned |  |

## Verification Checklist

For each scenario:
- Confirm process token detected.
- Confirm warning text includes tool/process context.
- Confirm no notification spam (dedup/cooldown works).
- Confirm diagnostic timeline contains conflict record.
- Confirm support bundle includes log evidence.

## Exit Criteria

- All scenarios executed at least once on real Windows targets.
- No false positive conflicts for clean systems.
- No false negative conflicts for listed vendor tools.
