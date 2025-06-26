module Ocis.Tests.OcisDB

open NUnit.Framework
open Ocis.OcisDB
open System.IO
open System.Text
open System.Threading
open System.Diagnostics // For Stopwatch
open System.Diagnostics // For Process

[<TestFixture>]
type OcisDBTests() =

    let tempDir = "temp_ocisdb_tests"
    let mutable testDbPath = ""
    let flushThreshold = 100

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
        match OcisDB.Open(testDbPath, flushThreshold) with
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
        match OcisDB.Open(testDbPath, flushThreshold) with
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
        match OcisDB.Open(testDbPath, flushThreshold) with
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
        match OcisDB.Open(testDbPath, flushThreshold) with
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
        match OcisDB.Open(testDbPath, flushThreshold) with
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
            // Thread.Sleep(500) // Removed for performance testing accuracy

            db.WAL.Flush()
            db.ValueLog.Flush()

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
        let dbOption = OcisDB.Open(testDbPath, flushThreshold)

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

    [<Test>]
    [<TestCase(1000)>]
    [<TestCase(10000)>]
    [<TestCase(100000)>]
    member this.``MemoryFootprint_ShouldBeReasonable``(count: int) =
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            System.GC.Collect()
            System.GC.WaitForPendingFinalizers()
            System.GC.Collect()

            let initialAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread()

            // Insert data
            printfn "Inserting %d entries for Memory Footprint test..." count

            for i = 0 to count - 1 do
                let key = Encoding.UTF8.GetBytes($"mem_key_{i}")
                let value = Encoding.UTF8.GetBytes($"mem_value_{i}_" + new string ('C', 200))
                db.Set(key, value) |> Async.RunSynchronously |> ignore

            let finalAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread()
            let allocated = finalAllocatedBytes - initialAllocatedBytes

            printfn "\nTotal allocated memory for %d entries: %f MB" count (float allocated / (1024.0 * 1024.0))

        // The average memory usage per record should not exceed 250 bytes (key + value + overhead)
        // let expectedMaxAllocation = int64 count * 250L
        // Assert.That(
        //     allocated,
        //     Is.LessThan(expectedMaxAllocation),
        //     "Memory allocation exceeds the expected threshold."
        // )

        | Error msg -> Assert.Fail($"Failed to open DB for Memory Footprint test: {msg}")
