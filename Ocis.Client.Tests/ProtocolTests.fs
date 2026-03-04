module Ocis.Client.Tests.ProtocolTests

open System
open System.Text
open NUnit.Framework
open Ocis.Client
open Ocis.Server.ProtocolSpec
open Ocis.Server.Protocol

[<TestFixture>]
type ProtocolTests () =

  [<Test>]
  member this.TestCreateSetRequest () =
    let key = "test_key"
    let value = Encoding.UTF8.GetBytes "test_value"

    let requestBytes = Request.createSetRequest key value

    Assert.That (requestBytes.Length, Is.GreaterThan (0))

    let magicNumber = BitConverter.ToUInt32 (requestBytes, 0)
    let version = requestBytes.[4]
    let commandType = enum<CommandType> (int requestBytes.[5])
    let totalLength = BitConverter.ToInt32 (requestBytes, 6)
    let keyLength = BitConverter.ToInt32 (requestBytes, 10)
    let valueLength = BitConverter.ToInt32 (requestBytes, 14)

    Assert.That (magicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (version, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (commandType, Is.EqualTo (CommandType.Set))
    Assert.That (keyLength, Is.EqualTo (Encoding.UTF8.GetBytes(key).Length))
    Assert.That (valueLength, Is.EqualTo (value.Length))

  [<Test>]
  member this.TestCreateGetRequest () =
    let key = "test_key"

    let requestBytes = Request.createGetRequest key

    Assert.That (requestBytes.Length, Is.GreaterThan (0))

    let magicNumber = BitConverter.ToUInt32 (requestBytes, 0)
    let version = requestBytes.[4]
    let commandType = enum<CommandType> (int requestBytes.[5])
    let keyLength = BitConverter.ToInt32 (requestBytes, 10)
    let valueLength = BitConverter.ToInt32 (requestBytes, 14)

    Assert.That (magicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (version, Is.EqualTo (PROTOCOL_VERSION))
    Assert.That (commandType, Is.EqualTo (CommandType.Get))
    Assert.That (keyLength, Is.EqualTo (Encoding.UTF8.GetBytes(key).Length))
    Assert.That (valueLength, Is.EqualTo (0))

  [<Test>]
  member this.TestCreateDeleteRequest () =
    let key = "test_key"

    let requestBytes = Request.createDeleteRequest key

    Assert.That (requestBytes.Length, Is.GreaterThan (0))

    let magicNumber = BitConverter.ToUInt32 (requestBytes, 0)
    let commandType = enum<CommandType> (int requestBytes.[5])
    let keyLength = BitConverter.ToInt32 (requestBytes, 10)
    let valueLength = BitConverter.ToInt32 (requestBytes, 14)

    Assert.That (magicNumber, Is.EqualTo (MAGIC_NUMBER))
    Assert.That (commandType, Is.EqualTo (CommandType.Delete))
    Assert.That (keyLength, Is.EqualTo (Encoding.UTF8.GetBytes(key).Length))
    Assert.That (valueLength, Is.EqualTo (0))

  [<Test>]
  member this.TestParseSuccessResponse () =
    let value = Encoding.UTF8.GetBytes "response_value"
    let responsePacket = Protocol.CreateSuccessResponse (Some value)
    let responseBytes = Protocol.SerializeResponse responsePacket

    match Response.parseResponse responseBytes with
    | Protocol.ParseSuccess response ->
      Assert.That (response.StatusCode, Is.EqualTo (StatusCode.Success))
      Assert.That (response.ValueLength, Is.EqualTo (value.Length))

      match response.Value with
      | Some v -> Assert.That (v, Is.EqualTo (value))
      | None -> Assert.Fail ("Expected value")
    | Protocol.ParseError msg ->
      Assert.Fail (sprintf "Failed to parse response: %s" msg)
    | Protocol.InsufficientData -> Assert.Fail ("Insufficient data")

  [<Test>]
  member this.TestParseNotFoundResponse () =
    let responsePacket = Protocol.CreateNotFoundResponse ()
    let responseBytes = Protocol.SerializeResponse responsePacket

    match Response.parseResponse responseBytes with
    | Protocol.ParseSuccess response ->
      Assert.That (response.StatusCode, Is.EqualTo (StatusCode.NotFound))
      Assert.That (response.Value.IsNone, Is.True)
    | Protocol.ParseError msg ->
      Assert.Fail (sprintf "Failed to parse response: %s" msg)
    | Protocol.InsufficientData -> Assert.Fail ("Insufficient data")

  [<Test>]
  member this.TestParseErrorResponse () =
    let errorMsg = "Test error message"
    let responsePacket = Protocol.CreateErrorResponse errorMsg
    let responseBytes = Protocol.SerializeResponse responsePacket

    match Response.parseResponse responseBytes with
    | Protocol.ParseSuccess response ->
      Assert.That (response.StatusCode, Is.EqualTo (StatusCode.Error))

      match response.ErrorMessage with
      | Some msg -> Assert.That (msg, Is.EqualTo (errorMsg))
      | None -> Assert.Fail ("Expected error message")
    | Protocol.ParseError msg ->
      Assert.Fail (sprintf "Failed to parse response: %s" msg)
    | Protocol.InsufficientData -> Assert.Fail ("Insufficient data")

  [<Test>]
  member this.TestParseInsufficientData () =
    let insufficientBytes = [| 0uy ; 0uy ; 0uy |]

    match Response.parseResponse insufficientBytes with
    | Protocol.ParseError msg ->
      Assert.That (msg, Is.EqualTo ("Insufficient data"))
    | Protocol.InsufficientData -> () // Expected
    | _ -> Assert.Fail ("Should have returned error")

  [<Test>]
  member this.TestRoundTrip () =
    let key = "roundtrip_key"
    let value = Encoding.UTF8.GetBytes "roundtrip_value"

    let setRequestBytes = Request.createSetRequest key value

    match Protocol.TryParseRequestPacket setRequestBytes with
    | Some packet ->
      Assert.That (packet.CommandType, Is.EqualTo (CommandType.Set))
      Assert.That (Encoding.UTF8.GetString packet.Key, Is.EqualTo (key))

      match packet.Value with
      | Some v -> Assert.That (v, Is.EqualTo (value))
      | None -> Assert.Fail ("Expected value in packet")
    | None -> Assert.Fail ("Failed to parse request packet")
