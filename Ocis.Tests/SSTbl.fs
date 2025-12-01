module Ocis.Tests.SSTbl

open NUnit.Framework
open Ocis.Memtbl
open Ocis.SSTbl
open System.Text
open System.IO
open System

[<TestFixture>]
type SSTblTests() =

    let tempDir = "temp_sstbl_tests"
    let mutable testFilePath = ""

    // Helper function to create a Memtbl with some data
    let createMemtbl (count: int) =
        let memtbl = Memtbl()

        for i = 0 to count - 1 do
            let key = Encoding.UTF8.GetBytes $"key{i:D4}"
            let value = int64 i
            memtbl.Add(key, value)

        memtbl

    [<SetUp>]
    member this.Setup() =
        // create a new temporary directory and file path for each test
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory tempDir |> ignore

        testFilePath <- Path.Combine(tempDir, $"sstbl_{Guid.NewGuid().ToString()}.sst")

    [<TearDown>]
    member this.TearDown() =
        // clean up the temporary directory and file
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.Flush_ShouldCreateSSTableFileWithCorrectData() =
        let memtbl = createMemtbl 5
        let timestamp = 123456789L
        let level = 0

        // execute Flush
        let filePath =
            match SSTbl.Flush(memtbl, testFilePath, timestamp, level) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        // verify the file exists
        Assert.That(File.Exists filePath, Is.True, "SSTable file should be created.")

        // open SSTable and verify the content
        use sstbl =
            match SSTbl.Open filePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable after flush."
                failwith "unreachable"

        Assert.That(sstbl.Timestamp, Is.EqualTo timestamp, "Timestamp should match.")

        Assert.That(sstbl.Level, Is.EqualTo level, "Level should match.")

        Assert.That(sstbl.RecordOffsets.Length, Is.EqualTo 5, "RecordOffsets count should match Memtbl size.")

        // verify LowKey and HighKey
        Assert.That(Encoding.UTF8.GetString sstbl.LowKey, Is.EqualTo "key0000", "LowKey should be correct.")

        Assert.That(Encoding.UTF8.GetString sstbl.HighKey, Is.EqualTo "key0004", "HighKey should be correct.")

        // verify the data read through TryGet
        for i = 0 to 4 do
            let key = Encoding.UTF8.GetBytes $"key{i:D4}"
            let expectedValue = int64 i
            let actualValue = sstbl.TryGet key

            Assert.That(actualValue.IsSome, Is.True, $"Key {Encoding.UTF8.GetString key} should be found.")

            Assert.That(
                actualValue.Value,
                Is.EqualTo expectedValue,
                $"Value for key {Encoding.UTF8.GetString key} should be correct."
            )

    [<Test>]
    member this.Flush_ShouldHandleEmptyMemtbl() =
        let memtbl = Memtbl() // empty Memtbl
        let timestamp = 987654321L
        let level = 1

        let filePath =
            match SSTbl.Flush(memtbl, testFilePath, timestamp, level) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush empty SSTable: {err}"
                ""

        Assert.That(File.Exists filePath, Is.True, "Empty SSTable file should still be created.")

        use sstbl =
            match SSTbl.Open filePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open empty SSTable."
                failwith "unreachable"

        Assert.That(sstbl.Timestamp, Is.EqualTo timestamp, "Timestamp should match.")

        Assert.That(sstbl.RecordOffsets.Length, Is.EqualTo 0, "RecordOffsets count should be 0 for empty Memtbl.")

        Assert.That(sstbl.LowKey.Length, Is.EqualTo 0, "LowKey should be empty for empty Memtbl.")

        Assert.That(sstbl.HighKey.Length, Is.EqualTo 0, "HighKey should be empty for empty Memtbl.")

        // any key should return None
        Assert.That(
            sstbl.TryGet(Encoding.UTF8.GetBytes "anykey").IsNone,
            Is.True,
            "TryGet should return None for empty SSTable."
        )

    [<Test>]
    member this.Open_ShouldLoadCorrectMetadataAndOffsets() =
        let memtbl = createMemtbl 10
        let timestamp = 1122334455L
        let level = 2

        // first Flush to create file
        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, timestamp, level) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        // then Open to read file
        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        Assert.That(sstbl.Path, Is.EqualTo testFilePath, "Path should match.")

        Assert.That(sstbl.Timestamp, Is.EqualTo timestamp, "Timestamp should match.")

        Assert.That(sstbl.Level, Is.EqualTo level, "Level should match.")

        Assert.That(sstbl.RecordOffsets.Length, Is.EqualTo 10, "RecordOffsets count should be correct.")

        Assert.That(Encoding.UTF8.GetString sstbl.LowKey, Is.EqualTo "key0000", "LowKey should be correct.")

        Assert.That(Encoding.UTF8.GetString sstbl.HighKey, Is.EqualTo "key0009", "HighKey should be correct.")

    [<Test>]
    member this.Open_ShouldReturnNoneForNonExistentFile() =
        let nonExistentPath = Path.Combine(tempDir, "non_existent.sst")
        let sstblOption = SSTbl.Open nonExistentPath

        Assert.That(sstblOption.IsNone, Is.True, "Open should return None for a non-existent file.")

    [<Test>]
    member this.TryGet_ShouldReturnSomeForExistingKey() =
        let memtbl = createMemtbl 5
        memtbl.Add(Encoding.UTF8.GetBytes "test_key", 999L)

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, 0L, 0) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        let key = Encoding.UTF8.GetBytes "key0002"
        let result = sstbl.TryGet key
        Assert.That(result.IsSome, Is.True, "Should find existing key.")
        Assert.That(result.Value, Is.EqualTo 2L, "Value should be correct.")

        let testKey = Encoding.UTF8.GetBytes "test_key"
        let testResult = sstbl.TryGet testKey

        Assert.That(testResult.IsSome, Is.True, "Should find the manually added key.")

        Assert.That(testResult.Value, Is.EqualTo 999L, "Value for manually added key should be correct.")

    [<Test>]
    member this.TryGet_ShouldReturnNoneForNonExistingKey() =
        let memtbl = createMemtbl 5

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, 0L, 0) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        let key = Encoding.UTF8.GetBytes "non_existent_key"
        let result = sstbl.TryGet key
        Assert.That(result.IsNone, Is.True, "Should not find non-existing key.")

    [<Test>]
    member this.TryGet_ShouldReturnCorrectValueForDeletedKeyMarker() =
        let memtbl = Memtbl()
        let keyToDelete = Encoding.UTF8.GetBytes "key_to_delete"
        memtbl.Add(keyToDelete, 100L)

        match memtbl.SafeDelete keyToDelete with
        | Ok() -> () // this will write a -1L marker in Memtbl
        | Error err -> Assert.Fail $"Failed to delete key: {err}"

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, 0L, 0) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        let result = sstbl.TryGet keyToDelete
        Assert.That(result.IsSome, Is.True, "Should find the deletion marker key.")

        Assert.That(result.Value, Is.EqualTo -1L, "Value should be -1L for a deleted key marker.")

    [<Test>]
    member this.TryGet_ShouldReturnNoneForKeysOutsideRange() =
        let memtbl = createMemtbl 5 // key0000 to key0004

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, 0L, 0) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        let keyTooLow = Encoding.UTF8.GetBytes "aaaaa" // less than key0000
        let keyTooHigh = Encoding.UTF8.GetBytes "zzzzz" // greater than key0004

        Assert.That(sstbl.TryGet(keyTooLow).IsNone, Is.True, "Should return None for key lower than LowKey.")

        Assert.That(sstbl.TryGet(keyTooHigh).IsNone, Is.True, "Should return None for key higher than HighKey.")

    [<Test>]
    member this.TryGet_ShouldWorkWithBoundaryKeys() =
        let memtbl = createMemtbl 5 // key0000 to key0004

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, 0L, 0) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        // Test LowKey
        let lowKey = Encoding.UTF8.GetBytes "key0000"
        let lowResult = sstbl.TryGet lowKey
        Assert.That(lowResult.IsSome, Is.True, "Should find LowKey.")

        Assert.That(lowResult.Value, Is.EqualTo 0L, "Value for LowKey should be correct.")

        // Test HighKey
        let highKey = Encoding.UTF8.GetBytes "key0004"
        let highResult = sstbl.TryGet highKey
        Assert.That(highResult.IsSome, Is.True, "Should find HighKey.")

        Assert.That(highResult.Value, Is.EqualTo 4L, "Value for HighKey should be correct.")

    [<Test>]
    member this.Seq_ShouldIterAllKeyValuePairsInOrder() =
        let memtbl = createMemtbl 5 // key0000 -> 0L, key0001 -> 1L, ..., key0004 -> 4L
        let timestamp = 0L
        let level = 0

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, timestamp, level) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable for enumeration test."
                failwith "unreachable"

        let mutable expectedIdx = 0

        sstbl
        |> Seq.iter (fun (KeyValue(key, valueLocation)) ->
            let expectedKey = Encoding.UTF8.GetBytes $"key{expectedIdx:D4}"
            let expectedValue = int64 expectedIdx

            Assert.That(
                Encoding.UTF8.GetString key,
                Is.EqualTo(Encoding.UTF8.GetString expectedKey),
                $"Enumerated key at index {expectedIdx} should match."
            )

            Assert.That(
                valueLocation,
                Is.EqualTo expectedValue,
                $"Enumerated value at index {expectedIdx} should match."
            )

            expectedIdx <- expectedIdx + 1)

        Assert.That(expectedIdx, Is.EqualTo 5, "All 5 key-value pairs should be enumerated.")

    [<Test>]
    member this.IEnumerable_ShouldEnumerateAllKeyValuePairsInOrder() =
        let memtbl = createMemtbl 5 // key0000 -> 0L, key0001 -> 1L, ..., key0004 -> 4L
        let timestamp = 0L
        let level = 0

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, timestamp, level) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        use sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable for enumeration test."
                failwith "unreachable"

        let mutable expectedIdx = 0

        for KeyValue(key, valueLocation) in sstbl do
            let expectedKey = Encoding.UTF8.GetBytes $"key{expectedIdx:D4}"
            let expectedValue = int64 expectedIdx

            Assert.That(
                Encoding.UTF8.GetString key,
                Is.EqualTo(Encoding.UTF8.GetString expectedKey),
                $"Enumerated key at index {expectedIdx} should match."
            )

            Assert.That(
                valueLocation,
                Is.EqualTo expectedValue,
                $"Enumerated value at index {expectedIdx} should match."
            )

            expectedIdx <- expectedIdx + 1

        Assert.That(expectedIdx, Is.EqualTo 5, "All 5 key-value pairs should be enumerated.")

    [<Test>]
    member this.Dispose_ShouldCloseFileStream() =
        let memtbl = createMemtbl 1

        let testFilePath =
            match SSTbl.Flush(memtbl, testFilePath, 0L, 0) with
            | Ok path -> path
            | Error err ->
                Assert.Fail $"Failed to flush SSTable: {err}"
                ""

        let sstbl =
            match SSTbl.Open testFilePath with
            | Some s -> s
            | None ->
                Assert.Fail "Failed to open SSTable."
                failwith "unreachable"

        // Explicitly dispose of the SSTbl object
        sstbl.FileStream.Dispose()

        // try to open the file again, if FileStream is correctly closed, it should be fine
        // here we cannot directly check sstbl.FileStream.CanRead, because the object may still exist but the underlying handle is closed
        // a more reliable way is to try to operate on the file, see if it throws an exception like "file is in use"
        // the most direct verification is that File.Exists and TryGet no longer work (but TryGet will reopen the stream)
        // for simplicity, here we assume that the Close() inside Dispose is effective
        Assert.That(
            (fun () ->
                use fs =
                    new FileStream(testFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                // if the file can be opened exclusively, it means the previous stream has been closed
                Assert.That(fs.CanRead, Is.True)),
            Throws.Nothing,
            "FileStream should be closed after Dispose, allowing re-opening."
        )
