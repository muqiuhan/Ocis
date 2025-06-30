module Ocis.Server.Tests.TestClient

open System
open System.IO
open System.Net.Sockets
open System.Threading.Tasks
open Ocis.Server.Protocol

/// Client operation result
type ClientResult<'T> =
    | Success of 'T
    | NotFound
    | Error of string

/// Simple test client
type TestClient (host : string, port : int) =

    /// Create and connect TCP client
    member private this.CreateConnection () =
        let client = new TcpClient ()
        client.Connect (host, port)
        client.GetStream ()

    /// Serialize request packet
    member private this.SerializeRequest (request : RequestPacket) =
        use ms = new MemoryStream ()
        use writer = new BinaryWriter (ms)

        writer.Write (request.MagicNumber)
        writer.Write (request.Version)
        writer.Write (byte request.CommandType)
        writer.Write (request.TotalPacketLength)
        writer.Write (request.KeyLength)
        writer.Write (request.ValueLength)
        writer.Write (request.Key)

        match request.Value with
        | Some value -> writer.Write (value)
        | None -> ()

        ms.ToArray ()

    /// Read exact number of bytes
    member private this.ReadExact (stream : NetworkStream, count : int) =
        let buffer = Array.zeroCreate<byte> count
        let mutable totalRead = 0

        while totalRead < count do
            let bytesRead = stream.Read (buffer, totalRead, count - totalRead)
            if bytesRead = 0 then
                failwith "Connection closed unexpectedly"
            totalRead <- totalRead + bytesRead

        buffer

    /// Deserialize response packet
    member private this.DeserializeResponse (buffer : byte array) =
        use ms = new MemoryStream (buffer)
        use reader = new BinaryReader (ms)

        let magicNumber = reader.ReadUInt32 ()
        let version = reader.ReadByte ()
        let statusCode = LanguagePrimitives.EnumOfValue<byte, StatusCode> (reader.ReadByte ())
        let totalPacketLength = reader.ReadInt32 ()
        let valueLength = reader.ReadInt32 ()
        let errorMessageLength = reader.ReadInt32 ()

        let value =
            if valueLength > 0 then
                Some (reader.ReadBytes (valueLength))
            else
                None

        let errorMessage =
            if errorMessageLength > 0 then
                let errorBytes = reader.ReadBytes (errorMessageLength)
                Some (System.Text.Encoding.UTF8.GetString (errorBytes))
            else
                None

        {
            MagicNumber = magicNumber
            Version = version
            StatusCode = statusCode
            TotalPacketLength = totalPacketLength
            ValueLength = valueLength
            ErrorMessageLength = errorMessageLength
            Value = value
            ErrorMessage = errorMessage
        }

    /// Send request and receive response
    member private this.SendRequest (request : RequestPacket) =
        use stream = this.CreateConnection ()

        // Send request
        let requestBytes = this.SerializeRequest (request)
        stream.Write (requestBytes, 0, requestBytes.Length)
        stream.Flush ()

        // Read response header
        let headerBytes = this.ReadExact (stream, 18)

        // Parse header to get total length
        use headerMs = new MemoryStream (headerBytes)
        use headerReader = new BinaryReader (headerMs)
        headerReader.ReadUInt32 () |> ignore // magic
        headerReader.ReadByte () |> ignore // version
        headerReader.ReadByte () |> ignore // status
        let totalLength = headerReader.ReadInt32 ()

        // Read complete response
        let remainingBytes = totalLength - 18
        let fullResponse =
            if remainingBytes > 0 then
                let remaining = this.ReadExact (stream, remainingBytes)
                Array.concat [ headerBytes; remaining ]
            else
                headerBytes

        this.DeserializeResponse (fullResponse)

    /// SET operation
    member this.Set (key : byte array, value : byte array) =
        try
            let request = {
                MagicNumber = 0x5349434Fu
                Version = 1uy
                CommandType = CommandType.Set
                TotalPacketLength = 18 + key.Length + value.Length
                KeyLength = key.Length
                ValueLength = value.Length
                Key = key
                Value = Some value
            }

            let response = this.SendRequest (request)
            match response.StatusCode with
            | StatusCode.Success -> Success ()
            | StatusCode.Error -> Error (response.ErrorMessage |> Option.defaultValue "Unknown error")
            | _ -> Error ("Unexpected response status")

        with ex ->
            Error (ex.Message)

    /// GET operation
    member this.Get (key : byte array) =
        try
            let request = {
                MagicNumber = 0x5349434Fu
                Version = 1uy
                CommandType = CommandType.Get
                TotalPacketLength = 18 + key.Length
                KeyLength = key.Length
                ValueLength = 0
                Key = key
                Value = None
            }

            let response = this.SendRequest (request)
            match response.StatusCode with
            | StatusCode.Success ->
                match response.Value with
                | Some value -> Success (value)
                | None -> Error ("Success response missing value")
            | StatusCode.NotFound -> NotFound
            | StatusCode.Error -> Error (response.ErrorMessage |> Option.defaultValue "Unknown error")
            | _ -> Error ("Unexpected response status")

        with ex ->
            Error (ex.Message)

    /// DELETE operation
    member this.Delete (key : byte array) =
        try
            let request = {
                MagicNumber = 0x5349434Fu
                Version = 1uy
                CommandType = CommandType.Delete
                TotalPacketLength = 18 + key.Length
                KeyLength = key.Length
                ValueLength = 0
                Key = key
                Value = None
            }

            let response = this.SendRequest (request)
            match response.StatusCode with
            | StatusCode.Success -> Success ()
            | StatusCode.Error -> Error (response.ErrorMessage |> Option.defaultValue "Unknown error")
            | _ -> Error ("Unexpected response status")

        with ex ->
            Error (ex.Message)

/// Client helper functions
module TestClientHelper =

    /// Create test client
    let createClient (host : string) (port : int) = new TestClient (host, port)

    /// String to byte array
    let stringToBytes (s : string) = System.Text.Encoding.UTF8.GetBytes (s)

    /// Byte array to string
    let bytesToString (bytes : byte array) = System.Text.Encoding.UTF8.GetString (bytes)

    /// Test connection availability
    let testConnection (host : string) (port : int) =
        try
            use client = new TcpClient ()
            client.Connect (host, port)
            true
        with _ ->
            false
