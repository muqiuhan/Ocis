module Ocis.Server.Config

type OcisConfig =
  {Dir: string
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
   SendTimeout: int}

module ConfigHelper =

  // Baseline defaults for server and storage runtime.
  let CreateDefault(dir: string) =
    {Dir = dir
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

  // Validate user-provided configuration before runtime initialization.
  let ValidateConfig(config: OcisConfig) : Result<unit, string> =
    let validatePort() =
      if config.Port <= 0 || config.Port > 65535 then
        Error "Port must be between 1 and 65535"
      else
        Ok()

    let validateMaxConnections() =
      if config.MaxConnections <= 0 then
        Error "MaxConnections must be greater than 0"
      else
        Ok()

    let validateFlushThreshold() =
      if config.FlushThreshold <= 0 then
        Error "FlushThreshold must be greater than 0"
      else
        Ok()

    let validateL0CompactionThreshold() =
      if config.L0CompactionThreshold <= 0 then
        Error "L0CompactionThreshold must be greater than 0"
      else
        Ok()

    let validateLevelSizeMultiplier() =
      if config.LevelSizeMultiplier <= 0 then
        Error "LevelSizeMultiplier must be greater than 0"
      else
        Ok()

    let validateLogLevel() =
      if
        config.LogLevel <> "Debug"
        && config.LogLevel <> "Info"
        && config.LogLevel <> "Warn"
        && config.LogLevel <> "Error"
        && config.LogLevel <> "Fatal"
      then
        Error "LogLevel must be one of: Debug, Info, Warn, Error, Fatal"
      else
        Ok()

    let validateDurabilityMode() =
      if
        config.DurabilityMode <> "Strict"
        && config.DurabilityMode <> "Balanced"
        && config.DurabilityMode <> "Fast"
      then
        Error "DurabilityMode must be one of: Strict, Balanced, Fast"
      else
        Ok()

    let validateGroupCommitWindowMs() =
      if config.GroupCommitWindowMs <= 0 then
        Error "GroupCommitWindowMs must be greater than 0"
      else
        Ok()

    let validateGroupCommitBatchSize() =
      if config.GroupCommitBatchSize <= 0 then
        Error "GroupCommitBatchSize must be greater than 0"
      else
        Ok()

    let validateDbQueueCapacity() =
      if config.DbQueueCapacity <= 0 then
        Error "DbQueueCapacity must be greater than 0"
      else
        Ok()

    let validateCheckpointMinIntervalMs() =
      if config.CheckpointMinIntervalMs <= 0 then
        Error "CheckpointMinIntervalMs must be greater than 0"
      else
        Ok()

    let validateReceiveTimeout() =
      if config.ReceiveTimeout <= 0 then
        Error "ReceiveTimeout must be greater than 0"
      else
        Ok()

    let validateSendTimeout() =
      if config.SendTimeout <= 0 then
        Error "SendTimeout must be greater than 0"
      else
        Ok()

    let results =
      [validatePort
       validateMaxConnections
       validateFlushThreshold
       validateL0CompactionThreshold
       validateLevelSizeMultiplier
       validateLogLevel
       validateDurabilityMode
       validateGroupCommitWindowMs
       validateGroupCommitBatchSize
       validateDbQueueCapacity
       validateCheckpointMinIntervalMs
       validateReceiveTimeout
       validateSendTimeout ]
      |> List.map(fun v -> v())
      |> List.filter Result.isError

    match results with
    | [] -> Ok()
    | errors -> errors.Head
