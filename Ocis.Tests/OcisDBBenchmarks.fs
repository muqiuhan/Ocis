module Ocis.Tests.OcisDBBenchmarks

open BenchmarkDotNet.Attributes

open Ocis.OcisDB
open System.IO
open System.Text

[<MemoryDiagnoser>]
[<ShortRunJob>]
type OcisDBBenchmarks () =

    let tempDir = "temp_ocisdb_benchmarks"
    let mutable db : OcisDB | null = null
    let mutable dbPath = ""
    let mutable keys : byte array array = [||]
    let mutable values : byte array array = [||]

    [<Params(1000, 10000, 100000)>]
    member val public Count = 0 with get, set

    [<GlobalSetup>]
    member this.GlobalSetup () =
        dbPath <- Path.Combine (tempDir, "ocisdb_benchmark_instance")

        if Directory.Exists (tempDir) then
            Directory.Delete (tempDir, true)

        Directory.CreateDirectory (tempDir) |> ignore

        // 为 Set 测试准备数据
        keys <- Array.init this.Count (fun i -> Encoding.UTF8.GetBytes ($"perf_key_{i}"))
        values <- Array.init this.Count (fun i -> Encoding.UTF8.GetBytes ($"perf_value_{i}_" + new string ('A', 100)))

    [<IterationSetup>]
    member this.IterationSetup () =
        if Directory.Exists (dbPath) then
            Directory.Delete (dbPath, true)

        match OcisDB.Open (dbPath, this.Count) with
        | Ok newDb -> db <- newDb
        | Error msg -> failwith $"Failed to open DB: {msg}"

    [<Benchmark>]
    member this.BulkSet () =
        async {
            for i = 0 to this.Count - 1 do
                let! _ = db.Set (keys.[i], values.[i])
                ()
        }
        |> Async.RunSynchronously

        db.WAL.Flush ()
        db.ValueLog.Flush ()

    [<Benchmark>]
    member this.BulkGet () =
        this.BulkSet ()

        async {
            for key in keys do
                let! _ = db.Get (key)
                ()
        }
        |> Async.RunSynchronously

    [<GlobalCleanup>]
    member this.GlobalCleanup () =
        (db :> System.IDisposable).Dispose ()

        if Directory.Exists (tempDir) then
            Directory.Delete (tempDir, true)
