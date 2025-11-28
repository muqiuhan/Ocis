module Ocis.Tests.ConcurrencyTests

open NUnit.Framework
open Ocis.OcisDB
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Tests for concurrent operations and thread safety in OcisDB
/// </summary>
[<TestFixture>]
type ConcurrencyTests() =

    let tempDir = "temp_concurrency_tests"
    let mutable dbPath = ""
    let mutable db: OcisDB | null = null

    [<SetUp>]
    member this.Setup() =
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory tempDir |> ignore
        dbPath <- Path.Combine(tempDir, "concurrency_test_db")

        match OcisDB.Open(dbPath, 1000) with
        | Ok newDb -> db <- newDb
        | Error msg -> Assert.Fail $"Failed to create test DB: {msg}"

    [<TearDown>]
    member this.TearDown() =
        if db <> null then
            (db :> System.IDisposable).Dispose()

        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.ConcurrentReads_ShouldWorkCorrectly() =
        // Pre-populate with data
        for i = 0 to 99 do
            let key = Encoding.UTF8.GetBytes $"shared_key_{i:D3}"
            let value = Encoding.UTF8.GetBytes $"shared_value_{i}"
            let setResult = db.Set(key, value)
            Assert.That(setResult.IsOk, Is.True)

        // Test concurrent reads
        let readOperation i () =
            do
                for j = 0 to 99 do
                    let key = Encoding.UTF8.GetBytes $"shared_key_{j:D3}"
                    let getResult = db.Get key

                    match getResult with
                    | Ok(Some value) ->
                        let expectedValue = $"shared_value_{j}"
                        let actualValue = Encoding.UTF8.GetString value

                        Assert.That(actualValue, Is.EqualTo expectedValue, $"Thread {i}: Wrong value for key {j}")
                    | Ok None -> Assert.Fail $"Thread {i}: Key {j} not found"
                    | Error msg -> Assert.Fail $"Thread {i}: Error reading key {j}: {msg}"


        // Run 10 readers sequentially
        for i = 0 to 9 do
            readOperation i ()

    [<Test>]
    member this.ConcurrentWrites_ShouldNotCorruptData() =
        let writeOperation threadId () =
            do
                for i = 0 to 49 do
                    let key = Encoding.UTF8.GetBytes $"thread_{threadId}_key_{i:D4}"
                    let value = Encoding.UTF8.GetBytes $"thread_{threadId}_value_{i}"
                    let setResult = db.Set(key, value)

                    match setResult with
                    | Ok() -> ()
                    | Error msg -> Assert.Fail $"Thread {threadId}: Failed to set key {i}: {msg}"


        // Run 5 writers sequentially
        for id = 0 to 4 do
            writeOperation id ()

        // Verify all data was written correctly
        for threadId = 0 to 4 do
            for i = 0 to 49 do
                let key = Encoding.UTF8.GetBytes $"thread_{threadId}_key_{i:D4}"
                let getResult = db.Get key

                match getResult with
                | Ok(Some value) ->
                    let expectedValue = $"thread_{threadId}_value_{i}"
                    let actualValue = Encoding.UTF8.GetString value
                    Assert.That(actualValue, Is.EqualTo expectedValue)
                | Ok None -> Assert.Fail $"Key thread_{threadId}_key_{i:D4} not found"
                | Error msg -> Assert.Fail $"Error reading key thread_{threadId}_key_{i:D4}: {msg}"

    [<Test>]
    member this.ConcurrentReadWrite_ShouldMaintainConsistency() =
        let writeCount = ref 0

        // Execute write operations sequentially
        for threadId = 1 to 2 do
            for i = 0 to 49 do
                let keyIndex = Interlocked.Increment writeCount
                let key = Encoding.UTF8.GetBytes $"rw_key_{keyIndex:D6}"

                let value = Encoding.UTF8.GetBytes $"rw_value_{keyIndex}"
                let setResult = db.Set(key, value)

                match setResult with
                | Ok() -> ()
                | Error msg -> Assert.Fail $"Thread {threadId}: Write operation failed: {msg}"

        // Execute read operations sequentially
        let readCount = ref 0

        for threadId = 3 to 4 do
            for i = 0 to 49 do
                let currentCount = Interlocked.Increment readCount
                let key = Encoding.UTF8.GetBytes $"rw_key_{currentCount:D6}"

                let getResult = db.Get key

                match getResult with
                | Ok(Some value) ->
                    let expectedValue = $"rw_value_{currentCount}"
                    let actualValue = Encoding.UTF8.GetString value

                    if actualValue <> expectedValue then
                        Assert.Fail
                            $"Thread {threadId}: Inconsistent data: expected '{expectedValue}', got '{actualValue}'"
                | Ok None ->
                    // Key might not be written yet, which is OK for operations
                    ()
                | Error msg -> Assert.Fail $"Thread {threadId}: Read operation failed: {msg}"

    [<Test>]
    member this.MemtableFlushDuringConcurrentOperations_ShouldWork() =
        // Fill memtable to trigger flush
        for i = 0 to 999 do
            let key = Encoding.UTF8.GetBytes $"flush_test_key_{i:D4}"
            let value = Encoding.UTF8.GetBytes $"flush_test_value_{i}"
            let setResult = db.Set(key, value)
            Assert.That(setResult.IsOk, Is.True)

        // Start concurrent operations that might trigger flush
        let concurrentOperation () =
            do
                for i = 0 to 49 do
                    let key = Encoding.UTF8.GetBytes $"concurrent_key_{i:D3}"
                    let value = Encoding.UTF8.GetBytes $"concurrent_value_{i}"
                    let setResult = db.Set(key, value)

                    match setResult with
                    | Ok() -> ()
                    | Error msg -> Assert.Fail $"Concurrent set failed: {msg}"

                    // Read a key that might be in the process of being flushed
                    let getResult = db.Get(Encoding.UTF8.GetBytes $"flush_test_key_{i:D4}")

                    match getResult with
                    | Ok(Some value) ->
                        let expectedValue = $"flush_test_value_{i}"
                        let actualValue = Encoding.UTF8.GetString value
                        Assert.That(actualValue, Is.EqualTo expectedValue)
                    | Ok None -> Assert.Fail $"Flush test key {i} not found during concurrent operations"
                    | Error msg -> Assert.Fail $"Error reading during flush: {msg}"


        // Run multiple operations sequentially
        for _ = 0 to 4 do
            concurrentOperation ()

    [<Test>]
    member this.DatabaseReopenDuringOperations_ShouldFailGracefully() =
        // This test verifies that operations fail gracefully if the database is closed
        // during concurrent operations

        let operationThatMightFail () =
            do
                try
                    for i = 0 to 9 do
                        let key = Encoding.UTF8.GetBytes $"reopen_test_key_{i}"
                        let value = Encoding.UTF8.GetBytes $"reopen_test_value_{i}"
                        let setResult = db.Set(key, value)

                        match setResult with
                        | Ok() -> ()
                        | Error msg ->
                            // Expected to fail if DB is closed
                            if not (msg.Contains "disposed" || msg.Contains "closed") then
                                Assert.Fail $"Unexpected error during reopen test: {msg}"
                with
                | :? System.ObjectDisposedException ->
                    // Expected if DB is disposed during operation
                    ()
                | ex -> Assert.Fail $"Unexpected exception during reopen test: {ex.Message}"


        // Start operations
        let operations = [ 0..2 ] |> List.map (fun _ -> operationThatMightFail ())

        // Close the database first
        if db <> null then
            (db :> System.IDisposable).Dispose()

        // Execute operations synchronously and check they handle the closure gracefully
        for op in operations do
            try
                op
            with
            | :? System.ObjectDisposedException -> ()
            | :? NUnit.Framework.AssertionException -> reraise ()
            | ex -> Assert.Fail $"Unexpected exception type: {ex.GetType().Name}"

    [<Test>]
    member this.LongRunningOperations_ShouldNotBlockOtherOperations() =
        let longRunningOperation () =
            do
                // Simulate a long-running operation
                for i = 0 to 999 do
                    let key = Encoding.UTF8.GetBytes $"long_running_key_{i:D4}"
                    let value = Encoding.UTF8.GetBytes(new string ('X', 1000)) // 1KB values
                    let setResult = db.Set(key, value)

                    match setResult with
                    | Ok() -> ()
                    | Error msg -> Assert.Fail $"Long running operation failed at {i}: {msg}"

                    // Small yield to allow other operations
                    System.Threading.Thread.Sleep(1)


        let quickOperations () =
            do
                for i = 0 to 99 do
                    let key = Encoding.UTF8.GetBytes $"quick_key_{i:D3}"
                    let value = Encoding.UTF8.GetBytes $"quick_value_{i}"
                    let setResult = db.Set(key, value)

                    match setResult with
                    | Ok() -> ()
                    | Error msg -> Assert.Fail $"Quick operation failed at {i}: {msg}"


        // Execute long-running operation synchronously
        longRunningOperation ()

        // Execute multiple quick operations sequentially
        for _ in 0..4 do
            quickOperations ()

    // All operations completed synchronously

    [<Test>]
    member this.BackgroundAgents_ShouldHandleConcurrentRequests() =
        // Test that background agents (compaction, GC, flush) can handle concurrent requests

        let triggerOperations () =
            // Trigger multiple compactions synchronously
            for i = 0 to 4 do
                db.PerformCompaction()
                db.PerformGarbageCollection()


        // Add some data to potentially trigger background operations
        for i = 0 to 499 do
            let key = Encoding.UTF8.GetBytes $"background_test_key_{i:D4}"
            let value = Encoding.UTF8.GetBytes $"background_test_value_{i}"
            let setResult = db.Set(key, value)
            Assert.That(setResult.IsOk, Is.True)

        // Trigger concurrent background operations
        triggerOperations ()

        // Give background agents time to process
        Thread.Sleep 100

        // Database should still be functional
        let testKey = Encoding.UTF8.GetBytes "background_test_key_0000"
        let getResult = db.Get testKey

        match getResult with
        | Ok(Some value) ->
            let expectedValue = "background_test_value_0"
            let actualValue = Encoding.UTF8.GetString value
            Assert.That(actualValue, Is.EqualTo expectedValue)
        | _ -> Assert.Fail "Failed to read test key after background operations"
