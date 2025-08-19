module Ocis.Server.Server

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections.Concurrent
open Ocis.Server.Connection
open Ocis.OcisDB
open Ocis.Utils.Logger

/// Server configuration
type ServerConfig =
  { Host : string
    Port : int
    MaxConnections : int
    ReceiveTimeout : int
    SendTimeout : int }

/// Server state
type ServerState =
  | Starting
  | Running
  | Stopping
  | Stopped
  | Error of string

/// TCP server
type TcpServer (config : ServerConfig, db : OcisDB) =
  let mutable state = ServerState.Starting
  let mutable tcpListener : TcpListener option = None
  let mutable isDisposed = false
  let cancellationTokenSource = new CancellationTokenSource ()

  // Active connection management
  let activeConnections = ConcurrentDictionary<string, Connection> ()

  member _.Config = config
  member _.State = state
  member _.ActiveConnectionCount = activeConnections.Count

  /// Start server
  member this.StartAsync () =
    async {
      try
        Logger.Info $"Starting TCP server on {config.Host}:{config.Port}"

        // Create and start TCP listener
        let endpoint = IPEndPoint (IPAddress.Parse config.Host, config.Port)
        let listener = new TcpListener (endpoint)
        listener.Start ()
        tcpListener <- Some listener

        state <- ServerState.Running

        Logger.Info
          $"TCP server started successfully on {config.Host}:{config.Port}"

        // Start accepting connections
        do! this.AcceptConnectionsAsync listener

      with ex ->
        state <- ServerState.Error ex.Message
        Logger.Error $"Failed to start TCP server: {ex.Message}"
    }

  /// Main loop of accepting client connections
  member private this.AcceptConnectionsAsync (listener : TcpListener) =
    async {
      try
        while not cancellationTokenSource.Token.IsCancellationRequested
              && state = ServerState.Running do
          try
            // Check connection limit
            if activeConnections.Count >= config.MaxConnections then
              Logger.Warn
                $"Maximum connections ({config.MaxConnections}) reached, waiting..."

              do! Async.Sleep 1000
            else
              // Accept new connection
              let! tcpClient =
                listener.AcceptTcpClientAsync () |> Async.AwaitTask

              // Configure client connection
              tcpClient.ReceiveTimeout <- config.ReceiveTimeout
              tcpClient.SendTimeout <- config.SendTimeout
              tcpClient.NoDelay <- true

              // Create connection handler
              let connection =
                ConnectionManager.createConnection tcpClient.Client db

              // Add connection to active connection list
              activeConnections.TryAdd (connection.ConnectionId, connection)
              |> ignore

              Logger.Debug
                $"New connection accepted: {connection.ConnectionId}, total connections: {activeConnections.Count}"

              // Asynchronously handle connection
              this.HandleConnectionAsync connection |> Async.Start

          with ex ->
            Logger.Error $"Error accepting connection: {ex.Message}"
            do! Async.Sleep 100 // Short wait and retry

      with ex ->
        Logger.Error $"Error in connection accept loop: {ex.Message}"
        state <- ServerState.Error ex.Message
    }

  /// Handle single client connection
  member private this.HandleConnectionAsync (connection : Connection) =
    async {
      try
        try
          // Start connection handling
          do! ConnectionManager.startConnection connection
        with ex ->
          Logger.Error
            $"Error handling connection {connection.ConnectionId}: {ex.Message}"
      finally
        // Clean up connection
        activeConnections.TryRemove connection.ConnectionId |> ignore

        Logger.Debug
          $"Connection {connection.ConnectionId} removed, remaining connections: {activeConnections.Count}"
    }

  /// Stop server
  member this.StopAsync () =
    async {
      if state = ServerState.Running then
        try
          Logger.Info "Stopping TCP server..."
          state <- ServerState.Stopping

          // Stop accepting new connections
          cancellationTokenSource.Cancel ()

          // Close listener
          match tcpListener with
          | Some listener ->
            listener.Stop ()
            tcpListener <- None
          | None -> ()

          // Close all active connections
          Logger.Info $"Closing {activeConnections.Count} active connections..."

          let closeConnections =
            async {
              let connectionCloses =
                activeConnections.Values
                |> Seq.map (fun conn -> conn.CloseAsync ())
                |> Seq.toArray

              do! Async.Parallel connectionCloses |> Async.Ignore
            }

          // Wait for connections to close, up to 10 seconds
          let! closeResult = Async.StartChild (closeConnections, 10000)

          try
            do! closeResult
          with ex ->
            Logger.Warn
              $"Timeout waiting for connections to close: {ex.Message}"

          state <- ServerState.Stopped
          Logger.Info "TCP server stopped successfully"

        with ex ->
          state <- ServerState.Error ex.Message
          Logger.Error $"Error stopping TCP server: {ex.Message}"
    }

  /// Get server statistics
  member this.GetStats () =
    {| State = state
       ActiveConnections = activeConnections.Count
       Host = config.Host
       Port = config.Port
       MaxConnections = config.MaxConnections |}

  interface IDisposable with
    member this.Dispose () =
      if not isDisposed then
        isDisposed <- true
        this.StopAsync () |> Async.RunSynchronously
        cancellationTokenSource.Dispose ()

/// Server manager module
module ServerManager =

  /// Create default server configuration
  let createDefaultConfig (host : string) (port : int) =
    { Host = host
      Port = port
      MaxConnections = 1000
      ReceiveTimeout = 30000 // 30秒
      SendTimeout = 30000 // 30秒
    }

  /// Create server instance
  let createServer (config : ServerConfig) (db : OcisDB) =
    new TcpServer (config, db)

  /// Start server and wait for stop signal
  let runServer (server : TcpServer) (cancellationToken : CancellationToken) =
    async {
      try
        // Start server
        let serverTask = server.StartAsync ()

        // Wait for cancel signal
        let! _ =
          Async.StartChild (
            async {
              while not cancellationToken.IsCancellationRequested do
                do! Async.Sleep 1000
            }
          )

        // Stop server
        do! server.StopAsync ()

      with ex ->
        Logger.Error $"Error running server: {ex.Message}"
    }
