module Ocis.Perf.Tests.ReportWriterTests

open System
open System.IO
open NUnit.Framework
open Ocis.Perf

[<TestFixture>]
type ReportWriterTests() =
  let outputDir =
    Path.Combine(Path.GetTempPath(), "ocis_perf_report_tests")

  [<SetUp>]
  member _.SetUp() =
    if Directory.Exists outputDir then
      Directory.Delete(outputDir, true)

    Directory.CreateDirectory outputDir |> ignore

  [<TearDown>]
  member _.TearDown() =
    if Directory.Exists outputDir then
      Directory.Delete(outputDir, true)

  [<Test>]
  member _.WriteCreatesCsvAndJsonWithExpectedHeader() =
    let config =
      {Target = Engine
       Operation = OperationMode.Mixed
       DurationSeconds = 10
       Workers = 4
       AllowUnsafeEngineConcurrency = false
       KeyCount = 100
       ValueBytes = 32
       DataDir = "tmp"
       Host = "127.0.0.1"
       Port = 7379
       FlushThreshold = 1000
       DurabilityMode = "Balanced"
       GroupCommitWindowMs = 5
       GroupCommitBatchSize = 64
       ClearCacheBeforeRun = false
       ColdStart = false
       PreloadKeyCount = 0
       SkipPreload = false
       OutputDir = outputDir
       OutputTag = "test"
       WarmupSeconds = 0
       RepeatCount = 1}

    let summary =
      {Target = "engine"
       Operation = "mixed"
       DurationSeconds = 10.0
       Workers = 4
       TotalOperations = 1000L
       SuccessfulOperations = 990L
       FailedOperations = 10L
       ErrorRate = 0.01
       ThroughputOpsPerSec = 99.0
       Latency =
          {P50Ms = 1.0
           P95Ms = 5.0
           P99Ms = 8.0
           MaxMs = 10.0
           MinMs = 0.5
           MeanMs = 2.3}}

    let jsonPath, csvPath = ReportWriter.write config summary

    Assert.That(File.Exists jsonPath, Is.True)
    Assert.That(File.Exists csvPath, Is.True)

    let firstLine = File.ReadLines(csvPath) |> Seq.head

    Assert.That(
      firstLine,
      Is.EqualTo(
        "target,operation,duration_sec,workers,total_ops,success_ops,failed_ops,error_rate,throughput_ops_sec,p50_ms,p95_ms,p99_ms,mean_ms,min_ms,max_ms"
      )
    )

  [<Test>]
  member _.WriteSummaryCreatesJsonFile() =
    let config =
      {Target = Engine
       Operation = OperationMode.Mixed
       DurationSeconds = 10
       Workers = 4
       AllowUnsafeEngineConcurrency = false
       KeyCount = 100
       ValueBytes = 32
       DataDir = "tmp"
       Host = "127.0.0.1"
       Port = 7379
       FlushThreshold = 1000
       DurabilityMode = "Balanced"
       GroupCommitWindowMs = 5
       GroupCommitBatchSize = 64
       ClearCacheBeforeRun = false
       ColdStart = false
       PreloadKeyCount = 0
       SkipPreload = false
       OutputDir = outputDir
       OutputTag = "test"
       WarmupSeconds = 0
       RepeatCount = 3}

    let aggregate =
      {RunCount = 3
       ThroughputMedianOpsPerSec = 100.0
       ThroughputMinOpsPerSec = 90.0
       ThroughputMaxOpsPerSec = 110.0
       ThroughputCoefficientOfVariation = 0.0816
       LatencyP50MedianMs = 1.0
       LatencyP95MedianMs = 4.0
       LatencyP99MedianMs = 8.0}

    let summaryPath = ReportWriter.writeSummary config aggregate

    Assert.That(File.Exists summaryPath, Is.True)
