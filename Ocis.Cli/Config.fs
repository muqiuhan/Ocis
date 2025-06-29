module Ocis.Cli.Config

type OcisConfig = {
    Dir : string
    FlushThreshold : int
    L0CompactionThreshold : int
    LevelSizeMultiplier : int
    LogLevel : string
}
