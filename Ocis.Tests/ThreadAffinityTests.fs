module Ocis.Tests.ThreadAffinity

open NUnit.Framework
open Ocis.OcisDB
open Ocis.Valog
open System
open System.IO
open System.Text
open System.Threading

[<TestFixture>]
type ThreadAffinityTests() =

    let tempDir = "temp_thread_affinity_tests"
    let mutable testDbPath = ""
    let flushThreshold = 100

    [<SetUp>]
    member _.Setup() =
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory tempDir |> ignore
        testDbPath <- Path.Combine(tempDir, Guid.NewGuid().ToString("N"))

    [<TearDown>]
    member _.TearDown() =
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        Thread.Sleep 25

        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

    [<Test>]
    member _.SameThreadCoreOperations_ShouldSucceed() =
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"
        | Ok db ->
            use db = db
            let key = Encoding.UTF8.GetBytes "thread-key"
            let value = Encoding.UTF8.GetBytes "thread-value"

            Assert.That(db.Set(key, value).IsOk, Is.True)

            match db.Get key with
            | Ok(Some actual) -> Assert.That(actual, Is.EqualTo value)
            | Ok None -> Assert.Fail "Expected value not found"
            | Error msg -> Assert.Fail $"Get failed: {msg}"

            Assert.That(db.Delete(key).IsOk, Is.True)

            match db.Get key with
            | Ok None -> ()
            | Ok(Some _) -> Assert.Fail "Key should be deleted"
            | Error msg -> Assert.Fail $"Get after delete failed: {msg}"

    [<Test>]
    member _.CrossThreadGet_ShouldThrowInvalidOperationException() =
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"
        | Ok db ->
            use db = db
            let key = Encoding.UTF8.GetBytes "cross-thread-key"
            let value = Encoding.UTF8.GetBytes "cross-thread-value"
            db.Set(key, value) |> ignore

            let mutable thrown: exn option = None

            let worker =
                Thread(ThreadStart(fun () ->
                    try
                        db.Get key |> ignore
                    with ex ->
                        thrown <- Some ex))

            worker.Start()
            worker.Join()

            match thrown with
            | Some(:? InvalidOperationException as ex) ->
                Assert.That(ex.Message, Does.Contain "owner thread")
                Assert.That(ex.Message, Does.Contain "Get")
            | Some ex -> Assert.Fail $"Expected InvalidOperationException but got {ex.GetType().FullName}: {ex.Message}"
            | None -> Assert.Fail "Expected InvalidOperationException from cross-thread access"

    [<Test>]
    member _.SameThreadValogOperations_ShouldNotRaiseFalsePositives() =
        let path = Path.Combine(tempDir, "same-thread-valog.vlog")

        match Valog.Create path with
        | Error msg -> Assert.Fail $"Failed to create Valog: {msg}"
        | Ok valog ->
            use valog = valog
            let key = Encoding.UTF8.GetBytes "k"
            let value = Encoding.UTF8.GetBytes "v"
            let offset = valog.Append(key, value)
            valog.Flush()

            match valog.Read offset with
            | Some(readKey, readValue) ->
                Assert.That(readKey, Is.EqualTo key)
                Assert.That(readValue, Is.EqualTo value)
            | None -> Assert.Fail "Expected Valog.Read to succeed on owner thread"

            match valog.ReadValueOnly offset with
            | Some readValue -> Assert.That(readValue, Is.EqualTo value)
            | None -> Assert.Fail "Expected Valog.ReadValueOnly to succeed on owner thread"
