namespace Ocis.Perf

open System

module Aggregation =
    let private median (values: float list) =
        let sorted = values |> List.sort |> List.toArray

        if sorted.Length = 0 then
            0.0
        else
            let middle = sorted.Length / 2

            if sorted.Length % 2 = 0 then
                (sorted[middle - 1] + sorted[middle]) / 2.0
            else
                sorted[middle]

    let private coefficientOfVariation (values: float list) =
        match values with
        | []
        | [ _ ] -> 0.0
        | _ ->
            let average = values |> List.average

            if average = 0.0 then
                0.0
            else
                let variance =
                    values
                    |> List.averageBy (fun value ->
                        let delta = value - average
                        delta * delta)

                sqrt variance / average

    let summarize (runs: RunSummary list) : AggregateSummary =
        match runs with
        | [] -> invalidArg "runs" "At least one run summary is required for aggregation."
        | _ ->
            let throughputs = runs |> List.map (fun run -> run.ThroughputOpsPerSec)

            { RunCount = runs.Length
              ThroughputMedianOpsPerSec = median throughputs
              ThroughputMinOpsPerSec = throughputs |> List.min
              ThroughputMaxOpsPerSec = throughputs |> List.max
              ThroughputCoefficientOfVariation = coefficientOfVariation throughputs
              LatencyP50MedianMs = runs |> List.map (fun run -> run.Latency.P50Ms) |> median
              LatencyP95MedianMs = runs |> List.map (fun run -> run.Latency.P95Ms) |> median
              LatencyP99MedianMs = runs |> List.map (fun run -> run.Latency.P99Ms) |> median }
