module Ocis.Cli.Program

open System.IO
open FSharp.SystemCommandLine
open Input
open Ocis.Cli.Config
open Ocis.Cli.Ocis

[<EntryPoint>]
let main argv = rootCommand argv {
    description "A cross-platform, robust asynchronous WiscKey storage engine."
    inputs (
        argument "working-dir"
        |> desc "The working directory"
        |> validateDirectoryExists
        |> validate (fun dir ->
            if dir.Exists then
                Ok ()
            else
                Error $"Directory does not exist"),

        optionMaybe "--flush-threshold"
        |> desc "Trigger Memtable flush threshold"
        |> validate (fun threshold ->
            match threshold with
            | Some threshold when threshold > 0 -> Ok ()
            | Some _ -> Error $"Threshold must be greater than 0"
            | _ -> Ok ()),

        optionMaybe "--l0-compaction-threshold"
        |> desc
            "The option threshold of Level 0 (L0) SSTables. When the number of SSTable files generated in the L0 level reaches or exceeds this value, the system triggers a merge operation from L0 to Level 1 (L1). For write-intensive applications, this value can be appropriately increased to reduce the merge frequency. For read-intensive applications, this value can be appropriately reduced to reduce the scan range of L0 and speed up the reading speed."
        |> validate (fun threshold ->
            match threshold with
            | Some threshold when threshold > 0 -> Ok ()
            | Some _ -> Error $"Threshold must be greater than 0"
            | _ -> Ok ()),

        optionMaybe "--level-size-multiplier"
        |> desc
            "This option determines the size ratio between adjacent levels in the LSM tree. A larger number (such as 10) usually means fewer levels, larger levels, less frequent merges but more data per merge. This may be beneficial to write throughput in some cases, but may cause long merge pauses. A smaller number (such as 2 or 4) usually means more levels, smaller levels, more frequent merges but less data per merge. This may be more beneficial to read performance because there are relatively fewer files per level."
        |> validate (fun multiplier ->
            match multiplier with
            | Some multiplier when multiplier > 0 -> Ok ()
            | Some _ -> Error $"Multiplier must be greater than 0"
            | _ -> Ok ()),

        optionMaybe "--log-level"
        |> desc "Set the log level. Available levels: Debug, Info, Warn, Error, Fatal."
        |> validate (fun level ->
            Option.bind
                (fun level ->
                    match level with
                    | "Debug"
                    | "Info"
                    | "Warn"
                    | "Error"
                    | "Fatal" -> Some level
                    | _ -> None)
                level
            |> Option.isSome
            |> function
                | true -> Ok ()
                | false -> Ok ())
    )
    setAction (fun (workingDir, flushThreshold, l0CompactionThreshold, levelSizeMultiplier, logLevel) ->
        {
            Dir = workingDir.FullName
            FlushThreshold = flushThreshold |> Option.defaultValue 1000
            L0CompactionThreshold = l0CompactionThreshold |> Option.defaultValue 4
            LevelSizeMultiplier = levelSizeMultiplier |> Option.defaultValue 5
            LogLevel = logLevel |> Option.defaultValue "Info"
        }
        |> Ocis.Run)
}
