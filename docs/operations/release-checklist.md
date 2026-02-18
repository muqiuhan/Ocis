# Ocis Release Checklist

## Pre-Release Validation

- [ ] `dotnet build --configuration Release`
- [ ] Fast suite: `dotnet test --settings test.fast.runsettings`
- [ ] Server suite: `dotnet test Ocis.Server.Tests/Ocis.Server.Tests.fsproj`
- [ ] Crash recovery: `dotnet test Ocis.Tests/Ocis.Tests.fsproj --filter "TestCategory=CrashRecovery"`
- [ ] Soak smoke: `bash scripts/soak-smoke.sh`

## Deployment Readiness

- [ ] Target config reviewed (`DurabilityMode=Balanced`, queue/window values)
- [ ] Disk capacity checked for WAL/SSTable growth
- [ ] Monitoring dashboards and alerts active
- [ ] Rollback plan confirmed with on-call owner

## Release Steps

1. Deploy to staging.
2. Run smoke read/write against staging.
3. Deploy to production.
4. Verify metrics and logs for first 15 minutes.

## Post-Release Verification

- [ ] No sustained request failure increase
- [ ] Dispatcher queue depth remains stable
- [ ] No startup/recovery regression alerts
