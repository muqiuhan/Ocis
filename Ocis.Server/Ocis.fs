module Ocis.Server.Ocis

open System
open System.Threading
open System.Threading.Tasks
open Ocis.Server.Config
open Ocis.Server.Server
open Ocis.Server.DbDispatcher
open Ocis.OcisDB
open Ocis.Utils.Logger
open Microsoft.Extensions.Logging

module Ocis =
    let private applyLogLevel (logLevel: string) =
        match logLevel with
        | "Debug" -> Logger.SetLogLevel LogLevel.Debug
        | "Info" -> Logger.SetLogLevel LogLevel.Information
        | "Warn" -> Logger.SetLogLevel LogLevel.Warning
        | "Error" -> Logger.SetLogLevel LogLevel.Error
        | "Fatal" -> Logger.SetLogLevel LogLevel.Critical
        | _ -> Logger.SetLogLevel LogLevel.Information

    type OcisRuntime(config: OcisConfig, db: OcisDB, dispatcher: OcisDbDispatcher, server: TcpServer) =
        let mutable serverTask: Task option = None
        let mutable disposed = false

        let rec waitForServerStartup (runningTask: Task) (cancellationToken: CancellationToken) =
            task {
                cancellationToken.ThrowIfCancellationRequested()

                if runningTask.IsFaulted then
                    let startupException =
                        if isNull runningTask.Exception then
                            InvalidOperationException("Server startup failed") :> exn
                        else
                            runningTask.Exception.GetBaseException()

                    return raise startupException
                elif runningTask.IsCanceled then
                    return raise (TaskCanceledException("Server startup was canceled") :> exn)
                elif server.State = ServerState.Running then
                    return ()
                else
                    do! Task.Delay(10, cancellationToken)
                    return! waitForServerStartup runningTask cancellationToken
            }

        member _.StartAsync(cancellationToken: CancellationToken) : Task =
            Logger.Info $"Starting Ocis Server in {config.Dir}"
            Logger.Info $"Network: {config.Host}:{config.Port}, Max connections: {config.MaxConnections}"
            Logger.Info $"Database: flush threshold={config.FlushThreshold}"

            let serverRunTask = server.StartAsync() |> Async.StartAsTask
            serverTask <- Some serverRunTask

            task {
                do! waitForServerStartup serverRunTask cancellationToken
                Logger.Info "Ocis Server started successfully"
            }
            :> Task

        member _.StopAsync(cancellationToken: CancellationToken) : Task =
            task {
                Logger.Info "Stopping server..."
                let mutable serverStopError: exn option = None
                let mutable dispatcherStopError: exn option = None

                try
                    do! server.StopAsync() |> Async.StartAsTask
                with ex ->
                    serverStopError <- Some ex
                    Logger.Error $"Error stopping TCP server: {ex.Message}"

                try
                    do! dispatcher.StopAsync() |> Async.StartAsTask
                with ex ->
                    dispatcherStopError <- Some ex
                    Logger.Error $"Error stopping dispatcher: {ex.Message}"

                match serverTask with
                | Some runningTask ->
                    let! completedTask =
                        Task.WhenAny(runningTask, Task.Delay(TimeSpan.FromSeconds 10.0, cancellationToken))

                    if not (Object.ReferenceEquals(completedTask, runningTask)) then
                        Logger.Warn "Server shutdown timeout"
                    elif runningTask.IsFaulted then
                        let serverException =
                            if isNull runningTask.Exception then
                                InvalidOperationException("Server task faulted") :> exn
                            else
                                runningTask.Exception.GetBaseException()

                        Logger.Error $"Server task faulted: {serverException.Message}"
                        return raise serverException
                    elif runningTask.IsCanceled then
                        Logger.Warn "Server task was canceled"
                | None -> ()

                match serverStopError, dispatcherStopError with
                | Some ex, _ -> return raise ex
                | None, Some ex -> return raise ex
                | None, None -> ()

                Logger.Info "Ocis Server shutdown completed"
            }
            :> Task

        interface IDisposable with
            member this.Dispose() =
                if not disposed then
                    disposed <- true
                    try
                        this.StopAsync(CancellationToken.None).GetAwaiter().GetResult()
                    with ex ->
                        Logger.Error $"Error during runtime shutdown: {ex.Message}"

                    (server :> IDisposable).Dispose()
                    (dispatcher :> IDisposable).Dispose()
                    (db :> IDisposable).Dispose()

    let TryCreateRuntime (config: OcisConfig) : Result<OcisRuntime, string> =
        match ConfigHelper.ValidateConfig config with
        | Result.Error msg ->
            Logger.Error $"Configuration validation failed: {msg}"
            Result.Error msg
        | Ok() ->
            Logger.Info "Configuration validation passed"
            OcisDB.L0CompactionThreshold <- config.L0CompactionThreshold
            OcisDB.LevelSizeMultiplier <- config.LevelSizeMultiplier
            applyLogLevel config.LogLevel

            match
                OcisDB.Open(
                    config.Dir,
                    config.FlushThreshold,
                    durabilityMode = config.DurabilityMode,
                    groupCommitWindowMs = config.GroupCommitWindowMs
                )
            with
            | Ok db ->
                Logger.Info "Database opened successfully"

                let serverConfig =
                    { Host = config.Host
                      Port = config.Port
                      MaxConnections = config.MaxConnections
                      ReceiveTimeout = config.ReceiveTimeout
                      SendTimeout = config.SendTimeout }

                try
                    let dispatcher = new OcisDbDispatcher(db, config.DbQueueCapacity)
                    let server = ServerManager.createServer serverConfig dispatcher

                    try
                        Ok(new OcisRuntime(config, db, dispatcher, server))
                    with ex ->
                        (server :> IDisposable).Dispose()
                        (dispatcher :> IDisposable).Dispose()
                        (db :> IDisposable).Dispose()
                        Logger.Error $"Failed to create runtime: {ex.Message}"
                        Result.Error ex.Message
                with ex ->
                    (db :> IDisposable).Dispose()
                    Logger.Error $"Failed to create server: {ex.Message}"
                    Result.Error ex.Message
            | Result.Error msg ->
                Logger.Error $"Failed to open database: {msg}"
                Result.Error msg
