module Ocis.Client.SDK.Tests.ResponseTests

open NUnit.Framework
open Ocis.Server.ProtocolSpec
open Ocis.Client.SDK.Protocol
open Ocis.Client.SDK.Response

[<TestFixture>]
type ResponseTests () =

  [<Test>]
  member this.ParseResponse_SuccessWithValue () =
    let value = [| 0xDEuy ; 0xADuy ; 0xBEuy ; 0xEFuy |]
    let response = CreateSuccessResponse (Some value)
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> = parseResponse bytes

    match result with
    | ParseSuccess p ->
      Assert.AreEqual (StatusCode.Success, p.StatusCode)
      Assert.That (p.Value.IsSome, Is.True)
      Assert.AreEqual (value, p.Value.Value)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.ParseResponse_SuccessWithoutValue () =
    let response = CreateSuccessResponse None
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> = parseResponse bytes

    match result with
    | ParseSuccess p ->
      Assert.AreEqual (StatusCode.Success, p.StatusCode)
      Assert.That (p.Value.IsNone, Is.True)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.ParseResponse_NotFound () =
    let response = CreateNotFoundResponse ()
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> = parseResponse bytes

    match result with
    | ParseSuccess p -> Assert.AreEqual (StatusCode.NotFound, p.StatusCode)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.ParseResponse_Error () =
    let errorMsg = "Key not found"
    let response = CreateErrorResponse errorMsg
    let bytes = SerializeResponse response

    let result : ParseResult<ResponsePacket> = parseResponse bytes

    match result with
    | ParseSuccess p ->
      Assert.AreEqual (StatusCode.Error, p.StatusCode)
      Assert.That (p.ErrorMessage.IsSome, Is.True)
      Assert.AreEqual (errorMsg, p.ErrorMessage.Value)
    | ParseError msg -> Assert.Fail (sprintf "ParseError: %s" msg)
    | InsufficientData -> Assert.Fail ("InsufficientData")

  [<Test>]
  member this.ParseResponse_InsufficientData () =
    let buffer = Array.zeroCreate<byte> 5

    let result : ParseResult<ResponsePacket> = parseResponse buffer

    match result with
    | InsufficientData -> ()
    | _ -> Assert.Fail ("Expected InsufficientData")

  [<Test>]
  member this.ToClientResult_Success () =
    let response = CreateSuccessResponse None
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResult parseResult

    match clientResult with
    | Success () -> ()
    | NotFound -> Assert.Fail ("Expected Success, got NotFound")
    | Error msg -> Assert.Fail (sprintf "Expected Success, got Error: %s" msg)

  [<Test>]
  member this.ToClientResult_NotFound () =
    let response = CreateNotFoundResponse ()
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResult parseResult

    match clientResult with
    | NotFound -> ()
    | Success () -> Assert.Fail ("Expected NotFound, got Success")
    | Error msg -> Assert.Fail (sprintf "Expected NotFound, got Error: %s" msg)

  [<Test>]
  member this.ToClientResult_Error () =
    let errorMsg = "Database error"
    let response = CreateErrorResponse errorMsg
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResult parseResult

    match clientResult with
    | Error msg -> Assert.AreEqual (errorMsg, msg)
    | Success () -> Assert.Fail ("Expected Error, got Success")
    | NotFound -> Assert.Fail ("Expected Error, got NotFound")

  [<Test>]
  member this.ToClientResult_ErrorNoMessage () =
    let response = CreateErrorResponse ""
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResult parseResult

    match clientResult with
    | Error msg -> Assert.AreEqual ("Unknown error", msg)
    | Success () -> Assert.Fail ("Expected Error, got Success")
    | NotFound -> Assert.Fail ("Expected Error, got NotFound")

  [<Test>]
  member this.ToClientResult_InsufficientData () =
    let buffer = Array.zeroCreate<byte> 5

    let parseResult : ParseResult<ResponsePacket> =
      DeserializeResponse buffer

    let clientResult = toClientResult parseResult

    match clientResult with
    | Error msg -> Assert.AreEqual ("Insufficient data", msg)
    | _ -> Assert.Fail ("Expected Error")

  [<Test>]
  member this.ToClientResultValue_SuccessWithValue () =
    let value = [| 0x12uy ; 0x34uy ; 0x56uy ; 0x78uy |]
    let response = CreateSuccessResponse (Some value)
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResultValue parseResult

    match clientResult with
    | Success v -> CollectionAssert.AreEqual (value, v)
    | NotFound -> Assert.Fail ("Expected Success with value, got NotFound")
    | Error msg ->
      Assert.Fail (sprintf "Expected Success with value, got Error: %s" msg)

  [<Test>]
  member this.ToClientResultValue_SuccessWithoutValue () =
    let response = CreateSuccessResponse None
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResultValue parseResult

    match clientResult with
    | Error msg -> Assert.AreEqual ("Success response missing value", msg)
    | _ -> Assert.Fail ("Expected Error")

  [<Test>]
  member this.ToClientResultValue_NotFound () =
    let response = CreateNotFoundResponse ()
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResultValue parseResult

    match clientResult with
    | NotFound -> ()
    | Success _ -> Assert.Fail ("Expected NotFound, got Success")
    | Error msg -> Assert.Fail (sprintf "Expected NotFound, got Error: %s" msg)

  [<Test>]
  member this.ToClientResultValue_Error () =
    let errorMsg = "Internal error"
    let response = CreateErrorResponse errorMsg
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResultValue parseResult

    match clientResult with
    | Error msg -> Assert.AreEqual (errorMsg, msg)
    | Success _ -> Assert.Fail ("Expected Error, got Success")
    | NotFound -> Assert.Fail ("Expected Error, got NotFound")

  [<Test>]
  member this.FullWorkflow_SetGetFlow () =
    let key = "workflowKey"
    let value = [| 0xFFuy ; 0xEEuy ; 0xDDuy |]

    let response = CreateSuccessResponse (Some value)
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResultValue parseResult

    match clientResult with
    | Success v -> Assert.AreEqual (value, v)
    | NotFound -> Assert.Fail ("Expected Success with value")
    | Error msg -> Assert.Fail (sprintf "Expected Success, got Error: %s" msg)

  [<Test>]
  member this.FullWorkflow_NotFoundFlow () =
    let response = CreateNotFoundResponse ()
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResultValue parseResult

    match clientResult with
    | NotFound -> ()
    | Success _ -> Assert.Fail ("Expected NotFound")
    | Error msg -> Assert.Fail (sprintf "Expected NotFound, got Error: %s" msg)

  [<Test>]
  member this.FullWorkflow_ErrorFlow () =
    let errorMsg = "Operation failed"
    let response = CreateErrorResponse errorMsg
    let bytes = SerializeResponse response

    let parseResult : ParseResult<ResponsePacket> =
      parseResponse bytes

    let clientResult = toClientResult parseResult

    match clientResult with
    | Error msg -> Assert.AreEqual (errorMsg, msg)
    | _ -> Assert.Fail ("Expected Error")

  [<Test>]
  member this.ClientResult_Type () =
    let success : ClientResult<unit> = Success ()
    let notFound : ClientResult<unit> = NotFound
    let error : ClientResult<unit> = Error "test"

    match success with
    | Success () -> ()
    | _ -> Assert.Fail ()

    match notFound with
    | NotFound -> ()
    | _ -> Assert.Fail ()

    match error with
    | Error _ -> ()
    | _ -> Assert.Fail ()
