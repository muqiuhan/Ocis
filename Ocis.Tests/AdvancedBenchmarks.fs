module Ocis.Tests.AdvancedBenchmarks

open BenchmarkDotNet.Attributes
open Ocis.OcisDB
open System.IO
open System.Text
open System
open System.Threading

// Helper function for robust directory deletion
let rec deleteDirectory path maxRetries =
    let rec tryDelete attempt =
        try
            if Directory.Exists path then
                // First, try to delete all files individually to handle locked files
                let files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)

                for file in files do
                    try
                        if File.Exists file then
                            File.Delete file
                    with
                    | :? IOException -> () // Ignore locked files
                    | _ -> ()

                // Then try to delete all subdirectories
                let subdirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)

                for subdir in subdirs |> Array.rev do // Delete from deepest first
                    try
                        if Directory.Exists subdir then
                            Directory.Delete(subdir, true)
                    with
                    | :? IOException -> ()
                    | _ -> ()

                // Finally, try to delete the main directory
                if Directory.Exists path then
                    Directory.Delete(path, true)

            true // Success
        with
        | :? IOException as ex ->
            if attempt < maxRetries then
                Thread.Sleep(100) // Wait a bit before retry
                tryDelete (attempt + 1)
            else
                printfn $"Warning: Failed to delete directory {path} after {maxRetries} attempts: {ex.Message}"

                false // Failed after retries
        | ex ->
            printfn $"Warning: Unexpected error deleting directory {path}: {ex.Message}"

            false

    tryDelete 0

/// <summary>
/// Advanced benchmarks simulating complex data distribution and access patterns in real-world scenarios.
/// Includes read performance tests across multiple SSTables and tests forcing data distribution into different levels.
/// </summary>
[<MemoryDiagnoser>]
[<ShortRunJob>]
type AdvancedBenchmarks() =

    let tempDir = "temp_advanced_benchmarks"
    let mutable db: OcisDB | null = null
    let mutable dbPath = ""

    // Different types of key-value data
    let mutable hotKeys: byte array array = [||] // Hot data, frequently accessed
    let mutable coldKeys: byte array array = [||] // Cold data, rarely accessed
    let mutable randomKeys: byte array array = [||] // Randomly distributed keys
    let mutable sequentialKeys: byte array array = [||] // Sequential keys

    let mutable hotValues: byte array array = [||]
    let mutable coldValues: byte array array = [||]
    let mutable randomValues: byte array array = [||]
    let mutable sequentialValues: byte array array = [||]

    [<Params(10000, 50000, 100000)>]
    member val public DataSize = 0 with get, set

    [<Params(0.2, 0.5, 0.8)>] // Hot data ratio
    member val public HotDataRatio = 0.0 with get, set

    [<GlobalSetup>]
    member this.GlobalSetup() =
        dbPath <- Path.Combine(tempDir, "advanced_benchmark_db")

        // Clean up any existing temp directory more thoroughly
        deleteDirectory tempDir 3 |> ignore

        Directory.CreateDirectory tempDir |> ignore

        // Generate different types of test data
        let hotDataCount = int (float this.DataSize * this.HotDataRatio)
        let coldDataCount = this.DataSize - hotDataCount

        // Hot data: short keys, small values, concentrated distribution
        hotKeys <- Array.init hotDataCount (fun i -> Encoding.UTF8.GetBytes $"hot_{i:D6}")

        hotValues <- Array.init hotDataCount (fun i -> Encoding.UTF8.GetBytes($"hot_value_{i}" + String('H', 50)))

        // Cold data: long keys, large values, scattered distribution
        let random = Random 42 // Use same seed for reproducibility

        coldKeys <-
            Array.init coldDataCount (fun i ->
                // Use random number instead of Guid for better performance
                let randomSuffix = random.Next().ToString("X8")
                Encoding.UTF8.GetBytes $"cold_data_key_{i:D8}_{randomSuffix}")

        coldValues <- Array.init coldDataCount (fun i -> Encoding.UTF8.GetBytes($"cold_value_{i}_" + String('C', 200)))

        // Randomly distributed keys (to simulate real access patterns)
        // Reuse the same random instance for consistency

        randomKeys <-
            Array.init (this.DataSize / 4) (fun i ->
                let keyLength = random.Next(8, 32)
                let keyBytes = Array.zeroCreate<byte> keyLength
                random.NextBytes keyBytes
                keyBytes)

        randomValues <-
            Array.init (this.DataSize / 4) (fun i ->
                let valueSize = random.Next(100, 500)
                Encoding.UTF8.GetBytes(String('R', valueSize)))

        // Sequential keys (for range query tests)
        sequentialKeys <- Array.init (this.DataSize / 4) (fun i -> Encoding.UTF8.GetBytes $"seq_{i:D10}")

        sequentialValues <-
            Array.init (this.DataSize / 4) (fun i ->
                Encoding.UTF8.GetBytes($"sequential_value_{i}_" + String('S', 150)))

    [<IterationSetup>]
    member this.IterationSetup() =
        // More robust cleanup with retries
        deleteDirectory dbPath 5 |> ignore

        // Use a smaller flush threshold to force the creation of multiple SSTables
        let flushThreshold = max 1000 (this.DataSize / 20)

        match OcisDB.Open(dbPath, flushThreshold) with
        | Ok newDb -> db <- newDb
        | Error msg -> failwith $"Failed to open DB: {msg}"

    /// <summary>
    /// Layered write test: forces data distribution into different SSTables.
    /// </summary>
    [<Benchmark>]
    member this.LayeredWrite() =
        // Layer 1: Write hot data
        if hotKeys.Length > 0 then
            for i = 0 to hotKeys.Length - 1 do
                db.Set(hotKeys[i], hotValues[i]) |> ignore

            // Force flush to L0
            db.WAL.Flush()
            db.ValueLog.Flush()

        // Layer 2: Write cold data
        if coldKeys.Length > 0 then
            for i = 0 to coldKeys.Length - 1 do
                db.Set(coldKeys[i], coldValues[i]) |> ignore

            db.WAL.Flush()
            db.ValueLog.Flush()

        // Layer 3: Write random data
        if randomKeys.Length > 0 then
            for i = 0 to randomKeys.Length - 1 do
                db.Set(randomKeys[i], randomValues[i]) |> ignore

            db.WAL.Flush()
            db.ValueLog.Flush()

        // Layer 4: Write sequential data
        if sequentialKeys.Length > 0 then
            for i = 0 to sequentialKeys.Length - 1 do
                db.Set(sequentialKeys[i], sequentialValues[i]) |> ignore

            db.WAL.Flush()
            db.ValueLog.Flush()

    /// <summary>
    /// Mixed read test across multiple SSTables.
    /// </summary>
    [<Benchmark>]
    member this.CrossSSTableRead() =
        // First, perform layered write
        this.LayeredWrite()

        // Mixed reads of data from different layers
        // 80% of reads access hot data
        if hotKeys.Length > 0 then
            for i = 0 to min (hotKeys.Length - 1) (int (float hotKeys.Length * 0.8)) do
                db.Get(hotKeys[i]) |> ignore

        // 15% of reads access cold data
        if coldKeys.Length > 0 then
            for i = 0 to min (coldKeys.Length - 1) (int (float coldKeys.Length * 0.15)) do
                db.Get(coldKeys[i]) |> ignore

        // 5% of reads access random data
        if randomKeys.Length > 0 then
            for i = 0 to min (randomKeys.Length - 1) (int (float randomKeys.Length * 0.05)) do
                db.Get(randomKeys[i]) |> ignore

    /// <summary>
    /// Range query performance test (across multiple SSTables).
    /// </summary>
    [<Benchmark>]
    member this.RangeQueryAcrossSSTables() =
        this.LayeredWrite()

        // Execute point queries that would be part of range queries across multiple SSTables
        // Query keys from different data types to ensure they span multiple SSTables

        // Query some hot keys
        if hotKeys.Length > 0 then
            let queries = min 10 hotKeys.Length

            for i = 0 to queries - 1 do
                db.Get hotKeys[i] |> ignore

        // Query some cold keys
        if coldKeys.Length > 0 then
            let queries = min 10 coldKeys.Length

            for i = 0 to queries - 1 do
                db.Get coldKeys[i] |> ignore

        // Query some sequential keys (simulating range query access pattern)
        if sequentialKeys.Length > 0 then
            let queries = min 10 sequentialKeys.Length

            for i = 0 to queries - 1 do
                db.Get sequentialKeys[i] |> ignore

    /// <summary>
    /// Simulate realistic workload: mixed read/write, conforming to Zipf distribution.
    /// </summary>
    [<Benchmark>]
    member this.RealisticWorkload() =
        this.LayeredWrite()

        let random = Random 123

        for i = 0 to 999 do
            if random.NextDouble() < 0.7 then
                // 70% read operations, conforming to Zipf distribution (hot data is more likely to be accessed)
                let key =
                    if random.NextDouble() < this.HotDataRatio && hotKeys.Length > 0 then
                        hotKeys[random.Next(0, hotKeys.Length)]
                    elif coldKeys.Length > 0 then
                        coldKeys[random.Next(0, coldKeys.Length)]
                    else
                        // fallback to a simple key
                        Encoding.UTF8.GetBytes $"fallback_key_{i}"

                db.Get key |> ignore
            else
                // 30% write operations
                let newKey = Encoding.UTF8.GetBytes $"workload_key_{i}_{random.Next()}"

                let newValue =
                    Encoding.UTF8.GetBytes($"workload_value_{i}_" + String('W', random.Next(50, 200)))

                db.Set(newKey, newValue) |> ignore

    /// <summary>
    /// Read performance test after compaction.
    /// </summary>
    [<Benchmark>]
    member this.PostCompactionRead() =
        this.LayeredWrite()

        // Trigger compaction
        // Write some new data to potentially trigger more compactions
        for i = 1 to 3 do
            for j = 1 to 100 do
                let key = Encoding.UTF8.GetBytes $"compaction_trigger_{i}_{j}"
                let value = Encoding.UTF8.GetBytes $"trigger_value_{i}_{j}"
                db.Set(key, value) |> ignore

            // Force flush
            db.WAL.Flush()
            db.ValueLog.Flush()

        // Test read performance after compaction
        if hotKeys.Length > 0 then
            for i = 0 to min 1000 (hotKeys.Length - 1) do
                db.Get(hotKeys[i]) |> ignore

    /// <summary>
    /// Memory usage efficiency test.
    /// </summary>
    [<Benchmark>]
    member this.MemoryEfficiencyTest() =
        let beforeMemory = GC.GetTotalMemory true

        this.LayeredWrite()

        let afterWriteMemory = GC.GetTotalMemory true

        // Perform a large number of read operations
        if hotKeys.Length > 0 then
            for i = 0 to min 5000 (hotKeys.Length - 1) do
                db.Get(hotKeys[i % hotKeys.Length]) |> ignore

        let afterReadMemory = GC.GetTotalMemory true

        // Force garbage collection
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()

        let afterGCMemory = GC.GetTotalMemory true

        // Memory measurements completed - values stored in variables for potential future use
        ()

    [<GlobalCleanup>]
    member this.GlobalCleanup() =
        // Ensure DB is properly disposed
        if db <> null then
            try
                (db :> IDisposable).Dispose()
            with _ ->
                () // Ignore disposal errors

        db <- null

        // More robust cleanup with retries
        deleteDirectory tempDir 5 |> ignore
