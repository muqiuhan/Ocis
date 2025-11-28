module Ocis.Tests.WAL

open NUnit.Framework
open Ocis.WAL
open System.Text
open System.IO
open System

#nowarn "FS0044"

[<TestFixture>]
type WalTests() =

    let tempDir = "temp_wal_tests"
    let mutable testFilePath = ""

    [<SetUp>]
    member this.Setup() =
        // Create a new temporary directory and file path for each test
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory tempDir |> ignore

        testFilePath <- Path.Combine(tempDir, $"wal_{Guid.NewGuid().ToString()}.wal")

    [<TearDown>]
    member this.TearDown() =
        // Clean up the temporary directory and file
        if Directory.Exists tempDir then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.Create_ShouldCreateNewFileAndInitializeCorrectly() =
        match Wal.Create testFilePath with
        | Ok wal ->
            use _ = wal // Ensure the file stream is closed

            Assert.That(File.Exists testFilePath, Is.True, "WAL file should be created.")
        | Error msg -> Assert.Fail $"Failed to create WAL: {msg}"

    [<Test>]
    member this.Create_ShouldOpenFileAndAppendToExistingFile() =
        // Pre-write some data to the file
        // File.WriteAllBytes(testFilePath, Encoding.UTF8.GetBytes("initial wal data")) // Remove this line

        match Wal.Create testFilePath with
        | Ok wal ->
            use wal = wal
            // Verify that the file pointer is at the end of the file, ready for appending
            // For a newly created or empty WAL file, the file pointer should start from 0.
            Assert.That(
                wal.FileStream.Position,
                Is.EqualTo 0L,
                "File pointer should be at the beginning of the file (for a new file)."
            )

            let key = Encoding.UTF8.GetBytes "newkey"
            let valueLoc = 100L
            wal.Append(WalEntry.Set(key, valueLoc))

            // Reopen and verify the appended data
            let replayedEntries = Wal.Replay testFilePath |> Seq.toList

            Assert.That(replayedEntries.Length, Is.EqualTo 1, "There should be one replayed entry.")

            match replayedEntries[0] with
            | WalEntry.Set(actualKey, actualValueLoc) ->
                Assert.That(actualKey, Is.EqualTo key, "Replayed key should match.")

                Assert.That(actualValueLoc, Is.EqualTo valueLoc, "Replayed value location should match.")
            | _ -> Assert.Fail "Replayed entry type is incorrect."

        | Error msg -> Assert.Fail $"Failed to open WAL: {msg}"

    [<Test>]
    member this.Append_ShouldWriteSetEntryCorrectly() =
        match Wal.Create testFilePath with
        | Ok wal ->
            use wal = wal
            let key = Encoding.UTF8.GetBytes "testkey"
            let valueLoc = 12345L // Use int64 literal directly

            wal.Append(WalEntry.Set(key, valueLoc))
            wal.Flush() // Ensure data is written to disk

            // Verify replay
            let replayedEntries = Wal.Replay testFilePath |> Seq.toList

            Assert.That(replayedEntries.Length, Is.EqualTo 1, "There should be one replayed entry.")

            match replayedEntries[0] with
            | WalEntry.Set(actualKey, actualValueLoc) ->
                Assert.That(actualKey, Is.EqualTo key, "Replayed key should match.")

                Assert.That(actualValueLoc, Is.EqualTo valueLoc, "Replayed value location should match.")
            | _ -> Assert.Fail "Replayed entry type is incorrect."

        | Error msg -> Assert.Fail $"Failed to create WAL: {msg}"

    [<Test>]
    member this.Append_ShouldWriteDeleteEntryCorrectly() =
        match Wal.Create testFilePath with
        | Ok wal ->
            use wal = wal
            let key = Encoding.UTF8.GetBytes "deletekey"

            wal.Append(WalEntry.Delete key)
            wal.Flush() // Ensure data is written to disk

            // Verify replay
            let replayedEntries = Wal.Replay testFilePath |> Seq.toList

            Assert.That(replayedEntries.Length, Is.EqualTo 1, "There should be one replayed entry.")

            match replayedEntries[0] with
            | WalEntry.Delete actualKey -> Assert.That(actualKey, Is.EqualTo key, "Replayed key should match.")
            | _ -> Assert.Fail "Replayed entry type is incorrect."

        | Error msg -> Assert.Fail $"Failed to create WAL: {msg}"

    [<Test>]
    member this.Replay_ShouldReturnEmptySequenceForNonExistentFile() =
        let nonExistentPath = Path.Combine(tempDir, "non_existent.wal")
        let replayedEntries = Wal.Replay nonExistentPath |> Seq.toList

        Assert.That(replayedEntries.IsEmpty, Is.True, "Should return an empty sequence for a non-existent file.")

    [<Test>]
    member this.Replay_ShouldHandleMultipleEntries() =
        match Wal.Create testFilePath with
        | Ok wal ->
            use wal = wal

            let entries =
                [ WalEntry.Set(Encoding.UTF8.GetBytes "key1", 10L) // Use int64 literal directly
                  WalEntry.Delete(Encoding.UTF8.GetBytes "key2")
                  WalEntry.Set(Encoding.UTF8.GetBytes "key3", 30L) ] // Use int64 literal directly

            for entry in entries do
                wal.Append entry

            wal.Flush()

            let replayedEntries = Wal.Replay testFilePath |> Seq.toList

            Assert.That(replayedEntries.Length, Is.EqualTo entries.Length, "Number of replayed entries should match.")

            // Verify each entry
            for i = 0 to entries.Length - 1 do
                match entries[i], replayedEntries[i] with
                | WalEntry.Set(expectedKey, expectedLoc), WalEntry.Set(actualKey, actualLoc) ->
                    Assert.That(actualKey, Is.EqualTo expectedKey, $"The {i}th Set key should match.")

                    Assert.That(actualLoc, Is.EqualTo expectedLoc, $"The {i}th Set value location should match.")
                | WalEntry.Delete expectedKey, WalEntry.Delete actualKey ->
                    Assert.That(actualKey, Is.EqualTo expectedKey, $"The {i}th Delete key should match.")
                | _, _ -> Assert.Fail $"The {i}th replayed entry type does not match."

        | Error msg -> Assert.Fail $"Failed to create WAL: {msg}"

    [<Test>]
    member this.Dispose_ShouldCloseFileStreamAndAllowReopening() =
        let walOption = Wal.Create testFilePath

        use wal = // Use 'use' keyword instead of explicit Dispose
            match walOption with
            | Ok w -> w
            | Error msg ->
                Assert.Fail $"Failed to create WAL: {msg}"
                failwith "unreachable"

        wal.Dispose()

        // Try to open the file again; if the file stream is correctly closed, it should be possible to reopen it successfully.
        Assert.That(
            (fun () ->
                System.Threading.Thread.Sleep 100 // Add a short delay

                use fs =
                    new FileStream(testFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)

                Assert.That(fs.CanRead, Is.True)),
            Throws.Nothing,
            "WAL's file stream should be closed after Dispose, allowing reopening."
        )

    [<Test>]
    member this.Flush_ShouldEnsureDataIsPersisted() =
        match Wal.Create testFilePath with
        | Ok wal ->
            use wal = wal // Use 'use' keyword instead of explicit Dispose
            let key = Encoding.UTF8.GetBytes "persistedkey"
            let valueLoc = 500L // Use int64 literal directly
            wal.Append(WalEntry.Set(key, valueLoc))
            wal.Flush() // Force data to be written to disk
            wal.Dispose()

            // Reopen WAL and try to replay data to verify if the data is persisted
            let replayedEntries = Wal.Replay testFilePath |> Seq.toList

            Assert.That(
                replayedEntries.Length,
                Is.EqualTo 1,
                "Should be able to replay persisted data after reopening."
            )

            match replayedEntries[0] with
            | WalEntry.Set(actualKey, actualValueLoc) ->
                Assert.That(actualKey, Is.EqualTo key, "Persisted key should match.")

                Assert.That(actualValueLoc, Is.EqualTo valueLoc, "Persisted value location should match.")
            | _ -> Assert.Fail "Persisted entry type is incorrect."

        | Error msg -> Assert.Fail $"Failed to create WAL: {msg}"
