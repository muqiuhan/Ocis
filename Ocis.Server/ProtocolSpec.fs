module Ocis.Server.ProtocolSpec

/// Protocol magic number "OCIS"
[<Literal>]
let MAGIC_NUMBER = 0x5349434Fu

/// Protocol version
[<Literal>]
let PROTOCOL_VERSION = 1uy

/// Protocol header size (bytes)
[<Literal>]
let HEADER_SIZE = 18

/// Command type
type CommandType =
    | Set = 1
    | Get = 2
    | Delete = 3

/// Status code
type StatusCode =
    | Success = 0uy
    | NotFound = 1uy
    | Error = 2uy

/// Request packet
type RequestPacket =
    { MagicNumber: uint32
      Version: byte
      CommandType: CommandType
      TotalPacketLength: int32
      KeyLength: int32
      ValueLength: int32
      Key: byte array
      Value: byte array option }

/// Response packetnse packet
type ResponsePacket =
    { MagicNumber: uint32
      Version: byte
      StatusCode: StatusCode
      TotalPacketLength: int32
      ValueLength: int32
      ErrorMessageLength: int32
      Value: byte array option
      ErrorMessage: string option }
