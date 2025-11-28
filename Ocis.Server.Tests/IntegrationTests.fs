module Ocis.Server.Tests.IntegrationTests

open System.IO
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
