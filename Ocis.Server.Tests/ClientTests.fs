module Ocis.Server.Tests.ClientTests

open System
open NUnit.Framework
open Ocis.Server.Tests.TestClient

[<TestFixture>]
type ClientTests () =

    let host = "127.0.0.1"
    let port = 7379

    [<Test>]
    member this.TestClientConnection () =
        let connectionExists = TestClientHelper.testConnection host port

        if connectionExists then
            Assert.Pass ("Connection test passed")
        else
            Assert.Inconclusive ("No server running on localhost:7379 - connection test skipped")

    [<Test>]
    member this.TestBasicOperations () =
        if not (TestClientHelper.testConnection host port) then
            Assert.Inconclusive ("No server running - skipping client operations test")
        else
            let client = TestClientHelper.createClient host port

            let key = TestClientHelper.stringToBytes "test_key_client"
            let value = TestClientHelper.stringToBytes "test_value_client"

            match client.Set (key, value) with
            | Success _ ->
                match client.Get (key) with
                | Success retrievedValue ->
                    let retrievedString = TestClientHelper.bytesToString retrievedValue
                    Assert.That (retrievedString, Is.EqualTo ("test_value_client"))

                    match client.Delete (key) with
                    | Success _ ->
                        match client.Get (key) with
                        | NotFound -> Assert.Pass ("All operations completed successfully")
                        | Success _ -> Assert.Fail ("Key should not exist after deletion")
                        | Error msg -> Assert.Fail ($"Unexpected error after deletion: {msg}")
                    | Error msg -> Assert.Fail ($"DELETE failed: {msg}")
                    | _ -> Assert.Fail ("Unexpected response")
                | NotFound -> Assert.Fail ("GET failed: key not found after SET")
                | Error msg -> Assert.Fail ($"GET failed: {msg}")
            | Error msg -> Assert.Fail ($"SET failed: {msg}")
            | _ -> Assert.Fail ("Unexpected response")

    [<Test>]
    member this.TestGetNonExistentKey () =
        if not (TestClientHelper.testConnection host port) then
            Assert.Inconclusive ("No server running - skipping test")
        else
            let client = TestClientHelper.createClient host port

            let nonExistentKey = TestClientHelper.stringToBytes "non_existent_key_12345"

            match client.Get (nonExistentKey) with
            | NotFound -> Assert.Pass ("Correctly returned NotFound for non-existent key")
            | Success _ -> Assert.Fail ("Should not find non-existent key")
            | Error msg -> Assert.Fail ($"Unexpected error: {msg}")

    [<Test>]
    member this.TestStringHelper () =
        let testString = "Hello, World! 🌍"
        let bytes = TestClientHelper.stringToBytes testString
        let convertedBack = TestClientHelper.bytesToString bytes

        Assert.That (convertedBack, Is.EqualTo (testString))
        Assert.That (bytes.Length, Is.GreaterThan (testString.Length)) // UTF-8 编码后应该更长
