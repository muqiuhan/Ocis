# Single Node Throughput Benchmarking Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a repeatable benchmark harness that measures real single-node throughput for both the Ocis storage engine and Ocis TCP server.

**Architecture:** Add a dedicated `Ocis.Perf` console app that runs controlled workloads against either embedded `OcisDB` or remote `Ocis.Server`, computes throughput and latency percentiles, and writes machine-readable CSV/JSON outputs. Keep benchmark logic deterministic with pre-generated payloads and fixed worker concurrency. Add unit tests for core stats calculation to prevent silent benchmark math regressions.

**Tech Stack:** F# (.NET 10), existing Ocis engine/server projects, `System.Diagnostics.Stopwatch`, `System.Threading.Tasks`, `System.Net.Sockets`, NUnit for perf utility tests.

---

### Task 1: Create benchmark project scaffolding

**Files:**
- Create: `Ocis.Perf/Ocis.Perf.fsproj`
- Create: `Ocis.Perf/Program.fs`
- Modify: `Ocis.sln`

**Step 1: Write the failing test**

Create utility tests (in Task 2) that reference types expected from `Ocis.Perf` (for example `LatencyStats`), and run tests to fail due to missing project/types.

**Step 2: Run test to verify it fails**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~LatencyStatsTests"`
Expected: FAIL due to missing project or missing types.

**Step 3: Write minimal implementation**

Create `Ocis.Perf` console project, include project references to `Ocis` and `Ocis.Server`, and add to solution.

**Step 4: Run test to verify it passes project load stage**

Run: `dotnet build Ocis.sln -c Release`
Expected: build succeeds and benchmark project is discoverable.

**Step 5: Commit**

```bash
git add Ocis.Perf/Ocis.Perf.fsproj Ocis.Perf/Program.fs Ocis.sln
git commit -m "feat: add dedicated throughput benchmark harness project"
```

### Task 2: Add tested stats and result primitives (TDD)

**Files:**
- Create: `Ocis.Perf/Stats.fs`
- Create: `Ocis.Perf/Models.fs`
- Create: `Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj`
- Create: `Ocis.Perf.Tests/LatencyStatsTests.fs`
- Modify: `Ocis.sln`

**Step 1: Write the failing test**

Add tests for:
- percentile calculation (P50/P95/P99)
- throughput calculation from operation count and duration
- error rate calculation

**Step 2: Run test to verify it fails**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~LatencyStatsTests"`
Expected: FAIL with missing module/type members.

**Step 3: Write minimal implementation**

Implement deterministic `LatencyStats.compute` and result record types in `Models.fs`.

**Step 4: Run test to verify it passes**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ocis.Perf/Stats.fs Ocis.Perf/Models.fs Ocis.Perf.Tests
git commit -m "feat: add tested throughput and latency stats primitives"
```

### Task 3: Implement engine throughput runner

**Files:**
- Create: `Ocis.Perf/EngineRunner.fs`
- Modify: `Ocis.Perf/Program.fs`

**Step 1: Write the failing test**

Add at least one integration-like test that invokes engine runner with a tiny workload and asserts non-zero operations and non-negative latencies.

**Step 2: Run test to verify it fails**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~EngineRunner"`
Expected: FAIL (runner missing).

**Step 3: Write minimal implementation**

Implement workload loop for embedded `OcisDB` with configurable:
- operation mode (`set`/`get`/`mixed`)
- key count
- payload bytes
- duration seconds
- workers
- durability mode / group commit window

Collect per-op latency samples and aggregate throughput.

**Step 4: Run test to verify it passes**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~EngineRunner"`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ocis.Perf/EngineRunner.fs Ocis.Perf/Program.fs
git commit -m "feat: add embedded engine throughput workload runner"
```

### Task 4: Implement server throughput runner

**Files:**
- Create: `Ocis.Perf/ServerProtocolClient.fs`
- Create: `Ocis.Perf/ServerRunner.fs`
- Modify: `Ocis.Perf/Program.fs`

**Step 1: Write the failing test**

Add parser/serialization tests for request/response packets to verify client correctness.

**Step 2: Run test to verify it fails**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~ServerProtocol"`
Expected: FAIL (client missing).

**Step 3: Write minimal implementation**

Implement long-lived TCP client workers and server workload runner with configurable concurrency and operation mix. Track per-request latency and errors.

**Step 4: Run test to verify it passes**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~ServerProtocol"`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ocis.Perf/ServerProtocolClient.fs Ocis.Perf/ServerRunner.fs Ocis.Perf/Program.fs
git commit -m "feat: add server-side throughput runner with persistent TCP workers"
```

### Task 5: Add reporting outputs and run scripts

**Files:**
- Create: `Ocis.Perf/ReportWriter.fs`
- Create: `scripts/run-throughput-engine.sh`
- Create: `scripts/run-throughput-server.sh`
- Create: `docs/operations/performance-testing.md`

**Step 1: Write the failing test**

Add test asserting CSV output contains expected headers and one data row.

**Step 2: Run test to verify it fails**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~ReportWriter"`
Expected: FAIL.

**Step 3: Write minimal implementation**

Write JSON + CSV results under `BenchmarkDotNet.Artifacts/results/throughput/`.
Add scripts with fixed baseline matrix and repeat-count.

**Step 4: Run test to verify it passes**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj --filter "FullyQualifiedName~ReportWriter"`
Expected: PASS.

**Step 5: Commit**

```bash
git add Ocis.Perf/ReportWriter.fs scripts/run-throughput-engine.sh scripts/run-throughput-server.sh docs/operations/performance-testing.md
git commit -m "feat: add throughput result exporters and execution scripts"
```

### Task 6: Verify full implementation and baseline run

**Files:**
- Modify: `docs/operations/performance-testing.md`

**Step 1: Run tests**

Run: `dotnet test Ocis.Perf.Tests/Ocis.Perf.Tests.fsproj`
Expected: PASS.

**Step 2: Run quick benchmark smoke**

Run: `dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- engine --duration-sec 15 --workers 4 --ops set`
Expected: outputs throughput summary and result files.

**Step 3: Run server benchmark smoke**

Run: `dotnet run --project Ocis.Perf/Ocis.Perf.fsproj -- server --host 127.0.0.1 --port 7379 --duration-sec 15 --workers 16 --ops mixed`
Expected: outputs throughput summary and result files.

**Step 4: Document baseline instructions**

Update doc with exact matrix and interpretation rules.

**Step 5: Commit**

```bash
git add docs/operations/performance-testing.md
git commit -m "docs: define repeatable single-node throughput baseline workflow"
```
