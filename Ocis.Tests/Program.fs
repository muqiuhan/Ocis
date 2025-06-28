module Program

open BenchmarkDotNet.Configs
open BenchmarkDotNet.Environments
open BenchmarkDotNet.Running
open BenchmarkDotNet.Diagnosers
open Ocis.Tests.OcisDBBenchmarks
open Ocis.Tests.AdvancedBenchmarks
open BenchmarkDotNet.Jobs
open System.IO
open Ocis.OcisDB
open System.Text
open BenchmarkDotNet.Exporters
open BenchmarkDotNet.Loggers
open BenchmarkDotNet.Columns

type AllowNonOptimizedConfig() =
    inherit ManualConfig()

    do
        base.AddJob(Job.Default) |> ignore
        base.AddDiagnoser(MemoryDiagnoser.Default) |> ignore
        base.AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net90)) |> ignore
        base.AddExporter(MarkdownExporter.Default) |> ignore
        base.AddLogger(ConsoleLogger.Default) |> ignore
        base.AddColumnProvider(DefaultColumnProviders.Instance) |> ignore

type SimpleTests() =
    let tempDir = "temp_ocisdb_tests"
    let mutable testDbPath = ""
    let flushThreshold = 100

    do
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory(tempDir) |> ignore
        testDbPath <- Path.Combine(tempDir, "ocisdb_instance")

    member _.Run() =
        match OcisDB.Open(testDbPath, flushThreshold) with
        | Ok db ->
            use db = db

            System.GC.Collect()
            System.GC.WaitForPendingFinalizers()
            System.GC.Collect()

            let initialAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread()

            printfn "Inserting 100000 entries for Memory Footprint test..."

            for i = 0 to 100000 - 1 do
                let key = Encoding.UTF8.GetBytes($"mem_key_{i}")
                let value = Encoding.UTF8.GetBytes($"mem_value_{i}_" + new string ('C', 200))
                db.Set(key, value) |> Async.RunSynchronously |> ignore

            printfn "Getting 100000 entries for Memory Footprint test..."

            for i = 0 to 100000 - 1 do
                let key = Encoding.UTF8.GetBytes($"mem_key_{i}")
                let value = Encoding.UTF8.GetBytes($"mem_value_{i}_" + new string ('C', 200))

                match db.Get(key) |> Async.RunSynchronously with
                | Ok None -> failwith $"Failed to get key {i}"
                | Ok(Some value') ->
                    if value <> value' then
                        failwith $"Value mismatch for key {i}"

                | Error msg -> failwith $"Failed to get key {i}: {msg}"

            let finalAllocatedBytes = System.GC.GetAllocatedBytesForCurrentThread()
            let allocated = finalAllocatedBytes - initialAllocatedBytes

            printfn $"\nTotal allocated memory for 100000 entries: {float allocated / (1024.0 * 1024.0)} MB"

        | Error msg -> failwith $"Failed to open DB for Memory Footprint test: {msg}"

[<EntryPoint>]
let main argv =
    let config = AllowNonOptimizedConfig()

    if argv.Length = 0 then
        BenchmarkRunner.Run<OcisDBBenchmarks>(config) |> ignore
    else
        match argv[0] with
        | "advance" -> BenchmarkRunner.Run<AdvancedBenchmarks>(config) |> ignore
        | "simple" -> SimpleTests().Run()
        | _ -> BenchmarkRunner.Run<OcisDBBenchmarks>(config) |> ignore

    0
