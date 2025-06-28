module Ocis.Utils.Logger


open System.Runtime.CompilerServices
open Hopac
open Logary
open Logary.Message
open Logary.Configuration
open Logary.Targets

module Logger =

    let LOGGER_INSTANCE =
        Config.create "Ocis" "laptop"
        |> Config.target (LiterateConsole.create LiterateConsole.empty "console")
        |> Config.ilogger (ILogger.Console Debug)
        |> Config.build
        |> run
        |> fun logary -> logary.getLogger "Ocis"

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Info (message : string) = LOGGER_INSTANCE.info (eventX message)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Error (message : string) = LOGGER_INSTANCE.error (eventX message)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Warn (message : string) = LOGGER_INSTANCE.warn (eventX message)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Debug (message : string) = LOGGER_INSTANCE.debug (eventX message)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Fatal (message : string) = LOGGER_INSTANCE.fatal (eventX message)

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Verbose (message : string) = LOGGER_INSTANCE.verbose (eventX message)
