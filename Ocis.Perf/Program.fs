module Ocis.Perf.Program

open System
open System.Globalization
open System.IO

let private toMap(args: string array) =
  // Expect CLI options as '--name value' pairs.
  args
  |> Array.skip 1
  |> Array.chunkBySize 2
  |> Array.choose(fun pair ->
    if pair.Length = 2 && pair[0].StartsWith("--") then
      Some(pair[0], pair[1])
    else
      None)
  |> Map.ofArray

let private getOrDefault map key fallback =
  match Map.tryFind key map with
  | Some value -> value
  | None -> fallback

let private parseInt(value: string) =
  Int32.Parse(value, CultureInfo.InvariantCulture)

let private parseBool (optionName: string) (value: string) =
  match value.ToLowerInvariant() with
  | "true" -> true
  | "false" -> false
  | _ -> failwith $"{optionName} must be true or false"

let private parseTarget(raw: string) =
  match raw.ToLowerInvariant() with
  | "engine" -> Engine
  | "server" -> Server
  | _ -> failwith "First argument must be 'engine' or 'server'"

let private parseOperation(raw: string) =
  match raw.ToLowerInvariant() with
  | "set" -> OperationMode.Set
  | "get" -> OperationMode.Get
  | "mixed" -> OperationMode.Mixed
  | _ -> failwith "--ops must be one of: set|get|mixed"

let private buildConfig(args: string array) =
  if args.Length = 0 then
    failwith
      "Usage: dotnet run --project Ocis.Perf -- <engine|server> [--key value ...]"

  let kv = toMap args
  let target = parseTarget args[0]
  let workersDefault = if target = Engine then "1" else "8"
  let dataDirDefault =
    Path.Combine("BenchmarkDotNet.Artifacts", "throughput-data")

  let outputDirDefault =
    Path.Combine("BenchmarkDotNet.Artifacts", "results", "throughput")

  {Target = target
   Operation = parseOperation(getOrDefault kv "--ops" "mixed")
   DurationSeconds = parseInt(getOrDefault kv "--duration-sec" "30")
   WarmupSeconds = parseInt(getOrDefault kv "--warmup-sec" "5")
   RepeatCount = parseInt(getOrDefault kv "--repeat" "3")
   Workers = parseInt(getOrDefault kv "--workers" workersDefault)
   AllowUnsafeEngineConcurrency =
      parseBool
        "--allow-unsafe-engine-concurrency"
        (getOrDefault kv "--allow-unsafe-engine-concurrency" "false")
   KeyCount = parseInt(getOrDefault kv "--key-count" "50000")
   ValueBytes = parseInt(getOrDefault kv "--value-bytes" "256")
   DataDir = getOrDefault kv "--data-dir" dataDirDefault
   Host = getOrDefault kv "--host" "127.0.0.1"
   Port = parseInt(getOrDefault kv "--port" "7379")
   FlushThreshold = parseInt(getOrDefault kv "--flush-threshold" "1000")
   DurabilityMode = getOrDefault kv "--durability-mode" "Balanced"
   GroupCommitWindowMs =
      parseInt(getOrDefault kv "--group-commit-window-ms" "1")
   GroupCommitBatchSize =
      parseInt(getOrDefault kv "--group-commit-batch-size" "10")
   ClearCacheBeforeRun =
      parseBool "--clear-cache" (getOrDefault kv "--clear-cache" "false")
   ColdStart = parseBool "--cold-start" (getOrDefault kv "--cold-start" "false")
   PreloadKeyCount = parseInt(getOrDefault kv "--preload-key-count" "0")
   SkipPreload =
      parseBool "--skip-preload" (getOrDefault kv "--skip-preload" "false")
   OutputDir = getOrDefault kv "--output-dir" outputDirDefault
   OutputTag = getOrDefault kv "--tag" "baseline"}

let private validateEngineWorkers(config: BenchmarkConfig) =
  // Engine target is intentionally single-threaded by default to honor the
  // storage engine thread-affinity contract.
  if
    config.Target = Engine
    && config.Workers > 1
    && not config.AllowUnsafeEngineConcurrency
  then
    failwith
      "Engine benchmark runs in strict single-thread mode and requires workers=1. For diagnostic experiments only, pass --allow-unsafe-engine-concurrency true."

  if config.WarmupSeconds < 0 then
    failwith "--warmup-sec must be >= 0"

  if config.RepeatCount < 1 then
    failwith "--repeat must be >= 1"

  if config.GroupCommitWindowMs <= 0 then
    failwith "--group-commit-window-ms must be > 0"

  if config.GroupCommitBatchSize <= 0 then
    failwith "--group-commit-batch-size must be > 0"

  if config.PreloadKeyCount < 0 then
    failwith "--preload-key-count must be >= 0"

  config

let parseAndValidateConfig(args: string array) =
  args |> buildConfig |> validateEngineWorkers

let private runOnce(config: BenchmarkConfig) =
  match config.Target with
  | Engine -> EngineRunner.run config
  | Server -> ServerRunner.run config

[<EntryPoint>]
let main args =
  try
    let config = parseAndValidateConfig args

    if config.Target = Engine then
      if Directory.Exists(config.DataDir) then
        Directory.Delete(config.DataDir, true)

    if config.WarmupSeconds > 0 then
      // Warmup runs with the same workload shape but does not count toward
      // reported runs, which reduces first-run effects.
      let warmupConfig =
        {config with
            DurationSeconds = config.WarmupSeconds
            WarmupSeconds = 0}

      let warmupSummary = runOnce warmupConfig
      printfn "Warmup complete: %.3f ops/s" warmupSummary.ThroughputOpsPerSec

    let runResults =
      [for runIndex in 1 .. config.RepeatCount do
         let runSummary = runOnce config

         let fileSuffix =
           if config.RepeatCount > 1 then
             Some(sprintf "run-%02d" runIndex)
           else
             None

         let jsonPath, csvPath =
           ReportWriter.writeWithSuffix config runSummary fileSuffix
         printfn
           "Run %d/%d throughput(ops/s): %.3f"
           runIndex
           config.RepeatCount
           runSummary.ThroughputOpsPerSec
         printfn "Run %d JSON: %s" runIndex jsonPath
         printfn "Run %d CSV: %s" runIndex csvPath
         runSummary, jsonPath, csvPath ]

    let runSummaries =
      runResults
      |> List.map(fun (summary, _, _) -> summary)

    let aggregate = Aggregation.summarize runSummaries
    let summaryPath = ReportWriter.writeSummary config aggregate

    let primarySummary, primaryJsonPath, primaryCsvPath =
      runResults[0]

    printfn "Target: %s" primarySummary.Target
    printfn "Operation: %s" primarySummary.Operation
    printfn "Workers: %d" primarySummary.Workers
    printfn "Duration(s): %.3f" primarySummary.DurationSeconds

    if config.RepeatCount = 1 then
      printfn "Throughput(ops/s): %.3f" primarySummary.ThroughputOpsPerSec
      printfn "ErrorRate: %.5f" primarySummary.ErrorRate

      printfn
        "Latency p50/p95/p99 (ms): %.3f / %.3f / %.3f"
        primarySummary.Latency.P50Ms
        primarySummary.Latency.P95Ms
        primarySummary.Latency.P99Ms

      printfn "Result JSON: %s" primaryJsonPath
      printfn "Result CSV: %s" primaryCsvPath
    else
      printfn
        "Median throughput(ops/s): %.3f"
        aggregate.ThroughputMedianOpsPerSec

      printfn
        "Throughput min/max(ops/s): %.3f / %.3f"
        aggregate.ThroughputMinOpsPerSec
        aggregate.ThroughputMaxOpsPerSec

      printfn "Throughput CV: %.6f" aggregate.ThroughputCoefficientOfVariation

      printfn
        "Median latency p50/p95/p99 (ms): %.3f / %.3f / %.3f"
        aggregate.LatencyP50MedianMs
        aggregate.LatencyP95MedianMs
        aggregate.LatencyP99MedianMs

    printfn "Aggregate summary JSON: %s" summaryPath
    0
  with ex ->
    eprintfn "Benchmark failed: %s" ex.Message
    1
