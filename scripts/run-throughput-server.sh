#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

HOST="${1:-127.0.0.1}"
PORT="${2:-7379}"

echo "[server-throughput] Using target ${HOST}:${PORT}"
echo "[server-throughput] Starting baseline matrix"

for OP in set get mixed; do
  for WORKERS in 8 32 128; do
    echo "[server-throughput] op=${OP} workers=${WORKERS}"
    dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- \
      server \
      --host "$HOST" \
      --port "$PORT" \
      --ops "$OP" \
      --workers "$WORKERS" \
      --warmup-sec 5 \
      --repeat 3 \
      --duration-sec 30 \
      --key-count 50000 \
      --value-bytes 256 \
      --tag "server-${OP}-w${WORKERS}"
  done
done

echo "[server-throughput] Completed baseline matrix"
