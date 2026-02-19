#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "[engine-throughput] Starting baseline matrix"

for MODE in Balanced Strict Fast; do
  for OP in set get mixed; do
    echo "[engine-throughput] mode=${MODE} op=${OP}"
    dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- \
      engine \
      --ops "$OP" \
      --workers 1 \
      --warmup-sec 5 \
      --repeat 3 \
      --duration-sec 30 \
      --key-count 50000 \
      --value-bytes 256 \
      --durability-mode "$MODE" \
      --group-commit-window-ms 5 \
      --tag "engine-${MODE}-${OP}"
  done
done

echo "[engine-throughput] Completed baseline matrix"
