module Ocis.Perf.Tests.ProgramConfigTests

open NUnit.Framework
open Ocis.Perf

[<TestFixture>]
type ProgramConfigTests() =

  [<Test>]
  member _.EngineWithSingleWorkerIsAccepted() =
    let args = [|"engine"; "--workers"; "1"|]
    let config = Ocis.Perf.Program.parseAndValidateConfig args

    Assert.That(config.Target, Is.EqualTo(Engine))
    Assert.That(config.Workers, Is.EqualTo(1))

  [<Test>]
  member _.EngineWithMultipleWorkersIsRejectedByDefault() =
    let args = [|"engine"; "--workers"; "2"|]

    let ex =
      Assert.Throws<System.Exception>(fun () ->
        Ocis.Perf.Program.parseAndValidateConfig args
        |> ignore)

    Assert.That(ex.Message, Does.Contain("workers=1"))
    Assert.That(
      ex.Message,
      Does.Contain("--allow-unsafe-engine-concurrency true")
    )

  [<Test>]
  member _.EngineWithMultipleWorkersIsAcceptedWhenUnsafeFlagEnabled() =
    let args =
      [|"engine"
        "--workers"
        "2"
        "--allow-unsafe-engine-concurrency"
        "true" |]

    let config = Ocis.Perf.Program.parseAndValidateConfig args

    Assert.That(config.Workers, Is.EqualTo(2))
    Assert.That(config.AllowUnsafeEngineConcurrency, Is.True)

  [<Test>]
  member _.DefaultsWarmupAndRepeat() =
    let args = [|"server"|]
    let config = Ocis.Perf.Program.parseAndValidateConfig args

    Assert.That(config.WarmupSeconds, Is.EqualTo(5))
    Assert.That(config.RepeatCount, Is.EqualTo(3))

  [<Test>]
  member _.CanSetWarmupAndRepeatExplicitly() =
    let args =
      [|"server"
        "--warmup-sec"
        "0"
        "--repeat"
        "1" |]
    let config = Ocis.Perf.Program.parseAndValidateConfig args

    Assert.That(config.WarmupSeconds, Is.EqualTo(0))
    Assert.That(config.RepeatCount, Is.EqualTo(1))
