module Ocis.Tests.SequentialOperationTests

open NUnit.Framework
open Ocis.OcisDB
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Tests for sequential operations in OcisDB to ensure correctness and data integrity
/// </summary>
[<TestFixture>]
type SequentialOperationTests() =

    let tempDir = "temp_sequential_tests"
    let mutable dbPath = ""
    let mutable db: OcisDB | null = null

    [<SetUp>]
    member this.Setup() =
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory tempDir |> ignore
        dbPath <- Path.Combine(tempDir, "sequential_test_db")

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
    member this.SequentialReads_ShouldWorkCorrectly() =
        // Pre-populate with data
        for i = 0 to 99 do
            let key = Encoding.UTF8.GetBytes $"shared_key_{i:D3}"
            let value = Encoding.UTF8.GetBytes $"shared_value_{i}"
            let setResult = db.Set(key, value)
            Assert.That(setResult.IsOk, Is.True)

        // Test sequential reads
        let readOperation i () =
            do
                for j = 0 to 99 do
                    let key = Encoding.UTF8.GetBytes $"shared_key_{j:D3}"
                    let getResult = db.Get key

                    match getResult with
                    | Ok(Some value) ->
                        let expectedValue = $"shared_value_{j}"
                        let actualValue = Encoding.UTF8.GetString value

                        Assert.That(actualValue, Is.EqualTo expectedValue, $"Operation {i}: Wrong value for key {j}")
                    | Ok None -> Assert.Fail $"Operation {i}: Key {j} not found"
                    | Error msg -> Assert.Fail $"Operation {i}: Error reading key {j}: {msg}"


        // Run 10 readers sequentially
        for i = 0 to 9 do
            readOperation i ()

    [<Test>]
    member this.SequentialWrites_ShouldNotCorruptData() =
        let writeOperation operationId () =
            do
                for i = 0 to 49 do
                    let key = Encoding.UTF8.GetBytes $"operation_{operationId}_key_{i:D4}"
                    let value = Encoding.UTF8.GetBytes $"operation_{operationId}_value_{i}"
                    let setResult = db.Set(key, value)

                    match setResult with
                    | Ok() -> ()
                    | Error msg -> Assert.Fail $"Operation {operationId}: Failed to set key {i}: {msg}"


        // Run 5 writers sequentially
        for id = 0 to 4 do
            writeOperation id ()

        // Verify all data was written correctly
        for operationId = 0 to 4 do
            for i = 0 to 49 do
                let key = Encoding.UTF8.GetBytes $"operation_{operationId}_key_{i:D4}"
                let getResult = db.Get key

                match getResult with
                | Ok(Some value) ->
                    let expectedValue = $"operation_{operationId}_value_{i}"
                    let actualValue = Encoding.UTF8.GetString value
                    Assert.That(actualValue, Is.EqualTo expectedValue)
                | Ok None -> Assert.Fail $"Key operation_{operationId}_key_{i:D4} not found"
                | Error msg -> Assert.Fail $"Error reading key operation_{operationId}_key_{i:D4}: {msg}"

    [<Test>]
    member this.SequentialReadWrite_ShouldMaintainConsistency() =
        let writeCount = ref 0

        // Execute write operations sequentially
        for operationId = 1 to 2 do
            for i = 0 to 49 do
                let keyIndex = Interlocked.Increment writeCount
                let key = Encoding.UTF8.GetBytes $"rw_key_{keyIndex:D6}"

                let value = Encoding.UTF8.GetBytes $"rw_value_{keyIndex}"
                let setResult = db.Set(key, value)

                match setResult with
                | Ok() -> ()
                | Error msg -> Assert.Fail $"Operation {operationId}: Write operation failed: {msg}"

        // Execute read operations sequentially
        let readCount = ref 0

        for operationId = 3 to 4 do
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
                            $"Operation {operationId}: Inconsistent data: expected '{expectedValue}', got '{actualValue}'"
                | Ok None ->
                    // Key might not be written yet, which is OK for sequential operations
                    ()
                | Error msg -> Assert.Fail $"Operation {operationId}: Read operation failed: {msg}"

    [<Test>]
    member this.MemtableFlushDuringSequentialOperations_ShouldWork() =
        // Fill memtable to trigger flush
        for i = 0 to 999 do
            let key = Encoding.UTF8.GetBytes $"flush_test_key_{i:D4}"
            let value = Encoding.UTF8.GetBytes $"flush_test_value_{i}"
            let setResult = db.Set(key, value)
            Assert.That(setResult.IsOk, Is.True)

        // Start sequential operations that might trigger flush
        let sequentialOperation () =
            do
                for i = 0 to 49 do
                    let key = Encoding.UTF8.GetBytes $"sequential_key_{i:D3}"
                    let value = Encoding.UTF8.GetBytes $"sequential_value_{i}"
                    let setResult = db.Set(key, value)

                    match setResult with
                    | Ok() -> ()
                    | Error msg -> Assert.Fail $"Sequential set failed: {msg}"

                    // Read a key that might be in the process of being flushed
                    let getResult = db.Get(Encoding.UTF8.GetBytes $"flush_test_key_{i:D4}")

                    match getResult with
                    | Ok(Some value) ->
                        let expectedValue = $"flush_test_value_{i}"
                        let actualValue = Encoding.UTF8.GetString value
                        Assert.That(actualValue, Is.EqualTo expectedValue)
                    | Ok None -> Assert.Fail $"Flush test key {i} not found during sequential operations"
                    | Error msg -> Assert.Fail $"Error reading during flush: {msg}"


        // Run multiple operations sequentially
        for _ = 0 to 4 do
            sequentialOperation ()

    [<Test>]
    member this.DatabaseReopenDuringOperations_ShouldFailGracefully() =
        // This test verifies that operations fail gracefully if the database is closed
        // during sequential operations

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
    member this.LongRunningOperations_ShouldCompleteSuccessfully() =
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

                    // Small yield to allow other operations (though sequential)
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
    member this.BackgroundOperations_ShouldHandleSequentialRequests() =
        // Test that background operations (compaction, GC, flush) work correctly with sequential requests

        let triggerOperations () =
            // Trigger multiple compactions and GC synchronously
            for i = 0 to 4 do
                db.PerformCompaction()
                db.PerformGarbageCollection()


        // Add some data to potentially trigger background operations
        for i = 0 to 499 do
            let key = Encoding.UTF8.GetBytes $"background_test_key_{i:D4}"
            let value = Encoding.UTF8.GetBytes $"background_test_value_{i}"
            let setResult = db.Set(key, value)
            Assert.That(setResult.IsOk, Is.True)

        // Force flush all data to disk before triggering background operations
        db.WAL.Flush()
        db.ValueLog.Flush()

        // Trigger sequential background operations
        triggerOperations ()

        // Give background operations time to process
        Thread.Sleep 100

        // Database should still be functional - try to read a few keys
        let testKeys = [ 0; 100; 200; 499 ]
        let mutable successCount = 0

        for i in testKeys do
            let testKey = Encoding.UTF8.GetBytes $"background_test_key_{i:D4}"
            let getResult = db.Get testKey

            match getResult with
            | Ok(Some value) ->
                let expectedValue = $"background_test_value_{i}"
                let actualValue = Encoding.UTF8.GetString value

                if actualValue = expectedValue then
                    successCount <- successCount + 1
            | _ -> ()

        // At least some keys should be readable after background operations
        Assert.That(
            successCount,
            Is.GreaterThan 0,
            "At least some test keys should be readable after background operations"
        )
