module Ocis.Server.Ocis

open Ocis.Server.Config
open Ocis.OcisDB
open Ocis.Utils.Logger
open Microsoft.Extensions.Logging

module Ocis =
    let Run (config : OcisConfig) =
        Logger.Info $"Starting Ocis in {config.Dir} with flush threshold={config.FlushThreshold}"

        Logger.Info $"Setting L0 compaction threshold={config.L0CompactionThreshold}"
        OcisDB.L0CompactionThreshold <- config.L0CompactionThreshold

        Logger.Info $"Setting level size multiplier={config.LevelSizeMultiplier}"
        OcisDB.LevelSizeMultiplier <- config.LevelSizeMultiplier

        match config.LogLevel with
        | "Debug" -> Logger.SetLogLevel LogLevel.Debug
        | "Info" -> Logger.SetLogLevel LogLevel.Information
        | "Warn" -> Logger.SetLogLevel LogLevel.Warning
        | "Error" -> Logger.SetLogLevel LogLevel.Error
        | "Fatal" -> Logger.SetLogLevel LogLevel.Critical
        | _ -> Logger.SetLogLevel LogLevel.Information

        match OcisDB.Open (config.Dir, config.FlushThreshold) with
        | Ok db ->
            use db = db

            System.GC.Collect ()
            System.GC.WaitForPendingFinalizers ()
            System.GC.Collect ()

            Logger.Info $"Ocis started successfully"

        | Error msg -> Logger.Error $"Failed to open DB: {msg}"

        Logger.Info $"Ocis shutting down...ok"
        0
