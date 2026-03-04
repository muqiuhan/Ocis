module Ocis.Client.SDK.Tests.ProtocolTests

open NUnit.Framework
open Ocis.Server.ProtocolSpec
open Ocis.Client.SDK.Protocol

[<TestFixture>]
type ProtocolTests () =

  [<Test>]
  member this.IsValidHeader_Valid () =
    let result = IsValidHeader MAGIC_NUMBER PROTOCOL_VERSION
    Assert.IsTrue (result)

  [<Test>]
  member this.IsValidHeader_InvalidMagic () =
    let result = IsValidHeader 0xFFFFFFFFu PROTOCOL_VERSION
    Assert.IsFalse (result)

  [<Test>]
  member this.IsValidHeader_InvalidVersion () =
    let result = IsValidHeader MAGIC_NUMBER 0xFFuy
    Assert.IsFalse (result)

  [<Test>]
  member this.IsValidHeader_ZeroVersion () =
    let result = IsValidHeader MAGIC_NUMBER 0uy
    Assert.IsFalse (result)

  [<Test>]
  member this.TryParseRequestHeader_InsufficientData () =
    let buffer = Array.zeroCreate<byte> 10
    let result = TryParseRequestHeader buffer
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.TryParseRequestHeader_ValidHeader () =
    let buffer = Array.zeroCreate<byte> HEADER_SIZE
    System.BitConverter.GetBytes(MAGIC_NUMBER).CopyTo (buffer, 0)
    buffer.[4] <- PROTOCOL_VERSION
    buffer.[5] <- byte CommandType.Set
    System.BitConverter.GetBytes(HEADER_SIZE + 4 + 3).CopyTo (buffer, 6)
    System.BitConverter.GetBytes(4).CopyTo (buffer, 10)
    System.BitConverter.GetBytes(3).CopyTo (buffer, 14)

    let result = TryParseRequestHeader buffer
    Assert.That (result.IsSome, Is.True)
    let header = result.Value
    Assert.AreEqual (MAGIC_NUMBER, header.MagicNumber)
    Assert.AreEqual (PROTOCOL_VERSION, header.Version)
    Assert.AreEqual (CommandType.Set, header.CommandType)
    Assert.AreEqual (4, header.KeyLength)
    Assert.AreEqual (3, header.ValueLength)

  [<Test>]
  member this.TryParseRequestHeader_InvalidMagic () =
    let buffer = Array.zeroCreate<byte> HEADER_SIZE
    System.BitConverter.GetBytes(0x12345678u).CopyTo (buffer, 0)
    buffer.[4] <- PROTOCOL_VERSION

    let result = TryParseRequestHeader buffer
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.TryParseRequestPacket_Complete () =
    let key = "test"
    let value = [| 0x01uy ; 0x02uy ; 0x03uy |]
    let keyBytes = System.Text.Encoding.UTF8.GetBytes (key)
    let bufferSize = HEADER_SIZE + keyBytes.Length + value.Length
    let buffer = Array.zeroCreate<byte> bufferSize

    System.BitConverter.GetBytes(MAGIC_NUMBER).CopyTo (buffer, 0)
    buffer.[4] <- PROTOCOL_VERSION
    buffer.[5] <- byte CommandType.Set
    System.BitConverter.GetBytes(bufferSize).CopyTo (buffer, 6)
    System.BitConverter.GetBytes(keyBytes.Length).CopyTo (buffer, 10)
    System.BitConverter.GetBytes(value.Length).CopyTo (buffer, 14)
    Array.blit keyBytes 0 buffer HEADER_SIZE keyBytes.Length
    Array.blit value 0 buffer (HEADER_SIZE + keyBytes.Length) value.Length

    let result = TryParseRequestPacket buffer
    Assert.That (result.IsSome, Is.True)
    let packet = result.Value
    Assert.AreEqual (keyBytes, packet.Key)
    Assert.That (packet.Value.IsSome, Is.True)
    Assert.AreEqual (value, packet.Value.Value)

  [<Test>]
  member this.TryParseRequestPacket_Incomplete () =
    let buffer = Array.zeroCreate<byte> HEADER_SIZE
    System.BitConverter.GetBytes(MAGIC_NUMBER).CopyTo (buffer, 0)
    buffer.[4] <- PROTOCOL_VERSION
    buffer.[5] <- byte CommandType.Get
    System.BitConverter.GetBytes(1000).CopyTo (buffer, 6)

    let result = TryParseRequestPacket buffer
    Assert.That (result.IsNone, Is.True)

  [<Test>]
  member this.SerializeRequest_EmptyKey () =
    let packet =
      { MagicNumber = MAGIC_NUMBER
        Version = PROTOCOL_VERSION
        CommandType = CommandType.Get
        TotalPacketLength = HEADER_SIZE
        KeyLength = 0
        ValueLength = 0
        Key = [||]
        Value = None }

    let bytes = SerializeRequest packet
    Assert.AreEqual (HEADER_SIZE, bytes.Length)

    let magic = System.BitConverter.ToUInt32 (bytes, 0)
    Assert.AreEqual (MAGIC_NUMBER, magic)

  [<Test>]
  member this.CreateSuccessResponse_WithValue () =
    let value = [| 0xAAuy ; 0xBBuy ; 0xCCuy |]
    let response = CreateSuccessResponse (Some value)

    Assert.AreEqual (StatusCode.Success, response.StatusCode)
    Assert.AreEqual (value.Length, response.ValueLength)
    Assert.That (response.Value.IsSome, Is.True)
    Assert.AreEqual (value, response.Value.Value)

  [<Test>]
  member this.CreateSuccessResponse_NoValue () =
    let response = CreateSuccessResponse None

    Assert.AreEqual (StatusCode.Success, response.StatusCode)
    Assert.AreEqual (0, response.ValueLength)
    Assert.That (response.Value.IsNone, Is.True)

  [<Test>]
  member this.CreateNotFoundResponse () =
    let response = CreateNotFoundResponse ()

    Assert.AreEqual (StatusCode.NotFound, response.StatusCode)
    Assert.AreEqual (0, response.ValueLength)
    Assert.AreEqual (0, response.ErrorMessageLength)
    Assert.That (response.Value.IsNone, Is.True)
    Assert.That (response.ErrorMessage.IsNone, Is.True)

  [<Test>]
  member this.CreateErrorResponse () =
    let message = "Something went wrong"
    let response = CreateErrorResponse message

    Assert.AreEqual (StatusCode.Error, response.StatusCode)
    Assert.That (response.ErrorMessage.IsSome, Is.True)
    Assert.AreEqual (message, response.ErrorMessage.Value)
    Assert.AreEqual (message.Length, response.ErrorMessageLength)

  [<Test>]
  member this.IsValidPacketSize_Valid () =
    Assert.IsTrue (IsValidPacketSize HEADER_SIZE)
    Assert.IsTrue (IsValidPacketSize (1024))
    Assert.IsTrue (IsValidPacketSize (10 * 1024 * 1024))

  [<Test>]
  member this.IsValidPacketSize_TooSmall () =
    Assert.IsFalse (IsValidPacketSize (HEADER_SIZE - 1))
    Assert.IsFalse (IsValidPacketSize (0))
    Assert.IsFalse (IsValidPacketSize (-1))

  [<Test>]
  member this.IsValidPacketSize_TooLarge () =
    Assert.IsFalse (IsValidPacketSize (10 * 1024 * 1024 + 1))

  [<Test>]
  member this.SerializeResponse_WithValue () =
    let value = [| 0x11uy ; 0x22uy |]

    let packet =
      { MagicNumber = MAGIC_NUMBER
        Version = PROTOCOL_VERSION
        StatusCode = StatusCode.Success
        TotalPacketLength = HEADER_SIZE + value.Length
        ValueLength = value.Length
        ErrorMessageLength = 0
        Value = Some value
        ErrorMessage = None }

    let bytes = SerializeResponse packet
    let statusCode = bytes.[5]
    Assert.AreEqual (byte StatusCode.Success, statusCode)

  [<Test>]
  member this.SerializeResponse_WithError () =
    let errorMsg = "error message"

    let packet =
      { MagicNumber = MAGIC_NUMBER
        Version = PROTOCOL_VERSION
        StatusCode = StatusCode.Error
        TotalPacketLength =
          HEADER_SIZE
          + System.Text.Encoding.UTF8.GetByteCount (errorMsg)
        ValueLength = 0
        ErrorMessageLength = System.Text.Encoding.UTF8.GetByteCount (errorMsg)
        Value = None
        ErrorMessage = Some errorMsg }

    let bytes = SerializeResponse packet
    let statusCode = bytes.[5]
    Assert.AreEqual (byte StatusCode.Error, statusCode)

  [<Test>]
  member this.DeserializeResponse_Success () =
    let value = [| 0x01uy ; 0x02uy ; 0x03uy |]
    let response = CreateSuccessResponse (Some value)
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> =
      DeserializeResponse bytes

    match result with
    | ParseSuccess p ->
      Assert.AreEqual (StatusCode.Success, p.StatusCode)
      Assert.That (p.Value.IsSome, Is.True)
      Assert.AreEqual (value, p.Value.Value)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.DeserializeResponse_NotFound () =
    let response = CreateNotFoundResponse ()
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> =
      DeserializeResponse bytes

    match result with
    | ParseSuccess p ->
      Assert.AreEqual (StatusCode.NotFound, p.StatusCode)
      Assert.That (p.Value.IsNone, Is.True)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.DeserializeResponse_Error () =
    let errorMsg = "test error"
    let response = CreateErrorResponse errorMsg
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> =
      DeserializeResponse bytes

    match result with
    | ParseSuccess p ->
      Assert.AreEqual (StatusCode.Error, p.StatusCode)
      Assert.That (p.ErrorMessage.IsSome, Is.True)
      Assert.AreEqual (errorMsg, p.ErrorMessage.Value)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.DeserializeResponse_InsufficientData () =
    let buffer = Array.zeroCreate<byte> 10

    let result : ParseResult<ResponsePacket> =
      DeserializeResponse buffer

    match result with
    | InsufficientData -> ()
    | _ -> Assert.Fail ("Expected InsufficientData")

  [<Test>]
  member this.DeserializeResponse_InvalidHeader () =
    let buffer = Array.zeroCreate<byte> HEADER_SIZE
    System.BitConverter.GetBytes(0xFFFFFFFFu).CopyTo (buffer, 0)
    buffer.[4] <- PROTOCOL_VERSION

    let result : ParseResult<ResponsePacket> =
      DeserializeResponse buffer

    match result with
    | ParseError _ -> ()
    | _ -> Assert.Fail ("Expected ParseError")

  [<Test>]
  member this.RoundTrip_SetRequest () =
    let key = "roundTripKey"
    let value = [| 0xAAuy ; 0xBBuy ; 0xCCuy |]

    let packet =
      { MagicNumber = MAGIC_NUMBER
        Version = PROTOCOL_VERSION
        CommandType = CommandType.Set
        TotalPacketLength =
          HEADER_SIZE
          + System.Text.Encoding.UTF8.GetByteCount (key)
          + value.Length
        KeyLength = System.Text.Encoding.UTF8.GetByteCount (key)
        ValueLength = value.Length
        Key = System.Text.Encoding.UTF8.GetBytes (key)
        Value = Some value }

    let bytes = SerializeRequest packet
    let result = TryParseRequestPacket bytes

    Assert.That (result.IsSome, Is.True)
    let parsed = result.Value
    Assert.AreEqual (packet.CommandType, parsed.CommandType)
    Assert.AreEqual (packet.Key, parsed.Key)
    Assert.AreEqual (packet.Value, parsed.Value)
