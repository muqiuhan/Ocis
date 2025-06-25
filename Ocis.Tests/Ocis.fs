module Ocis.Tests.OcisDB

open NUnit.Framework
open Ocis.OcisDB
open System.IO
open System.Text
open System.Threading
open System.Diagnostics // For Stopwatch

[<TestFixture>]
type OcisDBTests() =

    let tempDir = "temp_ocisdb_tests"
    let mutable testDbPath = ""

    [<SetUp>]
    member this.Setup() =
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory(tempDir) |> ignore
        testDbPath <- Path.Combine(tempDir, "ocisdb_instance")

    [<TearDown>]
    member this.TearDown() =
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.``Open_ShouldCreateNewDBAndInitializeCorrectly``() =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            Assert.That(Directory.Exists(testDbPath), Is.True)
            Assert.That(File.Exists(Path.Combine(testDbPath, "valog.vlog")), Is.True)
            Assert.That(File.Exists(Path.Combine(testDbPath, "wal.log")), Is.True)
            Assert.That(db.CurrentMemtbl.Count, Is.EqualTo(0))
            Assert.That(db.ImmutableMemtbl.IsEmpty, Is.True)
            Assert.That(db.SSTables.IsEmpty, Is.True)
        | Error msg -> Assert.Fail($"Failed to open DB: {msg}")

    [<Test>]
    member this.``SetAndGet_ShouldPersistAndRetrieveData``() =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let key = Encoding.UTF8.GetBytes("mykey")
            let value = Encoding.UTF8.GetBytes("myvalue")

            let setResult = db.Set(key, value) |> Async.RunSynchronously
            Assert.That(setResult.IsOk, Is.True)

            let getResult = db.Get(key) |> Async.RunSynchronously

            match getResult with
            | Ok(Some actualValue) -> Assert.That(Encoding.UTF8.GetString(actualValue), Is.EqualTo("myvalue"))
            | Ok None -> Assert.Fail("Key not found unexpectedly.")
            | Error msg -> Assert.Fail($"Failed to get value: {msg}")
        | Error msg -> Assert.Fail($"Failed to open DB: {msg}")

    [<Test>]
    member this.``SetAndGet_ShouldHandleUpdates``() =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let key = Encoding.UTF8.GetBytes("updatekey")
            let initialValue = Encoding.UTF8.GetBytes("initial")
            let updatedValue = Encoding.UTF8.GetBytes("updated")

            db.Set(key, initialValue) |> Async.RunSynchronously |> ignore
            db.Set(key, updatedValue) |> Async.RunSynchronously |> ignore

            let getResult = db.Get(key) |> Async.RunSynchronously

            match getResult with
            | Ok(Some actualValue) -> Assert.That(Encoding.UTF8.GetString(actualValue), Is.EqualTo("updated"))
            | Ok None -> Assert.Fail("Key not found unexpectedly after update.")
            | Error msg -> Assert.Fail($"Failed to get value after update: {msg}")
        | Error msg -> Assert.Fail($"Failed to open DB: {msg}")

    [<Test>]
    member this.``Delete_ShouldMarkAsDeletedAndNotRetrieve``() =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let key = Encoding.UTF8.GetBytes("deletekey")
            let value = Encoding.UTF8.GetBytes("deletevalue")

            db.Set(key, value) |> Async.RunSynchronously |> ignore

            let getBeforeDelete = db.Get(key) |> Async.RunSynchronously

            match getBeforeDelete with
            | Ok(Some _) -> () // Expected to find it before delete
            | Ok None -> Assert.Fail("Key unexpectedly not found before deletion.")
            | Error msg -> Assert.Fail($"Failed to get value before delete: {msg}")

            db.Delete(key) |> Async.RunSynchronously |> ignore

            // The value should now be marked as deleted (-1L in Memtbl), so Get should return None
            let getAfterDelete = db.Get(key) |> Async.RunSynchronously

            match getAfterDelete with
            | Ok None -> () // Expected None for deleted key
            | Ok(Some _) -> Assert.Fail("Key unexpectedly found after deletion.")
            | Error msg -> Assert.Fail($"Failed to get value after delete: {msg}")

        | Error msg -> Assert.Fail($"Failed to open DB: {msg}")

    [<Test>]
    member this.``Set_ShouldTriggerMemtableFlush``() =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let flushThreshold = 100 // Defined in Ocis.fs

            // Add 100 entries to trigger flush
            for i = 0 to flushThreshold - 1 do
                let key = Encoding.UTF8.GetBytes($"flushkey{i}")
                let value = Encoding.UTF8.GetBytes($"flushvalue{i}")
                db.Set(key, value) |> Async.RunSynchronously |> ignore

            // After 100 entries, the current Memtable should be empty, and immutable Memtable should have one
            Assert.That(db.CurrentMemtbl.Count, Is.EqualTo(0), "CurrentMemtbl should be empty after flush.")
            Assert.That(db.ImmutableMemtbl.Count, Is.EqualTo(1), "ImmutableMemtbl should contain the flushed memtable.")

            // Give compaction agent some time to flush to SSTable
            Thread.Sleep(500) // Small delay to allow agent to process message

            // Verify if an SSTable file is created (level 0)
            let sstblFiles = Directory.GetFiles(testDbPath, "sstbl-*.sst")
            Assert.That(sstblFiles.Length, Is.GreaterThanOrEqualTo(1), "At least one SSTable file should be created.")

            // Verify a key from the flushed memtable can still be retrieved from SSTable
            let keyToRetrieve = Encoding.UTF8.GetBytes("flushkey50")
            let valueToRetrieve = Encoding.UTF8.GetBytes("flushvalue50")

            let getResult = db.Get(keyToRetrieve) |> Async.RunSynchronously

            match getResult with
            | Ok(Some actualValue) ->
                Assert.That(Encoding.UTF8.GetString(actualValue), Is.EqualTo(Encoding.UTF8.GetString(valueToRetrieve)))
            | Ok None -> Assert.Fail("Key not found unexpectedly after flush.")
            | Error msg -> Assert.Fail($"Failed to get value after flush: {msg}")

        | Error msg -> Assert.Fail($"Failed to open DB: {msg}")

    [<Test>]
    member this.``Close_ShouldDisposeAllResources``() =
        let dbOption = OcisDB.Open(testDbPath)

        match dbOption with
        | Ok db ->
            // Simulate closing the DB
            (db :> System.IDisposable).Dispose()

            // Verify that WAL and Valog files can be reopened, indicating their streams are closed
            Assert.That(
                fun () ->
                    use walFs =
                        new FileStream(
                            Path.Combine(testDbPath, "wal.log"),
                            FileMode.Open,
                            FileAccess.ReadWrite,
                            FileShare.None
                        )

                    use valogFs =
                        new FileStream(
                            Path.Combine(testDbPath, "valog.vlog"),
                            FileMode.Open,
                            FileAccess.ReadWrite,
                            FileShare.None
                        )

                    Assert.That(walFs.CanRead, Is.True)
                    Assert.That(valogFs.CanRead, Is.True)
                , Throws.Nothing
                , "WAL and Valog streams should be closed after Dispose."
            )

        // Further tests can be added here to check if SSTable streams are also closed,
        // but this is harder to test directly without knowing their exact paths beforehand.
        | Error msg -> Assert.Fail($"Failed to open DB: {msg}")

[<TestFixture>]
type OcisDBPerformanceTests() =

    let tempDir = "temp_ocisdb_performance_tests"
    let mutable testDbPath = ""

    [<SetUp>]
    member this.Setup() =
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory(tempDir) |> ignore
        testDbPath <- Path.Combine(tempDir, "ocisdb_performance_instance")

    [<TearDown>]
    member this.TearDown() =
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

    /// <summary>
    /// Tests the performance of bulk Set operations.
    /// </summary>
    /// <param name="count">The number of key-value pairs to set.</param>
    [<TestCase(1000)>]
    [<TestCase(10000)>]
    [<TestCase(50000)>]
    member this.``BulkSet_ShouldPerformEfficiently``(count: int) =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let stopwatch = Stopwatch.StartNew()

            for i = 0 to count - 1 do
                let key = Encoding.UTF8.GetBytes($"perf_key_{i}")
                let value = Encoding.UTF8.GetBytes($"perf_value_{i}_" + new string ('A', 100)) // Value ~100 bytes
                db.Set(key, value) |> Async.RunSynchronously |> ignore

            // Give compaction agent some time to flush to SSTable
            Thread.Sleep(500) // Small delay to allow agent to process message

            stopwatch.Stop()
            printfn "\nBulk Set for %d entries completed in %f ms" count stopwatch.Elapsed.TotalMilliseconds
            Assert.Pass()
        | Error msg -> Assert.Fail($"Failed to open DB for Bulk Set test: {msg}")

    /// <summary>
    /// Tests the performance of bulk Get operations.
    /// </summary>
    /// <param name="count">The number of key-value pairs to get.</param>
    [<TestCase(1000)>]
    [<TestCase(10000)>]
    [<TestCase(50000)>]
    member this.``BulkGet_ShouldPerformEfficiently``(count: int) =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let keysToGet = ResizeArray<byte array>()

            // First, populate the database with data
            printfn "Populating DB with %d entries for Bulk Get test..." count

            for i = 0 to count - 1 do
                let key = Encoding.UTF8.GetBytes($"perf_key_{i}")
                let value = Encoding.UTF8.GetBytes($"perf_value_{i}_" + new string ('A', 100))
                db.Set(key, value) |> Async.RunSynchronously |> ignore
                keysToGet.Add(key)

            // Give compaction agent some time to flush to SSTable
            Thread.Sleep(500) // Small delay to allow agent to process message
            printfn "DB populated. Starting Bulk Get..."

            let stopwatch = Stopwatch.StartNew()
            let mutable foundCount = 0

            for key in keysToGet do
                let getResult = db.Get(key) |> Async.RunSynchronously

                match getResult with
                | Ok(Some _) -> foundCount <- foundCount + 1
                | Ok None -> ()
                | Error msg -> Assert.Fail($"Failed to get key {Encoding.UTF8.GetString(key)}: {msg}")

            stopwatch.Stop()

            printfn
                "Bulk Get for %d entries (found %d) completed in %f ms"
                count
                foundCount
                stopwatch.Elapsed.TotalMilliseconds

            Assert.That(foundCount, Is.EqualTo(count), "All keys should be found.")
        | Error msg -> Assert.Fail($"Failed to open DB for Bulk Get test: {msg}")

    /// <summary>
    /// Tests the performance of a mixed workload (Set and Get operations).
    /// </summary>
    /// <param name="count">The total number of operations (half Set, half Get).</param>
    [<TestCase(1000)>]
    [<TestCase(10000)>]
    [<TestCase(50000)>]
    member this.``MixedWorkload_ShouldPerformEfficiently``(count: int) =
        match OcisDB.Open(testDbPath) with
        | Ok db ->
            use db = db
            let numSets = count / 2
            let numGets = count - numSets
            let keys = ResizeArray<byte array>()

            let stopwatch = Stopwatch.StartNew()

            // Perform Set operations
            for i = 0 to numSets - 1 do
                let key = Encoding.UTF8.GetBytes($"mixed_key_{i}")
                let value = Encoding.UTF8.GetBytes($"mixed_value_{i}" + new string ('B', 50))
                db.Set(key, value) |> Async.RunSynchronously |> ignore
                keys.Add(key)

            // Give compaction agent some time to flush to SSTable
            Thread.Sleep(500) // Small delay to allow agent to process message

            // Perform Get operations (on existing and some non-existing keys)
            let mutable foundCount = 0

            for i = 0 to numGets - 1 do
                let keyIndex = i % keys.Count // Get existing keys
                let keyToGet = keys.[keyIndex]
                let getResult = db.Get(keyToGet) |> Async.RunSynchronously

                match getResult with
                | Ok(Some _) -> foundCount <- foundCount + 1
                | Ok None -> ()
                | Error msg ->
                    Assert.Fail($"Failed to get key in mixed workload {Encoding.UTF8.GetString(keyToGet)}: {msg}")

            stopwatch.Stop()

            printfn
                "\nMixed workload (%d Sets, %d Gets) completed in %f ms"
                numSets
                numGets
                stopwatch.Elapsed.TotalMilliseconds

            Assert.Pass()
        | Error msg -> Assert.Fail($"Failed to open DB for Mixed Workload test: {msg}")
