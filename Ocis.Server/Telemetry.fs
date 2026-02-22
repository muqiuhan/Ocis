module Ocis.Server.Telemetry

open System
open System.Diagnostics.Metrics
open System.Threading
open Ocis.Server.ProtocolSpec

[<Literal>]
let MeterName = "Ocis.Server"

[<Literal>]
let RequestTotalName = "ocis.server.requests.total"

[<Literal>]
let RequestFailedName = "ocis.server.requests.failed"

[<Literal>]
let RequestDurationName = "ocis.server.requests.duration.ms"

[<Literal>]
let DispatcherQueueDepthName = "ocis.server.dispatcher.queue.depth"

let private meter = new Meter(MeterName, "1.0.0")

let private requestTotalCounter = meter.CreateCounter<int64>(RequestTotalName, unit = "requests")

let private requestFailedCounter = meter.CreateCounter<int64>(RequestFailedName, unit = "requests")

let private requestDurationHistogram =
    meter.CreateHistogram<double>(RequestDurationName, unit = "ms")

let mutable private dispatcherQueueDepthProvider: (unit -> int) = fun () -> 0

let private dispatcherQueueDepthGauge =
    meter.CreateObservableGauge<int64>(
        DispatcherQueueDepthName,
        Func<int64>(fun () ->
            let current = Volatile.Read(&dispatcherQueueDepthProvider)
            int64 (current ())),
        unit = "items"
    )

let RecordRequest (statusCode: StatusCode) (durationMs: double) =
    // Total requests includes all terminal status codes.
    requestTotalCounter.Add 1L
    // Duration measures end-to-end request handling time in milliseconds.
    requestDurationHistogram.Record durationMs

    // Failed requests are protocol responses with StatusCode.Error.
    if statusCode = StatusCode.Error then
        requestFailedCounter.Add 1L

let SetDispatcherQueueDepthProvider(provider: unit -> int) =
    // The gauge callback reads this provider during metric collection.
    Volatile.Write(&dispatcherQueueDepthProvider, provider)
