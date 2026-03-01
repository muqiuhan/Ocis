namespace Ocis.Perf

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Text
open System.Threading
open System.Threading.Tasks

module ServerRunner =
  let private createPayload(size: int) =
    let bytes = Array.create size (byte 's')

    if size > 0 then
      bytes[0] <- byte 'v'

    bytes

  let private makeKeys count =
    Array.init count (fun i -> Encoding.UTF8.GetBytes($"srv-key-{i:D8}"))

  let private warmupData
    (config: BenchmarkConfig)
    (keys: byte array array)
    (payload: byte array)
    =
    use client = new ServerProtocolClient(config.Host, config.Port)

    for key in keys do
      client.Set(key, payload) |> ignore

  let run(config: BenchmarkConfig) : RunSummary =
    let keys = makeKeys config.KeyCount
    let payload = createPayload config.ValueBytes

    if
      config.Operation = OperationMode.Get
      || config.Operation = OperationMode.Mixed
    then
      warmupData config keys payload

    let mutable successCount = 0L
    let mutable failureCount = 0L
    let indexCounter = ref 0
    let latencies = ConcurrentQueue<float>()

    let swGlobal = Stopwatch.StartNew()
    let endAt =
      swGlobal.Elapsed
      + TimeSpan.FromSeconds(float config.DurationSeconds)

    // One protocol client per worker avoids shared-socket contention and
    // better reflects concurrent client behavior.
    let workers =
      [|for workerId in 0 .. config.Workers - 1 do
          Task.Run(fun () ->
            use client = new ServerProtocolClient(config.Host, config.Port)
            let rng = Random(3000 + workerId)

            while swGlobal.Elapsed < endAt do
              let keyIndex = Interlocked.Increment(indexCounter) % keys.Length
              let key = keys[keyIndex]
              let opStart = Stopwatch.GetTimestamp()

              let ok =
                match config.Operation with
                | OperationMode.Set -> client.Set(key, payload)
                | OperationMode.Get -> client.Get(key)
                | OperationMode.Mixed ->
                  // Mixed mode approximates read-heavy workloads.
                  if rng.NextDouble() < 0.7 then
                    client.Get(key)
                  else
                    client.Set(key, payload)

              let elapsedMs =
                Stopwatch.GetElapsedTime(opStart).TotalMilliseconds
              latencies.Enqueue elapsedMs

              if ok then
                Interlocked.Increment(&successCount) |> ignore
              else
                Interlocked.Increment(&failureCount) |> ignore) |]

    Task.WaitAll workers
    swGlobal.Stop()

    let opName =
      match config.Operation with
      | OperationMode.Set -> "set"
      | OperationMode.Get -> "get"
      | OperationMode.Mixed -> "mixed"

    Stats.summarizeRun
      "server"
      opName
      config.Workers
      successCount
      failureCount
      swGlobal.Elapsed.TotalSeconds
      (latencies |> Seq.toList)
