namespace Ocis.Perf

open System
open System.IO
open System.Net.Sockets
open Ocis.Server.ProtocolSpec

type ServerProtocolClient(host: string, port: int) =
    let client = new TcpClient()
    do client.Connect(host, port)
    let stream = client.GetStream()

    let readExact (count: int) =
        let buffer = Array.zeroCreate<byte> count
        let mutable totalRead = 0

        while totalRead < count do
            let read = stream.Read(buffer, totalRead, count - totalRead)
            if read = 0 then
                raise (EndOfStreamException("Connection closed while reading response"))
            totalRead <- totalRead + read

        buffer

    let sendRequest (request: RequestPacket) : ResponsePacket =
        use reqMs = new MemoryStream()
        use reqWriter = new BinaryWriter(reqMs)

        reqWriter.Write request.MagicNumber
        reqWriter.Write request.Version
        reqWriter.Write(byte request.CommandType)
        reqWriter.Write request.TotalPacketLength
        reqWriter.Write request.KeyLength
        reqWriter.Write request.ValueLength
        reqWriter.Write request.Key
        match request.Value with
        | Some v -> reqWriter.Write v
        | None -> ()

        let bytes = reqMs.ToArray()
        stream.Write(bytes, 0, bytes.Length)
        stream.Flush()

        let header = readExact HEADER_SIZE
        use headerMs = new MemoryStream(header)
        use headerReader = new BinaryReader(headerMs)
        let magic = headerReader.ReadUInt32()
        let version = headerReader.ReadByte()
        let status = LanguagePrimitives.EnumOfValue<byte, StatusCode>(headerReader.ReadByte())
        let totalLength = headerReader.ReadInt32()
        let valueLength = headerReader.ReadInt32()
        let errLength = headerReader.ReadInt32()

        let remain = totalLength - HEADER_SIZE

        let body =
            if remain > 0 then
                readExact remain
            else
                [||]

        use bodyMs = new MemoryStream(body)
        use bodyReader = new BinaryReader(bodyMs)

        let value = if valueLength > 0 then Some(bodyReader.ReadBytes valueLength) else None

        let errorMessage =
            if errLength > 0 then
                Some(System.Text.Encoding.UTF8.GetString(bodyReader.ReadBytes errLength))
            else
                None

        { MagicNumber = magic
          Version = version
          StatusCode = status
          TotalPacketLength = totalLength
          ValueLength = valueLength
          ErrorMessageLength = errLength
          Value = value
          ErrorMessage = errorMessage }

    member _.Set(key: byte array, value: byte array) =
        let request =
            { MagicNumber = MAGIC_NUMBER
              Version = PROTOCOL_VERSION
              CommandType = CommandType.Set
              TotalPacketLength = HEADER_SIZE + key.Length + value.Length
              KeyLength = key.Length
              ValueLength = value.Length
              Key = key
              Value = Some value }

        let response = sendRequest request
        response.StatusCode = StatusCode.Success

    member _.Get(key: byte array) =
        let request =
            { MagicNumber = MAGIC_NUMBER
              Version = PROTOCOL_VERSION
              CommandType = CommandType.Get
              TotalPacketLength = HEADER_SIZE + key.Length
              KeyLength = key.Length
              ValueLength = 0
              Key = key
              Value = None }

        let response = sendRequest request
        response.StatusCode = StatusCode.Success || response.StatusCode = StatusCode.NotFound

    member _.Delete(key: byte array) =
        let request =
            { MagicNumber = MAGIC_NUMBER
              Version = PROTOCOL_VERSION
              CommandType = CommandType.Delete
              TotalPacketLength = HEADER_SIZE + key.Length
              KeyLength = key.Length
              ValueLength = 0
              Key = key
              Value = None }

        let response = sendRequest request
        response.StatusCode = StatusCode.Success

    interface IDisposable with
        member _.Dispose() =
            stream.Dispose()
            client.Dispose()
