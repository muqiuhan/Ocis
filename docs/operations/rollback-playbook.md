# Ocis Rollback Playbook

## Rollback Triggers

Rollback immediately if any of the following is sustained:

- request failures exceed acceptable threshold
- severe latency regression with queue saturation
- startup/recovery failure after restart
- data consistency alarms from crash-recovery checks

## Immediate Actions

1. Stop new rollout traffic.
2. Route traffic away from the affected instance.
3. Capture logs and metrics snapshot for investigation.

## Rollback Procedure

1. Re-deploy last known good server artifact.
2. Start service with previous stable config values.
3. Run smoke checks:
   - set/get/delete basic flow
   - crash-recovery category tests in environment if feasible
4. Monitor for 15 minutes before restoring full traffic.

## Data Safety Notes

- Keep data directory untouched during binary rollback unless corruption is confirmed.
- If corruption is suspected, isolate data copy before any repair operations.

## Communication Template

- Incident start time
- Trigger symptom
- Rollback version and config
- Current status
- Next update time
