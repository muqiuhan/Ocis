module Ocis.Tests.HighLoadStabilityTests

open NUnit.Framework
open Ocis.OcisDB
open System.IO
open System.Text
open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Tests for high load stability issues that can cause NA (Not Available) results in benchmarks.
/// These tests specifically target the instability under high load conditions identified in AdvancedBenchmarks.
/// </summary>
[<TestFixture>]
type HighLoadStabilityTests () =

  let tempDir = "temp_high_load_stability"
  let mutable testDbPath = ""
  let mutable db : OcisDB | null = null

  [<SetUp>]
  member this.Setup () =
    if Directory.Exists tempDir then Directory.Delete (tempDir, true)
    Directory.CreateDirectory tempDir |> ignore
    testDbPath <- Path.Combine (tempDir, "high_load_stability_test_db")

  [<TearDown>]
  member this.TearDown () =
    if db <> null then
      try
        (db :> System.IDisposable).Dispose ()
      with ex ->
        printfn $"Warning: Error disposing DB: {ex.Message}"

    db <- null

    // Robust cleanup
    for attempt = 1 to 5 do
      try
        if Directory.Exists tempDir then Directory.Delete (tempDir, true)
        ()
      with ex ->
        if attempt = 5 then
          printfn $"Warning: Failed to cleanup {tempDir}: {ex.Message}"

        Thread.Sleep 100

  /// <summary>
  /// Test for memory pressure under high load - simulates the conditions that cause NA results
  /// </summary>
  [<Test>]
  member this.MemoryPressureUnderHighLoad_ShouldNotCauseFailure () =
    let dataSize = 25000 // Smaller size for testing
    let flushThreshold = 1000

    match OcisDB.Open (testDbPath, flushThreshold) with
    | Ok newDb ->
      db <- newDb

      try
        let keys =
          Array.init dataSize (fun i -> Encoding.UTF8.GetBytes $"key_{i:D6}")

        let values =
          Array.init dataSize (fun i ->
            let valueSize = 100 + (i % 400)

            Encoding.UTF8.GetBytes (
              $"value_{i}_" + new string ('x', valueSize)
            ))

        let batchSize = 5000
        let mutable batchIndex = 0

        for batchStart in 0..batchSize .. dataSize - 1 do
          let batchEnd = min (batchStart + batchSize - 1) (dataSize - 1)

          for i = batchStart to batchEnd do
            let setResult =
              db.Set (keys[i], values[i]) |> Async.RunSynchronously

            match setResult with
            | Ok () -> ()
            | Error msg -> Assert.Fail $"Failed to set key {i}: {msg}"

          db.WAL.Flush ()
          db.ValueLog.Flush ()

          GC.Collect ()
          GC.WaitForPendingFinalizers ()
          GC.Collect ()

          printfn
            $"Completed batch {batchIndex + 1}, memory: {GC.GetTotalMemory true / 1024L / 1024L}MB"

          batchIndex <- batchIndex + 1

        // Verify data integrity (sample)
        for i = 0 to min 1000 (dataSize - 1) do
          let getResult = db.Get keys[i] |> Async.RunSynchronously

          match getResult with
          | Ok (Some value) ->
            if
              not (System.Linq.Enumerable.SequenceEqual (value, values[i]))
            then
              Assert.Fail $"Data corruption detected for key {i}"
          | Ok None -> Assert.Fail $"Key {i} not found"
          | Error msg -> Assert.Fail $"Error reading key {i}: {msg}"

        printfn
          $"Successfully completed memory pressure test for data size {dataSize}"

      with ex ->
        Assert.Fail
          $"Memory pressure test failed for data size {dataSize}: {ex.Message}"

    | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

  /// <summary>
  /// Test for concurrent operations under high load
  /// </summary>
  [<Test>]
  member this.ConcurrentOperationsUnderHighLoad_ShouldNotDeadlock () =
    match OcisDB.Open (testDbPath, 1000) with
    | Ok newDb ->
      db <- newDb

      let operationCount = 5000
      let concurrentTasks = 5

      let operation taskId () =
        async {
          try
            for i = 0 to (operationCount / concurrentTasks) - 1 do
              let keyIndex = taskId * (operationCount / concurrentTasks) + i
              let key = Encoding.UTF8.GetBytes $"concurrent_key_{keyIndex:D6}"

              let value =
                Encoding.UTF8.GetBytes (
                  $"concurrent_value_{keyIndex}_" + new string ('c', 150)
                )

              if i % 2 = 0 then
                let! setResult = db.Set (key, value)

                match setResult with
                | Ok () -> ()
                | Error msg ->
                  failwith
                    $"Task {taskId}: Write failed for key {keyIndex}: {msg}"
              else
                let readKeyIndex = keyIndex - 1

                if readKeyIndex >= 0 then
                  let readKey =
                    Encoding.UTF8.GetBytes $"concurrent_key_{readKeyIndex:D6}"

                  let! getResult = db.Get readKey

                  match getResult with
                  | Ok (Some _) -> ()
                  | Ok None -> ()
                  | Error msg ->
                    failwith
                      $"Task {taskId}: Read failed for key {readKeyIndex}: {msg}"

              if i % 100 = 0 then
                db.WAL.Flush ()
                db.ValueLog.Flush ()

            return ()
          with ex ->
            return failwith $"Task {taskId} failed: {ex.Message}"
        }

      try
        let tasks =
          [ 0 .. concurrentTasks - 1 ] |> List.map (fun id -> operation id ())

        let asyncTasks = tasks |> List.map Async.StartAsTask

        let timeout = TimeSpan.FromMinutes 2.0

        let taskArray =
          asyncTasks
          |> Array.ofList
          |> Array.map (fun (t : Task<unit>) -> t :> Task)

        let allCompleted =
          Task.WaitAll (taskArray, int timeout.TotalMilliseconds)

        if not allCompleted then
          Assert.Fail "Concurrent operations timed out - possible deadlock"

        for task in taskArray do
          if task.Exception <> null then
            Assert.Fail $"Task failed with exception: {task.Exception.Message}"

        printfn $"Successfully completed {operationCount} concurrent operations"

      with
      | :? System.AggregateException as ae ->
        let innerExceptions = ae.Flatten().InnerExceptions

        let deadlockIndicators =
          [ "deadlock"
            "timeout"
            "hung"
            "blocked" ]

        let hasDeadlock =
          innerExceptions
          |> Seq.exists (fun ex ->
            deadlockIndicators
            |> List.exists (fun indicator ->
              ex.Message.ToLower().Contains indicator))

        if hasDeadlock then
          Assert.Fail
            $"Deadlock detected in concurrent operations: {ae.Message}"
        else
          Assert.Fail $"Concurrent operations failed: {ae.Message}"

      | ex ->
        Assert.Fail
          $"Unexpected exception in concurrent operations: {ex.Message}"

    | Error msg ->
      Assert.Fail $"Failed to open DB for concurrent operations test: {msg}"
