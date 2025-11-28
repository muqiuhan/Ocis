module Ocis.Server.Program

open FSharp.SystemCommandLine
open Input
open Ocis.Server.Config
open Ocis.Server.Ocis

[<EntryPoint>]
let main argv =
    rootCommand argv {
        description "A cross-platform, robust asynchronous WiscKey storage engine with TCP server."

        inputs (
            argument "working-dir"
            |> desc "The working directory"
            |> validateDirectoryExists
            |> validate (fun dir ->
                if dir.Exists then
                    Ok()
                else
                    Error "Directory does not exist"),

            optionMaybe "--flush-threshold"
            |> desc "Trigger Memtable flush threshold (default: 1000)"
            |> validate (fun threshold ->
                match threshold with
                | Some threshold when threshold > 0 -> Ok()
                | Some _ -> Error "Threshold must be greater than 0"
                | _ -> Ok()),

            optionMaybe "--l0-compaction-threshold"
            |> desc "L0 SSTables compaction threshold (default: 4)"
            |> validate (fun threshold ->
                match threshold with
                | Some threshold when threshold > 0 -> Ok()
                | Some _ -> Error "Threshold must be greater than 0"
                | _ -> Ok()),

            optionMaybe "--level-size-multiplier"
            |> desc "LSM tree level size multiplier (default: 5)"
            |> validate (fun multiplier ->
                match multiplier with
                | Some multiplier when multiplier > 0 -> Ok()
                | Some _ -> Error "Multiplier must be greater than 0"
                | _ -> Ok()),

            optionMaybe "--log-level"
            |> desc "Log level: Debug, Info, Warn, Error, Fatal (default: Info)"
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
                    | true -> Ok()
                    | false -> Ok()),

            optionMaybe "--host"
            |> desc "Server host address (default: 0.0.0.0)"
            |> validate (fun host ->
                match host with
                | Some _ -> Ok()
                | None -> Ok()),

            optionMaybe "--port"
            |> desc "Server port (default: 7379)"
            |> validate (fun port ->
                match port with
                | Some port when port > 0 && port <= 65535 -> Ok()
                | Some _ -> Error "Port must be between 1 and 65535"
                | None -> Ok()),

            optionMaybe "--max-connections"
            |> desc "Maximum concurrent connections (default: 1000)"
            |> validate (fun maxConn ->
                match maxConn with
                | Some maxConn when maxConn > 0 -> Ok()
                | Some _ -> Error "Max connections must be greater than 0"
                | None -> Ok())
        )

        setAction
            (fun
                (workingDir,
                 flushThreshold,
                 l0CompactionThreshold,
                 levelSizeMultiplier,
                 logLevel,
                 host,
                 port,
                 maxConnections) ->
                {
                  // Database configuration
                  Dir = workingDir.FullName
                  FlushThreshold = flushThreshold |> Option.defaultValue 1000
                  L0CompactionThreshold = l0CompactionThreshold |> Option.defaultValue 4
                  LevelSizeMultiplier = levelSizeMultiplier |> Option.defaultValue 5
                  LogLevel = logLevel |> Option.defaultValue "Info"

                  // Network configuration (using default values)
                  Host = host |> Option.defaultValue "0.0.0.0"
                  Port = port |> Option.defaultValue 7379
                  MaxConnections = maxConnections |> Option.defaultValue 1000
                  ReceiveTimeout = 30000 // Default 30 seconds
                  SendTimeout = 30000 // Default 30 seconds
                }
                |> Ocis.Run)
    }
