module Ocis.Client.SDK.ProtocolSpec

// NOTE: This file should be kept in sync with Ocis.Server.ProtocolSpec
// It is duplicated here to allow Fable compilation independently from Server

[<Literal>]
let MAGIC_NUMBER = 0x5349434Fu

[<Literal>]
let PROTOCOL_VERSION = 1uy

[<Literal>]
let HEADER_SIZE = 18

type CommandType =
  | Set = 1
  | Get = 2
  | Delete = 3

type StatusCode =
  | Success = 0uy
  | NotFound = 1uy
  | Error = 2uy

type RequestPacket =
  { MagicNumber : uint32
    Version : byte
    CommandType : CommandType
    TotalPacketLength : int32
    KeyLength : int32
    ValueLength : int32
    Key : byte array
    Value : byte array option }

type ResponsePacket =
  { MagicNumber : uint32
    Version : byte
    StatusCode : StatusCode
    TotalPacketLength : int32
    ValueLength : int32
    ErrorMessageLength : int32
    Value : byte array option
    ErrorMessage : string option }
