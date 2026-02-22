module Ocis.Server.Server

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections.Concurrent
open Ocis.Server.Connection
open Ocis.Server.DbDispatcher
open Ocis.Server.Resilience
open Ocis.Utils.Logger

/// Server configuration
type ServerConfig =
    { Host: string
      Port: int
      MaxConnections: int
      ReceiveTimeout: int
      SendTimeout: int }

/// Server state
type ServerState =
    | Starting
    | Running
    | Stopping
    | Stopped
    | Error of string

/// TCP server
type TcpServer(config: ServerConfig, dispatcher: OcisDbDispatcher) =
    let mutable state = ServerState.Starting
    let mutable tcpListener: TcpListener option = None
    let mutable isDisposed = false
    let cancellationTokenSource = new CancellationTokenSource()

    // Active connection management
    let activeConnections = ConcurrentDictionary<string, Connection>()

    member _.Config = config
    member _.State = state
    member _.ActiveConnectionCount = activeConnections.Count

    /// Start server
    member this.StartAsync() =
        async {
            try
                Logger.Info $"Starting TCP server on {config.Host}:{config.Port}"

                // Create and start TCP listener
                let endpoint = IPEndPoint(IPAddress.Parse config.Host, config.Port)
                let listener = new TcpListener(endpoint)
                listener.Start()
                tcpListener <- Some listener

                state <- ServerState.Running

                Logger.Info $"TCP server started successfully on {config.Host}:{config.Port}"

                // Start accepting connections
                do! this.AcceptConnectionsAsync listener

            with ex ->
                state <- ServerState.Error ex.Message
                Logger.Error $"Failed to start TCP server: {ex.Message}"
                return raise ex
        }

    /// Main accept loop with connection-limit backpressure and bounded retry
    /// for transient socket failures.
    member private this.AcceptConnectionsAsync(listener: TcpListener) =
        async {
            let mutable transientAcceptFailureCount = 0

            try
                while not cancellationTokenSource.Token.IsCancellationRequested
                      && state = ServerState.Running do
                    try
                        // Backpressure when connection budget is exhausted.
                        if activeConnections.Count >= config.MaxConnections then
                            Logger.Warn $"Maximum connections ({config.MaxConnections}) reached, waiting..."

                            do! Async.Sleep 1000
                        else
                            // Accept and configure one client connection.
                            let! tcpClient = listener.AcceptTcpClientAsync() |> Async.AwaitTask
                            transientAcceptFailureCount <- 0

                            // Configure client connection
                            tcpClient.ReceiveTimeout <- config.ReceiveTimeout
                            tcpClient.SendTimeout <- config.SendTimeout
                            tcpClient.NoDelay <- true

                            // Create connection handler
                            let connection = ConnectionManager.createConnection tcpClient.Client dispatcher

                            // Add connection to active connection list
                            activeConnections.TryAdd(connection.ConnectionId, connection) |> ignore

                            Logger.Debug
                                $"New connection accepted: {connection.ConnectionId}, total connections: {activeConnections.Count}"

                            // Asynchronously handle connection
                            this.HandleConnectionAsync connection |> Async.Start

                    with ex ->
                        let shuttingDown =
                            cancellationTokenSource.Token.IsCancellationRequested || state <> ServerState.Running

                        if shuttingDown then
                            ()
                        elif isTransientAcceptException ex then
                            let retryDelayMs = computeBoundedRetryDelayMs transientAcceptFailureCount 50 1000
                            transientAcceptFailureCount <- transientAcceptFailureCount + 1

                            Logger.Warn
                                $"Transient accept error ({ex.Message}); retrying in {retryDelayMs}ms"

                            do! Async.Sleep retryDelayMs
                        else
                            Logger.Error $"Fatal error accepting connection: {ex.Message}"
                            return raise ex

            with ex ->
                Logger.Error $"Error in connection accept loop: {ex.Message}"
                state <- ServerState.Error ex.Message
                return raise ex
        }

    /// Handle a single client lifecycle. Cleanup is guaranteed in finally.
    member private this.HandleConnectionAsync(connection: Connection) =
        async {
            try
                try
                    // Start connection handling
                    do! ConnectionManager.startConnection connection
                with ex ->
                    Logger.Error $"Error handling connection {connection.ConnectionId}: {ex.Message}"
            finally
                // Clean up connection
                activeConnections.TryRemove connection.ConnectionId |> ignore

                Logger.Debug
                    $"Connection {connection.ConnectionId} removed, remaining connections: {activeConnections.Count}"
        }

    /// Graceful shutdown order:
    /// 1) stop accepting, 2) close listener, 3) close active connections.
    member this.StopAsync() =
        async {
            match state with
            | ServerState.Running
            | ServerState.Error _ ->
                try
                    Logger.Info "Stopping TCP server..."
                    state <- ServerState.Stopping

                    // Stop accepting new connections
                    cancellationTokenSource.Cancel()

                    // Close listener
                    match tcpListener with
                    | Some listener ->
                        listener.Stop()
                        tcpListener <- None
                    | None -> ()

                    // Close all active connections
                    Logger.Info $"Closing {activeConnections.Count} active connections..."

                    let closeConnections =
                        async {
                            let connectionCloses =
                                activeConnections.Values
                                |> Seq.map (fun conn -> conn.CloseAsync())
                                |> Seq.toArray

                            do! Async.Parallel connectionCloses |> Async.Ignore
                        }

                    // Wait for connections to close, up to 10 seconds
                    let! closeResult = Async.StartChild(closeConnections, 10000)

                    try
                        do! closeResult
                    with ex ->
                        Logger.Warn $"Timeout waiting for connections to close: {ex.Message}"

                    state <- ServerState.Stopped
                    Logger.Info "TCP server stopped successfully"

                with ex ->
                    state <- ServerState.Error ex.Message
                    Logger.Error $"Error stopping TCP server: {ex.Message}"
            | _ -> ()
        }

    /// Get server statistics
    member this.GetStats() =
        {| State = state
           ActiveConnections = activeConnections.Count
           Host = config.Host
           Port = config.Port
           MaxConnections = config.MaxConnections |}

    interface IDisposable with
        member this.Dispose() =
            if not isDisposed then
                isDisposed <- true
                this.StopAsync() |> Async.RunSynchronously
                cancellationTokenSource.Dispose()

/// Server manager module
module ServerManager =

    /// Create default server configuration
    let createDefaultConfig (host: string) (port: int) =
        { Host = host
          Port = port
          MaxConnections = 1000
          ReceiveTimeout = 30000 // 30 seconds
          SendTimeout = 30000 // 30 seconds
        }

    /// Create server instance
    let createServer (config: ServerConfig) (dispatcher: OcisDbDispatcher) =
        new TcpServer(config, dispatcher)

    /// Start server and wait for stop signal
    let runServer (server: TcpServer) (cancellationToken: CancellationToken) =
        async {
            try
                // Start server
                let serverTask = server.StartAsync()

                // Wait for cancel signal
                let! _ =
                    Async.StartChild(
                        async {
                            while not cancellationToken.IsCancellationRequested do
                                do! Async.Sleep 1000
                        }
                    )

                // Stop server
                do! server.StopAsync()

            with ex ->
                Logger.Error $"Error running server: {ex.Message}"
        }
