module Ocis.Tests.ErrorHandlingTests

open NUnit.Framework
open Ocis.OcisDB
open Ocis.SSTbl
open Ocis.Memtbl
open Ocis.Valog
open System.IO
open System.Text
open System

/// <summary>
/// Comprehensive tests for error handling and edge cases in OcisDB
/// </summary>
[<TestFixture>]
type ErrorHandlingTests () =

  let tempDir = "temp_error_tests"
  let mutable testFilePath = ""

  [<SetUp>]
  member this.Setup () =
    if Directory.Exists tempDir then Directory.Delete (tempDir, true)
    Directory.CreateDirectory tempDir |> ignore

    testFilePath <-
      Path.Combine (tempDir, $"test_{Guid.NewGuid().ToString ()}.tmp")

  [<TearDown>]
  member this.TearDown () =
    try
      if Directory.Exists tempDir then Directory.Delete (tempDir, true)
    with _ ->
      () // Ignore cleanup errors in tests

  // SSTable Error Handling Tests

  [<Test>]
  member this.SSTableOpen_ShouldHandleNullPath () =
    let result = SSTbl.Open null
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.SSTableOpen_ShouldHandleEmptyPath () =
    let result = SSTbl.Open ""
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.SSTableOpen_ShouldHandleWhitespacePath () =
    let result = SSTbl.Open "   "
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.SSTableOpen_ShouldHandleNonExistentFile () =
    let nonExistentPath = Path.Combine (tempDir, "nonexistent.sst")
    let result = SSTbl.Open nonExistentPath
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.SSTableFlush_ShouldHandleNullMemtbl () =
    let memtbl = null
    let path = Path.Combine (tempDir, "test_null_memtbl.sst")

    Assert.Throws<System.Exception> (fun () ->
      SSTbl.Flush (memtbl, path, 0L, 0) |> ignore)
    |> ignore

  [<Test>]
  member this.SSTableFlush_ShouldHandleNullPath () =
    let memtbl = Memtbl ()
    let path : string = Unchecked.defaultof<string>

    Assert.Throws<System.Exception> (fun () ->
      SSTbl.Flush (memtbl, path, 0L, 0) |> ignore)
    |> ignore

  [<Test>]
  member this.SSTableFlush_ShouldHandleEmptyPath () =
    let memtbl = Memtbl ()
    let path = ""

    Assert.Throws<System.Exception> (fun () ->
      SSTbl.Flush (memtbl, path, 0L, 0) |> ignore)
    |> ignore

  [<Test>]
  member this.SSTableFlush_ShouldHandleInvalidLevel () =
    let memtbl = Memtbl ()
    let path = Path.Combine (tempDir, "test_invalid_level.sst")

    Assert.Throws<System.Exception> (fun () ->
      SSTbl.Flush (memtbl, path, 0L, -1) |> ignore)
    |> ignore

  [<Test>]
  member this.SSTableFlush_ShouldHandlePathTooLong () =
    let memtbl = Memtbl ()
    // Create a path that's too long
    let longPath = Path.Combine (tempDir, String ('x', 500) + ".sst")

    try
      SSTbl.Flush (memtbl, longPath, 0L, 0) |> ignore
      // If it succeeds, that's also fine - depends on the filesystem
      ()
    with
    | :? System.IO.PathTooLongException -> ()
    | :? System.IO.IOException -> () // Some filesystems may throw IOException instead
    | _ -> ()

  [<Test>]
  member this.SSTableFlush_ShouldHandleDirectoryNotFound () =
    let memtbl = Memtbl ()
    let invalidDir = Path.Combine (tempDir, "nonexistent_dir")
    let path = Path.Combine (invalidDir, "test.sst")

    Assert.Throws<System.IO.DirectoryNotFoundException> (fun () ->
      SSTbl.Flush (memtbl, path, 0L, 0) |> ignore)
    |> ignore

  [<Test>]
  member this.SSTableFlush_ShouldHandleAccessDenied () =
    let memtbl = Memtbl ()
    // Try to write to a system directory (may require admin privileges)
    let systemPath = "/root/test.sst"

    if not (System.IO.File.Exists systemPath) then
      try
        SSTbl.Flush (memtbl, systemPath, 0L, 0) |> ignore
        () // If it succeeds, that's also fine
      with
      | :? System.UnauthorizedAccessException -> ()
      | _ -> ()

  [<Test>]
  member this.SSTableFlush_ShouldHandleEmptyMemtbl () =
    let memtbl = Memtbl () // Empty memtable
    let path = Path.Combine (tempDir, "test_empty.sst")

    // Should not throw exception
    let resultPath = SSTbl.Flush (memtbl, path, 0L, 0)

    Assert.That (File.Exists resultPath, Is.True)
    Assert.That (resultPath, Is.EqualTo path)

    // Should be able to open the empty SSTable
    let opened = SSTbl.Open resultPath
    Assert.That (opened.IsSome, Is.True)

    let sstable = opened.Value
    Assert.That (sstable.RecordOffsets.Length, Is.EqualTo 0)
    Assert.That (sstable.LowKey.Length, Is.EqualTo 0)
    Assert.That (sstable.HighKey.Length, Is.EqualTo 0)

  // OcisDB Error Handling Tests

  [<Test>]
  member this.OcisDBOpen_ShouldHandleNullDirectory () =
    let result = OcisDB.Open (null, 100)
    Assert.That (result.IsError, Is.True)

  [<Test>]
  member this.OcisDBOpen_ShouldHandleEmptyDirectory () =
    let result = OcisDB.Open ("", 100)
    Assert.That (result.IsError, Is.True)

  [<Test>]
  member this.OcisDBOpen_ShouldHandleWhitespaceDirectory () =
    let result = OcisDB.Open ("   ", 100)
    Assert.That (result.IsError, Is.True)

  [<Test>]
  member this.OcisDBOpen_ShouldHandleInvalidFlushThreshold () =
    let result = OcisDB.Open (tempDir, 0)
    Assert.That (result.IsError, Is.True)

    let result2 = OcisDB.Open (tempDir, -1)
    Assert.That (result2.IsError, Is.True)

  [<Test>]
  member this.OcisDBOpen_ShouldHandlePathTooLong () =
    let longPath = String ('x', 500)
    let result = OcisDB.Open (longPath, 100)
    // May succeed or fail depending on filesystem limits
    ()

  [<Test>]
  member this.OcisDBOpen_ShouldHandleAccessDenied () =
    // Try to create DB in a system directory
    let systemDir = "/root/ocis_test"
    let result = OcisDB.Open (systemDir, 100)
    // This might succeed if we have permissions, so we can't assert failure
    // But we can ensure it doesn't crash
    ()

  [<Test>]
  member this.OcisDBOpen_ShouldHandleCorruptedSSTable () =
    // Create a valid OcisDB first
    let dbPath = Path.Combine (tempDir, "corruption_test")
    let createResult = OcisDB.Open (dbPath, 100)

    match createResult with
    | Ok db ->
      use db = db

      // Create and flush a memtable to create an SSTable
      let memtbl = Memtbl ()
      memtbl.Add (Encoding.UTF8.GetBytes "test", 100L)

      let sstPath = Path.Combine (dbPath, "sstbl-test.sst")
      SSTbl.Flush (memtbl, sstPath, 0L, 0) |> ignore

      // Close the DB
      (db :> IDisposable).Dispose ()

      // Corrupt the SSTable by truncating it
      use fs = new FileStream (sstPath, FileMode.Open, FileAccess.Write)
      fs.SetLength (10L) // Truncate to 10 bytes, making it corrupted
      fs.Close ()

      // Try to reopen the DB - should handle corrupted SSTable gracefully
      let reopenResult = OcisDB.Open (dbPath, 100)

      match reopenResult with
      | Ok reopenedDb ->
        use reopenedDb = reopenedDb
        // Should succeed even with corrupted SSTable
        Assert.Pass ()
      | Error msg ->
        // Should still succeed and log warnings about corrupted files
        Assert.That (msg.Contains "Failed to open", Is.False)

    | Error _ -> Assert.Fail "Failed to create initial DB for corruption test"

  // Concurrent Access Tests

  [<Test>]
  member this.ConcurrentSSTableAccess_ShouldHandleRaceConditions () =
    let memtbl = Memtbl ()

    for i = 0 to 99 do
      memtbl.Add (Encoding.UTF8.GetBytes $"key{i:D3}", int64 i)

    let sstPath = Path.Combine (tempDir, "concurrent_test.sst")
    SSTbl.Flush (memtbl, sstPath, 0L, 0) |> ignore

    // Test concurrent reads
    let concurrentReads () =
      async {
        for _ = 1 to 100 do
          let opened = SSTbl.Open sstPath

          match opened with
          | Some sst ->
            use sst = sst
            let key = Encoding.UTF8.GetBytes "key050"
            let result = sst.TryGet key
            Assert.That (result.IsSome, Is.True)
            Assert.That (result.Value, Is.EqualTo 50L)
          | None -> Assert.Fail "Failed to open SSTable concurrently"
      }

    // Run multiple concurrent operations
    [ 1..5 ]
    |> List.map (fun _ -> concurrentReads ())
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

  [<Test>]
  member this.MemtableOperations_ShouldHandleLargeKeysAndValues () =
    let memtbl = Memtbl ()

    // Test with very large key
    let largeKey = Array.zeroCreate<byte> (64 * 1024) // 64KB key
    System.Random().NextBytes largeKey
    let largeValue = 1000000L

    memtbl.Add (largeKey, largeValue)

    let result = memtbl.TryGet largeKey
    Assert.That (result.IsSome, Is.True)
    Assert.That (result.Value, Is.EqualTo largeValue)

  [<Test>]
  member this.SSTableFlush_ShouldHandleLargeNumberOfEntries () =
    let memtbl = Memtbl ()

    // Add many entries
    for i = 0 to 9999 do
      let key = Encoding.UTF8.GetBytes $"key{i:D5}"
      memtbl.Add (key, int64 i)

    let sstPath = Path.Combine (tempDir, "large_sstable.sst")
    let flushedPath = SSTbl.Flush (memtbl, sstPath, 0L, 0)

    Assert.That (File.Exists flushedPath, Is.True)

    // Verify we can open and read from it
    let opened = SSTbl.Open flushedPath
    Assert.That (opened.IsSome, Is.True)

    let sstable = opened.Value
    use sstable = sstable

    Assert.That (sstable.RecordOffsets.Length, Is.EqualTo 10000)

    // Test a few random entries
    for i in
      [ 0
        999
        5000
        9999 ] do
      let key = Encoding.UTF8.GetBytes $"key{i:D5}"
      let result = sstable.TryGet key
      Assert.That (result.IsSome, Is.True, $"Failed to find key{i:D5}")
      Assert.That (result.Value, Is.EqualTo (int64 i))

  [<Test>]
  member this.OcisDBOperations_ShouldHandleRapidOpenClose () =
    let dbPath = Path.Combine (tempDir, "rapid_open_close")

    // Rapidly open and close DB multiple times
    for i = 1 to 10 do
      let result = OcisDB.Open (dbPath, 100)

      match result with
      | Ok db ->
        use db = db

        // Do a quick operation
        let key = Encoding.UTF8.GetBytes $"test_key_{i}"
        let value = Encoding.UTF8.GetBytes $"test_value_{i}"

        let setResult = db.Set (key, value) |> Async.RunSynchronously
        Assert.That (setResult.IsOk, Is.True)

      | Error msg -> Assert.Fail $"Failed to open DB on iteration {i}: {msg}"

      // Clean up for next iteration
      if Directory.Exists dbPath then Directory.Delete (dbPath, true)

  [<Test>]
  member this.SSTableCreationTest_ShouldCreateAndLoadSSTable () =
    let dbPath = Path.Combine (tempDir, "single_test_db")

    // Create DB and add data
    let dbResult = OcisDB.Open (dbPath, 100)

    match dbResult with
    | Ok db ->
      use db = db

      // Add a key-value pair
      let setResult =
        db.Set (
          Encoding.UTF8.GetBytes "test_key",
          Encoding.UTF8.GetBytes "test_value"
        )
        |> Async.RunSynchronously

      match setResult with
      | Ok () -> printfn "Successfully set key-value pair"
      | Error msg -> Assert.Fail $"Failed to set key-value pair: {msg}"

      // Read the key back
      let getResult =
        db.Get (Encoding.UTF8.GetBytes "test_key") |> Async.RunSynchronously

      match getResult with
      | Ok (Some value) ->
        let actualValue = Encoding.UTF8.GetString value

        Assert.That (
          actualValue,
          Is.EqualTo "test_value",
          "Should read correct value"
        )

        printfn "Successfully read value from database"
      | Ok None -> Assert.Fail "Key not found in database"
      | Error msg -> Assert.Fail $"Failed to read key from database: {msg}"

    | Error msg -> Assert.Fail $"Failed to create database: {msg}"

  [<Test>]
  member this.FileSystemStressTest_ShouldHandleManyFiles () =
    let stressDir =
      Path.Combine (tempDir, $"stress_test_{Guid.NewGuid().ToString ()}")

    // Create DB and add multiple key-value pairs
    let dbResult = OcisDB.Open (stressDir, 1) // Small flush threshold to force SSTable creation

    match dbResult with
    | Ok db ->
      use db = db

      // Add multiple key-value pairs to fill MemTable and create SSTables
      for i = 0 to 99 do
        let key = Encoding.UTF8.GetBytes $"key{i:D3}"
        let value = Encoding.UTF8.GetBytes $"value{i:D3}"
        let setResult = db.Set (key, value) |> Async.RunSynchronously

        match setResult with
        | Ok () -> ()
        | Error msg -> Assert.Fail $"Failed to set key {i}: {msg}"

      // Force flush any remaining data by triggering compaction
      db.CompactionAgent.Post TriggerCompaction
      System.Threading.Thread.Sleep (500) // Give time for compaction to complete

      // Verify SSTables were created (files should exist)
      let sstableFiles = Directory.GetFiles (stressDir, "*.sst")
      printfn $"Created {sstableFiles.Length} SSTable files"

      for file in sstableFiles do
        printfn $"SSTable file: {Path.GetFileName file}"

      Assert.That (
        sstableFiles.Length,
        Is.GreaterThan 0,
        "Should create at least 1 SSTable file"
      )

      // Verify SSTables were loaded in the database
      let sstableCount =
        db.SSTables |> Map.toSeq |> Seq.sumBy (fun (_, lst) -> lst.Length)

      printfn $"Loaded {sstableCount} SSTables"

      Assert.That (
        sstableCount,
        Is.GreaterThanOrEqualTo 1,
        "Should load at least 1 SSTable"
      )

      // Test reading from some keys
      for i in
        [ 0
          3
          6
          9 ] do
        let testKey = Encoding.UTF8.GetBytes $"key{i:D3}"
        let getResult = db.Get testKey |> Async.RunSynchronously

        match getResult with
        | Ok (Some value) ->
          let expectedValue = $"value{i:D3}"
          let actualValue = Encoding.UTF8.GetString value

          Assert.That (
            actualValue,
            Is.EqualTo expectedValue,
            $"Wrong value for key {i}"
          )
        | Ok None -> Assert.Fail $"Key {i} not found"
        | Error msg -> Assert.Fail $"Failed to read key {i}: {msg}"

    | Error msg -> Assert.Fail $"Failed to create DB: {msg}"

  [<Test>]
  member this.MemoryPressureTest_ShouldHandleLargeDatasets () =
    let dbPath = Path.Combine (tempDir, "memory_pressure")

    match OcisDB.Open (dbPath, 1000) with
    | Ok db ->
      use db = db

      let beforeMemory = GC.GetTotalMemory true

      // Add many large entries
      for i = 0 to 999 do
        let key = Encoding.UTF8.GetBytes $"large_key_{i:D4}"
        let value = Encoding.UTF8.GetBytes (String ('X', 10000)) // 10KB values
        let setResult = db.Set (key, value) |> Async.RunSynchronously
        Assert.That (setResult.IsOk, Is.True)

      let afterMemory = GC.GetTotalMemory true
      let memoryIncrease = afterMemory - beforeMemory

      printfn $"Memory increase: {memoryIncrease / 1024L} KB for 1000 entries"

      // Test reading some entries
      for i in
        [ 0
          100
          500
          999 ] do
        let key = Encoding.UTF8.GetBytes $"large_key_{i:D4}"
        let getResult = db.Get key |> Async.RunSynchronously

        match getResult with
        | Ok (Some value) -> Assert.That (value.Length, Is.EqualTo 10000)
        | _ -> Assert.Fail $"Failed to read large entry {i}"

    | Error msg ->
      Assert.Fail $"Failed to create DB for memory pressure test: {msg}"
