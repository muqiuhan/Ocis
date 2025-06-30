module Ocis.Protocol.Tests

open System
open System.Text
open NUnit.Framework
open Ocis.Server.ProtocolSpec
open Ocis.Protocol

[<TestFixture>]
type LibraryTests () =

    [<Test>]
    member this.TestIsValidHeader () =
        Assert.That (Ocis.Protocol.IsValidHeader MAGIC_NUMBER PROTOCOL_VERSION, Is.True)
        Assert.That (Ocis.Protocol.IsValidHeader 0x12345678u PROTOCOL_VERSION, Is.False)
        Assert.That (Ocis.Protocol.IsValidHeader MAGIC_NUMBER 0uy, Is.False)

    [<Test>]
    member this.TestRequestPacketParsing () =
        let key = Encoding.UTF8.GetBytes ("hello")
        let value = Encoding.UTF8.GetBytes ("world")

        // Manually construct the byte array for the request packet
        let magicNumberBytes = BitConverter.GetBytes (MAGIC_NUMBER)
        let versionByte = [| PROTOCOL_VERSION |]
        let commandTypeByte = [| byte CommandType.Set |]
        let totalPacketLengthBytes = BitConverter.GetBytes (HEADER_SIZE + key.Length + value.Length)
        let keyLengthBytes = BitConverter.GetBytes (key.Length)
        let valueLengthBytes = BitConverter.GetBytes (value.Length)

        let headerBytes =
            Array.concat [|
                magicNumberBytes
                versionByte
                commandTypeByte
                totalPacketLengthBytes
                keyLengthBytes
                valueLengthBytes
            |]

        let requestBytes = Array.concat [| headerBytes; key; value |]

        match Ocis.Protocol.TryParseRequestPacket requestBytes with
        | Some parsedRequest ->
            Assert.That (parsedRequest.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
            Assert.That (parsedRequest.Version, Is.EqualTo (PROTOCOL_VERSION))
            Assert.That (parsedRequest.CommandType, Is.EqualTo (CommandType.Set))
            Assert.That (parsedRequest.Key, Is.EqualTo (key))
            Assert.That (parsedRequest.Value, Is.EqualTo (Some value))
        | None -> Assert.Fail ("Failed to parse valid request packet")


    [<Test>]
    member this.TestInvalidHeader () =
        let invalidMagicNumber = 0x12345678u
        let version = PROTOCOL_VERSION
        let commandType = CommandType.Get
        let totalPacketLength = HEADER_SIZE
        let keyLength = 0
        let valueLength = 0

        let magicNumberBytes = BitConverter.GetBytes (invalidMagicNumber)
        let versionByte = [| version |]
        let commandTypeByte = [| byte commandType |]
        let totalPacketLengthBytes = BitConverter.GetBytes (totalPacketLength)
        let keyLengthBytes = BitConverter.GetBytes (keyLength)
        let valueLengthBytes = BitConverter.GetBytes (valueLength)

        let headerBytes =
            Array.concat [|
                magicNumberBytes
                versionByte
                commandTypeByte
                totalPacketLengthBytes
                keyLengthBytes
                valueLengthBytes
            |]

        match Ocis.Protocol.TryParseRequestPacket headerBytes with
        | Some _ -> Assert.Fail ("Should not parse packet with invalid magic number")
        | None -> Assert.Pass ()

    [<Test>]
    member this.TestCreateRequest () =
        let key = Encoding.UTF8.GetBytes ("test_key")
        let value = Encoding.UTF8.GetBytes ("test_value")

        // Test with value
        let requestWithValue = Ocis.Protocol.CreateRequest CommandType.Set key (Some value)
        Assert.That (requestWithValue.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (requestWithValue.Version, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (requestWithValue.CommandType, Is.EqualTo (CommandType.Set))
        Assert.That (requestWithValue.Key, Is.EqualTo (key))
        Assert.That (requestWithValue.Value, Is.EqualTo (Some value))
        Assert.That (requestWithValue.KeyLength, Is.EqualTo (key.Length))
        Assert.That (requestWithValue.ValueLength, Is.EqualTo (value.Length))
        Assert.That (requestWithValue.TotalPacketLength, Is.EqualTo (HEADER_SIZE + key.Length + value.Length))

        // Test without value
        let requestNoValue = Ocis.Protocol.CreateRequest CommandType.Get key None
        Assert.That (requestNoValue.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (requestNoValue.Version, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (requestNoValue.CommandType, Is.EqualTo (CommandType.Get))
        Assert.That (requestNoValue.Key, Is.EqualTo (key))
        Assert.That (requestNoValue.Value, Is.EqualTo (None))
        Assert.That (requestNoValue.KeyLength, Is.EqualTo (key.Length))
        Assert.That (requestNoValue.ValueLength, Is.EqualTo (0))
        Assert.That (requestNoValue.TotalPacketLength, Is.EqualTo (HEADER_SIZE + key.Length))

    [<Test>]
    member this.TestSerializeRequest () =
        let key = Encoding.UTF8.GetBytes ("serialize_key")
        let value = Encoding.UTF8.GetBytes ("serialize_value")
        let request = Ocis.Protocol.CreateRequest CommandType.Set key (Some value)
        let serializedBytes = Ocis.Protocol.SerializeRequest request

        Assert.That (serializedBytes, Is.Not.Null)
        Assert.That (serializedBytes.Length, Is.EqualTo (request.TotalPacketLength))

        // Verify basic header components after serialization
        let parsedMagicNumber = BitConverter.ToUInt32 (serializedBytes, 0)
        let parsedVersion = serializedBytes.[4]
        let parsedCommandType = enum<CommandType> (int serializedBytes.[5])
        let parsedTotalPacketLength = BitConverter.ToInt32 (serializedBytes, 6)
        let parsedKeyLength = BitConverter.ToInt32 (serializedBytes, 10)
        let parsedValueLength = BitConverter.ToInt32 (serializedBytes, 14)

        Assert.That (parsedMagicNumber, Is.EqualTo (MAGIC_NUMBER))
        Assert.That (parsedVersion, Is.EqualTo (PROTOCOL_VERSION))
        Assert.That (parsedCommandType, Is.EqualTo (CommandType.Set))
        Assert.That (parsedTotalPacketLength, Is.EqualTo (request.TotalPacketLength))
        Assert.That (parsedKeyLength, Is.EqualTo (request.KeyLength))
        Assert.That (parsedValueLength, Is.EqualTo (request.ValueLength))

        // Use TryParseRequestPacket for full validation
        match Ocis.Protocol.TryParseRequestPacket serializedBytes with
        | Some parsedRequest ->
            Assert.That (parsedRequest.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
            Assert.That (parsedRequest.Version, Is.EqualTo (PROTOCOL_VERSION))
            Assert.That (parsedRequest.CommandType, Is.EqualTo (CommandType.Set))
            Assert.That (parsedRequest.Key, Is.EqualTo (key))
            Assert.That (parsedRequest.Value, Is.EqualTo (Some value))
        | None -> Assert.Fail ("Failed to parse serialized request packet")
