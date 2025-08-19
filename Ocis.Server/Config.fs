module Ocis.Server.Config

type OcisConfig =
  { Dir : string
    FlushThreshold : int
    L0CompactionThreshold : int
    LevelSizeMultiplier : int
    LogLevel : string

    Host : string
    Port : int
    MaxConnections : int
    ReceiveTimeout : int
    SendTimeout : int }

module ConfigHelper =

  let CreateDefault (dir : string) =
    { Dir = dir
      FlushThreshold = 1000
      L0CompactionThreshold = 4
      LevelSizeMultiplier = 5
      LogLevel = "Info"

      Host = "0.0.0.0"
      Port = 7379
      MaxConnections = 1000
      ReceiveTimeout = 30000 // 30 seconds
      SendTimeout = 30000 // 30 seconds
    }

  let ValidateConfig (config : OcisConfig) : Result<unit, string> =
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
    elif config.ReceiveTimeout <= 0 then
      Error "ReceiveTimeout must be greater than 0"
    elif config.SendTimeout <= 0 then
      Error "SendTimeout must be greater than 0"
    else
      Ok ()
