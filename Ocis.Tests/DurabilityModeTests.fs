module Ocis.Tests.DurabilityModeTests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open NUnit.Framework
open Ocis.OcisDB
open Ocis.WalCommitCoordinator

[<TestFixture>]
type DurabilityModeTests() =

  let tempRoot =
    Path.Combine(Path.GetTempPath(), "ocis_durability_tests")
  let mutable testDbPath = ""

  let openDb
    (mode: string)
    (groupCommitWindowMs: int)
    (durableFlushOverride: unit -> unit)
    : OcisDB
    =
    match
      OcisDB.Open(
        testDbPath,
        1000,
        durabilityMode = mode,
        groupCommitWindowMs = groupCommitWindowMs,
        durableFlushOverride = durableFlushOverride
      )
    with
    | Ok db -> db
    | Error msg ->
      Assert.Fail $"Failed to open DB: {msg}"
      Unchecked.defaultof<OcisDB>

  [<SetUp>]
  member _.SetUp() =
    if Directory.Exists tempRoot then
      Directory.Delete(tempRoot, true)

    Directory.CreateDirectory tempRoot |> ignore
    testDbPath <- Path.Combine(tempRoot, Guid.NewGuid().ToString("N"))

  [<TearDown>]
  member _.TearDown() =
    GC.Collect()
    GC.WaitForPendingFinalizers()
    GC.Collect()
    Thread.Sleep 50

    if Directory.Exists tempRoot then
      Directory.Delete(tempRoot, true)

  [<Test>]
  member _.StrictMode_SetWaitsForDurableFlush() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore
      Thread.Sleep 80

    use db = openDb "Strict" 5 durableFlushOverride

    let sw = Stopwatch.StartNew()

    let result =
      db.Set(
        Encoding.UTF8.GetBytes "strict-key",
        Encoding.UTF8.GetBytes "strict-value"
      )

    sw.Stop()

    Assert.That(result.IsOk, Is.True)
    Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo 70L)
    Assert.That(flushCount, Is.EqualTo 1)

  [<Test>]
  member _.StrictMode_DeleteWaitsForDurableFlush() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore
      Thread.Sleep 80

    use db = openDb "Strict" 5 durableFlushOverride

    let seedResult =
      db.Set(
        Encoding.UTF8.GetBytes "strict-delete-key",
        Encoding.UTF8.GetBytes "strict-delete-value"
      )

    Assert.That(seedResult.IsOk, Is.True)

    let sw = Stopwatch.StartNew()
    let deleteResult =
      db.Delete(Encoding.UTF8.GetBytes "strict-delete-key")
    sw.Stop()

    Assert.That(deleteResult.IsOk, Is.True)
    Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo 70L)
    Assert.That(flushCount, Is.EqualTo 2)

  [<Test>]
  member _.BalancedMode_RejectsCrossThreadSetWithThreadAffinityViolation() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore
      Thread.Sleep 20

    use db = openDb "Balanced" 60 durableFlushOverride

    let crossThreadSet =
      Task.Run(fun () ->
        db.Set(
          Encoding.UTF8.GetBytes "balanced-key-1",
          Encoding.UTF8.GetBytes "v1"
        ))

    let ex =
      Assert.Throws<AggregateException>(fun () -> crossThreadSet.Wait())
    let baseEx = ex.GetBaseException()

    Assert.That(baseEx :? InvalidOperationException, Is.True)
    Assert.That(baseEx.Message, Does.Contain "owner thread")
    Assert.That(baseEx.Message, Does.Contain "Set")
    Assert.That(flushCount, Is.EqualTo 0)

  [<Test>]
  member _.FastMode_RemainsCompatibleWithoutPerRequestDurableWait() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore
      Thread.Sleep 40

    use db = openDb "Fast" 5 durableFlushOverride

    let setResult =
      db.Set(
        Encoding.UTF8.GetBytes "fast-key",
        Encoding.UTF8.GetBytes "fast-value"
      )

    Assert.That(setResult.IsOk, Is.True)
    Assert.That(flushCount, Is.EqualTo 0)

    match db.Get(Encoding.UTF8.GetBytes "fast-key") with
    | Ok(Some value) ->
      Assert.That(Encoding.UTF8.GetString value, Is.EqualTo "fast-value")
    | Ok None -> Assert.Fail "Expected key to exist"
    | Error msg -> Assert.Fail $"Get failed: {msg}"

  [<Test>]
  member _.BalancedMode_DisposeDoesNotHangAfterCrossThreadViolation() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore
      Thread.Sleep 60

    let db = openDb "Balanced" 500 durableFlushOverride

    let pendingSet =
      Task.Run(fun () ->
        db.Set(
          Encoding.UTF8.GetBytes "dispose-key",
          Encoding.UTF8.GetBytes "dispose-value"
        ))

    Thread.Sleep 40

    let disposeTask =
      Task.Run(fun () -> (db :> IDisposable).Dispose())

    let mutable waitRaisedAggregate = false

    try
      Task.WaitAll([|pendingSet :> Task; disposeTask|], 3000)
      |> ignore
    with :? AggregateException ->
      waitRaisedAggregate <- true

    Assert.That(waitRaisedAggregate, Is.True)
    Assert.That(pendingSet.IsCompleted, Is.True)

    Assert.That(
      disposeTask.IsCompleted,
      Is.True,
      "Dispose should complete even after a cross-thread access violation"
    )

    Assert.That(pendingSet.IsFaulted, Is.True)
    Assert.That(
      pendingSet.Exception.GetBaseException() :? InvalidOperationException,
      Is.True
    )
    Assert.That(flushCount, Is.EqualTo 0)

  [<Test>]
  member _.BalancedCoordinator_FlushesImmediatelyWhenBatchSizeReached() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore

    use coordinator =
      new WalCommitCoordinator(
        DurabilityMode.Balanced,
        2000,
        3,
        durableFlushOverride
      )

    use startGate = new ManualResetEventSlim(false)

    let waiters =
      [|1..3|]
      |> Array.map(fun _ ->
        Task.Run(fun () ->
          startGate.Wait()
          coordinator.AwaitDurableCommit()))

    let sw = Stopwatch.StartNew()
    startGate.Set()

    let completed = Task.WaitAll(waiters, 1000)
    sw.Stop()

    Assert.That(
      completed,
      Is.True,
      "Batch-size trigger should complete before timer window"
    )
    Assert.That(sw.ElapsedMilliseconds, Is.LessThan 1500L)
    Assert.That(flushCount, Is.EqualTo 1)

  [<Test>]
  member _.BalancedCoordinator_FlushesOnTimerWhenBatchNotReached() =
    let mutable flushCount = 0

    let durableFlushOverride() =
      Interlocked.Increment(&flushCount) |> ignore

    use coordinator =
      new WalCommitCoordinator(
        DurabilityMode.Balanced,
        80,
        8,
        durableFlushOverride
      )

    let sw = Stopwatch.StartNew()
    let result =
      coordinator.RegisterDurableCommit().GetAwaiter().GetResult()
    sw.Stop()

    Assert.That(result.IsOk, Is.True)
    Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo 40L)
    Assert.That(sw.ElapsedMilliseconds, Is.LessThan 1000L)
    Assert.That(flushCount, Is.EqualTo 1)
