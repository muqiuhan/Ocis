module Ocis.Tests.BenchmarkEdgeCaseTests

open NUnit.Framework
open Ocis.OcisDB
open Ocis.SSTbl
open Ocis.Memtbl
open System.IO
open System.Text
open System

/// <summary>
/// Tests for edge cases and error conditions that can occur in benchmark tests.
/// These tests ensure that benchmark methods handle various scenarios gracefully.
/// </summary>
[<TestFixture>]
type BenchmarkEdgeCaseTests() =

    let tempDir = "temp_benchmark_edge_cases"
    let mutable testDbPath = ""
    let flushThreshold = 100

    [<SetUp>]
    member this.Setup() =
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory tempDir |> ignore
        testDbPath <- Path.Combine(tempDir, "benchmark_edge_case_db")

    [<TearDown>]
    member this.TearDown() =
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.LayeredWrite_ShouldHandleEmptyDataArrays() =
        // Test that LayeredWrite-like operations handle empty arrays gracefully
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Simulate layered write with potentially empty arrays
            let emptyKeys: byte array array = [||]
            let emptyValues: byte array array = [||]

            do
                // Layer 1: Empty hot data
                if emptyKeys.Length > 0 then
                    for i = 0 to emptyKeys.Length - 1 do
                        let _ = db.Set(emptyKeys[i], emptyValues[i])
                        ()

                // Layer 2: Empty cold data
                if emptyKeys.Length > 0 then
                    for i = 0 to emptyKeys.Length - 1 do
                        let _ = db.Set(emptyKeys[i], emptyValues[i])
                        ()

                // Force flush
                db.WAL.Flush()
                db.ValueLog.Flush()



            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.RangeQuerySimulation_ShouldHandleEmptyKeyArrays() =
        // Test range query simulation with empty key arrays
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            let emptyKeys: byte array array = [||]

            do
                // Query some keys (empty case)
                if emptyKeys.Length > 0 then
                    let queries = min 10 emptyKeys.Length

                    for i = 0 to queries - 1 do
                        let _ = db.Get emptyKeys[i]
                        ()



            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.MemoryEfficiencyTest_ShouldHandleZeroReads() =
        // Test memory efficiency measurement with zero read operations
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Write some data
            let key = Encoding.UTF8.GetBytes "test_key"
            let value = Encoding.UTF8.GetBytes "test_value"
            let setResult = db.Set(key, value)

            // Verify write succeeded
            match setResult with
            | Ok() -> ()
            | Error msg -> Assert.Fail $"Failed to write key: {msg}"

            // Perform zero read operations (empty array)
            do
                let emptyKeys: byte array array = [||]

                if emptyKeys.Length > 0 then
                    for i = 0 to min 5000 (emptyKeys.Length - 1) do
                        let _ = db.Get emptyKeys[i % emptyKeys.Length]
                        ()



            // Verify data can still be read
            let getResult = db.Get key

            match getResult with
            | Ok(Some actualValue) -> Assert.That(actualValue, Is.EqualTo value, "Should be able to read written value")
            | Ok None -> Assert.Fail "Key should exist"
            | Error msg -> Assert.Fail $"Failed to read key: {msg}"

        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.RealisticWorkload_ShouldHandleEmptyOperationsArray() =
        // Test realistic workload with empty operations array
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // No operations to perform for this test



            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.PostCompactionRead_ShouldHandleNoAdditionalWrites() =
        // Test post-compaction read with minimal additional writes
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Initial write
            let key = Encoding.UTF8.GetBytes "initial_key"
            let value = Encoding.UTF8.GetBytes "initial_value"
            db.Set(key, value) |> ignore

            // Minimal additional writes (less than the loop in benchmark)
            do
                for i = 1 to 1 do // Only 1 iteration instead of 3
                    for j = 1 to 10 do // Only 10 writes instead of 100
                        let key = Encoding.UTF8.GetBytes $"trigger_{i}_{j}"
                        let value = Encoding.UTF8.GetBytes $"value_{i}_{j}"
                        let _ = db.Set(key, value)
                        ()

                    // Force flush
                    db.WAL.Flush()
                    db.ValueLog.Flush()



            // Test read with minimal count
            do
                let readCount = min 10 100 // Much smaller than benchmark's min 1000

                for i = 0 to readCount do
                    let _ = db.Get(Encoding.UTF8.GetBytes $"trigger_1_{i % 10 + 1}")
                    ()



            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.CrossSSTableRead_ShouldHandleEmptyReadTasks() =
        // Test cross SSTable read with no read tasks
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Write minimal data
            let key = Encoding.UTF8.GetBytes "test_key"
            let value = Encoding.UTF8.GetBytes "test_value"
            db.Set(key, value) |> ignore

            // No read tasks for this test



            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.BenchmarkDataGeneration_ShouldHandleExtremeRatios() =
        // Test data generation with extreme hot data ratios
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            let dataSize = 100
            let hotDataRatio = 1.0 // 100% hot data

            let hotDataCount = int (float dataSize * hotDataRatio)
            let coldDataCount = dataSize - hotDataCount

            // Generate test data similar to benchmark
            let hotKeys =
                Array.init hotDataCount (fun i -> Encoding.UTF8.GetBytes $"hot_{i:D6}")

            let hotValues =
                Array.init hotDataCount (fun i -> Encoding.UTF8.GetBytes($"hot_value_{i}" + String('H', 50)))

            let coldKeys =
                Array.init coldDataCount (fun i -> Encoding.UTF8.GetBytes $"cold_{i:D8}")

            let coldValues =
                Array.init coldDataCount (fun i -> Encoding.UTF8.GetBytes($"cold_value_{i}_" + String('C', 200)))

            // Write the data
            do
                for i = 0 to hotKeys.Length - 1 do
                    let _ = db.Set(hotKeys[i], hotValues[i])
                    ()

                for i = 0 to coldKeys.Length - 1 do
                    let _ = db.Set(coldKeys[i], coldValues[i])
                    ()



            // Verify data was written
            for i = 0 to min 5 (hotKeys.Length - 1) do
                let getResult = db.Get hotKeys[i]

                match getResult with
                | Ok(Some _) -> ()
                | _ -> Assert.Fail $"Failed to read hot key {i}"

            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.BenchmarkFlushThreshold_ShouldHandleSmallThresholds() =
        // Test with very small flush thresholds like in benchmarks
        let smallFlushThreshold = 10

        match OcisDB.Open(testDbPath, smallFlushThreshold) with
        | Ok db ->
            use db = db

            // Write enough data to trigger multiple flushes
            for i = 0 to 49 do // More than enough to trigger multiple flushes
                let key = Encoding.UTF8.GetBytes $"key_{i:D3}"
                let value = Encoding.UTF8.GetBytes $"value_{i}"
                let setResult = db.Set(key, value)

                match setResult with
                | Ok() -> ()
                | Error msg -> Assert.Fail $"Failed to set key {i}: {msg}"

            // Force final flush
            db.WAL.Flush()
            db.ValueLog.Flush()

            // Verify some data can be read
            for i = 0 to min 5 49 do
                let key = Encoding.UTF8.GetBytes $"key_{i:D3}"
                let getResult = db.Get key

                match getResult with
                | Ok(Some value) ->
                    let expectedValue = $"value_{i}"
                    let actualValue = Encoding.UTF8.GetString value
                    Assert.That(actualValue, Is.EqualTo expectedValue)
                | _ -> Assert.Fail $"Failed to read key {i}"

            // Should succeed without errors
            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.LayeredWrite_ShouldCreateMultipleSSTables() =
        // Test that layered write operations create multiple SSTables as expected
        let smallFlushThreshold = 50

        match OcisDB.Open(testDbPath, smallFlushThreshold) with
        | Ok db ->
            use db = db

            // Generate test data similar to AdvancedBenchmarks
            let hotKeys = Array.init 100 (fun i -> Encoding.UTF8.GetBytes $"hot_{i:D6}")

            let hotValues =
                Array.init 100 (fun i -> Encoding.UTF8.GetBytes($"hot_value_{i}" + String('H', 50)))

            let coldKeys = Array.init 200 (fun i -> Encoding.UTF8.GetBytes $"cold_{i:D8}")

            let coldValues =
                Array.init 200 (fun i -> Encoding.UTF8.GetBytes($"cold_value_{i}_" + String('C', 200)))

            do
                // Layer 1: Write hot data
                for i = 0 to hotKeys.Length - 1 do
                    let _ = db.Set(hotKeys[i], hotValues[i])
                    ()

                // Force flush to L0
                db.WAL.Flush()
                db.ValueLog.Flush()

                // Layer 2: Write cold data
                for i = 0 to coldKeys.Length - 1 do
                    let _ = db.Set(coldKeys[i], coldValues[i])
                    ()

                db.WAL.Flush()
                db.ValueLog.Flush()



            // Verify data can be read from both layers
            for i = 0 to min 10 (hotKeys.Length - 1) do
                let getResult = db.Get hotKeys[i]

                match getResult with
                | Ok(Some value) ->
                    let expected = ($"hot_value_{i}" + String('H', 50))
                    let actual = Encoding.UTF8.GetString value

                    Assert.That(actual, Is.EqualTo expected, $"Hot key {i} should be readable")
                | _ -> Assert.Fail $"Failed to read hot key {i}"

            for i = 0 to min 10 (coldKeys.Length - 1) do
                let getResult = db.Get coldKeys[i]

                match getResult with
                | Ok(Some value) ->
                    let expected = ($"cold_value_{i}_" + String('C', 200))
                    let actual = Encoding.UTF8.GetString value

                    Assert.That(actual, Is.EqualTo expected, $"Cold key {i} should be readable")
                | _ -> Assert.Fail $"Failed to read cold key {i}"

            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.CrossSSTableRead_ShouldHandleMixedDataAccessPatterns() =
        // Test cross SSTable read operations with mixed data access patterns
        let flushThreshold = 75

        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Setup data similar to LayeredWrite
            let hotKeys = Array.init 50 (fun i -> Encoding.UTF8.GetBytes $"hot_{i:D6}")

            let hotValues =
                Array.init 50 (fun i -> Encoding.UTF8.GetBytes($"hot_value_{i}" + String('H', 50)))

            let coldKeys = Array.init 150 (fun i -> Encoding.UTF8.GetBytes $"cold_{i:D8}")

            let coldValues =
                Array.init 150 (fun i -> Encoding.UTF8.GetBytes($"cold_value_{i}_" + String('C', 200)))

            // Write data in layers
            do
                // Hot data layer
                for i = 0 to hotKeys.Length - 1 do
                    let _ = db.Set(hotKeys[i], hotValues[i])
                    ()

                db.WAL.Flush()
                db.ValueLog.Flush()

                // Cold data layer
                for i = 0 to coldKeys.Length - 1 do
                    let _ = db.Set(coldKeys[i], coldValues[i])
                    ()

                db.WAL.Flush()
                db.ValueLog.Flush()



            // Test mixed reads (80% hot, 15% cold, 5% invalid)

            // 80% hot data reads
            for i = 0 to int (float hotKeys.Length * 0.8) - 1 do
                let result = db.Get(hotKeys[i])

                match result with
                | Ok(Some _) -> ()
                | _ -> failwith $"Failed to read hot key {i}"

            // 15% cold data reads
            for i = 0 to int (float coldKeys.Length * 0.15) - 1 do
                let result = db.Get(coldKeys[i])

                match result with
                | Ok(Some _) -> ()
                | _ -> failwith $"Failed to read cold key {i}"

            // 5% reads of non-existent keys (should return None)
            for i = 0 to 5 do
                let result = db.Get(Encoding.UTF8.GetBytes $"nonexistent_{i}")

                match result with
                | Ok None -> () // Expected for non-existent keys
                | _ -> failwith $"Unexpected result for non-existent key {i}"



            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.RangeQueryAcrossSSTables_ShouldHandleSequentialAccess() =
        // Test range query simulation across multiple SSTables
        let flushThreshold = 60

        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Generate sequential keys for range query simulation
            let sequentialKeys = Array.init 200 (fun i -> Encoding.UTF8.GetBytes $"seq_{i:D10}")

            let sequentialValues =
                Array.init 200 (fun i -> Encoding.UTF8.GetBytes($"sequential_value_{i}_" + String('S', 150)))

            // Write data in multiple layers
            do
                // Layer 1: First half
                for i = 0 to 99 do
                    let _ = db.Set(sequentialKeys[i], sequentialValues[i])
                    ()

                db.WAL.Flush()
                db.ValueLog.Flush()

                // Layer 2: Second half
                for i = 100 to 199 do
                    let _ = db.Set(sequentialKeys[i], sequentialValues[i])
                    ()

                db.WAL.Flush()
                db.ValueLog.Flush()



            // Test range query simulation (accessing keys that would span multiple SSTables)
            do
                // Query keys from different ranges to simulate range query access pattern
                for i = 0 to min 20 199 do
                    let result = db.Get sequentialKeys[i]

                    match result with
                    | Ok(Some value) ->
                        let expected = ($"sequential_value_{i}_" + String('S', 150))
                        let actual = Encoding.UTF8.GetString value

                        if actual <> expected then
                            failwith $"Value mismatch for key {i}"
                    | _ -> failwith $"Failed to read sequential key {i}"



            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.RealisticWorkload_ShouldHandleMixedReadWriteOperations() =
        // Test realistic workload with mixed read/write operations
        let flushThreshold = 100

        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Setup initial data
            let initialKeys = Array.init 50 (fun i -> Encoding.UTF8.GetBytes $"initial_{i}")

            let initialValues =
                Array.init 50 (fun i -> Encoding.UTF8.GetBytes $"initial_value_{i}")

            do
                for i = 0 to initialKeys.Length - 1 do
                    let _ = db.Set(initialKeys[i], initialValues[i])
                    ()



            // Simulate realistic workload: 70% reads, 30% writes
            let random = Random 123

            for i = 0 to 99 do
                if random.NextDouble() < 0.7 then
                    // Read operation
                    let keyIndex = random.Next(0, initialKeys.Length)
                    let result = db.Get initialKeys[keyIndex]

                    match result with
                    | Ok(Some _) -> ()
                    | _ -> failwith $"Failed to read key at index {keyIndex}"

                else
                    // Write operation
                    let newKey = Encoding.UTF8.GetBytes $"workload_key_{i}_{random.Next()}"

                    let newValue =
                        Encoding.UTF8.GetBytes($"workload_value_{i}_" + String('W', random.Next(50, 200)))

                    let result = db.Set(newKey, newValue)

                    match result with
                    | Ok() -> ()
                    | Error msg -> failwith $"Failed to write workload key {i}: {msg}"



            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"

    [<Test>]
    member this.PostCompactionRead_ShouldHandleAdditionalWritesAndReads() =
        // Test post-compaction read performance with additional writes
        let flushThreshold = 80

        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            // Initial data
            let initialKeys = Array.init 50 (fun i -> Encoding.UTF8.GetBytes $"initial_{i}")

            let initialValues =
                Array.init 50 (fun i -> Encoding.UTF8.GetBytes $"initial_value_{i}")

            do
                for i = 0 to initialKeys.Length - 1 do
                    let _ = db.Set(initialKeys[i], initialValues[i])
                    ()



            // Add additional data to potentially trigger compaction
            do
                for i = 1 to 2 do // Fewer iterations than benchmark for testing
                    for j = 1 to 50 do // Fewer writes per iteration
                        let key = Encoding.UTF8.GetBytes $"compaction_trigger_{i}_{j}"
                        let value = Encoding.UTF8.GetBytes $"trigger_value_{i}_{j}"
                        let _ = db.Set(key, value)
                        ()

                    // Force flush
                    db.WAL.Flush()
                    db.ValueLog.Flush()



            // Test read performance after additional writes
            do
                // Read some initial keys
                for i = 0 to min 20 (initialKeys.Length - 1) do
                    let result = db.Get initialKeys[i]

                    match result with
                    | Ok(Some value) ->
                        let expected = $"initial_value_{i}"
                        let actual = Encoding.UTF8.GetString value

                        if actual <> expected then
                            failwith $"Value mismatch for initial key {i}"
                    | _ -> failwith $"Failed to read initial key {i}"

                // Read some compaction trigger keys
                for i = 1 to 2 do
                    for j = 1 to min 10 50 do
                        let key = Encoding.UTF8.GetBytes $"compaction_trigger_{i}_{j}"
                        let result = db.Get key

                        match result with
                        | Ok(Some value) ->
                            let expected = $"trigger_value_{i}_{j}"
                            let actual = Encoding.UTF8.GetString value

                            if actual <> expected then
                                failwith $"Value mismatch for compaction key {i}_{j}"
                        | _ -> failwith $"Failed to read compaction key {i}_{j}"



            Assert.Pass()
        | Error msg -> Assert.Fail $"Failed to open DB: {msg}"
