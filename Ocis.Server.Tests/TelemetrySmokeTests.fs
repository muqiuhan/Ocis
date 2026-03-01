module Ocis.Server.Tests.TelemetrySmokeTests

open System
open System.Collections.Generic
open System.Diagnostics.Metrics
open System.IO
open System.Text
open NUnit.Framework
open Ocis.OcisDB
open Ocis.Server.DbDispatcher
open Ocis.Server.Handler
open Ocis.Server.ProtocolSpec
open Ocis.Server.Telemetry

[<TestFixture>]
type TelemetrySmokeTests() =
  let testDbDir =
    Path.Combine(Path.GetTempPath(), $"ocis_telemetry_tests_{Guid.NewGuid():N}")

  let toBytes(value: string) = Encoding.UTF8.GetBytes value

  let openDbOrFail dir =
    match OcisDB.Open(dir, 1000) with
    | Ok opened -> opened
    | Error msg ->
      Assert.Fail $"Failed to open db: {msg}"
      Unchecked.defaultof<OcisDB>

  let validSetRequest (key: byte array) (value: byte array) =
    {MagicNumber = MAGIC_NUMBER
     Version = PROTOCOL_VERSION
     CommandType = CommandType.Set
     TotalPacketLength = HEADER_SIZE + key.Length + value.Length
     KeyLength = key.Length
     ValueLength = value.Length
     Key = key
     Value = Some value}

  let invalidSetRequestMissingValue(key: byte array) =
    {MagicNumber = MAGIC_NUMBER
     Version = PROTOCOL_VERSION
     CommandType = CommandType.Set
     TotalPacketLength = HEADER_SIZE + key.Length
     KeyLength = key.Length
     ValueLength = 1
     Key = key
     Value = None}

  let collectMeasurements(operation: unit -> unit) =
    let measurements = Dictionary<string, ResizeArray<double>>()

    let appendMeasurement instrumentName measurement =
      let bucket =
        match measurements.TryGetValue instrumentName with
        | true, values -> values
        | _ ->
          let values = ResizeArray<double>()
          measurements[instrumentName] <- values
          values

      bucket.Add measurement

    use listener = new MeterListener()

    listener.InstrumentPublished <-
      Action<Instrument, MeterListener>(fun instrument listener ->
        if instrument.Meter.Name = MeterName then
          listener.EnableMeasurementEvents instrument)

    listener.SetMeasurementEventCallback<int64>
      (fun instrument measurement _ _ ->
        appendMeasurement instrument.Name (float measurement))

    listener.SetMeasurementEventCallback<double>
      (fun instrument measurement _ _ ->
        appendMeasurement instrument.Name measurement)

    listener.Start()
    operation()
    listener.RecordObservableInstruments()
    measurements

  [<SetUp>]
  member _.SetUp() =
    if not(Directory.Exists testDbDir) then
      Directory.CreateDirectory testDbDir |> ignore

  [<TearDown>]
  member _.TearDown() =
    try
      if Directory.Exists testDbDir then
        Directory.Delete(testDbDir, true)
    with _ ->
      ()

  [<Test>]
  member _.SuccessfulRequestEmitsTotalAndDurationMetrics() =
    let db = openDbOrFail testDbDir
    use db = db
    use dispatcher = new OcisDbDispatcher(db, 32)

    let metrics =
      collectMeasurements(fun () ->
        let request =
          validSetRequest (toBytes "ok-key") (toBytes "ok-value")

        let response =
          RequestHandler.ProcessValidRequest dispatcher request
          |> Async.RunSynchronously

        Assert.That(response.StatusCode, Is.EqualTo StatusCode.Success))

    Assert.That(metrics.ContainsKey RequestTotalName, Is.True)
    Assert.That(metrics[RequestTotalName].Count, Is.GreaterThan 0)
    Assert.That(metrics.ContainsKey RequestDurationName, Is.True)
    Assert.That(metrics[RequestDurationName].Count, Is.GreaterThan 0)

  [<Test>]
  member _.FailedRequestEmitsFailedMetric() =
    let db = openDbOrFail testDbDir
    use db = db
    use dispatcher = new OcisDbDispatcher(db, 32)

    let metrics =
      collectMeasurements(fun () ->
        let request = invalidSetRequestMissingValue(toBytes "bad-key")

        let response =
          RequestHandler.ProcessValidRequest dispatcher request
          |> Async.RunSynchronously

        Assert.That(response.StatusCode, Is.EqualTo StatusCode.Error))

    Assert.That(metrics.ContainsKey RequestTotalName, Is.True)
    Assert.That(metrics[RequestTotalName].Count, Is.GreaterThan 0)
    Assert.That(metrics.ContainsKey RequestFailedName, Is.True)
    Assert.That(metrics[RequestFailedName].Count, Is.GreaterThan 0)
    Assert.That(metrics.ContainsKey RequestDurationName, Is.True)
    Assert.That(metrics[RequestDurationName].Count, Is.GreaterThan 0)
