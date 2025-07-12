module Ocis.Tests.AdvancedBenchmarks

open BenchmarkDotNet.Attributes
open Ocis.OcisDB
open System.IO
open System.Text
open System

/// <summary>
/// Advanced benchmarks simulating complex data distribution and access patterns in real-world scenarios.
/// Includes read performance tests across multiple SSTables and tests forcing data distribution into different levels.
/// </summary>
[<MemoryDiagnoser>]
[<ShortRunJob>]
type AdvancedBenchmarks () =

    let tempDir = "temp_advanced_benchmarks"
    let mutable db : OcisDB | null = null
    let mutable dbPath = ""

    // Different types of key-value data
    let mutable hotKeys : byte array array = [||] // Hot data, frequently accessed
    let mutable coldKeys : byte array array = [||] // Cold data, rarely accessed
    let mutable randomKeys : byte array array = [||] // Randomly distributed keys
    let mutable sequentialKeys : byte array array = [||] // Sequential keys

    let mutable hotValues : byte array array = [||]
    let mutable coldValues : byte array array = [||]
    let mutable randomValues : byte array array = [||]
    let mutable sequentialValues : byte array array = [||]

    [<Params(10000, 50000, 100000)>]
    member val public DataSize = 0 with get, set

    [<Params(0.2, 0.5, 0.8)>] // Hot data ratio
    member val public HotDataRatio = 0.0 with get, set

    [<GlobalSetup>]
    member this.GlobalSetup () =
        dbPath <- Path.Combine (tempDir, "advanced_benchmark_db")

        if Directory.Exists tempDir then
            Directory.Delete (tempDir, true)

        Directory.CreateDirectory tempDir |> ignore

        // Generate different types of test data
        let hotDataCount = int (float this.DataSize * this.HotDataRatio)
        let coldDataCount = this.DataSize - hotDataCount

        // Hot data: short keys, small values, concentrated distribution
        hotKeys <- Array.init hotDataCount (fun i -> Encoding.UTF8.GetBytes $"hot_{i:D6}")
        hotValues <- Array.init hotDataCount (fun i -> Encoding.UTF8.GetBytes ($"hot_value_{i}" + String ('H', 50)))

        // Cold data: long keys, large values, scattered distribution
        coldKeys <- Array.init coldDataCount (fun i -> Encoding.UTF8.GetBytes $"cold_data_key_{i:D8}_{Guid.NewGuid().ToString ()}")

        coldValues <- Array.init coldDataCount (fun i -> Encoding.UTF8.GetBytes ($"cold_value_{i}_" + String ('C', 200)))

        // Randomly distributed keys (to simulate real access patterns)
        let random = Random 42 // Fixed seed for reproducibility

        randomKeys <-
            Array.init (this.DataSize / 4) (fun i ->
                let keyLength = random.Next (8, 32)
                let keyBytes = Array.zeroCreate<byte> keyLength
                random.NextBytes keyBytes
                keyBytes)

        randomValues <-
            Array.init (this.DataSize / 4) (fun i ->
                let valueSize = random.Next (100, 500)
                Encoding.UTF8.GetBytes (String ('R', valueSize)))

        // Sequential keys (for range query tests)
        sequentialKeys <- Array.init (this.DataSize / 4) (fun i -> Encoding.UTF8.GetBytes $"seq_{i:D10}")

        sequentialValues <- Array.init (this.DataSize / 4) (fun i -> Encoding.UTF8.GetBytes ($"sequential_value_{i}_" + String ('S', 150)))

    [<IterationSetup>]
    member this.IterationSetup () =
        if Directory.Exists dbPath then
            Directory.Delete (dbPath, true)

        // Use a smaller flush threshold to force the creation of multiple SSTables
        let flushThreshold = max 1000 (this.DataSize / 20)

        match OcisDB.Open (dbPath, flushThreshold) with
        | Ok newDb -> db <- newDb
        | Error msg -> failwith $"Failed to open DB: {msg}"

    /// <summary>
    /// Layered write test: forces data distribution into different SSTables.
    /// </summary>
    [<Benchmark>]
    member this.LayeredWrite () =
        async {
            // Layer 1: Write hot data
            for i = 0 to hotKeys.Length - 1 do
                let! _ = db.Set (hotKeys[i], hotValues[i])
                ()

            // Force flush to L0
            db.WAL.Flush ()
            db.ValueLog.Flush ()
            System.Threading.Thread.Sleep 100 // Wait for compaction

            // Layer 2: Write cold data
            for i = 0 to coldKeys.Length - 1 do
                let! _ = db.Set (coldKeys[i], coldValues[i])
                ()

            db.WAL.Flush ()
            db.ValueLog.Flush ()
            System.Threading.Thread.Sleep 100

            // Layer 3: Write random data
            for i = 0 to randomKeys.Length - 1 do
                let! _ = db.Set (randomKeys[i], randomValues[i])
                ()

            db.WAL.Flush ()
            db.ValueLog.Flush ()
            System.Threading.Thread.Sleep 100

            // Layer 4: Write sequential data
            for i = 0 to sequentialKeys.Length - 1 do
                let! _ = db.Set (sequentialKeys[i], sequentialValues[i])
                ()

            db.WAL.Flush ()
            db.ValueLog.Flush ()
        }
        |> Async.RunSynchronously

    /// <summary>
    /// Mixed read test across multiple SSTables.
    /// </summary>
    [<Benchmark>]
    member this.CrossSSTableRead () =
        // First, perform layered write
        this.LayeredWrite ()

        async {
            // Mixed reads of data from different layers
            let readTasks = [
                // 80% of reads access hot data
                for i = 0 to min (hotKeys.Length - 1) (int (float hotKeys.Length * 0.8)) do
                    yield async {
                        let! _ = db.Get (hotKeys[i])
                        ()
                    }

                // 15% of reads access cold data
                for i = 0 to min (coldKeys.Length - 1) (int (float coldKeys.Length * 0.15)) do
                    yield async {
                        let! _ = db.Get (coldKeys[i])
                        ()
                    }

                // 5% of reads access random data
                for i = 0 to min (randomKeys.Length - 1) (int (float randomKeys.Length * 0.05)) do
                    yield async {
                        let! _ = db.Get (randomKeys[i])
                        ()
                    }
            ]

            // Execute all read operations concurrently
            let! _ = Async.Parallel readTasks
            ()
        }
        |> Async.RunSynchronously

    /// <summary>
    /// Range query performance test (across multiple SSTables).
    /// </summary>
    [<Benchmark>]
    member this.RangeQueryAcrossSSTables () =
        this.LayeredWrite ()

        async {
            // Execute multiple range queries, each potentially spanning multiple SSTables
            let queryRanges = [
                (Encoding.UTF8.GetBytes "hot_000000", Encoding.UTF8.GetBytes "hot_000100")
                (Encoding.UTF8.GetBytes "cold_data_key_00000000", Encoding.UTF8.GetBytes "cold_data_key_00000050")
                (Encoding.UTF8.GetBytes "seq_0000000000", Encoding.UTF8.GetBytes "seq_0000000100")
            ]

            for startKey, endKey in queryRanges do
                // Range query functionality needs to be implemented here
                // Temporarily using single-point query for simulation
                let midKey = Encoding.UTF8.GetBytes "seq_0000000050"
                let! _ = db.Get midKey
                ()
        }
        |> Async.RunSynchronously

    /// <summary>
    /// Simulate realistic workload: mixed read/write, conforming to Zipf distribution.
    /// </summary>
    [<Benchmark>]
    member this.RealisticWorkload () =
        this.LayeredWrite ()

        async {
            let random = Random 123
            let operations = Array.zeroCreate<Async<unit>> 1000

            for i = 0 to operations.Length - 1 do
                if random.NextDouble () < 0.7 then
                    // 70% read operations, conforming to Zipf distribution (hot data is more likely to be accessed)
                    let keyIndex =
                        if random.NextDouble () < this.HotDataRatio then
                            random.Next (0, hotKeys.Length)
                        else
                            random.Next (0, coldKeys.Length)

                    operations[i] <- async {
                        let key =
                            if keyIndex < hotKeys.Length then
                                hotKeys[keyIndex]
                            else
                                coldKeys[keyIndex - hotKeys.Length]

                        let! _ = db.Get key
                        ()
                    }
                else
                    // 30% write operations
                    let newKey = Encoding.UTF8.GetBytes $"workload_key_{i}_{random.Next ()}"

                    let newValue = Encoding.UTF8.GetBytes ($"workload_value_{i}_" + String ('W', random.Next (50, 200)))

                    operations[i] <- async {
                        let! _ = db.Set (newKey, newValue)
                        ()
                    }

            // Execute all operations concurrently
            let! _ = Async.Parallel operations
            ()
        }
        |> Async.RunSynchronously

    /// <summary>
    /// Read performance test after compaction.
    /// </summary>
    [<Benchmark>]
    member this.PostCompactionRead () =
        this.LayeredWrite ()

        // Wait for multiple compactions to complete
        for i = 1 to 5 do
            System.Threading.Thread.Sleep 200
            // Write some new data to trigger more compactions
            async {
                for j = 1 to 100 do
                    let key = Encoding.UTF8.GetBytes $"compaction_trigger_{i}_{j}"
                    let value = Encoding.UTF8.GetBytes $"trigger_value_{i}_{j}"
                    let! _ = db.Set (key, value)
                    ()
            }
            |> Async.RunSynchronously

        // Test read performance after compaction
        async {
            for i = 0 to min 1000 (hotKeys.Length - 1) do
                let! _ = db.Get (hotKeys[i])
                ()
        }
        |> Async.RunSynchronously

    /// <summary>
    /// Memory usage efficiency test.
    /// </summary>
    [<Benchmark>]
    member this.MemoryEfficiencyTest () =
        let beforeMemory = GC.GetTotalMemory true

        this.LayeredWrite ()

        let afterWriteMemory = GC.GetTotalMemory true

        // Perform a large number of read operations
        async {
            for i = 0 to min 5000 (hotKeys.Length - 1) do
                let! _ = db.Get (hotKeys[i % hotKeys.Length])
                ()
        }
        |> Async.RunSynchronously

        let afterReadMemory = GC.GetTotalMemory true

        // Force garbage collection
        GC.Collect ()
        GC.WaitForPendingFinalizers ()
        GC.Collect ()

        let afterGCMemory = GC.GetTotalMemory true

        // Output memory usage statistics
        printfn
            $"Memory usage - Before: {beforeMemory / 1024L} KB, After Write: {afterWriteMemory / 1024L} KB, After Read: {afterReadMemory / 1024L} KB, After GC: {afterGCMemory / 1024L} KB"

    [<GlobalCleanup>]
    member this.GlobalCleanup () =
        if db <> null then
            (db :> IDisposable).Dispose ()

        if Directory.Exists tempDir then
            Directory.Delete (tempDir, true)
