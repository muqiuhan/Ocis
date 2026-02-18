module Ocis.Server.Tests.DispatcherConcurrencyTests

open System
open System.IO
open System.Text
open NUnit.Framework
open Ocis.OcisDB
open Ocis.Server.DbDispatcher

[<TestFixture>]
type DispatcherConcurrencyTests() =

    let testDbDir = Path.Combine(Path.GetTempPath(), $"ocis_dispatcher_tests_{Guid.NewGuid():N}")

    let toBytes (value: string) = Encoding.UTF8.GetBytes value

    let openDbOrFail dir =
        match OcisDB.Open(dir, 1000) with
        | Ok opened -> opened
        | Error msg ->
            Assert.Fail $"Failed to open db: {msg}"
            Unchecked.defaultof<OcisDB>

    [<SetUp>]
    member _.SetUp() =
        if not (Directory.Exists testDbDir) then
            Directory.CreateDirectory testDbDir |> ignore

    [<TearDown>]
    member _.TearDown() =
        try
            if Directory.Exists testDbDir then
                Directory.Delete(testDbDir, true)
        with _ ->
            ()

    [<Test>]
    member _.ConcurrentMixedRequestsCompleteWithoutUnhandledExceptions() =
        let db = openDbOrFail testDbDir
        use db = db
        use dispatcher = new OcisDbDispatcher(db, 4096)

        let operations =
            [| for i in 1..300 ->
                   async {
                       let key = toBytes $"mixed-key-{i % 40}"

                       match i % 3 with
                       | 0 ->
                           let value = toBytes $"value-{i}"
                           return! dispatcher.DispatchSet(key, value)
                       | 1 ->
                           let! result = dispatcher.DispatchGet key
                           return result |> Result.map ignore
                       | _ ->
                           return! dispatcher.DispatchDelete key
                   } |]

        let results = operations |> Async.Parallel |> Async.RunSynchronously
        Assert.That(results, Has.Length.EqualTo 300)
        Assert.That(results |> Array.forall Result.isOk, Is.True)

    [<Test>]
    member _.WrittenKeysAreRetrievableAfterConcurrentDispatch() =
        let db = openDbOrFail testDbDir
        use db = db
        use dispatcher = new OcisDbDispatcher(db, 4096)

        let writes =
            [| for i in 1..200 ->
                   let keyText = $"key-{i}"
                   let valueText = $"value-{i}"

                   async {
                       let! setResult = dispatcher.DispatchSet(toBytes keyText, toBytes valueText)
                       return keyText, valueText, setResult
                   } |]

        let writeResults = writes |> Async.Parallel |> Async.RunSynchronously
        Assert.That(writeResults |> Array.forall (fun (_, _, result) -> Result.isOk result), Is.True)

        let reads =
            writeResults
            |> Array.map (fun (keyText, valueText, _) ->
                async {
                    let! getResult = dispatcher.DispatchGet(toBytes keyText)
                    return valueText, getResult
                })

        let readResults = reads |> Async.Parallel |> Async.RunSynchronously

        readResults
        |> Array.iter (fun (expectedValue, getResult) ->
            match getResult with
            | Ok(Some bytes) -> Assert.That(Encoding.UTF8.GetString bytes, Is.EqualTo expectedValue)
            | Ok None -> Assert.Fail $"Expected value '{expectedValue}' but key was missing"
            | Error msg -> Assert.Fail $"GET failed: {msg}")

    [<Test>]
    member _.DispatchAfterStopReturnsControlledError() =
        let db = openDbOrFail testDbDir
        use db = db
        use dispatcher = new OcisDbDispatcher(db, 16)

        dispatcher.StopAsync() |> Async.RunSynchronously

        let result = dispatcher.DispatchGet(toBytes "key-after-stop") |> Async.RunSynchronously

        match result with
        | Error msg -> Assert.That(msg, Is.EqualTo "Database dispatcher queue is full or closed")
        | Ok _ -> Assert.Fail "Dispatch should fail with controlled error after dispatcher has stopped"
