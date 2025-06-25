module Ocis.Tests.Valog

open NUnit.Framework
open Ocis.Valog
open System.Text
open System.IO
open System

#nowarn "FS0044"

[<TestFixture>]
type ValogTests() =

    let tempDir = "temp_valog_tests"
    let mutable testFilePath = ""

    [<SetUp>]
    member this.Setup() =
        // Create a new temporary directory for each test
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory(tempDir) |> ignore
        testFilePath <- Path.Combine(tempDir, $"valog_{Guid.NewGuid().ToString()}.vlog")

    [<TearDown>]
    member this.TearDown() =
        // Clean up the temporary directory and file
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.``Create_ShouldCreateNewFileAndInitializeHeadTailCorrectly``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            use _ = valog // Ensure the file stream is closed
            Assert.That(File.Exists(testFilePath), Is.True, "Value Log file should be created.")
            Assert.That(valog.Head, Is.EqualTo(0L), "The Head of the new Valog should be 0.")
            Assert.That(valog.Tail, Is.EqualTo(0L), "The Tail of the new Valog should be 0.")
        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")

    [<Test>]
    member this.``Create_ShouldOpenFileAndSetHeadToExistingLength``() =
        // Write some data to the file in advance
        File.WriteAllBytes(testFilePath, Encoding.UTF8.GetBytes("some initial data"))
        let expectedLength = File.ReadAllBytes(testFilePath).Length |> int64

        match Valog.Create(testFilePath) with
        | Ok valog ->
            use _ = valog

            Assert.That(
                valog.Head,
                Is.EqualTo(expectedLength),
                "The Head of the existing file should be equal to the file length."
            )

            Assert.That(valog.Tail, Is.EqualTo(0L), "The Tail of the existing file should be 0.")
        | Error msg -> Assert.Fail($"Failed to open Valog: {msg}")

    [<Test>]
    member this.``Create_ShouldReturnErrorForInvalidPath``() =
        let invalidPath = "/nonexistent_dir/valog.vlog" // Invalid path

        match Valog.Create(invalidPath) with
        | Ok valog ->
            use _ = valog
            Assert.Fail("Creating Valog on an invalid path should fail.")
        | Error msg ->
            Assert.That(
                msg,
                Does.Contain("Failed to open or create Value Log file"),
                "The error message should contain the expected text."
            )

    [<Test>]
    member this.``Append_ShouldWriteDataAndReturnCorrectOffset``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            use valog = valog
            let key = Encoding.UTF8.GetBytes("testkey1")
            let value = Encoding.UTF8.GetBytes("testvalue1")

            let offset1 = valog.Append(key, value)
            Assert.That(offset1, Is.EqualTo(0L), "The offset of the first write should be 0.")
            Assert.That(valog.Head, Is.GreaterThan(0L), "The Head should be updated after writing.")

            let key2 = Encoding.UTF8.GetBytes("testkey2_longer")
            let value2 = Encoding.UTF8.GetBytes("testvalue2_even_longer")
            let offset2 = valog.Append(key2, value2)

            Assert.That(
                offset2,
                Is.EqualTo(offset1 + int64 (4 + key.Length + 4 + value.Length)),
                "The offset of the second write should be correct."
            )

            Assert.That(valog.Head, Is.GreaterThan(offset2), "The Head should be updated again after writing.")

        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")

    [<Test>]
    member this.``Append_ShouldHandleEmptyKeyAndValue``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            use valog = valog
            let emptyKey = [||]
            let emptyValue = [||]

            let offset1 = valog.Append(emptyKey, emptyValue)
            Assert.That(offset1, Is.EqualTo(0L), "The offset of the first write of empty key-value pair should be 0.")

            Assert.That(
                valog.Head,
                Is.GreaterThan(0L),
                "The Head should be updated after writing empty key-value pair."
            )

            let key = Encoding.UTF8.GetBytes("normalkey")
            let value = Encoding.UTF8.GetBytes("normalvalue")
            let offset2 = valog.Append(key, value)
            Assert.That(offset2, Is.GreaterThan(offset1), "The offset of the second write should be correct.")

        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")

    [<Test>]
    member this.``Read_ShouldRetrieveCorrectKeyValuePair``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            use valog = valog
            let key = Encoding.UTF8.GetBytes("readkey")
            let value = Encoding.UTF8.GetBytes("readvalue")

            let offset = valog.Append(key, value)
            valog.Flush() // Ensure the data is written to disk

            // Reopen Valog to simulate a new session read
            match Valog.Create(testFilePath) with
            | Ok reopenedValog ->
                use reopenedValog = reopenedValog
                let result = reopenedValog.Read(offset)
                Assert.That(result.IsSome, Is.True, "Should be able to read the data.")
                let actualKey, actualValue = result.Value
                Assert.That(actualKey, Is.EqualTo(key), "The read key should match.")
                Assert.That(actualValue, Is.EqualTo(value), "The read value should match.")
            | Error msg -> Assert.Fail($"Failed to reopen Valog: {msg}")

        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")

    [<Test>]
    member this.``Read_ShouldReturnNoneForInvalidLocation``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            use valog = valog
            let key = Encoding.UTF8.GetBytes("somekey")
            let value = Encoding.UTF8.GetBytes("somevalue")

            let offset = valog.Append(key, value)
            valog.Flush()

            // Try to read an invalid offset
            let invalidOffsetBefore = offset - 1L
            let invalidOffsetAfter = valog.Head + 1L

            Assert.That(
                valog.Read(invalidOffsetBefore).IsNone,
                Is.True,
                "Reading an invalid offset before Head should return None."
            )

            Assert.That(
                valog.Read(invalidOffsetAfter).IsNone,
                Is.True,
                "Reading an invalid offset after Head should return None."
            )

            Assert.That(
                valog.Read(valog.Tail - 1L).IsNone,
                Is.True,
                "Reading an invalid offset before Tail should return None."
            )

        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")

    [<Test>]
    member this.``Read_ShouldHandleMultipleEntriesCorrectly``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            use valog = valog

            let entries =
                [ for i in 0..9 -> (Encoding.UTF8.GetBytes($"key{i}"), Encoding.UTF8.GetBytes($"value{i}")) ]

            let offsets = [ for key, value in entries -> valog.Append(key, value) ]
            valog.Flush()

            for i = 0 to 9 do
                let expectedKey, expectedValue = entries.[i]
                let offset = offsets.[i]
                let result = valog.Read(offset)
                Assert.That(result.IsSome, Is.True, $"Should be able to read the {i}th data.")
                let actualKey, actualValue = result.Value
                Assert.That(actualKey, Is.EqualTo(expectedKey), $"The {i}th key should match.")
                Assert.That(actualValue, Is.EqualTo(expectedValue), $"The {i}th value should match.")

        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")

    [<Test>]
    member this.``Dispose_ShouldCloseFileStreamAndAllowReopening``() =
        let valogOption = Valog.Create(testFilePath)

        let valog =
            match valogOption with
            | Ok v -> v
            | Error msg ->
                Assert.Fail($"Failed to create Valog: {msg}")
                failwith "unreachable" // NUnit.Assert.Fail will interrupt the test, here is just for type safety

        // Explicitly dispose the Valog object
        valog.Dispose()

        // Try to reopen the file, if the file stream is correctly closed, it should be able to reopen successfully.
        Assert.That(
            fun () ->
                use fs =
                    new FileStream(testFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                // If the file can be opened exclusively, it means that the previous stream is closed
                Assert.That(fs.CanRead, Is.True)
            , Throws.Nothing
            , "The file stream of Valog should be closed after Dispose, allowing reopening."
        )

    [<Test>]
    member this.``Flush_ShouldEnsureDataIsPersisted``() =
        match Valog.Create(testFilePath) with
        | Ok valog ->
            let key = Encoding.UTF8.GetBytes("persistkey")
            let value = Encoding.UTF8.GetBytes("persistvalue")
            let offset = valog.Append(key, value)

            valog.Flush() // Force the data to be written to disk
            valog.Dispose() // Close and release resources

            // Reopen Valog and try to read the data to verify if the data is persisted
            match Valog.Create(testFilePath) with
            | Ok reopenedValog ->
                use reopenedValog = reopenedValog
                let result = reopenedValog.Read(offset)
                Assert.That(result.IsSome, Is.True, "Should be able to read the persisted data after reopening.")
                let actualKey, actualValue = result.Value
                Assert.That(actualKey, Is.EqualTo(key), "The persisted key should match.")
                Assert.That(actualValue, Is.EqualTo(value), "The persisted value should match.")
            | Error msg -> Assert.Fail($"Failed to reopen Valog: {msg}")
        | Error msg -> Assert.Fail($"Failed to create Valog: {msg}")
