module Ocis.Protocol.Tests

open System
open System.Text
open NUnit.Framework
open Ocis.Protocol
open Ocis.Server.ProtocolSpec

[<TestFixture>]
type LibraryTests () =

  [<Test>]
  member this.TestIsValidHeader () =
    // test valid header
    Assert.That (
      Ocis.Protocol.IsValidHeader MAGIC_NUMBER PROTOCOL_VERSION,
      Is.True
    )

    // test invalid magic number
    Assert.That (
      Ocis.Protocol.IsValidHeader 0x12345678u PROTOCOL_VERSION,
      Is.False
    )

    // test invalid version
    Assert.That (Ocis.Protocol.IsValidHeader MAGIC_NUMBER 0uy, Is.False)

  [<Test>]
  member this.TestCreateRequest () =
    let key = Encoding.UTF8.GetBytes ("test_key")
    let value = Encoding.UTF8.GetBytes ("test_value")

    // test SET request with value
    let setRequest =
      Ocis.Protocol.CreateRequest CommandType.Set key (Some value)

    Assert.That (setRequest.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (setRequest.Version, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (setRequest.CommandType, Is.EqualTo (CommandType.Set))
    Assert.That (setRequest.Key, Is.EqualTo (key))
    Assert.That (setRequest.Value, Is.EqualTo (Some value))
    Assert.That (setRequest.KeyLength, Is.EqualTo (key.Length))
    Assert.That (setRequest.ValueLength, Is.EqualTo (value.Length))

    Assert.That (
      setRequest.TotalPacketLength,
      Is.EqualTo (HEADER_SIZE + key.Length + value.Length)
    )

    // test GET request without value
    let getRequest = Ocis.Protocol.CreateRequest CommandType.Get key None
    Assert.That (getRequest.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (getRequest.Version, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (getRequest.CommandType, Is.EqualTo (CommandType.Get))
    Assert.That (getRequest.Key, Is.EqualTo (key))
    Assert.That (getRequest.Value, Is.EqualTo (None))
    Assert.That (getRequest.KeyLength, Is.EqualTo (key.Length))
    Assert.That (getRequest.ValueLength, Is.EqualTo (0))

    Assert.That (
      getRequest.TotalPacketLength,
      Is.EqualTo (HEADER_SIZE + key.Length)
    )

    // test DELETE request without value
    let deleteRequest = Ocis.Protocol.CreateRequest CommandType.Delete key None
    Assert.That (deleteRequest.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (deleteRequest.Version, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (deleteRequest.CommandType, Is.EqualTo (CommandType.Delete))
    Assert.That (deleteRequest.Key, Is.EqualTo (key))
    Assert.That (deleteRequest.Value, Is.EqualTo (None))
    Assert.That (deleteRequest.KeyLength, Is.EqualTo (key.Length))
    Assert.That (deleteRequest.ValueLength, Is.EqualTo (0))

    Assert.That (
      deleteRequest.TotalPacketLength,
      Is.EqualTo (HEADER_SIZE + key.Length)
    )

  [<Test>]
  member this.TestSerializeRequest () =
    let key = Encoding.UTF8.GetBytes ("serialize_key")
    let value = Encoding.UTF8.GetBytes ("serialize_value")

    // test serializing SET request with value
    let setRequest =
      Ocis.Protocol.CreateRequest CommandType.Set key (Some value)

    let serializedBytes = Ocis.Protocol.SerializeRequest setRequest

    Assert.That (serializedBytes, Is.Not.Null)

    Assert.That (
      serializedBytes.Length,
      Is.EqualTo (setRequest.TotalPacketLength)
    )

    // verify header components after serialization
    let parsedMagicNumber = BitConverter.ToUInt32 (serializedBytes, 0)
    let parsedVersion = serializedBytes.[4]
    let parsedCommandType = enum<CommandType> (int serializedBytes.[5])
    let parsedTotalPacketLength = BitConverter.ToInt32 (serializedBytes, 6)
    let parsedKeyLength = BitConverter.ToInt32 (serializedBytes, 10)
    let parsedValueLength = BitConverter.ToInt32 (serializedBytes, 14)

    Assert.That (parsedMagicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (parsedVersion, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (parsedCommandType, Is.EqualTo (CommandType.Set))

    Assert.That (
      parsedTotalPacketLength,
      Is.EqualTo (setRequest.TotalPacketLength)
    )

    Assert.That (parsedKeyLength, Is.EqualTo (setRequest.KeyLength))
    Assert.That (parsedValueLength, Is.EqualTo (setRequest.ValueLength))

    // verify key and value are correctly positioned
    let keyStart = HEADER_SIZE
    let valueStart = keyStart + key.Length
    let extractedKey = Array.sub serializedBytes keyStart key.Length
    let extractedValue = Array.sub serializedBytes valueStart value.Length

    Assert.That (extractedKey, Is.EqualTo (key))
    Assert.That (extractedValue, Is.EqualTo (value))

    // test serializing GET request without value
    let getRequest = Ocis.Protocol.CreateRequest CommandType.Get key None
    let serializedGetBytes = Ocis.Protocol.SerializeRequest getRequest

    Assert.That (serializedGetBytes, Is.Not.Null)

    Assert.That (
      serializedGetBytes.Length,
      Is.EqualTo (getRequest.TotalPacketLength)
    )

    Assert.That (
      serializedGetBytes.Length,
      Is.EqualTo (HEADER_SIZE + key.Length)
    )

  [<Test>]
  member this.TestDeserializeResponse () =
    // test successful response with value
    let responseValue = Encoding.UTF8.GetBytes ("response_value")
    let valueLength = responseValue.Length
    let totalLength = HEADER_SIZE + valueLength

    let responseBytes =
      Array.concat
        [| BitConverter.GetBytes (MAGIC_NUMBER)
           [| PROTOCOL_VERSION |]
           [| byte StatusCode.Success |]
           BitConverter.GetBytes (totalLength)
           BitConverter.GetBytes (valueLength)
           BitConverter.GetBytes (0) // error message length
           responseValue |]

    match Ocis.Protocol.DeserializeResponse responseBytes with
    | ParseSuccess response ->
      Assert.That (response.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
      Assert.That (response.Version, Is.EqualTo (PROTOCOL_VERSION))
      Assert.That (response.StatusCode, Is.EqualTo (StatusCode.Success))
      Assert.That (response.TotalPacketLength, Is.EqualTo (totalLength))
      Assert.That (response.ValueLength, Is.EqualTo (valueLength))
      Assert.That (response.ErrorMessageLength, Is.EqualTo (0))
      Assert.That (response.Value, Is.EqualTo (Some responseValue))
      Assert.That (response.ErrorMessage, Is.EqualTo (None))
    | ParseError msg ->
      Assert.Fail (sprintf "Failed to parse valid response: %s" msg)
    | InsufficientData -> Assert.Fail ("Insufficient data for valid response")

    // test error response with error message
    let errorMessage = "test error message"
    let errorBytes = Encoding.UTF8.GetBytes (errorMessage)
    let errorLength = errorBytes.Length
    let errorTotalLength = HEADER_SIZE + errorLength

    let errorResponseBytes =
      Array.concat
        [| BitConverter.GetBytes (MAGIC_NUMBER)
           [| PROTOCOL_VERSION |]
           [| byte StatusCode.Error |]
           BitConverter.GetBytes (errorTotalLength)
           BitConverter.GetBytes (0) // value length
           BitConverter.GetBytes (errorLength)
           errorBytes |]

    match Ocis.Protocol.DeserializeResponse errorResponseBytes with
    | ParseSuccess response ->
      Assert.That (response.MagicNumber, Is.EqualTo (MAGIC_NUMBER))
      Assert.That (response.Version, Is.EqualTo (PROTOCOL_VERSION))
      Assert.That (response.StatusCode, Is.EqualTo (StatusCode.Error))
      Assert.That (response.TotalPacketLength, Is.EqualTo (errorTotalLength))
      Assert.That (response.ValueLength, Is.EqualTo (0))
      Assert.That (response.ErrorMessageLength, Is.EqualTo (errorLength))
      Assert.That (response.Value, Is.EqualTo (None))
      Assert.That (response.ErrorMessage, Is.EqualTo (Some errorMessage))
    | ParseError msg ->
      Assert.Fail (sprintf "Failed to parse valid error response: %s" msg)
    | InsufficientData ->
      Assert.Fail ("Insufficient data for valid error response")

    // test not found response
    let notFoundBytes =
      Array.concat
        [| BitConverter.GetBytes (MAGIC_NUMBER)
           [| PROTOCOL_VERSION |]
           [| byte StatusCode.NotFound |]
           BitConverter.GetBytes (HEADER_SIZE)
           BitConverter.GetBytes (0) // value length
           BitConverter.GetBytes (0) |] // error message length

    match Ocis.Protocol.DeserializeResponse notFoundBytes with
    | ParseSuccess response ->
      Assert.That (response.StatusCode, Is.EqualTo (StatusCode.NotFound))
      Assert.That (response.Value, Is.EqualTo (None))
      Assert.That (response.ErrorMessage, Is.EqualTo (None))
    | ParseError msg ->
      Assert.Fail (sprintf "Failed to parse valid not found response: %s" msg)
    | InsufficientData ->
      Assert.Fail ("Insufficient data for valid not found response")

  [<Test>]
  member this.TestDeserializeResponseErrors () =
    // test insufficient data
    let shortBuffer = Array.zeroCreate<byte> (HEADER_SIZE - 1)

    match Ocis.Protocol.DeserializeResponse shortBuffer with
    | InsufficientData -> Assert.Pass ()
    | _ -> Assert.Fail ("Should return InsufficientData for short buffer")

    // test invalid magic number
    let invalidMagicBytes =
      Array.concat
        [| BitConverter.GetBytes (0x12345678u) // invalid magic
           [| PROTOCOL_VERSION |]
           [| byte StatusCode.Success |]
           BitConverter.GetBytes (HEADER_SIZE)
           BitConverter.GetBytes (0)
           BitConverter.GetBytes (0) |]

    match Ocis.Protocol.DeserializeResponse invalidMagicBytes with
    | ParseError msg -> Assert.That (msg, Is.EqualTo ("invalid header"))
    | _ -> Assert.Fail ("Should return ParseError for invalid magic number")

    // test invalid length field
    let invalidLengthBytes =
      Array.concat
        [| BitConverter.GetBytes (MAGIC_NUMBER)
           [| PROTOCOL_VERSION |]
           [| byte StatusCode.Success |]
           BitConverter.GetBytes (HEADER_SIZE)
           BitConverter.GetBytes (-1) // invalid value length
           BitConverter.GetBytes (0) |]

    match Ocis.Protocol.DeserializeResponse invalidLengthBytes with
    | ParseError msg -> Assert.That (msg, Is.EqualTo ("invalid length field"))
    | _ -> Assert.Fail ("Should return ParseError for invalid length field")

  [<Test>]
  member this.TestProtocolHelper () =
    // test string/bytes conversion
    let testString = "hello world"
    let testBytes = ProtocolHelper.stringToBytes testString
    let convertedBack = ProtocolHelper.bytesToString testBytes
    Assert.That (convertedBack, Is.EqualTo (testString))

    // test create SET request
    let setRequest = ProtocolHelper.createSetRequest "key1" "value1"
    Assert.That (setRequest.CommandType, Is.EqualTo (CommandType.Set))

    Assert.That (
      ProtocolHelper.bytesToString setRequest.Key,
      Is.EqualTo ("key1")
    )

    Assert.That (setRequest.Value.IsSome, Is.True)

    Assert.That (
      ProtocolHelper.bytesToString setRequest.Value.Value,
      Is.EqualTo ("value1")
    )

    // test create GET request
    let getRequest = ProtocolHelper.createGetRequest "key2"
    Assert.That (getRequest.CommandType, Is.EqualTo (CommandType.Get))

    Assert.That (
      ProtocolHelper.bytesToString getRequest.Key,
      Is.EqualTo ("key2")
    )

    Assert.That (getRequest.Value, Is.EqualTo (None))

    // test create DELETE request
    let deleteRequest = ProtocolHelper.createDeleteRequest "key3"
    Assert.That (deleteRequest.CommandType, Is.EqualTo (CommandType.Delete))

    Assert.That (
      ProtocolHelper.bytesToString deleteRequest.Key,
      Is.EqualTo ("key3")
    )

    Assert.That (deleteRequest.Value, Is.EqualTo (None))

  [<Test>]
  member this.TestRoundTripSerialization () =
    // test that we can create a request, serialize it, and the serialized data is valid
    let originalKey = "round_trip_key"
    let originalValue = "round_trip_value"

    let request = ProtocolHelper.createSetRequest originalKey originalValue
    let serializedBytes = Ocis.Protocol.SerializeRequest request

    // manually parse the serialized bytes to verify correctness
    Assert.That (serializedBytes.Length, Is.EqualTo (request.TotalPacketLength))

    let parsedMagic = BitConverter.ToUInt32 (serializedBytes, 0)
    let parsedVersion = serializedBytes.[4]
    let parsedCommand = enum<CommandType> (int serializedBytes.[5])
    let parsedTotal = BitConverter.ToInt32 (serializedBytes, 6)
    let parsedKeyLen = BitConverter.ToInt32 (serializedBytes, 10)
    let parsedValueLen = BitConverter.ToInt32 (serializedBytes, 14)

    Assert.That (parsedMagic, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (parsedVersion, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (parsedCommand, Is.EqualTo (CommandType.Set))
    Assert.That (parsedTotal, Is.EqualTo (request.TotalPacketLength))
    Assert.That (parsedKeyLen, Is.EqualTo (request.KeyLength))
    Assert.That (parsedValueLen, Is.EqualTo (request.ValueLength))

    // extract and verify key and value
    let extractedKey = Array.sub serializedBytes HEADER_SIZE parsedKeyLen

    let extractedValue =
      Array.sub serializedBytes (HEADER_SIZE + parsedKeyLen) parsedValueLen

    Assert.That (
      ProtocolHelper.bytesToString extractedKey,
      Is.EqualTo (originalKey)
    )

    Assert.That (
      ProtocolHelper.bytesToString extractedValue,
      Is.EqualTo (originalValue)
    )
