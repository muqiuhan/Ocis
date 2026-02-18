module Ocis.Server.Tests.IntegrationTests

open System
open System.IO
open System.Net
open System.Net.Sockets
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open NUnit.Framework
open Ocis.Server.Config

[<TestFixture>]
type IntegrationTests() =

    let testDbDir = Path.Combine(Path.GetTempPath(), "ocis_test_integration")

    [<SetUp>]
    member this.Setup() =
        if not (Directory.Exists testDbDir) then
            Directory.CreateDirectory testDbDir |> ignore

    [<TearDown>]
    member this.Cleanup() =
        try
            if Directory.Exists testDbDir then
                Directory.Delete(testDbDir, true)
        with _ ->
            ()

    [<Test>]
    member this.TestConfigValidation() =
        let validConfig = ConfigHelper.CreateDefault testDbDir
        let validatedConfig = { validConfig with Port = 7382 }

        match ConfigHelper.ValidateConfig validatedConfig with
        | Ok() -> Assert.Pass "Valid configuration passed validation"
        | Error msg -> Assert.Fail $"Valid configuration failed validation: {msg}"

    [<Test>]
    member this.TestInvalidConfig() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with Port = -1 }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Invalid configuration should fail validation"
        | Error _ -> Assert.Pass "Invalid configuration correctly failed validation"

    [<Test>]
    member this.TestConfigDefaults() =
        let config = ConfigHelper.CreateDefault testDbDir

        Assert.That(config.Dir, Is.EqualTo testDbDir)
        Assert.That(config.FlushThreshold, Is.EqualTo 1000)
        Assert.That(config.L0CompactionThreshold, Is.EqualTo 4)
        Assert.That(config.LevelSizeMultiplier, Is.EqualTo 5)
        Assert.That(config.LogLevel, Is.EqualTo "Info")
        Assert.That(config.Host, Is.EqualTo "0.0.0.0")
        Assert.That(config.Port, Is.EqualTo 7379)
        Assert.That(config.MaxConnections, Is.EqualTo 1000)
        Assert.That(config.ReceiveTimeout, Is.EqualTo 30000)
        Assert.That(config.SendTimeout, Is.EqualTo 30000)
        Assert.That(config.DurabilityMode, Is.EqualTo "Balanced")
        Assert.That(config.GroupCommitWindowMs, Is.EqualTo 5)
        Assert.That(config.DbQueueCapacity, Is.EqualTo 8192)
        Assert.That(config.CheckpointMinIntervalMs, Is.EqualTo 30000)

    [<Test>]
    member this.TestInvalidDurabilityMode() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with DurabilityMode = "Unsafe" }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Invalid durability mode should fail validation"
        | Error _ -> Assert.Pass "Invalid durability mode correctly failed validation"

    [<Test>]
    member this.TestInvalidGroupCommitWindowMs() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with GroupCommitWindowMs = 0 }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Invalid group commit window should fail validation"
        | Error _ -> Assert.Pass "Invalid group commit window correctly failed validation"

    [<Test>]
    member this.TestInvalidDbQueueCapacityZero() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with DbQueueCapacity = 0 }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Invalid DB queue capacity should fail validation"
        | Error msg -> Assert.That(msg, Is.EqualTo "DbQueueCapacity must be greater than 0")

    [<Test>]
    member this.TestInvalidDbQueueCapacityNegative() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with DbQueueCapacity = -1 }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Negative DB queue capacity should fail validation"
        | Error msg -> Assert.That(msg, Is.EqualTo "DbQueueCapacity must be greater than 0")

    [<Test>]
    member this.TestInvalidCheckpointMinIntervalMsZero() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with CheckpointMinIntervalMs = 0 }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Invalid checkpoint minimum interval should fail validation"
        | Error msg -> Assert.That(msg, Is.EqualTo "CheckpointMinIntervalMs must be greater than 0")

    [<Test>]
    member this.TestInvalidCheckpointMinIntervalMsNegative() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with CheckpointMinIntervalMs = -100 }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Negative checkpoint minimum interval should fail validation"
        | Error msg -> Assert.That(msg, Is.EqualTo "CheckpointMinIntervalMs must be greater than 0")

    [<Test>]
    member this.TestInvalidLogLevel() =
        let invalidConfig = ConfigHelper.CreateDefault testDbDir
        let badConfig = { invalidConfig with LogLevel = "Trace" }

        match ConfigHelper.ValidateConfig badConfig with
        | Ok() -> Assert.Fail "Invalid log level should fail validation"
        | Error msg -> Assert.That(msg, Is.EqualTo "LogLevel must be one of: Debug, Info, Warn, Error, Fatal")

    [<Test>]
    member this.TestProgramLogLevelOptionValidationRejectsInvalid() =
        match Ocis.Server.Program.validateLogLevelOption (Some "Trace") with
        | Ok() -> Assert.Fail "Invalid log level option should fail validation"
        | Error msg -> Assert.That(msg, Is.EqualTo "Log level must be one of: Debug, Info, Warn, Error, Fatal")

    [<Test>]
    member this.TestHostLifecycleStartupAndGracefulShutdown() =
        let config =
            { ConfigHelper.CreateDefault testDbDir with
                Host = "127.0.0.1"
                Port = 7382 }

        use host =
            Host
                .CreateDefaultBuilder()
                .ConfigureServices(fun _ services ->
                    services
                        .AddSingleton<IOptions<Ocis.Server.Host.OcisServerOptions>>(
                            Options.Create(Ocis.Server.Host.OcisServerOptions.FromConfig config)
                        )
                    |> ignore
                    services.AddHostedService<Ocis.Server.Host.OcisHostedService>() |> ignore)
                .Build()

        Assert.DoesNotThrow(fun () -> host.StartAsync().GetAwaiter().GetResult())
        Assert.DoesNotThrow(fun () -> host.StopAsync().GetAwaiter().GetResult())

    [<Test>]
    member this.TestHostStartupFailsWhenPortAlreadyInUse() =
        use occupiedPortListener = new TcpListener(IPAddress.Loopback, 0)
        occupiedPortListener.Start()

        let inUsePort =
            (occupiedPortListener.LocalEndpoint :?> IPEndPoint).Port

        let config =
            { ConfigHelper.CreateDefault testDbDir with
                Host = "127.0.0.1"
                Port = inUsePort }

        use host =
            Host
                .CreateDefaultBuilder()
                .ConfigureServices(fun _ services ->
                    services
                        .AddSingleton<IOptions<Ocis.Server.Host.OcisServerOptions>>(
                            Options.Create(Ocis.Server.Host.OcisServerOptions.FromConfig config)
                        )
                    |> ignore
                    services.AddHostedService<Ocis.Server.Host.OcisHostedService>() |> ignore)
                .Build()

        let ex =
            Assert.Throws<SocketException>(fun () -> host.StartAsync().GetAwaiter().GetResult())

        Assert.That(ex.Message, Does.Contain("in use").IgnoreCase)
