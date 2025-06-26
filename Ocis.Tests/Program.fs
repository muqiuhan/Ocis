module Program

open BenchmarkDotNet.Running
open Ocis.Tests.OcisDBBenchmarks
open Ocis.Tests.AdvancedBenchmarks

[<EntryPoint>]
let main argv =
    if argv.Length = 0 then
        BenchmarkRunner.Run<OcisDBBenchmarks>() |> ignore
    else
        match argv[0] with
        | "advance" -> BenchmarkRunner.Run<AdvancedBenchmarks>() |> ignore
        | _ -> BenchmarkRunner.Run<OcisDBBenchmarks>() |> ignore

    0
