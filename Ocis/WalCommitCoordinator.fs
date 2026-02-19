module Ocis.WalCommitCoordinator

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

type DurabilityMode =
    | Strict
    | Balanced
    | Fast

module DurabilityMode =
    let TryParse(mode: string) : Result<DurabilityMode, string> =
        match (if isNull mode then "" else mode.Trim()) with
        | value when value.Equals("Strict", StringComparison.OrdinalIgnoreCase) -> Ok Strict
        | value when value.Equals("Balanced", StringComparison.OrdinalIgnoreCase) -> Ok Balanced
        | value when value.Equals("Fast", StringComparison.OrdinalIgnoreCase) -> Ok Fast
        | _ -> Error "DurabilityMode must be one of: Strict, Balanced, Fast"

type WalCommitCoordinator(
    mode: DurabilityMode,
    groupCommitWindowMs: int,
    groupCommitBatchSize: int,
    durableFlush: unit -> unit
) =
    do
        if mode = Balanced && groupCommitWindowMs <= 0 then
            invalidArg "groupCommitWindowMs" "groupCommitWindowMs must be greater than 0"

        if mode = Balanced && groupCommitBatchSize <= 0 then
            invalidArg "groupCommitBatchSize" "groupCommitBatchSize must be greater than 0"

    let gate = obj ()
    let pending = ResizeArray<TaskCompletionSource<unit>>()
    let mutable disposed = false
    let mutable timer: Timer option = None
    let mutable flushScheduled = false

    let completeBatch (batch: TaskCompletionSource<unit> array) =
        if batch.Length > 0 then
            try
                durableFlush ()

                for waiter in batch do
                    waiter.TrySetResult() |> ignore
            with ex ->
                for waiter in batch do
                    waiter.TrySetException ex |> ignore

    let flushPendingBatch () =
        let batch =
            lock gate (fun () ->
                if pending.Count = 0 then
                    flushScheduled <- false
                    Array.empty
                else
                    let toFlush = pending.ToArray()
                    pending.Clear()
                    flushScheduled <- false
                    toFlush)

        completeBatch batch

        lock gate (fun () ->
            if not disposed && pending.Count > 0 && not flushScheduled then
                flushScheduled <- true

                match timer with
                | Some activeTimer -> activeTimer.Change(groupCommitWindowMs, Timeout.Infinite) |> ignore
                | None -> ())

    let ensureTimerScheduled () =
        lock gate (fun () ->
            if not flushScheduled then
                flushScheduled <- true

                match timer with
                | Some activeTimer -> activeTimer.Change(groupCommitWindowMs, Timeout.Infinite) |> ignore
                | None ->
                    let callback = TimerCallback(fun _ -> flushPendingBatch ())
                    timer <- Some(new Timer(callback, null, groupCommitWindowMs, Timeout.Infinite)))

    member _.AwaitDurableCommit() : unit =
        match mode with
        | Fast -> ()
        | Strict ->
            lock gate (fun () ->
                if disposed then
                    raise (ObjectDisposedException(nameof WalCommitCoordinator)))

            durableFlush ()
        | Balanced ->
            let waiter = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            let shouldFlushImmediately =
                lock gate (fun () ->
                    if disposed then
                        raise (ObjectDisposedException(nameof WalCommitCoordinator))

                    pending.Add waiter

                    if pending.Count >= groupCommitBatchSize then
                        flushScheduled <- false

                        match timer with
                        | Some activeTimer -> activeTimer.Change(Timeout.Infinite, Timeout.Infinite) |> ignore
                        | None -> ()

                        true
                    else
                        false)

            if shouldFlushImmediately then
                flushPendingBatch ()
            else
                ensureTimerScheduled ()

            waiter.Task.GetAwaiter().GetResult()

    interface IDisposable with
        member _.Dispose() =
            let batchToDrain, timerToDispose =
                lock gate (fun () ->
                    if disposed then
                        Array.empty, None
                    else
                        disposed <- true
                        let drained = pending.ToArray()
                        pending.Clear()
                        flushScheduled <- false
                        let existingTimer = timer
                        timer <- None
                        drained, existingTimer)

            completeBatch batchToDrain

            match timerToDispose with
            | Some activeTimer -> activeTimer.Dispose()
            | None -> ()
