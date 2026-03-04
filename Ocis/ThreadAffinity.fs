module Ocis.ThreadAffinity

open System
open System.Threading

type ThreadOwner private (initialOwnerThreadId : int) =
  let mutable ownerThreadId = initialOwnerThreadId
  let mutable rebound = false
  let gate = obj ()

  member _.OwnerThreadId = Volatile.Read (&ownerThreadId)

  member _.AssertOwnerThread (operationName : string) : unit =
    // All engine operations must execute on the captured owner thread.
    let currentThreadId = Environment.CurrentManagedThreadId

    if currentThreadId <> Volatile.Read (&ownerThreadId) then
      raise
      <| InvalidOperationException (
        $"Thread affinity violation in '{operationName}': operation must run on owner thread {Volatile.Read (&ownerThreadId)}, but was called on thread {currentThreadId}."
      )

  member _.RebindOwnerThread (operationName : string) : unit =
    // Rebind is allowed once so a dispatcher can transfer ownership from
    // construction thread to its dedicated worker thread.
    lock
      gate
      (fun () ->
        if rebound then
          raise
          <| InvalidOperationException (
            $"Thread affinity owner can only be rebound once. Second attempt in '{operationName}' is not allowed."
          )

        ownerThreadId <- Environment.CurrentManagedThreadId
        rebound <- true
      )

  static member CaptureOwnerThread () : ThreadOwner =
    ThreadOwner (Environment.CurrentManagedThreadId)
