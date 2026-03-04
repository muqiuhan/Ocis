module Ocis.Client.SDK.Request

open System
open System.Text
open Ocis.Server.ProtocolSpec
open Ocis.Client.SDK.Protocol

let private createPacket
  (commandType : CommandType)
  (key : string)
  (value : byte array option)
  : RequestPacket
  =
  let keyBytes = Encoding.UTF8.GetBytes key
  let keyLen = keyBytes.Length

  let valueLen =
    value
    |> Option.map (fun v -> v.Length)
    |> Option.defaultValue 0

  let totalLen = HEADER_SIZE + keyLen + valueLen

  { MagicNumber = MAGIC_NUMBER
    Version = PROTOCOL_VERSION
    CommandType = commandType
    TotalPacketLength = totalLen
    KeyLength = keyLen
    ValueLength = valueLen
    Key = keyBytes
    Value = value }

let createSetRequest (key : string) (value : byte array) : byte[] =
  let packet = createPacket CommandType.Set key (Some value)
  SerializeRequest packet

let createGetRequest (key : string) : byte[] =
  let packet = createPacket CommandType.Get key None
  SerializeRequest packet

let createDeleteRequest (key : string) : byte[] =
  let packet = createPacket CommandType.Delete key None
  SerializeRequest packet
