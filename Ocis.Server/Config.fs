module Ocis.Server.Config

type OcisConfig =
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

module ConfigHelper =

    let CreateDefault (dir: string) =
        { Dir = dir
          FlushThreshold = 1000
          L0CompactionThreshold = 4
          LevelSizeMultiplier = 5
          LogLevel = "Info"
          DurabilityMode = "Balanced"
          GroupCommitWindowMs = 5
          GroupCommitBatchSize = 64
          DbQueueCapacity = 8192
          CheckpointMinIntervalMs = 30000

          Host = "0.0.0.0"
          Port = 7379
          MaxConnections = 1000
          ReceiveTimeout = 30000 // 30 seconds
          SendTimeout = 30000 // 30 seconds
        }

    let ValidateConfig (config: OcisConfig) : Result<unit, string> =
        if config.Port <= 0 || config.Port > 65535 then
            Error "Port must be between 1 and 65535"
        elif config.MaxConnections <= 0 then
            Error "MaxConnections must be greater than 0"
        elif config.FlushThreshold <= 0 then
            Error "FlushThreshold must be greater than 0"
        elif config.L0CompactionThreshold <= 0 then
            Error "L0CompactionThreshold must be greater than 0"
        elif config.LevelSizeMultiplier <= 0 then
            Error "LevelSizeMultiplier must be greater than 0"
        elif
            config.LogLevel <> "Debug"
            && config.LogLevel <> "Info"
            && config.LogLevel <> "Warn"
            && config.LogLevel <> "Error"
            && config.LogLevel <> "Fatal"
        then
            Error "LogLevel must be one of: Debug, Info, Warn, Error, Fatal"
        elif
            config.DurabilityMode <> "Strict"
            && config.DurabilityMode <> "Balanced"
            && config.DurabilityMode <> "Fast"
        then
            Error "DurabilityMode must be one of: Strict, Balanced, Fast"
        elif config.GroupCommitWindowMs <= 0 then
            Error "GroupCommitWindowMs must be greater than 0"
        elif config.GroupCommitBatchSize <= 0 then
            Error "GroupCommitBatchSize must be greater than 0"
        elif config.DbQueueCapacity <= 0 then
            Error "DbQueueCapacity must be greater than 0"
        elif config.CheckpointMinIntervalMs <= 0 then
            Error "CheckpointMinIntervalMs must be greater than 0"
        elif config.ReceiveTimeout <= 0 then
            Error "ReceiveTimeout must be greater than 0"
        elif config.SendTimeout <= 0 then
            Error "SendTimeout must be greater than 0"
        else
            Ok()
