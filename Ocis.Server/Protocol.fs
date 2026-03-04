module Ocis.Server.Protocol

open System
open System.IO
open System.Text
open Ocis.Server.ProtocolSpec

/// Protocol operations module
module Protocol =

  /// Validate magic number and version
  let inline IsValidHeader (magicNumber : uint32) (version : byte) =
    magicNumber = MAGIC_NUMBER
    && version = PROTOCOL_VERSION

  /// Parse the fixed-size request header from a byte buffer.
  /// Parsing failure returns None rather than throwing to caller code.
  let TryParseRequestHeader (buffer : byte array) =
    if buffer.Length < HEADER_SIZE then
      None
    else
      try
        use ms = new MemoryStream (buffer)
        use reader = new BinaryReader (ms)

        let magicNumber = reader.ReadUInt32 ()
        let version = reader.ReadByte ()
        let commandType = enum<CommandType> (int32 (reader.ReadByte ()))
        let totalPacketLength = reader.ReadInt32 ()
        let keyLength = reader.ReadInt32 ()
        let valueLength = reader.ReadInt32 ()

        if IsValidHeader magicNumber version then
          Some
            { MagicNumber = magicNumber
              Version = version
              CommandType = commandType
              TotalPacketLength = totalPacketLength
              KeyLength = keyLength
              ValueLength = valueLength
              Key = [||]
              Value = None }
        else
          None
      with _ ->
        None

  /// Parse one complete request frame (header + payload bytes).
  let TryParseRequestPacket (buffer : byte array) =
    match TryParseRequestHeader buffer with
    | Some header ->
      if buffer.Length >= header.TotalPacketLength then
        try
          use ms = new MemoryStream (buffer)
          use reader = new BinaryReader (ms)

          // Skip header
          ms.Seek (int64 HEADER_SIZE, SeekOrigin.Begin)
          |> ignore

          // Read payload
          let key = reader.ReadBytes header.KeyLength

          let value =
            if header.ValueLength > 0 then
              Some (reader.ReadBytes header.ValueLength)
            else
              None

          Some
            { header with
                Key = key
                Value = value }
        with _ ->
          None
      else
        None
    | None -> None

  /// Serialize a response frame in protocol wire order.
  let SerializeResponse (packet : ResponsePacket) =
    use ms = new MemoryStream ()
    use writer = new BinaryWriter (ms)

    // Write header
    writer.Write packet.MagicNumber
    writer.Write packet.Version
    writer.Write (byte packet.StatusCode)
    writer.Write packet.TotalPacketLength
    writer.Write packet.ValueLength
    writer.Write packet.ErrorMessageLength

    // Write payload data
    match packet.Value with
    | Some value -> writer.Write value
    | None -> ()

    match packet.ErrorMessage with
    | Some msg ->
      let msgBytes = Encoding.UTF8.GetBytes msg
      writer.Write msgBytes
    | None -> ()

    ms.ToArray ()

  /// Create success response
  let CreateSuccessResponse (value : byte array option) =
    let valueLen, totalLen =
      match value with
      | Some v -> v.Length, HEADER_SIZE + v.Length
      | None -> 0, HEADER_SIZE

    { MagicNumber = MAGIC_NUMBER
      Version = PROTOCOL_VERSION
      StatusCode = StatusCode.Success
      TotalPacketLength = totalLen
      ValueLength = valueLen
      ErrorMessageLength = 0
      Value = value
      ErrorMessage = None }

  /// Create not found response
  let CreateNotFoundResponse () =
    { MagicNumber = MAGIC_NUMBER
      Version = PROTOCOL_VERSION
      StatusCode = StatusCode.NotFound
      TotalPacketLength = HEADER_SIZE
      ValueLength = 0
      ErrorMessageLength = 0
      Value = None
      ErrorMessage = None }

  /// Create error response
  let CreateErrorResponse (errorMessage : string) =
    let msgBytes = Encoding.UTF8.GetBytes errorMessage
    let totalLen = HEADER_SIZE + msgBytes.Length

    { MagicNumber = MAGIC_NUMBER
      Version = PROTOCOL_VERSION
      StatusCode = StatusCode.Error
      TotalPacketLength = totalLen
      ValueLength = 0
      ErrorMessageLength = msgBytes.Length
      Value = None
      ErrorMessage = Some errorMessage }

  /// Validate packet size bounds to prevent oversized allocations.
  let IsValidPacketSize (totalLength : int32) =
    totalLength >= HEADER_SIZE
    && totalLength <= (10 * 1024 * 1024) // Maximum 10MB

  /// Serialize a request packet to bytes.
  let SerializeRequest (packet : RequestPacket) =
    use ms = new MemoryStream ()
    use writer = new BinaryWriter (ms)

    writer.Write packet.MagicNumber
    writer.Write packet.Version
    writer.Write (byte packet.CommandType)
    writer.Write packet.TotalPacketLength
    writer.Write packet.KeyLength
    writer.Write packet.ValueLength
    writer.Write packet.Key

    match packet.Value with
    | Some value -> writer.Write value
    | None -> ()

    ms.ToArray ()

  /// Protocol parse result
  type ParseResult<'T> =
    | ParseSuccess of 'T
    | ParseError of string
    | InsufficientData

  /// Deserialize a response packet from bytes.
  let DeserializeResponse (buffer : byte array) : ParseResult<ResponsePacket> =
    try
      if buffer.Length < HEADER_SIZE then
        InsufficientData
      else
        let mutable offset = 0

        let magicNumber = BitConverter.ToUInt32 (buffer, offset)
        offset <- offset + 4

        let version = buffer.[offset]
        offset <- offset + 1

        let statusCode =
          LanguagePrimitives.EnumOfValue<byte, StatusCode> (buffer.[offset])

        offset <- offset + 1

        let totalPacketLength = BitConverter.ToInt32 (buffer, offset)
        offset <- offset + 4

        let valueLength = BitConverter.ToInt32 (buffer, offset)
        offset <- offset + 4

        let errorMessageLength = BitConverter.ToInt32 (buffer, offset)
        offset <- offset + 4

        if buffer.Length < totalPacketLength then
          InsufficientData
        elif not (IsValidHeader magicNumber version) then
          ParseError "invalid header"
        elif valueLength < 0 || errorMessageLength < 0 then
          ParseError "invalid length field"
        elif
          totalPacketLength
          <> HEADER_SIZE + valueLength + errorMessageLength
        then
          ParseError "packet length mismatch"
        else
          let value =
            if valueLength > 0 then
              Some (Array.sub buffer offset valueLength)
            else
              None

          offset <- offset + valueLength

          let errorMessage =
            if errorMessageLength > 0 then
              let errorBytes = Array.sub buffer offset errorMessageLength
              Some (Encoding.UTF8.GetString errorBytes)
            else
              None

          let packet =
            { MagicNumber = magicNumber
              Version = version
              StatusCode = statusCode
              TotalPacketLength = totalPacketLength
              ValueLength = valueLength
              ErrorMessageLength = errorMessageLength
              Value = value
              ErrorMessage = errorMessage }

          ParseSuccess packet
    with ex ->
      ParseError (sprintf "error parsing response: %s" ex.Message)
