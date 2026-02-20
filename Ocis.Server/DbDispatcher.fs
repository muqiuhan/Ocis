module Ocis.Server.DbDispatcher

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Ocis.OcisDB
open Ocis.Server.Telemetry

type private DbWorkItem = { Execute: OcisDB -> unit }

type OcisDbDispatcher(db: OcisDB, queueCapacity: int) =
    do
        if queueCapacity <= 0 then
            invalidArg "queueCapacity" "queueCapacity must be greater than 0"

    let options = BoundedChannelOptions(queueCapacity)
    do
        options.SingleReader <- true
        options.SingleWriter <- false
        options.FullMode <- BoundedChannelFullMode.Wait

    let channel = Channel.CreateBounded<DbWorkItem>(options)
    let mutable stopped = false
    let mutable disposed = false
    let stopLock = obj ()
    let cancellation = new CancellationTokenSource()

    do
        SetDispatcherQueueDepthProvider(fun () ->
            if channel.Reader.CanCount then
                channel.Reader.Count
            else
                0)

    let workerCompletion =
        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    let workerThread =
        Thread(ThreadStart(fun () ->
            try
                db.BindToCurrentThread()
                let mutable keepRunning = true

                while keepRunning do
                    let canRead = channel.Reader.WaitToReadAsync(cancellation.Token).AsTask().GetAwaiter().GetResult()

                    if canRead then
                        let mutable nextItem = Unchecked.defaultof<DbWorkItem>

                        while channel.Reader.TryRead(&nextItem) do
                            nextItem.Execute db
                    else
                        keepRunning <- false

                workerCompletion.TrySetResult() |> ignore
            with
            | :? OperationCanceledException ->
                workerCompletion.TrySetResult() |> ignore
            | ex ->
                workerCompletion.TrySetException ex |> ignore))

    do
        workerThread.IsBackground <- true
        workerThread.Name <- "ocis-db-dispatcher"
        workerThread.Start()

    member private _.TryDispatch<'T>(work: OcisDB -> Result<'T, string>) : Async<Result<'T, string>> =
        async {
            if stopped then
                return Error "Database dispatcher queue is full or closed"
            else
                let reply = TaskCompletionSource<Result<'T, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

                let item =
                    { Execute =
                        fun sharedDb ->
                            try
                                reply.TrySetResult(work sharedDb) |> ignore
                             with ex ->
                                 reply.TrySetResult(Error $"Database dispatcher execution failed: {ex.Message}")
                                 |> ignore
                    }

                try
                    // FullMode.Wait is intended to apply backpressure instead of dropping writes.
                    do! channel.Writer.WriteAsync(item, cancellation.Token).AsTask() |> Async.AwaitTask
                    return! reply.Task |> Async.AwaitTask
                with
                | :? ChannelClosedException
                | :? OperationCanceledException ->
                    return Error "Database dispatcher queue is full or closed"
        }

    member this.DispatchSet(key: byte array, value: byte array) : Async<Result<unit, string>> =
        this.TryDispatch(fun sharedDb -> sharedDb.Set(key, value))

    member this.DispatchSetDeferred(key: byte array, value: byte array) : Async<Result<Task<Result<unit, string>>, string>> =
        this.TryDispatch(fun sharedDb -> sharedDb.SetDeferred(key, value))

    member this.DispatchGet(key: byte array) : Async<Result<byte array option, string>> =
        this.TryDispatch(fun sharedDb -> sharedDb.Get key)

    member this.DispatchDelete(key: byte array) : Async<Result<unit, string>> =
        this.TryDispatch(fun sharedDb -> sharedDb.Delete key)

    member this.DispatchDeleteDeferred(key: byte array) : Async<Result<Task<Result<unit, string>>, string>> =
        this.TryDispatch(fun sharedDb -> sharedDb.DeleteDeferred key)

    member _.StopAsync() : Async<unit> =
        async {
            lock stopLock (fun () ->
                if not stopped then
                    stopped <- true
                    channel.Writer.TryComplete() |> ignore)

            do! workerCompletion.Task |> Async.AwaitTask
        }

    interface IDisposable with
        member this.Dispose() =
            if not disposed then
                disposed <- true

                try
                    this.StopAsync() |> Async.RunSynchronously
                finally
                    cancellation.Cancel()
                    cancellation.Dispose()
    
