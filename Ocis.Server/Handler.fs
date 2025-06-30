module Ocis.Server.Handler

open System
open System.Threading.Tasks
open Ocis.Server.Protocol
open Ocis.OcisDB
open Ocis.Utils.Logger

module RequestHandler =

    /// Handle a single request and return a response
    let HandleRequest (db : OcisDB) (request : RequestPacket) : Async<ResponsePacket> = async {
        try
            match request.CommandType with
            | CommandType.Set ->
                match request.Value with
                | Some value ->
                    let! result = db.Set (request.Key, value)
                    match result with
                    | Ok () ->
                        Logger.Debug $"SET success: key length={request.Key.Length}, value length={value.Length}"
                        return Protocol.CreateSuccessResponse None
                    | Error msg ->
                        Logger.Error $"SET failed: {msg}"
                        return Protocol.CreateErrorResponse msg
                | None ->
                    Logger.Error "SET command missing value"
                    return Protocol.CreateErrorResponse "SET command requires a value"

            | CommandType.Get ->
                let! result = db.Get (request.Key)
                match result with
                | Ok (Some value) ->
                    Logger.Debug $"GET success: key length={request.Key.Length}, value length={value.Length}"
                    return Protocol.CreateSuccessResponse (Some value)
                | Ok None ->
                    Logger.Debug $"GET not found: key length={request.Key.Length}"
                    return Protocol.CreateNotFoundResponse ()
                | Error msg ->
                    Logger.Error $"GET failed: {msg}"
                    return Protocol.CreateErrorResponse msg

            | CommandType.Delete ->
                let! result = db.Delete (request.Key)
                match result with
                | Ok () ->
                    Logger.Debug $"DELETE success: key length={request.Key.Length}"
                    return Protocol.CreateSuccessResponse None
                | Error msg ->
                    Logger.Error $"DELETE failed: {msg}"
                    return Protocol.CreateErrorResponse msg

            | _ ->
                let errorMsg = $"Unknown command type: {int request.CommandType}"
                Logger.Error errorMsg
                return Protocol.CreateErrorResponse errorMsg

        with ex ->
            let errorMsg = $"Unexpected error handling request: {ex.Message}"
            Logger.Error errorMsg
            return Protocol.CreateErrorResponse errorMsg
    }

    /// Validate request packet
    let ValidateRequest (request : RequestPacket) : Result<unit, string> =
        // Validate packet size
        if not (Protocol.IsValidPacketSize request.TotalPacketLength) then
            Error "Invalid packet size"
        // Validate key length
        elif
            request.KeyLength <= 0
            || request.KeyLength > (1 * 1024 * 1024)
        then // Maximum 1MB key
            Error "Invalid key length"
        // Validate value length (only for SET command)
        elif
            request.CommandType = CommandType.Set
            && (request.ValueLength <= 0
                || request.ValueLength > (8 * 1024 * 1024))
        then // Maximum 8MB value
            Error "Invalid value length for SET command"
        // Validate GET and DELETE commands should not have a value
        elif
            (request.CommandType = CommandType.Get
             || request.CommandType = CommandType.Delete)
            && request.ValueLength > 0
        then
            Error $"{request.CommandType} command should not have a value"
        // Validate key and value actual length matches declared length
        elif request.Key.Length <> request.KeyLength then
            Error "Key length mismatch"
        elif request.CommandType = CommandType.Set then
            match request.Value with
            | Some value when value.Length <> request.ValueLength -> Error "Value length mismatch"
            | None when request.ValueLength > 0 -> Error "Missing value for SET command"
            | _ -> Ok ()
        else
            Ok ()

    /// Process validated request
    let ProcessValidRequest (db : OcisDB) (request : RequestPacket) : Async<ResponsePacket> = async {
        match ValidateRequest request with
        | Ok () -> return! HandleRequest db request
        | Error msg ->
            Logger.Warn $"Invalid request: {msg}"
            return Protocol.CreateErrorResponse msg
    }
