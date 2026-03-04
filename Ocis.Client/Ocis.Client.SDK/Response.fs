module Ocis.Client.SDK.Response

open Ocis.Server.ProtocolSpec
open Ocis.Client.SDK.Protocol

type ClientResult<'T> =
  | Success of 'T
  | NotFound
  | Error of string

let parseResponse (bytes : byte array) : ParseResult<ResponsePacket> =
  DeserializeResponse bytes

let toClientResult
  (parseResult : ParseResult<ResponsePacket>)
  : ClientResult<unit>
  =
  match parseResult with
  | ParseSuccess response ->
    match response.StatusCode with
    | StatusCode.Success -> Success ()
    | StatusCode.NotFound -> NotFound
    | StatusCode.Error ->
      Error (
        response.ErrorMessage
        |> Option.defaultValue "Unknown error"
      )
    | _ -> Error "Invalid status code"
  | ParseError msg -> Error msg
  | InsufficientData -> Error "Insufficient data"

let toClientResultValue
  (parseResult : ParseResult<ResponsePacket>)
  : ClientResult<byte array>
  =
  match parseResult with
  | ParseSuccess response ->
    match response.StatusCode with
    | StatusCode.Success ->
      match response.Value with
      | Some value -> Success value
      | None -> Error "Success response missing value"
    | StatusCode.NotFound -> NotFound
    | StatusCode.Error ->
      Error (
        response.ErrorMessage
        |> Option.defaultValue "Unknown error"
      )
    | _ -> Error "Invalid status code"
  | ParseError msg -> Error msg
  | InsufficientData -> Error "Insufficient data"
