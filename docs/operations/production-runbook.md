# Ocis Production Runbook

## Purpose

Operate a single-instance Ocis deployment with predictable durability and recovery behavior.

## Baseline Configuration

- `DurabilityMode`: `Balanced`
- `GroupCommitWindowMs`: `5`
- `DbQueueCapacity`: `8192`
- `CheckpointMinIntervalMs`: `30000`

Use CLI flags in `Ocis.Server/Program.fs` to override per environment.

## Start

```bash
dotnet run --project Ocis.Server/Ocis.Server.fsproj -- --dir <data-dir> --host 0.0.0.0 --port 5000
```

## Health Checks

Before routing traffic, verify:

- process is running and listener is bound
- no startup errors in logs
- crash-recovery tests are green in CI

## Key Operational Signals

- request total / failed request counters
- request duration histogram
- dispatcher queue depth gauge
- WAL growth trend and checkpoint frequency

## Incident Triage

1. Check server logs for startup/shutdown faults and connection loop errors.
2. Check dispatcher queue depth and request failure rate.
3. If recovery related, inspect WAL/SSTable files and run crash-recovery tests.
4. If latency spikes, reduce load and inspect queue depth plus compaction/flush behavior.

## Graceful Stop

1. Stop accepting new traffic upstream.
2. Stop Ocis process.
3. Confirm shutdown logs include dispatcher and server stop completion.
4. Restart and verify read/write smoke checks.
