#!/usr/bin/env bash
set -euo pipefail

echo "[soak-smoke] Running crash-recovery smoke loop"

for i in 1 2 3; do
  echo "[soak-smoke] Iteration ${i}/3"
  dotnet test Ocis.Tests/Ocis.Tests.fsproj --no-build --no-restore --verbosity minimal --filter "TestCategory=CrashRecovery"
done

echo "[soak-smoke] Completed crash-recovery smoke loop"
