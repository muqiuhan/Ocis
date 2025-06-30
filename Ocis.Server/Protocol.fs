module Ocis.Server.Protocol

open System
open System.IO
open System.Text
open Ocis.Server.ProtocolSpec

/// Protocol operations module
module Protocol =

    /// Validate magic number and version
    let inline IsValidHeader (magicNumber : uint32) (version : byte) = magicNumber = MAGIC_NUMBER && version = PROTOCOL_VERSION

    /// Parse request packet header from byte array
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
                    use ms = new MemoryStream (buffer)
                    use reader = new BinaryReader (ms)

                    // Skip header
                    ms.Seek (int64 HEADER_SIZE, SeekOrigin.Begin) |> ignore

                    // Read payload
                    let key = reader.ReadBytes (header.KeyLength)
                    let value =
                        if header.ValueLength > 0 then
                            Some (reader.ReadBytes (header.ValueLength))
                        else
                            None

                    Some { header with Key = key; Value = value }
                with _ ->
                    None
            else
                None
        | None -> None

    /// Serialize response packet to byte array
    let SerializeResponse (packet : ResponsePacket) =
        use ms = new MemoryStream ()
        use writer = new BinaryWriter (ms)

        // Write header
        writer.Write (packet.MagicNumber)
        writer.Write (packet.Version)
        writer.Write (byte packet.StatusCode)
        writer.Write (packet.TotalPacketLength)
        writer.Write (packet.ValueLength)
        writer.Write (packet.ErrorMessageLength)

        // Write payload data
        match packet.Value with
        | Some value -> writer.Write (value)
        | None -> ()

        match packet.ErrorMessage with
        | Some msg ->
            let msgBytes = Encoding.UTF8.GetBytes (msg)
            writer.Write (msgBytes)
        | None -> ()

        ms.ToArray ()

    /// Create success response
    let CreateSuccessResponse (value : byte array option) =
        let valueLen, totalLen =
            match value with
            | Some v -> v.Length, HEADER_SIZE + v.Length
            | None -> 0, HEADER_SIZE

        {
            MagicNumber = MAGIC_NUMBER
            Version = PROTOCOL_VERSION
            StatusCode = StatusCode.Success
            TotalPacketLength = totalLen
            ValueLength = valueLen
            ErrorMessageLength = 0
            Value = value
            ErrorMessage = None
        }

    /// Create not found response
    let CreateNotFoundResponse () = {
        MagicNumber = MAGIC_NUMBER
        Version = PROTOCOL_VERSION
        StatusCode = StatusCode.NotFound
        TotalPacketLength = HEADER_SIZE
        ValueLength = 0
        ErrorMessageLength = 0
        Value = None
        ErrorMessage = None
    }

    /// Create error response
    let CreateErrorResponse (errorMessage : string) =
        let msgBytes = Encoding.UTF8.GetBytes (errorMessage)
        let totalLen = HEADER_SIZE + msgBytes.Length

        {
            MagicNumber = MAGIC_NUMBER
            Version = PROTOCOL_VERSION
            StatusCode = StatusCode.Error
            TotalPacketLength = totalLen
            ValueLength = 0
            ErrorMessageLength = msgBytes.Length
            Value = None
            ErrorMessage = Some errorMessage
        }

    /// Validate packet size is reasonable (prevent malicious packets)
    let IsValidPacketSize (totalLength : int32) =
        totalLength >= HEADER_SIZE
        && totalLength <= (10 * 1024 * 1024) // Maximum 10MB
