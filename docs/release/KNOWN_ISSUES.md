# Known Issues (RC Track)

Last updated: 2026-03-15

## Functional and compatibility limits

1. Hardware matrix is still expanding; not all motherboard variants are validated hardware-in-the-loop.
2. Some OEM boards expose telemetry without safe writable fan channels (`Monitoring Only` expected).
3. Vendor fan suites running in parallel can interfere with control consistency.

## Operational limits

1. MSI build and lifecycle checks require Windows environment (`windows-latest` pipeline or local Windows host).
2. 24h soak metrics are still collected per target hardware profile and may vary by driver stack.

## Workarounds

1. If control stability is uncertain, switch profile to safer preset or `Monitoring Only`.
2. Disable parallel vendor fan tools (Armoury Crate / MSI Center / GCC / iCUE / CAM) during tests.
3. Attach support bundle with every RC issue for fast triage.

## Escalation rules

- Safety/security risk: classify as `P0` immediately.
- Major regression without workaround: classify as `P1`.
- Minor UX/diagnostic issues: classify as `P2`.
