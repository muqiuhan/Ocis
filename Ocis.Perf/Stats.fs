namespace Ocis.Perf

open System

module Stats =
    // Percentile uses nearest-rank over sorted samples.
    let private percentile (sorted: float array) (p: float) =
        if sorted.Length = 0 then
            0.0
        else
            let index = int (Math.Ceiling(p * float sorted.Length)) - 1
            let bounded = min (sorted.Length - 1) (max 0 index)
            sorted[bounded]

    let computeLatencyStats (latenciesMs: float list) : LatencyStats =
        if List.isEmpty latenciesMs then
            { P50Ms = 0.0
              P95Ms = 0.0
              P99Ms = 0.0
              MaxMs = 0.0
              MinMs = 0.0
              MeanMs = 0.0 }
        else
            let sorted = latenciesMs |> List.toArray |> Array.sort
            let mean = Array.average sorted

            { P50Ms = percentile sorted 0.50
              P95Ms = percentile sorted 0.95
              P99Ms = percentile sorted 0.99
              MaxMs = sorted[sorted.Length - 1]
              MinMs = sorted[0]
              MeanMs = mean }

    let summarizeRun
        (target: string)
        (op: string)
        (workers: int)
        (successes: int64)
        (failures: int64)
        (durationSeconds: float)
        (latenciesMs: float list)
        : RunSummary =
        let total = successes + failures

        let errorRate =
            if total = 0L then
                0.0
            else
                float failures / float total

        // Throughput is based on successful operations per second.
        let throughput =
            if durationSeconds <= 0.0 then
                0.0
            else
                float successes / durationSeconds

        { Target = target
          Operation = op
          DurationSeconds = durationSeconds
          Workers = workers
          TotalOperations = total
          SuccessfulOperations = successes
          FailedOperations = failures
          ErrorRate = errorRate
          ThroughputOpsPerSec = throughput
          Latency = computeLatencyStats latenciesMs }
