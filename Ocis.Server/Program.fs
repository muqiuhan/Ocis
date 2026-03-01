module Ocis.Server.Program

open FSharp.SystemCommandLine
open Input
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open Ocis.Server.Config
open Ocis.Server.Host
open Ocis.Utils.Logger

// CLI entrypoint that converts command-line options into OcisConfig and then
// boots the hosted runtime.
let private runWithHost(config: OcisConfig) =
  let options = OcisServerOptions.FromConfig config

  let hostBuilder =
    Host
      .CreateDefaultBuilder()
      .ConfigureServices(fun _ services ->
        services.AddSingleton<IOptions<OcisServerOptions>>(
          Options.Create(options)
        )
        |> ignore
        services.AddHostedService<OcisHostedService>()
        |> ignore)

  try
    use host = hostBuilder.Build()
    host.Run()
    0
  with ex ->
    Logger.Error $"Host startup failed: {ex.Message}"
    1

let validateLogLevelOption level =
  match level with
  | Some "Debug"
  | Some "Info"
  | Some "Warn"
  | Some "Error"
  | Some "Fatal"
  | None -> Ok()
  | Some _ -> Error "Log level must be one of: Debug, Info, Warn, Error, Fatal"

// fsharplint:disable-next-line FL0025
[<EntryPoint>]
let main argv =
  // Optional durability and dispatcher tuning options.
  let durabilityModeInput =
    optionMaybe "--durability-mode"
    |> desc "Durability mode: Strict, Balanced, Fast (default: Balanced)"
    |> validate(fun mode ->
      match mode with
      | Some "Strict"
      | Some "Balanced"
      | Some "Fast"
      | None -> Ok()
      | Some _ ->
        Error "Durability mode must be one of: Strict, Balanced, Fast")

  let groupCommitWindowMsInput =
    optionMaybe "--group-commit-window-ms"
    |> desc "Group commit window in milliseconds (default: 5)"
    |> validate(fun value ->
      match value with
      | Some value when value > 0 -> Ok()
      | Some _ -> Error "Group commit window must be greater than 0"
      | None -> Ok())

  let groupCommitBatchSizeInput =
    optionMaybe "--group-commit-batch-size"
    |> desc "Group commit batch size (default: 64)"
    |> validate(fun value ->
      match value with
      | Some value when value > 0 -> Ok()
      | Some _ -> Error "Group commit batch size must be greater than 0"
      | None -> Ok())

  let dbQueueCapacityInput =
    optionMaybe "--db-queue-capacity"
    |> desc "Database queue capacity (default: 8192)"
    |> validate(fun value ->
      match value with
      | Some value when value > 0 -> Ok()
      | Some _ -> Error "DB queue capacity must be greater than 0"
      | None -> Ok())

  let checkpointMinIntervalMsInput =
    optionMaybe "--checkpoint-min-interval-ms"
    |> desc "Minimum checkpoint interval in milliseconds (default: 30000)"
    |> validate(fun value ->
      match value with
      | Some value when value > 0 -> Ok()
      | Some _ -> Error "Checkpoint minimum interval must be greater than 0"
      | None -> Ok())

  let maxConnectionsInput =
    optionMaybe "--max-connections"
    |> desc "Maximum concurrent connections (default: 1000)"
    |> validate(fun maxConn ->
      match maxConn with
      | Some maxConn when maxConn > 0 -> Ok()
      | Some _ -> Error "Max connections must be greater than 0"
      | None -> Ok())

  rootCommand argv {
    description
      "A cross-platform, robust asynchronous WiscKey storage engine with TCP server."

    inputs(
      argument "working-dir"
      |> desc "The working directory"
      |> validateDirectoryExists
      |> validate(fun dir ->
        if dir.Exists then
          Ok()
        else
          Error "Directory does not exist"),

      optionMaybe "--flush-threshold"
      |> desc "Trigger Memtable flush threshold (default: 1000)"
      |> validate(fun threshold ->
        match threshold with
        | Some threshold when threshold > 0 -> Ok()
        | Some _ -> Error "Threshold must be greater than 0"
        | _ -> Ok()),

      optionMaybe "--l0-compaction-threshold"
      |> desc "L0 SSTables compaction threshold (default: 4)"
      |> validate(fun threshold ->
        match threshold with
        | Some threshold when threshold > 0 -> Ok()
        | Some _ -> Error "Threshold must be greater than 0"
        | _ -> Ok()),

      optionMaybe "--level-size-multiplier"
      |> desc "LSM tree level size multiplier (default: 5)"
      |> validate(fun multiplier ->
        match multiplier with
        | Some multiplier when multiplier > 0 -> Ok()
        | Some _ -> Error "Multiplier must be greater than 0"
        | _ -> Ok()),

      optionMaybe "--log-level"
      |> desc "Log level: Debug, Info, Warn, Error, Fatal (default: Info)"
      |> validate validateLogLevelOption,

      optionMaybe<string> "--host"
      |> desc "Server host address (default: 0.0.0.0)"
      |> validate(fun host ->
        match host with
        | Some _ -> Ok()
        | None -> Ok()),

      optionMaybe "--port"
      |> desc "Server port (default: 7379)"
      |> validate(fun port ->
        match port with
        | Some port when port > 0 && port <= 65535 -> Ok()
        | Some _ -> Error "Port must be between 1 and 65535"
        | None -> Ok()),

      context
    )

    addInput maxConnectionsInput
    addInput durabilityModeInput
    addInput groupCommitWindowMsInput
    addInput groupCommitBatchSizeInput
    addInput dbQueueCapacityInput
    addInput checkpointMinIntervalMsInput

    setAction
      (fun
           (workingDir,
            flushThreshold,
            l0CompactionThreshold,
            levelSizeMultiplier,
            logLevel,
            host,
            port,
            actionContext) ->
        let maxConnections =
          maxConnectionsInput.GetValue actionContext.ParseResult
        let durabilityMode =
          durabilityModeInput.GetValue actionContext.ParseResult
        let groupCommitWindowMs =
          groupCommitWindowMsInput.GetValue actionContext.ParseResult
        let groupCommitBatchSize =
          groupCommitBatchSizeInput.GetValue actionContext.ParseResult
        let dbQueueCapacity =
          dbQueueCapacityInput.GetValue actionContext.ParseResult
        let checkpointMinIntervalMs =
          checkpointMinIntervalMsInput.GetValue actionContext.ParseResult

        let config: OcisConfig =
          {Dir = workingDir.FullName
           FlushThreshold = flushThreshold |> Option.defaultValue 1000
           L0CompactionThreshold =
              l0CompactionThreshold |> Option.defaultValue 4
           LevelSizeMultiplier = levelSizeMultiplier |> Option.defaultValue 5
           LogLevel = logLevel |> Option.defaultValue "Info"
           DurabilityMode = durabilityMode |> Option.defaultValue "Balanced"
           GroupCommitWindowMs = groupCommitWindowMs |> Option.defaultValue 5
           GroupCommitBatchSize =
              groupCommitBatchSize |> Option.defaultValue 64
           DbQueueCapacity = dbQueueCapacity |> Option.defaultValue 8192
           CheckpointMinIntervalMs =
              checkpointMinIntervalMs
              |> Option.defaultValue 30000
           Host = host |> Option.defaultValue "0.0.0.0"
           Port = port |> Option.defaultValue 7379
           MaxConnections = maxConnections |> Option.defaultValue 1000
           ReceiveTimeout = 30000
           SendTimeout = 30000}

        runWithHost config)
  }
