module Ocis.Server.Connection

open System
open System.Net.Sockets
open System.Threading
open Ocis.Server.ProtocolSpec
open Ocis.Server.Protocol
open Ocis.Server.Handler
open Ocis.Server.DbDispatcher
open Ocis.Utils.Logger

/// Connection state
type ConnectionState =
  | Connected
  | Disconnected
  | Error of string

/// Connection manager
type Connection
  (socket : Socket, dispatcher : OcisDbDispatcher, connectionId : string)
  =
  let mutable state = ConnectionState.Connected
  let mutable isDisposed = false
  let cancellationTokenSource = new CancellationTokenSource ()

  member _.ConnectionId = connectionId
  member _.State = state
  member _.Socket = socket

  /// Per-connection request loop. Requests are processed sequentially for
  /// each socket while many connections run concurrently.
  member this.HandleAsync () =
    async {
      try
        use _connectionScope =
          Logger.BeginScope $"connectionId={connectionId}"

        Logger.Debug $"Connection {connectionId} started"

        // Create network stream
        use networkStream = new NetworkStream (socket)

        // Message processing loop
        let mutable continueProcessing = true
        let mutable requestOrdinal = 0

        while continueProcessing
              && not cancellationTokenSource.Token.IsCancellationRequested
              && socket.Connected do
          match! this.ReadMessageAsync networkStream with
          | Some request ->
            requestOrdinal <- requestOrdinal + 1
            let requestId = $"{connectionId}-{requestOrdinal}"

            use _requestScope =
              Logger.BeginScope
                $"connectionId={connectionId} requestId={requestId}"

            let! response =
              RequestHandler.ProcessValidRequest dispatcher request

            Logger.Debug $"Processed request with status={response.StatusCode}"
            do! this.SendResponseAsync (networkStream, response)
          | None ->
            continueProcessing <- false
            Logger.Debug $"Connection {connectionId} ended by client"

        Logger.Debug $"Connection {connectionId} ended"

      with ex ->
        state <- ConnectionState.Error ex.Message
        Logger.Error $"Connection {connectionId} error: {ex.Message}"
    }

  /// Read one framed request: fixed-size header first, then payload bytes.
  /// Returns None on clean disconnect, framing error, or read failure.
  member private this.ReadMessageAsync (stream : NetworkStream) =
    async {
      try
        // Read header
        let headerBuffer = Array.zeroCreate<byte> HEADER_SIZE

        let! headerBytesRead =
          this.ReadExactAsync (stream, headerBuffer, HEADER_SIZE)

        if headerBytesRead < HEADER_SIZE then
          return None
        else
          match Protocol.TryParseRequestHeader headerBuffer with
          | Some header ->
            if Protocol.IsValidPacketSize header.TotalPacketLength then
              // Read complete packet
              let packetBuffer =
                Array.zeroCreate<byte> header.TotalPacketLength

              Array.Copy (headerBuffer, packetBuffer, HEADER_SIZE)

              let remainingBytes = header.TotalPacketLength - HEADER_SIZE

              if remainingBytes > 0 then
                let! remainingBytesRead =
                  this.ReadExactAsync (
                    stream,
                    packetBuffer,
                    remainingBytes,
                    HEADER_SIZE
                  )

                if remainingBytesRead < remainingBytes then
                  return None
                else
                  return Protocol.TryParseRequestPacket packetBuffer
              else
                return Protocol.TryParseRequestPacket packetBuffer
            else
              Logger.Warn
                $"Connection {connectionId} received packet with invalid size: {header.TotalPacketLength}"

              return None
          | None ->
            Logger.Debug $"Connection {connectionId} received invalid header"
            return None
      with ex ->
        Logger.Error $"Connection {connectionId} read error: {ex.Message}"
        state <- ConnectionState.Error ex.Message
        return None
    }

  /// Ensure reading specified number of bytes
  member private this.ReadExactAsync
    (stream : NetworkStream, buffer : byte array, count : int)
    =
    async { return! this.ReadExactAsync (stream, buffer, count, 0) }

  /// Ensure a target number of bytes are read unless the peer closes
  /// the connection or cancellation is requested.
  member private this.ReadExactAsync
    (stream : NetworkStream, buffer : byte array, count : int, offset : int)
    =
    async {
      let mutable totalRead = 0
      let mutable currentOffset = offset

      let mutable shouldContinue = true

      while shouldContinue
            && totalRead < count
            && not cancellationTokenSource.Token.IsCancellationRequested do
        let! bytesRead =
          stream.ReadAsync (
            buffer,
            currentOffset,
            count - totalRead,
            cancellationTokenSource.Token
          )
          |> Async.AwaitTask

        if bytesRead = 0 then
          // Connection closed
          shouldContinue <- false
        else
          totalRead <- totalRead + bytesRead
          currentOffset <- currentOffset + bytesRead

      return totalRead
    }

  /// Serialize and send one response frame.
  member private this.SendResponseAsync
    (stream : NetworkStream, response : ResponsePacket)
    =
    async {
      try
        let responseBytes = Protocol.SerializeResponse response

        do!
          stream.WriteAsync (
            responseBytes,
            0,
            responseBytes.Length,
            cancellationTokenSource.Token
          )
          |> Async.AwaitTask

        do!
          stream.FlushAsync cancellationTokenSource.Token
          |> Async.AwaitTask
      with ex ->
        Logger.Error
          $"Connection {connectionId} send response error: {ex.Message}"

        state <- ConnectionState.Error ex.Message
    }

  /// Close the connection and transition state to Disconnected when possible.
  member this.CloseAsync () =
    async {
      if not isDisposed then
        try
          cancellationTokenSource.Cancel ()

          if socket.Connected then
            socket.Shutdown SocketShutdown.Both

          socket.Close ()
          state <- ConnectionState.Disconnected
          Logger.Debug $"Connection {connectionId} closed"
        with ex ->
          Logger.Error $"Connection {connectionId} close error: {ex.Message}"
    }

  interface IDisposable with
    member this.Dispose () =
      if not isDisposed then
        isDisposed <- true
        this.CloseAsync () |> Async.RunSynchronously
        cancellationTokenSource.Dispose ()

/// Connection manager module
module ConnectionManager =

  /// Create new connection
  let createConnection
    (socket : Socket)
    (dispatcher : OcisDbDispatcher)
    : Connection
    =
    let connectionId = Guid.NewGuid().ToString("N").[..7]
    new Connection (socket, dispatcher, connectionId)

  /// Start connection handling
  let startConnection (connection : Connection) =
    async {
      use connection = connection
      do! connection.HandleAsync ()
    }
