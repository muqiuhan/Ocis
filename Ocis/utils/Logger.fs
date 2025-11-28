module Ocis.Utils.Logger

open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging

module Logger =
    let mutable LOGGER_FACTORY =
        LoggerFactory.Create(fun builder ->
            builder
                .AddSimpleConsole(fun options ->
                    options.IncludeScopes <- true
                    options.SingleLine <- true
                    options.TimestampFormat <- "HH:mm:ss ")
                .SetMinimumLevel
                LogLevel.Information
            |> ignore)

    let mutable LOGGER = LOGGER_FACTORY.CreateLogger "Ocis"

    let inline SetLogLevel (level: LogLevel) =
        LOGGER_FACTORY <-
            LoggerFactory.Create(fun builder ->
                builder
                    .AddSimpleConsole(fun options ->
                        options.IncludeScopes <- true
                        options.SingleLine <- true
                        options.TimestampFormat <- "HH:mm:ss ")
                    .SetMinimumLevel
                    level
                |> ignore)

        LOGGER <- LOGGER_FACTORY.CreateLogger "Ocis"

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Info (message: string) = LOGGER.LogInformation message

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Error (message: string) = LOGGER.LogError message

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Warn (message: string) = LOGGER.LogWarning message

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Debug (message: string) = LOGGER.LogDebug message

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Fatal (message: string) = LOGGER.LogCritical message

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    let inline Verbose (message: string) = LOGGER.LogTrace message
