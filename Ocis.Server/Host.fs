module Ocis.Server.Host

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open Ocis.Server.Config
open Ocis.Server.Ocis

[<CLIMutable>]
type OcisServerOptions =
    { Dir: string
      FlushThreshold: int
      L0CompactionThreshold: int
      LevelSizeMultiplier: int
      LogLevel: string
      DurabilityMode: string
      GroupCommitWindowMs: int
      GroupCommitBatchSize: int
      DbQueueCapacity: int
      CheckpointMinIntervalMs: int
      Host: string
      Port: int
      MaxConnections: int
      ReceiveTimeout: int
      SendTimeout: int }

module OcisServerOptions =
    [<Literal>]
    let SectionName = "OcisServer"

    // Map runtime config to host options binding shape.
    let FromConfig (config: OcisConfig) =
        { Dir = config.Dir
          FlushThreshold = config.FlushThreshold
          L0CompactionThreshold = config.L0CompactionThreshold
          LevelSizeMultiplier = config.LevelSizeMultiplier
          LogLevel = config.LogLevel
          DurabilityMode = config.DurabilityMode
          GroupCommitWindowMs = config.GroupCommitWindowMs
          GroupCommitBatchSize = config.GroupCommitBatchSize
          DbQueueCapacity = config.DbQueueCapacity
          CheckpointMinIntervalMs = config.CheckpointMinIntervalMs
          Host = config.Host
          Port = config.Port
          MaxConnections = config.MaxConnections
          ReceiveTimeout = config.ReceiveTimeout
          SendTimeout = config.SendTimeout }

    // Convert host-bound options back to runtime config.
    let ToConfig (options: OcisServerOptions) : OcisConfig =
        { Dir = options.Dir
          FlushThreshold = options.FlushThreshold
          L0CompactionThreshold = options.L0CompactionThreshold
          LevelSizeMultiplier = options.LevelSizeMultiplier
          LogLevel = options.LogLevel
          DurabilityMode = options.DurabilityMode
          GroupCommitWindowMs = options.GroupCommitWindowMs
          GroupCommitBatchSize = options.GroupCommitBatchSize
          DbQueueCapacity = options.DbQueueCapacity
          CheckpointMinIntervalMs = options.CheckpointMinIntervalMs
          Host = options.Host
          Port = options.Port
          MaxConnections = options.MaxConnections
          ReceiveTimeout = options.ReceiveTimeout
          SendTimeout = options.SendTimeout }

    let ToConfigurationValues (config: OcisConfig) =
        let options = FromConfig config
        let values = Dictionary<string, string>()

        values.Add($"{SectionName}:Dir", options.Dir)
        values.Add($"{SectionName}:FlushThreshold", string options.FlushThreshold)
        values.Add($"{SectionName}:L0CompactionThreshold", string options.L0CompactionThreshold)
        values.Add($"{SectionName}:LevelSizeMultiplier", string options.LevelSizeMultiplier)
        values.Add($"{SectionName}:LogLevel", options.LogLevel)
        values.Add($"{SectionName}:DurabilityMode", options.DurabilityMode)
        values.Add($"{SectionName}:GroupCommitWindowMs", string options.GroupCommitWindowMs)
        values.Add($"{SectionName}:GroupCommitBatchSize", string options.GroupCommitBatchSize)
        values.Add($"{SectionName}:DbQueueCapacity", string options.DbQueueCapacity)
        values.Add($"{SectionName}:CheckpointMinIntervalMs", string options.CheckpointMinIntervalMs)
        values.Add($"{SectionName}:Host", options.Host)
        values.Add($"{SectionName}:Port", string options.Port)
        values.Add($"{SectionName}:MaxConnections", string options.MaxConnections)
        values.Add($"{SectionName}:ReceiveTimeout", string options.ReceiveTimeout)
        values.Add($"{SectionName}:SendTimeout", string options.SendTimeout)
        values

type OcisHostedService(options: IOptions<OcisServerOptions>) =
    let mutable runtime: Ocis.OcisRuntime option = None

    interface IHostedService with
        member _.StartAsync(cancellationToken: CancellationToken) =
            match Ocis.TryCreateRuntime(options.Value |> OcisServerOptions.ToConfig) with
            | Ok createdRuntime ->
                runtime <- Some createdRuntime

                task {
                    try
                        do! createdRuntime.StartAsync(cancellationToken)
                    with ex ->
                        runtime <- None
                        (createdRuntime :> IDisposable).Dispose()
                        return raise ex
                }
                :> Task
            | Error msg -> Task.FromException(InvalidOperationException(msg))

        member _.StopAsync(cancellationToken: CancellationToken) =
            // Graceful stop is delegated to OcisRuntime.
            match runtime with
            | Some activeRuntime -> activeRuntime.StopAsync(cancellationToken)
            | None -> Task.CompletedTask

    interface IDisposable with
        member _.Dispose() =
            match runtime with
            | Some activeRuntime ->
                (activeRuntime :> IDisposable).Dispose()
                runtime <- None
            | None -> ()
