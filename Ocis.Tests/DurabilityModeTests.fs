module Ocis.Tests.DurabilityModeTests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open NUnit.Framework
open Ocis.OcisDB

[<TestFixture>]
type DurabilityModeTests() =

    let tempRoot = Path.Combine(Path.GetTempPath(), "ocis_durability_tests")
    let mutable testDbPath = ""

    let openDb
        (mode: string)
        (groupCommitWindowMs: int)
        (durableFlushOverride: unit -> unit)
        : OcisDB =
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
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"; Unchecked.defaultof<OcisDB>

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

        let durableFlushOverride () =
            Interlocked.Increment(&flushCount) |> ignore
            Thread.Sleep 80

        use db = openDb "Strict" 5 durableFlushOverride

        let sw = Stopwatch.StartNew()
        let result = db.Set(Encoding.UTF8.GetBytes "strict-key", Encoding.UTF8.GetBytes "strict-value")
        sw.Stop()

        Assert.That(result.IsOk, Is.True)
        Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo 70L)
        Assert.That(flushCount, Is.EqualTo 1)

    [<Test>]
    member _.StrictMode_DeleteWaitsForDurableFlush() =
        let mutable flushCount = 0

        let durableFlushOverride () =
            Interlocked.Increment(&flushCount) |> ignore
            Thread.Sleep 80

        use db = openDb "Strict" 5 durableFlushOverride

        let seedResult = db.Set(Encoding.UTF8.GetBytes "strict-delete-key", Encoding.UTF8.GetBytes "strict-delete-value")
        Assert.That(seedResult.IsOk, Is.True)

        let sw = Stopwatch.StartNew()
        let deleteResult = db.Delete(Encoding.UTF8.GetBytes "strict-delete-key")
        sw.Stop()

        Assert.That(deleteResult.IsOk, Is.True)
        Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo 70L)
        Assert.That(flushCount, Is.EqualTo 2)

    [<Test>]
    member _.BalancedMode_BatchesConcurrentSetsIntoOneDurableFlush() =
        let mutable flushCount = 0

        let durableFlushOverride () =
            Interlocked.Increment(&flushCount) |> ignore
            Thread.Sleep 20

        use db = openDb "Balanced" 60 durableFlushOverride

        let set1 =
            Task.Run(fun () -> db.Set(Encoding.UTF8.GetBytes "balanced-key-1", Encoding.UTF8.GetBytes "v1"))

        Thread.Sleep 10

        let set2 =
            Task.Run(fun () -> db.Set(Encoding.UTF8.GetBytes "balanced-key-2", Encoding.UTF8.GetBytes "v2"))

        let completed = Task.WaitAll([| set1 :> Task; set2 :> Task |], 2000)
        Assert.That(completed, Is.True, "Balanced mode writes should complete in bounded time")
        Assert.That(set1.Result.IsOk, Is.True)
        Assert.That(set2.Result.IsOk, Is.True)
        Assert.That(flushCount, Is.EqualTo 1)

    [<Test>]
    member _.FastMode_RemainsCompatibleWithoutPerRequestDurableWait() =
        let mutable flushCount = 0

        let durableFlushOverride () =
            Interlocked.Increment(&flushCount) |> ignore
            Thread.Sleep 40

        use db = openDb "Fast" 5 durableFlushOverride

        let setResult = db.Set(Encoding.UTF8.GetBytes "fast-key", Encoding.UTF8.GetBytes "fast-value")
        Assert.That(setResult.IsOk, Is.True)
        Assert.That(flushCount, Is.EqualTo 0)

        match db.Get(Encoding.UTF8.GetBytes "fast-key") with
        | Ok(Some value) -> Assert.That(Encoding.UTF8.GetString value, Is.EqualTo "fast-value")
        | Ok None -> Assert.Fail "Expected key to exist"
        | Error msg -> Assert.Fail $"Get failed: {msg}"

    [<Test>]
    member _.BalancedMode_DisposeDoesNotHangWithPendingWrites() =
        let mutable flushCount = 0

        let durableFlushOverride () =
            Interlocked.Increment(&flushCount) |> ignore
            Thread.Sleep 60

        let db = openDb "Balanced" 500 durableFlushOverride

        let pendingSet =
            Task.Run(fun () -> db.Set(Encoding.UTF8.GetBytes "dispose-key", Encoding.UTF8.GetBytes "dispose-value"))

        Thread.Sleep 40

        let disposeTask = Task.Run(fun () -> (db :> IDisposable).Dispose())

        let completed = Task.WaitAll([| pendingSet :> Task; disposeTask |], 3000)
        Assert.That(completed, Is.True, "Dispose should drain pending group-commit writes")
        Assert.That(pendingSet.Result.IsOk, Is.True)
        Assert.That(flushCount, Is.GreaterThanOrEqualTo 1)
