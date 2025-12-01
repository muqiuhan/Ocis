module Ocis.Tests.Memtbl

open NUnit.Framework
open Ocis.Memtbl
open System.Text

[<TestFixture>]
type MemtblTests() =

    let mutable memtbl = Memtbl()

    [<SetUp>]
    member this.Setup() = memtbl <- Memtbl()

    [<Test>]
    member this.Add_ShouldInsertNewKeyValuePair() =
        let key = Encoding.UTF8.GetBytes "key1"
        let value = 10L
        memtbl.Add(key, value)

        let result = memtbl.TryGet key
        Assert.That(result.IsSome, Is.True)
        Assert.That(result.Value, Is.EqualTo value)

    [<Test>]
    member this.Add_ShouldUpdateExistingKeyValuePair() =
        let key = Encoding.UTF8.GetBytes "key2"
        let initialValue = 20L
        let updatedValue = 25L

        memtbl.Add(key, initialValue)
        memtbl.Add(key, updatedValue)

        let result = memtbl.TryGet key
        Assert.That(result.IsSome, Is.True)
        Assert.That(result.Value, Is.EqualTo updatedValue)

    [<Test>]
    member this.TryGet_ShouldReturnSomeForExistingKey() =
        let key = Encoding.UTF8.GetBytes "key3"
        let value = 30L
        memtbl.Add(key, value)

        let result = memtbl.TryGet key
        Assert.That(result.IsSome, Is.True)
        Assert.That(result.Value, Is.EqualTo value)

    [<Test>]
    member this.TryGet_ShouldReturnNoneForNonExistingKey() =
        let key = Encoding.UTF8.GetBytes "nonexistent_key"
        let result = memtbl.TryGet key
        Assert.That(result.IsNone, Is.True)

    [<Test>]
    member this.SafeDelete_ShouldSetDeletionMarker() =
        let key = Encoding.UTF8.GetBytes "key4"
        let value = 40L
        memtbl.Add(key, value)

        match memtbl.SafeDelete key with
        | Ok() -> ()
        | Error err -> Assert.Fail $"Failed to delete key: {err}"

        let result = memtbl.TryGet key
        Assert.That(result.IsSome, Is.True)
        Assert.That(result.Value, Is.EqualTo -1L)

    [<Test>]
    member this.SafeDelete_ShouldReturnErrorForNonExistingKey() =
        let key = Encoding.UTF8.GetBytes "another_nonexistent_key"

        match memtbl.SafeDelete key with
        | Ok() -> Assert.Fail "Expected an error but got Ok"
        | Error err -> Assert.That(err.ToString(), Does.Contain "Key not found", "Error should indicate key not found")

    [<Test>]
    member this.ByteArrayComparer_ShouldHandleDifferentLengths() =
        let key1 = Encoding.UTF8.GetBytes "a"
        let key2 = Encoding.UTF8.GetBytes "aa"
        memtbl.Add(key2, 2L)
        memtbl.Add(key1, 1L)

        let resultList = List.ofSeq memtbl
        Assert.That(resultList.Length, Is.EqualTo 2)
        Assert.That(resultList[0].Value, Is.EqualTo 1L)
        Assert.That(resultList[1].Value, Is.EqualTo 2L)

    [<Test>]
    member this.ByteArrayComparer_ShouldHandleSameLengthsDifferentContent() =
        let key1 = Encoding.UTF8.GetBytes "apple"
        let key2 = Encoding.UTF8.GetBytes "apply"
        memtbl.Add(key2, 2L)
        memtbl.Add(key1, 1L)

        let resultList = List.ofSeq memtbl
        Assert.That(resultList.Length, Is.EqualTo 2)
        Assert.That(resultList[0].Value, Is.EqualTo 1L)
        Assert.That(resultList[1].Value, Is.EqualTo 2L)

    [<Test>]
    member this.ByteArrayComparer_ShouldHandleIdenticalArrays() =
        let key1 = Encoding.UTF8.GetBytes "same"
        let key2 = Encoding.UTF8.GetBytes "same"
        memtbl.Add(key1, 1L)
        memtbl.Add(key2, 2L) // This should overwrite the previous one

        let result = memtbl.TryGet key1
        Assert.That(result.IsSome, Is.True)
        Assert.That(result.Value, Is.EqualTo 2L)
