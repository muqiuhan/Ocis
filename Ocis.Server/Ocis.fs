module Ocis.Server.Ocis

open System
open System.Threading
open Ocis.Server.Config
open Ocis.Server.Server
open Ocis.OcisDB
open Ocis.Utils.Logger
open Microsoft.Extensions.Logging

module Ocis =
  let Run (config : OcisConfig) =
    Logger.Info $"Starting Ocis Server in {config.Dir}"

    Logger.Info
      $"Network: {config.Host}:{config.Port}, Max connections: {config.MaxConnections}"

    Logger.Info $"Database: flush threshold={config.FlushThreshold}"

    // Validate configuration
    match ConfigHelper.ValidateConfig config with
    | Result.Error msg ->
      Logger.Error $"Configuration validation failed: {msg}"
      1
    | Ok () ->
      Logger.Info "Configuration validation passed"

      Logger.Info
        $"Setting L0 compaction threshold={config.L0CompactionThreshold}"

      OcisDB.L0CompactionThreshold <- config.L0CompactionThreshold

      Logger.Info $"Setting level size multiplier={config.LevelSizeMultiplier}"
      OcisDB.LevelSizeMultiplier <- config.LevelSizeMultiplier

      // Set log level
      match config.LogLevel with
      | "Debug" -> Logger.SetLogLevel LogLevel.Debug
      | "Info" -> Logger.SetLogLevel LogLevel.Information
      | "Warn" -> Logger.SetLogLevel LogLevel.Warning
      | "Error" -> Logger.SetLogLevel LogLevel.Error
      | "Fatal" -> Logger.SetLogLevel LogLevel.Critical
      | _ -> Logger.SetLogLevel LogLevel.Information

      // Open database
      match OcisDB.Open (config.Dir, config.FlushThreshold) with
      | Ok db ->
        use db = db
        Logger.Info "Database opened successfully"

        // Create server configuration
        let serverConfig =
          { Host = config.Host
            Port = config.Port
            MaxConnections = config.MaxConnections
            ReceiveTimeout = config.ReceiveTimeout
            SendTimeout = config.SendTimeout }

        // Create and start server
        use server = ServerManager.createServer serverConfig db
        use cancellationTokenSource = new CancellationTokenSource ()

        // Set graceful shutdown signal handling
        Console.CancelKeyPress.Add (fun args ->
          args.Cancel <- true
          Logger.Info "Shutdown signal received, stopping server..."
          cancellationTokenSource.Cancel ())

        try
          // Start server
          let serverTask = server.StartAsync () |> Async.StartAsTask

          Logger.Info "Ocis Server started successfully"
          Logger.Info "Press Ctrl+C to stop the server"

          // Wait for stop signal
          try
            while not cancellationTokenSource.Token.IsCancellationRequested do
              Thread.Sleep 1000

              // Periodically output server status
              let stats = server.GetStats ()

              if stats.ActiveConnections > 0 then
                Logger.Debug
                  $"Server status: {stats.State}, Active connections: {stats.ActiveConnections}"
          with :? OperationCanceledException ->
            ()

          // Stop server
          Logger.Info "Stopping server..."
          server.StopAsync () |> Async.RunSynchronously

          // Wait for server task to complete
          try
            serverTask.Wait (TimeSpan.FromSeconds 10.0) |> ignore
          with
          | :? TimeoutException -> Logger.Warn "Server shutdown timeout"
          | ex -> Logger.Error $"Error during server shutdown: {ex.Message}"

          Logger.Info "Ocis Server shutdown completed"
          0

        with ex ->
          Logger.Error $"Server error: {ex.Message}"
          1

      | Result.Error msg ->
        Logger.Error $"Failed to open database: {msg}"
        1
