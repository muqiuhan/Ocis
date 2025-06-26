module Program

open BenchmarkDotNet.Running
open Ocis.Tests.OcisDBBenchmarks

[<EntryPoint>]
let main _ =
    BenchmarkRunner.Run<OcisDBBenchmarks>() |> ignore
    0
