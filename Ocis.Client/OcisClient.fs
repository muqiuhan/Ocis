module Ocis.Client

open System
open System.IO
open System.Net.Sockets
open System.Text
open Ocis.Server.ProtocolSpec
open Ocis.Server.Protocol

type ClientResult<'T> =
  | Success of 'T
  | NotFound
  | Error of string

module Request =

  let private createPacket
    (commandType : CommandType)
    (key : string)
    (value : byte array option)
    : RequestPacket
    =
    let keyBytes = Encoding.UTF8.GetBytes key

    let valueLen =
      value
      |> Option.map (fun v -> v.Length)
      |> Option.defaultValue 0

    let totalLen = HEADER_SIZE + keyBytes.Length + valueLen

    { MagicNumber = MAGIC_NUMBER
      Version = PROTOCOL_VERSION
      CommandType = commandType
      TotalPacketLength = totalLen
      KeyLength = keyBytes.Length
      ValueLength = valueLen
      Key = keyBytes
      Value = value }

  let createSetRequest (key : string) (value : byte array) : byte[] =
    let packet = createPacket CommandType.Set key (Some value)
    Protocol.SerializeRequest packet

  let createGetRequest (key : string) : byte[] =
    let packet = createPacket CommandType.Get key None
    Protocol.SerializeRequest packet

  let createDeleteRequest (key : string) : byte[] =
    let packet = createPacket CommandType.Delete key None
    Protocol.SerializeRequest packet

module Response =

  let parseResponse
    (bytes : byte array)
    : Protocol.ParseResult<ResponsePacket>
    =
    Protocol.DeserializeResponse bytes

  let toClientResult
    (parseResult : Protocol.ParseResult<ResponsePacket>)
    : ClientResult<unit>
    =
    match parseResult with
    | Protocol.ParseSuccess response ->
      match response.StatusCode with
      | StatusCode.Success -> Success ()
      | StatusCode.NotFound -> NotFound
      | StatusCode.Error ->
        Error (
          response.ErrorMessage
          |> Option.defaultValue "Unknown error"
        )
      | _ -> Error "Invalid status code"
    | Protocol.ParseError msg -> Error msg
    | Protocol.InsufficientData -> Error "Insufficient data"

  let toClientResultValue
    (parseResult : Protocol.ParseResult<ResponsePacket>)
    : ClientResult<byte array>
    =
    match parseResult with
    | Protocol.ParseSuccess response ->
      match response.StatusCode with
      | StatusCode.Success ->
        match response.Value with
        | Some value -> Success value
        | None -> Error "Success response missing value"
      | StatusCode.NotFound -> NotFound
      | StatusCode.Error ->
        Error (
          response.ErrorMessage
          |> Option.defaultValue "Unknown error"
        )
      | _ -> Error "Invalid status code"
    | Protocol.ParseError msg -> Error msg
    | Protocol.InsufficientData -> Error "Insufficient data"

module TcpClient =

  type Connection
    (
      host : string,
      port : int,
      client : System.Net.Sockets.TcpClient,
      stream : NetworkStream
    )
    =
    member this.Host = host
    member this.Port = port
    member this.Client = client
    member this.Stream = stream

    interface IDisposable with
      member this.Dispose () =
        stream.Dispose ()
        client.Dispose ()

  let connect (host : string) (port : int) : Connection =
    let client = new System.Net.Sockets.TcpClient ()
    client.Connect (host, port)
    Connection (host, port, client, client.GetStream ())

  let disconnect (conn : Connection) : unit = (conn :> IDisposable).Dispose ()

  let private readExact (stream : NetworkStream) (count : int) : byte[] =
    let buffer = Array.zeroCreate<byte> count
    let mutable totalRead = 0

    while totalRead < count do
      let bytesRead =
        stream.Read (buffer, totalRead, count - totalRead)

      if bytesRead = 0 then
        failwith "Connection closed unexpectedly"

      totalRead <- totalRead + bytesRead

    buffer

  let private sendRequest (conn : Connection) (requestBytes : byte[]) : byte[] =
    conn.Stream.Write (requestBytes, 0, requestBytes.Length)
    conn.Stream.Flush ()

    let headerBytes = readExact conn.Stream 18

    use headerMs = new MemoryStream (headerBytes)
    use headerReader = new BinaryReader (headerMs)
    headerReader.ReadUInt32 () |> ignore
    headerReader.ReadByte () |> ignore
    headerReader.ReadByte () |> ignore
    let totalLength = headerReader.ReadInt32 ()

    let remainingBytes = totalLength - 18

    if remainingBytes > 0 then
      let remaining = readExact conn.Stream remainingBytes
      Array.concat [ headerBytes ; remaining ]
    else
      headerBytes

  let set
    (conn : Connection)
    (key : string)
    (value : byte array)
    : ClientResult<unit>
    =
    try
      let requestBytes = Request.createSetRequest key value
      let responseBytes = sendRequest conn requestBytes
      let parseResult = Response.parseResponse responseBytes
      Response.toClientResult parseResult
    with ex ->
      Error ex.Message

  let get (conn : Connection) (key : string) : ClientResult<byte array> =
    try
      let requestBytes = Request.createGetRequest key
      let responseBytes = sendRequest conn requestBytes
      let parseResult = Response.parseResponse responseBytes
      Response.toClientResultValue parseResult
    with ex ->
      Error ex.Message

  let delete (conn : Connection) (key : string) : ClientResult<unit> =
    try
      let requestBytes = Request.createDeleteRequest key
      let responseBytes = sendRequest conn requestBytes
      let parseResult = Response.parseResponse responseBytes
      Response.toClientResult parseResult
    with ex ->
      Error ex.Message

module Helper =

  let stringToBytes (s : string) = Encoding.UTF8.GetBytes s
  let bytesToString (bytes : byte array) = Encoding.UTF8.GetString bytes

  let testConnection (host : string) (port : int) =
    try
      use client = new System.Net.Sockets.TcpClient ()
      client.Connect (host, port)
      true
    with _ ->
      false
