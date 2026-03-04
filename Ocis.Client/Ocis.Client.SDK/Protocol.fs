module Ocis.Client.SDK.Protocol

open System
open Ocis.Server.ProtocolSpec
open Ocis.Client.SDK.Binary

let inline IsValidHeader (magicNumber : uint32) (version : byte) =
  magicNumber = MAGIC_NUMBER
  && version = PROTOCOL_VERSION

let TryParseRequestHeader (buffer : byte[]) =
  if buffer.Length < HEADER_SIZE then
    None
  else
    try
      let magicNumber = readUInt32LittleEndian buffer 0
      let version = readByte buffer 4
      let commandType = unbox<CommandType> (int (readByte buffer 5))
      let totalPacketLength = readInt32LittleEndian buffer 6
      let keyLength = readInt32LittleEndian buffer 10
      let valueLength = readInt32LittleEndian buffer 14

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

let TryParseRequestPacket (buffer : byte[]) =
  match TryParseRequestHeader buffer with
  | Some header ->
    if buffer.Length >= header.TotalPacketLength then
      try
        let keyOffset = HEADER_SIZE
        let key = Array.sub buffer keyOffset header.KeyLength
        let valueOffset = keyOffset + header.KeyLength

        let value =
          if header.ValueLength > 0 then
            Some (Array.sub buffer valueOffset header.ValueLength)
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

let SerializeRequest (packet : RequestPacket) : byte[] =
  let keyLen = packet.Key.Length

  let valueLen =
    packet.Value
    |> Option.map (fun v -> v.Length)
    |> Option.defaultValue 0

  let totalLen = HEADER_SIZE + keyLen + valueLen

  let buffer = Array.zeroCreate<byte> totalLen
  let mutable offset = 0

  writeUInt32LittleEndian packet.MagicNumber buffer offset
  offset <- offset + 4

  writeByte packet.Version buffer offset
  offset <- offset + 1

  writeByte (byte packet.CommandType) buffer offset
  offset <- offset + 1

  writeInt32LittleEndian packet.TotalPacketLength buffer offset
  offset <- offset + 4

  writeInt32LittleEndian packet.KeyLength buffer offset
  offset <- offset + 4

  writeInt32LittleEndian packet.ValueLength buffer offset
  offset <- offset + 4

  if keyLen > 0 then
    Array.blit packet.Key 0 buffer offset keyLen
    offset <- offset + keyLen

  match packet.Value with
  | Some value -> Array.blit value 0 buffer offset value.Length
  | None -> ()

  buffer

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

let CreateNotFoundResponse () =
  { MagicNumber = MAGIC_NUMBER
    Version = PROTOCOL_VERSION
    StatusCode = StatusCode.NotFound
    TotalPacketLength = HEADER_SIZE
    ValueLength = 0
    ErrorMessageLength = 0
    Value = None
    ErrorMessage = None }

let CreateErrorResponse (errorMessage : string) =
  let msgBytes = System.Text.Encoding.UTF8.GetBytes errorMessage
  let totalLen = HEADER_SIZE + msgBytes.Length

  { MagicNumber = MAGIC_NUMBER
    Version = PROTOCOL_VERSION
    StatusCode = StatusCode.Error
    TotalPacketLength = totalLen
    ValueLength = 0
    ErrorMessageLength = msgBytes.Length
    Value = None
    ErrorMessage = Some errorMessage }

let IsValidPacketSize (totalLength : int32) =
  totalLength >= HEADER_SIZE
  && totalLength <= (10 * 1024 * 1024)

let SerializeResponse (packet : ResponsePacket) : byte[] =
  let valueLen =
    packet.Value
    |> Option.map (fun v -> v.Length)
    |> Option.defaultValue 0

  let errorLen =
    packet.ErrorMessage
    |> Option.map (fun m -> System.Text.Encoding.UTF8.GetBytes(m).Length)
    |> Option.defaultValue 0

  let totalLen = HEADER_SIZE + valueLen + errorLen

  let buffer = Array.zeroCreate<byte> totalLen
  let mutable offset = 0

  writeUInt32LittleEndian packet.MagicNumber buffer offset
  offset <- offset + 4

  writeByte packet.Version buffer offset
  offset <- offset + 1

  writeByte (byte packet.StatusCode) buffer offset
  offset <- offset + 1

  writeInt32LittleEndian packet.TotalPacketLength buffer offset
  offset <- offset + 4

  writeInt32LittleEndian packet.ValueLength buffer offset
  offset <- offset + 4

  writeInt32LittleEndian packet.ErrorMessageLength buffer offset
  offset <- offset + 4

  match packet.Value with
  | Some value ->
    Array.blit value 0 buffer offset value.Length
    offset <- offset + value.Length
  | None -> ()

  match packet.ErrorMessage with
  | Some msg ->
    let msgBytes = System.Text.Encoding.UTF8.GetBytes msg
    Array.blit msgBytes 0 buffer offset msgBytes.Length
  | None -> ()

  buffer

type ParseResult<'T> =
  | ParseSuccess of 'T
  | ParseError of string
  | InsufficientData

let DeserializeResponse (buffer : byte[]) : ParseResult<ResponsePacket> =
  try
    if buffer.Length < HEADER_SIZE then
      InsufficientData
    else
      let mutable offset = 0

      let magicNumber = readUInt32LittleEndian buffer offset
      offset <- offset + 4

      let version = readByte buffer offset
      offset <- offset + 1

      let statusCodeByte = readByte buffer offset

      let statusCode : StatusCode =
        LanguagePrimitives.EnumOfValue statusCodeByte

      offset <- offset + 1

      let totalPacketLength = readInt32LittleEndian buffer offset
      offset <- offset + 4

      let valueLength = readInt32LittleEndian buffer offset
      offset <- offset + 4

      let errorMessageLength = readInt32LittleEndian buffer offset
      offset <- offset + 4

      if buffer.Length < totalPacketLength then
        InsufficientData
      elif not (IsValidHeader magicNumber version) then
        ParseError "Invalid header"
      elif valueLength < 0 || errorMessageLength < 0 then
        ParseError "Invalid length field"
      elif
        totalPacketLength
        <> HEADER_SIZE + valueLength + errorMessageLength
      then
        ParseError "Packet length mismatch"
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
            Some (System.Text.Encoding.UTF8.GetString errorBytes)
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
    ParseError (sprintf "Error parsing response: %s" ex.Message)
