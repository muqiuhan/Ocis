module Program

open BenchmarkDotNet.Running
open Ocis.Tests.OcisDBBenchmarks
open Ocis.Tests.AdvancedBenchmarks

[<EntryPoint>]
let main argv =
    match argv[0] with
    | "advance" -> BenchmarkRunner.Run<AdvancedBenchmarks>() |> ignore
    | _ -> BenchmarkRunner.Run<OcisDBBenchmarks>() |> ignore

    0
