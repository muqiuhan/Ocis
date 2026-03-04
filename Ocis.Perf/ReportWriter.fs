namespace Ocis.Perf

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json

module ReportWriter =
  let private formatFloat (value : float) =
    value.ToString ("0.###", CultureInfo.InvariantCulture)

  let writeWithSuffix
    (config : BenchmarkConfig)
    (summary : RunSummary)
    (fileSuffix : string option)
    =
    Directory.CreateDirectory (config.OutputDir)
    |> ignore

    let timestamp =
      DateTime.UtcNow.ToString ("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)

    let filePrefix =
      let basePrefix =
        $"{summary.Target}-{summary.Operation}-{config.OutputTag}-{timestamp}"

      match fileSuffix with
      | Some suffix when not (String.IsNullOrWhiteSpace suffix) ->
        $"{basePrefix}-{suffix}"
      | _ -> basePrefix

    let jsonPath =
      Path.Combine (config.OutputDir, $"{filePrefix}.json")

    let csvPath =
      Path.Combine (config.OutputDir, $"{filePrefix}.csv")

    let json =
      JsonSerializer.Serialize (
        summary,
        JsonSerializerOptions (
          WriteIndented = true,
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        )
      )

    File.WriteAllText (jsonPath, json)

    // Keep CSV schema stable for downstream scripts.
    let csvHeader =
      "target,operation,duration_sec,workers,total_ops,success_ops,failed_ops,error_rate,throughput_ops_sec,p50_ms,p95_ms,p99_ms,mean_ms,min_ms,max_ms"

    let csvLine =
      String.Join (
        ",",
        [| summary.Target
           summary.Operation
           formatFloat summary.DurationSeconds
           string summary.Workers
           string summary.TotalOperations
           string summary.SuccessfulOperations
           string summary.FailedOperations
           formatFloat summary.ErrorRate
           formatFloat summary.ThroughputOpsPerSec
           formatFloat summary.Latency.P50Ms
           formatFloat summary.Latency.P95Ms
           formatFloat summary.Latency.P99Ms
           formatFloat summary.Latency.MeanMs
           formatFloat summary.Latency.MinMs
           formatFloat summary.Latency.MaxMs |]
      )

    File.WriteAllText (
      csvPath,
      csvHeader
      + Environment.NewLine
      + csvLine
      + Environment.NewLine,
      Encoding.UTF8
    )

    jsonPath, csvPath

  let write (config : BenchmarkConfig) (summary : RunSummary) =
    writeWithSuffix config summary None

  let writeSummary (config : BenchmarkConfig) (summary : AggregateSummary) =
    Directory.CreateDirectory (config.OutputDir)
    |> ignore

    let timestamp =
      DateTime.UtcNow.ToString ("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)

    let summaryPath =
      Path.Combine (
        config.OutputDir,
        $"aggregate-{config.OutputTag}-{timestamp}.json"
      )

    let json =
      JsonSerializer.Serialize (
        summary,
        JsonSerializerOptions (
          WriteIndented = true,
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        )
      )

    File.WriteAllText (summaryPath, json)
    summaryPath
