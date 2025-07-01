module Ocis.Protocol

open System
open System.Text
open Ocis.Server.ProtocolSpec

/// verify header magic number and version
let inline IsValidHeader (magicNumber : uint32) (version : byte) = magicNumber = MAGIC_NUMBER && version = PROTOCOL_VERSION

/// create request packet
let CreateRequest (commandType : CommandType) (key : byte array) (value : byte array option) =
    let valueLen =
        match value with
        | Some v -> v.Length
        | None -> 0
    let totalLen = HEADER_SIZE + key.Length + valueLen

    {
        MagicNumber = MAGIC_NUMBER
        Version = PROTOCOL_VERSION
        CommandType = commandType
        TotalPacketLength = totalLen
        KeyLength = key.Length
        ValueLength = valueLen
        Key = key
        Value = value
    }

/// serialize request packet to bytes
let SerializeRequest (packet : RequestPacket) =
    let parts = ResizeArray<byte array> ()

    // add header fields
    parts.Add (BitConverter.GetBytes (packet.MagicNumber))
    parts.Add ([| packet.Version |])
    parts.Add ([| byte packet.CommandType |])
    parts.Add (BitConverter.GetBytes (packet.TotalPacketLength))
    parts.Add (BitConverter.GetBytes (packet.KeyLength))
    parts.Add (BitConverter.GetBytes (packet.ValueLength))

    // add key
    parts.Add (packet.Key)

    // add value (if exists)
    match packet.Value with
    | Some value -> parts.Add (value)
    | None -> ()

    Array.concat (parts.ToArray ())

/// protocol parse result
type ParseResult<'T> =
    | ParseSuccess of 'T
    | ParseError of string
    | InsufficientData

/// deserialize response from bytes
let DeserializeResponse (buffer : byte array) =
    try
        if buffer.Length < HEADER_SIZE then
            InsufficientData
        else
            let mutable offset = 0

            // parse header fields
            let magicNumber = BitConverter.ToUInt32 (buffer, offset)
            offset <- offset + 4

            let version = buffer.[offset]
            offset <- offset + 1

            let statusCode = LanguagePrimitives.EnumOfValue<byte, StatusCode> (buffer.[offset])
            offset <- offset + 1

            let totalPacketLength = BitConverter.ToInt32 (buffer, offset)
            offset <- offset + 4

            let valueLength = BitConverter.ToInt32 (buffer, offset)
            offset <- offset + 4

            let errorMessageLength = BitConverter.ToInt32 (buffer, offset)
            offset <- offset + 4

            // verify packet integrity
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
                // extract value (if exists)
                let value =
                    if valueLength > 0 then
                        Some (Array.sub buffer offset valueLength)
                    else
                        None
                offset <- offset + valueLength

                // extract error message (if exists)
                let errorMessage =
                    if errorMessageLength > 0 then
                        let errorBytes = Array.sub buffer offset errorMessageLength
                        Some (Encoding.UTF8.GetString (errorBytes))
                    else
                        None

                let packet = {
                    MagicNumber = magicNumber
                    Version = version
                    StatusCode = statusCode
                    TotalPacketLength = totalPacketLength
                    ValueLength = valueLength
                    ErrorMessageLength = errorMessageLength
                    Value = value
                    ErrorMessage = errorMessage
                }

                ParseSuccess packet
    with ex ->
        ParseError (sprintf "error parsing response: %s" ex.Message)

/// protocol helper module
module ProtocolHelper =

    /// convert string to bytes
    let stringToBytes (s : string) = Encoding.UTF8.GetBytes (s)

    /// convert bytes to string
    let bytesToString (bytes : byte array) = Encoding.UTF8.GetString (bytes)

    /// create SET request
    let createSetRequest (key : string) (value : string) = CreateRequest CommandType.Set (stringToBytes key) (Some (stringToBytes value))

    /// create GET request
    let createGetRequest (key : string) = CreateRequest CommandType.Get (stringToBytes key) None

    /// create DELETE request
    let createDeleteRequest (key : string) = CreateRequest CommandType.Delete (stringToBytes key) None
