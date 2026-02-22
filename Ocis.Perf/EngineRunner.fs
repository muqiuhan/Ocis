namespace Ocis.Perf

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Text
open System.Threading
open System.Threading.Tasks
open Ocis.OcisDB

module EngineRunner =
    let private createPayload (size: int) =
        let bytes = Array.create size (byte 'x')
        if size > 0 then
            bytes[0] <- byte 'v'
        bytes

    let private makeKeys count =
        Array.init count (fun i -> Encoding.UTF8.GetBytes($"perf-key-{i:D8}"))

    let private preloadForReads (db: OcisDB) (keys: byte array array) (value: byte array) =
        for key in keys do
            db.Set(key, value) |> ignore

    let run (config: BenchmarkConfig) : RunSummary =
        let keys = makeKeys config.KeyCount
        let payload = createPayload config.ValueBytes

        use db =
            match
                OcisDB.Open(
                    config.DataDir,
                    config.FlushThreshold,
                    durabilityMode = config.DurabilityMode,
                    groupCommitWindowMs = config.GroupCommitWindowMs
                )
            with
            | Ok opened -> opened
            | Error msg -> failwith $"Failed to open OcisDB: {msg}"

        if config.Operation = OperationMode.Get || config.Operation = OperationMode.Mixed then
            preloadForReads db keys payload

        let mutable successCount = 0L
        let mutable failureCount = 0L
        let indexCounter = ref 0
        let latencies = ConcurrentQueue<float>()

        let swGlobal = Stopwatch.StartNew()
        let endAt = swGlobal.Elapsed + TimeSpan.FromSeconds(float config.DurationSeconds)

        // Each worker loops until duration expires and records per-op latency.
        let runWorker (rng: Random) =
            while swGlobal.Elapsed < endAt do
                let keyIndex = Interlocked.Increment(indexCounter) % keys.Length
                let key = keys[keyIndex]
                let opStart = Stopwatch.GetTimestamp()

                let ok =
                    match config.Operation with
                    | OperationMode.Set ->
                        db.Set(key, payload).IsOk
                    | OperationMode.Get ->
                        db.Get(key).IsOk
                    | OperationMode.Mixed ->
                        // Mixed mode approximates read-heavy workloads.
                        if rng.NextDouble() < 0.7 then
                            db.Get(key).IsOk
                        else
                            db.Set(key, payload).IsOk

                let elapsedMs = Stopwatch.GetElapsedTime(opStart).TotalMilliseconds
                latencies.Enqueue elapsedMs

                if ok then
                    Interlocked.Increment(&successCount) |> ignore
                else
                    Interlocked.Increment(&failureCount) |> ignore

        if config.Workers = 1 then
            runWorker (Random(1000))
        else
            let workers =
                [| for workerId in 0 .. config.Workers - 1 do
                       Task.Run(fun () -> runWorker (Random(1000 + workerId))) |]

            Task.WaitAll workers
        swGlobal.Stop()

        let opName =
            match config.Operation with
            | OperationMode.Set -> "set"
            | OperationMode.Get -> "get"
            | OperationMode.Mixed -> "mixed"

        Stats.summarizeRun
            "engine"
            opName
            config.Workers
            successCount
            failureCount
            swGlobal.Elapsed.TotalSeconds
            (latencies |> Seq.toList)
