module Ocis.Server.Tests.ProtocolTests

open System
open System.IO
open NUnit.Framework
open Ocis.Server.Protocol
open Ocis.Server.ProtocolSpec

[<TestFixture>]
type ProtocolTests () =

    [<Test>]
    member this.TestRequestPacketSerialization () =
        let key = System.Text.Encoding.UTF8.GetBytes ("test_key")
        let value = System.Text.Encoding.UTF8.GetBytes ("test_value")

        let request = {
            MagicNumber = MAGIC_NUMBER
            Version = PROTOCOL_VERSION
            CommandType = CommandType.Set
            TotalPacketLength = HEADER_SIZE + key.Length + value.Length
            KeyLength = key.Length
            ValueLength = value.Length
            Key = key
            Value = Some value
        }

        Assert.That (request.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (request.Version, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (request.CommandType, Is.EqualTo (CommandType.Set))
        Assert.That (request.Key, Is.EqualTo (key))
        Assert.That (request.Value, Is.EqualTo (Some value))

    [<Test>]
    member this.TestRequestPacketParsing () =
        let key = System.Text.Encoding.UTF8.GetBytes ("hello")
        let value = System.Text.Encoding.UTF8.GetBytes ("world")

        use ms = new MemoryStream ()
        use writer = new BinaryWriter (ms)

        writer.Write (MAGIC_NUMBER)
        writer.Write (PROTOCOL_VERSION)
        writer.Write (byte CommandType.Set)
        writer.Write (HEADER_SIZE + key.Length + value.Length)
        writer.Write (key.Length)
        writer.Write (value.Length)
        writer.Write (key)
        writer.Write (value)

        let requestBytes = ms.ToArray ()

        match Protocol.TryParseRequestPacket requestBytes with
        | Some parsedRequest ->
            Assert.That (parsedRequest.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
            Assert.That (parsedRequest.Version, Is.EqualTo (PROTOCOL_VERSION))
            Assert.That (parsedRequest.CommandType, Is.EqualTo (CommandType.Set))
            Assert.That (parsedRequest.Key, Is.EqualTo (key))
            Assert.That (parsedRequest.Value, Is.EqualTo (Some value))
        | None -> Assert.Fail ("Failed to parse valid request packet")

    [<Test>]
    member this.TestResponsePacketSerialization () =
        let value = System.Text.Encoding.UTF8.GetBytes ("response_data")
        let response = Protocol.CreateSuccessResponse (Some value)

        Assert.That (response.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (response.Version, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (response.StatusCode, Is.EqualTo (StatusCode.Success))
        Assert.That (response.Value, Is.EqualTo (Some value))

        let responseBytes = Protocol.SerializeResponse response
        Assert.That (responseBytes, Is.Not.Null)
        Assert.That (responseBytes.Length, Is.GreaterThanOrEqualTo (HEADER_SIZE))

    [<Test>]
    member this.TestNotFoundResponse () =
        let response = Protocol.CreateNotFoundResponse ()

        Assert.That (response.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (response.Version, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (response.StatusCode, Is.EqualTo (StatusCode.NotFound))
        Assert.That (response.Value, Is.EqualTo (None))
        Assert.That (response.TotalPacketLength, Is.EqualTo (HEADER_SIZE))

    [<Test>]
    member this.TestErrorResponse () =
        let errorMessage = "Test error message"
        let response = Protocol.CreateErrorResponse errorMessage

        Assert.That (response.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (response.Version, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (response.StatusCode, Is.EqualTo (StatusCode.Error))
        Assert.That (response.ErrorMessage, Is.EqualTo (Some errorMessage))
        Assert.That (response.Value, Is.EqualTo (None))

    [<Test>]
    member this.TestInvalidMagicNumber () =
        use ms = new MemoryStream ()
        use writer = new BinaryWriter (ms)

        writer.Write (0x12345678u)
        writer.Write (PROTOCOL_VERSION)
        writer.Write (byte CommandType.Get)
        writer.Write (HEADER_SIZE + 4)
        writer.Write (4)
        writer.Write (0)
        writer.Write ([| 1uy; 2uy; 3uy; 4uy |])

        let invalidBytes = ms.ToArray ()

        match Protocol.TryParseRequestPacket invalidBytes with
        | Some _ -> Assert.Fail ("Should not parse packet with invalid magic number")
        | None -> Assert.Pass ()

    [<Test>]
    member this.TestPacketSizeValidation () =
        Assert.That (Protocol.IsValidPacketSize HEADER_SIZE, Is.True)
        Assert.That (Protocol.IsValidPacketSize (HEADER_SIZE + 1000), Is.True)
        Assert.That (Protocol.IsValidPacketSize (HEADER_SIZE - 1), Is.False)
        Assert.That (Protocol.IsValidPacketSize (11 * 1024 * 1024), Is.False)

    [<Test>]
    member this.TestGetRequestPacket () =
        let key = System.Text.Encoding.UTF8.GetBytes ("get_key")

        let request = {
            MagicNumber = MAGIC_NUMBER
            Version = PROTOCOL_VERSION
            CommandType = CommandType.Get
            TotalPacketLength = HEADER_SIZE + key.Length
            KeyLength = key.Length
            ValueLength = 0
            Key = key
            Value = None
        }

        Assert.That (request.CommandType, Is.EqualTo (CommandType.Get))
        Assert.That (request.Value, Is.EqualTo (None))
        Assert.That (request.ValueLength, Is.EqualTo (0))

    [<Test>]
    member this.TestDeleteRequestPacket () =
        let key = System.Text.Encoding.UTF8.GetBytes ("delete_key")

        let request = {
            MagicNumber = MAGIC_NUMBER
            Version = PROTOCOL_VERSION
            CommandType = CommandType.Delete
            TotalPacketLength = HEADER_SIZE + key.Length
            KeyLength = key.Length
            ValueLength = 0
            Key = key
            Value = None
        }

        Assert.That (request.CommandType, Is.EqualTo (CommandType.Delete))
        Assert.That (request.Value, Is.EqualTo (None))
        Assert.That (request.ValueLength, Is.EqualTo (0))
