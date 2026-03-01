# Ocis Performance Testing

## Goal

Measure single-node throughput and latency for:

- embedded storage engine (`OcisDB`)
- TCP server (`Ocis.Server`)

All benchmark outputs are written to `BenchmarkDotNet.Artifacts/results/throughput/` as JSON and CSV.
When `--repeat` is greater than `1`, per-run artifacts are emitted with run suffixes and one aggregate JSON summary is emitted with medians and variability statistics.

## Build

```bash
dotnet build Ocis.sln -c Release
dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj
```

## Quick Smoke Runs

### Engine

```bash
dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- \
  engine --ops mixed --workers 1 --warmup-sec 3 --repeat 2 --duration-sec 15 --key-count 20000 --value-bytes 256 --durability-mode Balanced --tag smoke-engine
```

### Server

Start server first (example):

```bash
dotnet run --project Ocis.Server/Ocis.Server.fsproj -- \
  --dir ./tmp-perf-db --host 127.0.0.1 --port 7379 --durability-mode Balanced --group-commit-window-ms 5
```

Then run benchmark:

```bash
dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- \
  server --host 127.0.0.1 --port 7379 --ops mixed --workers 32 --warmup-sec 3 --repeat 2 --duration-sec 15 --key-count 20000 --value-bytes 256 --tag smoke-server
```

## Baseline Matrix

### Engine baseline

```bash
bash scripts/run-throughput-engine.sh
```

Matrix:

- durability: `Balanced`, `Strict`, `Fast`
- operation: `set`, `get`, `mixed`
- workers: `1` (strict single-thread baseline requirement)
- warmup: `5s`
- repeat: `3`
- duration: `30s`

Engine benchmarks fail fast when `--workers` is greater than `1` unless `--allow-unsafe-engine-concurrency true` is explicitly provided for diagnostic-only experiments.

### Server baseline

```bash
bash scripts/run-throughput-server.sh 127.0.0.1 7379
```

Matrix:

- operation: `set`, `get`, `mixed`
- workers: `8`, `32`, `128`
- warmup: `5s`
- repeat: `3`
- duration: `30s`

## Metrics Definition

- `throughput_ops_sec`: successful operations per second
- `error_rate`: failed operations / total operations
- `p50_ms`, `p95_ms`, `p99_ms`: operation latency percentiles

Aggregate summary JSON contains:

- `median_throughput_ops_sec`
- `median_latency_p50_ms`
- `median_latency_p95_ms`
- `median_latency_p99_ms`
- `throughput_min_ops_sec`
- `throughput_max_ops_sec`
- `throughput_cv` (coefficient of variation across repeats)

## Reproducibility Rules

- Keep machine and disk type constant for comparison runs.
- Do not run heavy background workloads during benchmark.
- Run each command at least 3 times and compare median throughput.
- Always record git commit hash with benchmark artifact set.

## Interpreting Results

- Use `Balanced` engine/server results as primary production baseline.
- `Strict` indicates durability cost envelope.
- `Fast` indicates best-case upper bound without per-op durability wait.
- For server, use highest worker count that does not cause sustained error growth.

## Advanced Options

### Group Commit Parameters

For `Balanced` durability mode, the following parameters control group commit behavior:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--group-commit-window-ms` | `1` | Time window (in ms) to collect writes before flushing |
| `--group-commit-batch-size` | `10` | Number of writes to accumulate before triggering flush |

**Tuning guidance:**
- Smaller window/batch = lower latency, lower throughput
- Larger window/batch = higher latency, higher throughput
- For single-threaded engine: `1ms/10` is optimal
- For multi-threaded server: `5ms/64` may be better

### Cache and Cold Start Control

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--clear-cache` | `false` | Clear OS page cache before run (Linux only, requires sudo) |
| `--cold-start` | `false` | Delete data directory before each run |
| `--skip-preload` | `false` | Skip preloading keys for GET tests |
| `--preload-key-count` | `0` | Number of keys to preload (0 = all) |

**Testing real disk I/O:**

```bash
# Test with fresh data directory (no warm cache)
dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- \
  engine --ops set --durability-mode Strict --cold-start true \
  --duration-sec 30 --key-count 100000 --tag cold-strict

# Test without preloading (simulates cache miss)
dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- \
  engine --ops get --skip-preload --cold-start true \
  --duration-sec 30 --key-count 100000 --tag cold-get
```

**Note:** `--clear-cache` requires root/sudo access on Linux to write to `/proc/sys/vm/drop_caches`.
