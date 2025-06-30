module Ocis.Protocol

open System
open System.Text
open Ocis.Server.ProtocolSpec

/// Validate magic number and version
let inline IsValidHeader (magicNumber : uint32) (version : byte) = magicNumber = MAGIC_NUMBER && version = PROTOCOL_VERSION

/// Parse request packet header from byte array
let TryParseRequestHeader (buffer : byte array) =
    if buffer.Length < HEADER_SIZE then
        None
    else
        try
            let mutable offset = 0

            let magicNumber = BitConverter.ToUInt32 (buffer, offset)
            offset <- offset + 4

            let version = buffer.[offset]
            offset <- offset + 1

            let commandType = enum<CommandType> (int32 (buffer.[offset]))
            offset <- offset + 1

            let totalPacketLength = BitConverter.ToInt32 (buffer, offset)
            offset <- offset + 4

            let keyLength = BitConverter.ToInt32 (buffer, offset)
            offset <- offset + 4

            let valueLength = BitConverter.ToInt32 (buffer, offset)
            // offset <- offset + 4 // Not strictly necessary for the last read as 'offset' is not used afterwards

            if IsValidHeader magicNumber version then
                Some {
                    MagicNumber = magicNumber
                    Version = version
                    CommandType = commandType
                    TotalPacketLength = totalPacketLength
                    KeyLength = keyLength
                    ValueLength = valueLength
                    Key = [||]
                    Value = None
                }
            else
                None
        with _ ->
            None

/// Parse complete request packet from byte array
let TryParseRequestPacket (buffer : byte array) =
    match TryParseRequestHeader buffer with
    | Some header ->
        if buffer.Length >= header.TotalPacketLength then
            try
                // Skip header
                let mutable offset = HEADER_SIZE

                // Read payload
                let key = Array.sub buffer offset header.KeyLength
                offset <- offset + header.KeyLength

                let value =
                    if header.ValueLength > 0 then
                        Some (Array.sub buffer offset header.ValueLength)
                    else
                        None

                Some { header with Key = key; Value = value }
            with _ ->
                None
        else
            None
    | None -> None

/// Create request packet
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

/// Serialize request packet to byte array
let SerializeRequest (packet : RequestPacket) =
    let parts = ResizeArray<byte array> ()

    parts.Add (BitConverter.GetBytes (packet.MagicNumber))
    parts.Add ([| packet.Version |])
    parts.Add ([| byte packet.CommandType |])
    parts.Add (BitConverter.GetBytes (packet.TotalPacketLength))
    parts.Add (BitConverter.GetBytes (packet.KeyLength))
    parts.Add (BitConverter.GetBytes (packet.ValueLength))
    parts.Add (packet.Key)

    match packet.Value with
    | Some value -> parts.Add (value)
    | None -> ()

    Array.concat (parts.ToArray ())
