module Ocis.Server.Resilience

open System
open System.Net.Sockets

let isTransientSocketError (socketError: SocketError) =
    match socketError with
    | SocketError.TimedOut
    | SocketError.WouldBlock
    | SocketError.TryAgain
    | SocketError.Interrupted
    | SocketError.NoBufferSpaceAvailable -> true
    | _ -> false

let isTransientAcceptException (ex: exn) =
    match ex with
    | :? SocketException as socketEx -> isTransientSocketError socketEx.SocketErrorCode
    | _ -> false

// Exponential backoff with cap and overflow protection.
let computeBoundedRetryDelayMs (attempt: int) (baseDelayMs: int) (maxDelayMs: int) =
    let safeAttempt = max 0 attempt
    let effectiveBaseDelay = max 0 baseDelayMs
    let effectiveMaxDelay = max effectiveBaseDelay maxDelayMs

    let maxMultiplierByCap =
        if effectiveBaseDelay = 0 then
            1L
        else
            max 1L (int64 effectiveMaxDelay / int64 effectiveBaseDelay)

    let rec boundedPow2 acc remaining =
        if remaining <= 0 || acc >= maxMultiplierByCap then
            min acc maxMultiplierByCap
        else
            let next =
                if acc > Int64.MaxValue / 2L then
                    maxMultiplierByCap
                else
                    acc * 2L

            boundedPow2 (min next maxMultiplierByCap) (remaining - 1)

    let multiplier = boundedPow2 1L safeAttempt
    let delay = int64 effectiveBaseDelay * multiplier
    min effectiveMaxDelay (int (min delay (int64 Int32.MaxValue)))
