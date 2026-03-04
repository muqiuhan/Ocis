module Ocis.WalCommitCoordinator

open System
open System.Threading
open System.Threading.Tasks

type DurabilityMode =
  | Strict
  | Balanced
  | Fast

module DurabilityMode =
  let TryParse (mode : string) : Result<DurabilityMode, string> =
    match (if isNull mode then "" else mode.Trim ()) with
    | value when value.Equals ("Strict", StringComparison.OrdinalIgnoreCase) ->
      Ok Strict
    | value when value.Equals ("Balanced", StringComparison.OrdinalIgnoreCase) ->
      Ok Balanced
    | value when value.Equals ("Fast", StringComparison.OrdinalIgnoreCase) ->
      Ok Fast
    | _ -> Error "DurabilityMode must be one of: Strict, Balanced, Fast"

type WalCommitCoordinator
  (
    mode : DurabilityMode,
    groupCommitWindowMs : int,
    groupCommitBatchSize : int,
    durableFlush : unit -> unit
  )
  =
  // Strict: flush per write, highest durability and latency.
  // Balanced: batch flushes by time window or batch size.
  // Fast: do not wait for durable flush in the write path.
  do
    if mode = Balanced && groupCommitWindowMs <= 0 then
      invalidArg
        "groupCommitWindowMs"
        "groupCommitWindowMs must be greater than 0"

    if mode = Balanced && groupCommitBatchSize <= 0 then
      invalidArg
        "groupCommitBatchSize"
        "groupCommitBatchSize must be greater than 0"

  let gate = obj ()

  let pending =
    ResizeArray<TaskCompletionSource<Result<unit, string>>> ()

  let mutable disposed = false
  let mutable timer : Timer option = None
  let mutable flushScheduled = false

  // Flushes one captured batch and completes all waiters with the same result.
  let completeBatch (batch : TaskCompletionSource<Result<unit, string>> array) =
    if batch.Length > 0 then
      try
        durableFlush ()

        for waiter in batch do
          waiter.TrySetResult (Ok ()) |> ignore
      with ex ->
        let error = Error ex.Message

        for waiter in batch do
          waiter.TrySetResult (error) |> ignore

  // Drains current waiters, performs one durable flush, then optionally
  // schedules another timer if new waiters arrived while flushing.
  let flushPendingBatch () =
    let batch =
      lock
        gate
        (fun () ->
          if pending.Count = 0 then
            flushScheduled <- false
            Array.empty
          else
            let toFlush = pending.ToArray ()
            pending.Clear ()
            flushScheduled <- false
            toFlush
        )

    completeBatch batch

    lock
      gate
      (fun () ->
        if
          not disposed
          && pending.Count > 0
          && not flushScheduled
        then
          flushScheduled <- true

          match timer with
          | Some activeTimer ->
            activeTimer.Change (groupCommitWindowMs, Timeout.Infinite)
            |> ignore
          | None -> ()
      )

  // Schedules a single-shot timer for the next balanced flush window.
  let ensureTimerScheduled () =
    lock
      gate
      (fun () ->
        if not flushScheduled then
          flushScheduled <- true

          match timer with
          | Some activeTimer ->
            activeTimer.Change (groupCommitWindowMs, Timeout.Infinite)
            |> ignore
          | None ->
            let callback = TimerCallback (fun _ -> flushPendingBatch ())

            timer <-
              Some (
                new Timer (
                  callback,
                  null,
                  groupCommitWindowMs,
                  Timeout.Infinite
                )
              )
      )

  member _.RegisterDurableCommit () : Task<Result<unit, string>> =
    match mode with
    | Fast -> Task.FromResult (Ok ())
    | Strict ->
      Task.Run (fun () ->
        try
          durableFlush ()
          Ok ()
        with ex ->
          Error ex.Message
      )
    | Balanced ->
      let waiter =
        TaskCompletionSource<Result<unit, string>> (
          TaskCreationOptions.RunContinuationsAsynchronously
        )

      let shouldFlushImmediately =
        lock
          gate
          (fun () ->
            if disposed then
              raise (ObjectDisposedException (nameof WalCommitCoordinator))

            pending.Add waiter

            if pending.Count >= groupCommitBatchSize then
              flushScheduled <- false

              match timer with
              | Some activeTimer ->
                activeTimer.Change (Timeout.Infinite, Timeout.Infinite)
                |> ignore
              | None -> ()

              true
            else
              false
          )

      if shouldFlushImmediately then
        Task.Run (fun () -> flushPendingBatch ())
        |> ignore
      else
        ensureTimerScheduled ()

      waiter.Task

  member this.AwaitDurableCommit () : unit =
    match this.RegisterDurableCommit().GetAwaiter().GetResult () with
    | Ok () -> ()
    | Error msg -> raise (InvalidOperationException (msg))

  interface IDisposable with
    member _.Dispose () =
      let batchToDrain, timerToDispose =
        lock
          gate
          (fun () ->
            if disposed then
              Array.empty, None
            else
              disposed <- true
              let drained = pending.ToArray ()
              pending.Clear ()
              flushScheduled <- false
              let existingTimer = timer
              timer <- None
              drained, existingTimer
          )

      completeBatch batchToDrain

      match timerToDispose with
      | Some activeTimer -> activeTimer.Dispose ()
      | None -> ()
