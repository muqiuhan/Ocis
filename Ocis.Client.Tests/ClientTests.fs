module Ocis.Client.Tests.ClientTests

open System
open System.Threading
open NUnit.Framework
open Ocis.Client

let testHost = "127.0.0.1"
let testPort = 7379

[<TestFixture>]
type IntegrationTests() =

  [<SetUp>]
  member this.EnsureServerRunning() =
    if not(Helper.testConnection testHost testPort) then
      Assert.Ignore "Server not running - start Ocis.Server first on port 7379"

  [<Test>]
  member this.TestSetAndGet() =
    let key = "test_key_dotnet"
    let value = "test_value_dotnet" |> Helper.stringToBytes

    let conn = TcpClient.connect testHost testPort
    try
      match TcpClient.set conn key value with
      | Success() -> ()
      | NotFound -> Assert.Fail("Unexpected NotFound on SET")
      | Error msg -> Assert.Fail(sprintf "SET failed: %s" msg)

      match TcpClient.get conn key with
      | Success returnedValue ->
        let returnedStr = Helper.bytesToString returnedValue
        Assert.That(returnedStr, Is.EqualTo(Helper.bytesToString value))
      | NotFound -> Assert.Fail("Key not found after SET")
      | Error msg -> Assert.Fail(sprintf "GET failed: %s" msg)
    finally
      TcpClient.disconnect conn

  [<Test>]
  member this.TestDelete() =
    let key = "delete_test_key"
    let value = "delete_test_value" |> Helper.stringToBytes

    let conn = TcpClient.connect testHost testPort
    try
      match TcpClient.set conn key value with
      | Success() -> ()
      | _ -> Assert.Fail("SET failed")

      match TcpClient.delete conn key with
      | Success() -> ()
      | Error msg -> Assert.Fail(sprintf "DELETE failed: %s" msg)

      match TcpClient.get conn key with
      | NotFound -> ()
      | Success _ -> Assert.Fail("Key should have been deleted")
      | Error msg -> Assert.Fail(sprintf "GET after DELETE failed: %s" msg)
    finally
      TcpClient.disconnect conn

  [<Test>]
  member this.TestGetNonExistentKey() =
    let conn = TcpClient.connect testHost testPort
    try
      match TcpClient.get conn "non_existent_key_12345" with
      | NotFound -> ()
      | Success _ -> Assert.Fail("Should not find non-existent key")
      | Error msg -> Assert.Fail(sprintf "GET failed: %s" msg)
    finally
      TcpClient.disconnect conn

  [<Test>]
  member this.TestEmptyKey() =
    let conn = TcpClient.connect testHost testPort
    try
      let value = "empty_key_value" |> Helper.stringToBytes

      match TcpClient.set conn "" value with
      | Success() -> ()
      | Error msg -> Assert.Fail(sprintf "SET with empty key failed: %s" msg)

      match TcpClient.get conn "" with
      | Success returnedValue ->
        let returnedStr = Helper.bytesToString returnedValue
        Assert.That(returnedStr, Is.EqualTo(Helper.bytesToString value))
      | NotFound -> Assert.Fail("Empty key not found")
      | Error msg -> Assert.Fail(sprintf "GET empty key failed: %s" msg)
    finally
      TcpClient.disconnect conn

  [<Test>]
  member this.TestLargeValue() =
    let conn = TcpClient.connect testHost testPort
    try
      let key = "large_value_key"
      let value = Array.zeroCreate<byte>(1024 * 1024)
      Random(42).NextBytes(value)

      match TcpClient.set conn key value with
      | Success() -> ()
      | Error msg -> Assert.Fail(sprintf "SET large value failed: %s" msg)

      match TcpClient.get conn key with
      | Success returnedValue ->
        Assert.That(returnedValue.Length, Is.EqualTo(value.Length))
      | NotFound -> Assert.Fail("Large value not found")
      | Error msg -> Assert.Fail(sprintf "GET large value failed: %s" msg)
    finally
      TcpClient.disconnect conn

  [<Test>]
  member this.TestMultipleOperations() =
    let conn = TcpClient.connect testHost testPort
    try
      for i in 1..10 do
        let key = sprintf "multi_key_%d" i
        let value = sprintf "multi_value_%d" i |> Helper.stringToBytes

        match TcpClient.set conn key value with
        | Success() -> ()
        | Error msg -> Assert.Fail(sprintf "SET %d failed: %s" i msg)

      for i in 1..10 do
        let key = sprintf "multi_key_%d" i
        let expectedValue =
          sprintf "multi_value_%d" i |> Helper.stringToBytes

        match TcpClient.get conn key with
        | Success returnedValue ->
          Assert.That(
            Helper.bytesToString returnedValue,
            Is.EqualTo(Helper.bytesToString expectedValue)
          )
        | NotFound -> Assert.Fail(sprintf "Key %d not found" i)
        | Error msg -> Assert.Fail(sprintf "GET %d failed: %s" i msg)
    finally
      TcpClient.disconnect conn
