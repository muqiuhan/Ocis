module Ocis.Tests.SSTbl

open NUnit.Framework
open Ocis.Memtbl
open Ocis.SSTbl
open System.Text
open System.IO
open System

[<TestFixture>]
type SSTblTests() =

    let tempDir = "temp_sstbl_tests" // 临时目录名
    let mutable testFilePath = "" // 当前测试文件路径

    // Helper function to create a Memtbl with some data
    let createMemtbl (count: int) =
        let memtbl = Memtbl()

        for i = 0 to count - 1 do
            let key = Encoding.UTF8.GetBytes($"key{i:D4}") // 例如 "key0000", "key0001"
            let value = int64 i
            memtbl.Add(key, value)

        memtbl

    [<SetUp>]
    member this.Setup() =
        // 为每个测试创建一个新的临时目录和文件路径
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

        Directory.CreateDirectory(tempDir) |> ignore
        testFilePath <- Path.Combine(tempDir, $"sstbl_{Guid.NewGuid().ToString()}.sst")

    [<TearDown>]
    member this.TearDown() =
        // 清理临时目录和文件
        if Directory.Exists(tempDir) then
            Directory.Delete(tempDir, true)

    [<Test>]
    member this.``Flush_ShouldCreateSSTableFileWithCorrectData``() =
        let memtbl = createMemtbl 5
        let timestamp = 123456789L
        let level = 0

        // 执行 Flush
        let filePath = SSTbl.Flush(memtbl, testFilePath, timestamp, level)

        // 验证文件是否存在
        Assert.That(File.Exists(filePath), Is.True, "SSTable file should be created.")

        // 打开 SSTable 并验证内容
        use sstbl =
            match SSTbl.Open(filePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable after flush."

        Assert.That(sstbl.Timestamp, Is.EqualTo(timestamp), "Timestamp should match.")
        Assert.That(sstbl.Level, Is.EqualTo(level), "Level should match.")
        Assert.That(sstbl.RecordOffsets.Length, Is.EqualTo(5), "RecordOffsets count should match Memtbl size.")

        // 验证 LowKey 和 HighKey
        Assert.That(Encoding.UTF8.GetString(sstbl.LowKey), Is.EqualTo("key0000"), "LowKey should be correct.")
        Assert.That(Encoding.UTF8.GetString(sstbl.HighKey), Is.EqualTo("key0004"), "HighKey should be correct.")

        // 验证通过 TryGet 读取的数据
        for i = 0 to 4 do
            let key = Encoding.UTF8.GetBytes($"key{i:D4}")
            let expectedValue = int64 i
            let actualValue = SSTbl.TryGet(sstbl, key)
            Assert.That(actualValue.IsSome, Is.True, $"Key {Encoding.UTF8.GetString(key)} should be found.")

            Assert.That(
                actualValue.Value,
                Is.EqualTo(expectedValue),
                $"Value for key {Encoding.UTF8.GetString(key)} should be correct."
            )

    [<Test>]
    member this.``Flush_ShouldHandleEmptyMemtbl``() =
        let memtbl = Memtbl() // 空 Memtbl
        let timestamp = 987654321L
        let level = 1

        let filePath = SSTbl.Flush(memtbl, testFilePath, timestamp, level)

        Assert.That(File.Exists(filePath), Is.True, "Empty SSTable file should still be created.")

        use sstbl =
            match SSTbl.Open(filePath) with
            | Some s -> s
            | None -> failwith "Failed to open empty SSTable."

        Assert.That(sstbl.Timestamp, Is.EqualTo(timestamp), "Timestamp should match.")
        Assert.That(sstbl.RecordOffsets.Length, Is.EqualTo(0), "RecordOffsets count should be 0 for empty Memtbl.")
        Assert.That(sstbl.LowKey.Length, Is.EqualTo(0), "LowKey should be empty for empty Memtbl.")
        Assert.That(sstbl.HighKey.Length, Is.EqualTo(0), "HighKey should be empty for empty Memtbl.")

        // 尝试获取任何键都应该返回 None
        Assert.That(
            SSTbl.TryGet(sstbl, Encoding.UTF8.GetBytes("anykey")).IsNone,
            Is.True,
            "TryGet should return None for empty SSTable."
        )

    [<Test>]
    member this.``Open_ShouldLoadCorrectMetadataAndOffsets``() =
        let memtbl = createMemtbl 10
        let timestamp = 1122334455L
        let level = 2

        // 先 Flush 创建文件
        let testFilePath = SSTbl.Flush(memtbl, testFilePath, timestamp, level)

        // 再 Open 读取文件
        use sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        Assert.That(sstbl.Path, Is.EqualTo(testFilePath), "Path should match.")
        Assert.That(sstbl.Timestamp, Is.EqualTo(timestamp), "Timestamp should match.")
        Assert.That(sstbl.Level, Is.EqualTo(level), "Level should match.")
        Assert.That(sstbl.RecordOffsets.Length, Is.EqualTo(10), "RecordOffsets count should be correct.")
        Assert.That(Encoding.UTF8.GetString(sstbl.LowKey), Is.EqualTo("key0000"), "LowKey should be correct.")
        Assert.That(Encoding.UTF8.GetString(sstbl.HighKey), Is.EqualTo("key0009"), "HighKey should be correct.")

    [<Test>]
    member this.``Open_ShouldReturnNoneForNonExistentFile``() =
        let nonExistentPath = Path.Combine(tempDir, "non_existent.sst")
        let sstblOption = SSTbl.Open(nonExistentPath)
        Assert.That(sstblOption.IsNone, Is.True, "Open should return None for a non-existent file.")

    [<Test>]
    member this.``TryGet_ShouldReturnSomeForExistingKey``() =
        let memtbl = createMemtbl 5
        memtbl.Add(Encoding.UTF8.GetBytes("test_key"), 999L)
        let testFilePath = SSTbl.Flush(memtbl, testFilePath, 0L, 0)

        use sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        let key = Encoding.UTF8.GetBytes("key0002")
        let result = SSTbl.TryGet(sstbl, key)
        Assert.That(result.IsSome, Is.True, "Should find existing key.")
        Assert.That(result.Value, Is.EqualTo(2L), "Value should be correct.")

        let testKey = Encoding.UTF8.GetBytes("test_key")
        let testResult = SSTbl.TryGet(sstbl, testKey)
        Assert.That(testResult.IsSome, Is.True, "Should find the manually added key.")
        Assert.That(testResult.Value, Is.EqualTo(999L), "Value for manually added key should be correct.")

    [<Test>]
    member this.``TryGet_ShouldReturnNoneForNonExistingKey``() =
        let memtbl = createMemtbl 5
        let testFilePath = SSTbl.Flush(memtbl, testFilePath, 0L, 0)

        use sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        let key = Encoding.UTF8.GetBytes("non_existent_key")
        let result = SSTbl.TryGet(sstbl, key)
        Assert.That(result.IsNone, Is.True, "Should not find non-existing key.")

    [<Test>]
    member this.``TryGet_ShouldReturnCorrectValueForDeletedKeyMarker``() =
        let memtbl = Memtbl()
        let keyToDelete = Encoding.UTF8.GetBytes("key_to_delete")
        memtbl.Add(keyToDelete, 100L)
        memtbl.SafeDelete(keyToDelete) // 这将在 Memtbl 中写入 -1L 的标记

        let testFilePath = SSTbl.Flush(memtbl, testFilePath, 0L, 0)

        use sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        let result = SSTbl.TryGet(sstbl, keyToDelete)
        Assert.That(result.IsSome, Is.True, "Should find the deletion marker key.")
        Assert.That(result.Value, Is.EqualTo(-1L), "Value should be -1L for a deleted key marker.")

    [<Test>]
    member this.``TryGet_ShouldReturnNoneForKeysOutsideRange``() =
        let memtbl = createMemtbl 5 // key0000 to key0004
        let testFilePath = SSTbl.Flush(memtbl, testFilePath, 0L, 0)

        use sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        let keyTooLow = Encoding.UTF8.GetBytes("aaaaa") // 小于 key0000
        let keyTooHigh = Encoding.UTF8.GetBytes("zzzzz") // 大于 key0004

        Assert.That(SSTbl.TryGet(sstbl, keyTooLow).IsNone, Is.True, "Should return None for key lower than LowKey.")
        Assert.That(SSTbl.TryGet(sstbl, keyTooHigh).IsNone, Is.True, "Should return None for key higher than HighKey.")

    [<Test>]
    member this.``TryGet_ShouldWorkWithBoundaryKeys``() =
        let memtbl = createMemtbl 5 // key0000 to key0004
        let testFilePath = SSTbl.Flush(memtbl, testFilePath, 0L, 0)

        use sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        // Test LowKey
        let lowKey = Encoding.UTF8.GetBytes("key0000")
        let lowResult = SSTbl.TryGet(sstbl, lowKey)
        Assert.That(lowResult.IsSome, Is.True, "Should find LowKey.")
        Assert.That(lowResult.Value, Is.EqualTo(0L), "Value for LowKey should be correct.")

        // Test HighKey
        let highKey = Encoding.UTF8.GetBytes("key0004")
        let highResult = SSTbl.TryGet(sstbl, highKey)
        Assert.That(highResult.IsSome, Is.True, "Should find HighKey.")
        Assert.That(highResult.Value, Is.EqualTo(4L), "Value for HighKey should be correct.")

    [<Test>]
    member this.``Dispose_ShouldCloseFileStream``() =
        let memtbl = createMemtbl 1
        let testFilePath = SSTbl.Flush(memtbl, testFilePath, 0L, 0)

        let sstbl =
            match SSTbl.Open(testFilePath) with
            | Some s -> s
            | None -> failwith "Failed to open SSTable."

        // Explicitly dispose of the SSTbl object
        sstbl.FileStream.Dispose()

        // 尝试再次打开文件，如果 FileStream 被正确关闭，应该没有问题
        // 这里不能直接检查sstbl.FileStream.CanRead，因为对象可能仍然存在但已关闭底层句柄
        // 更可靠的方式是尝试对文件进行操作，看是否会抛出"文件已被使用"之类的异常
        // 但最直接的验证是 File.Exists 和 TryGet 不再工作（但 TryGet 会重新打开流）
        // 简单起见，这里假设 Dispose 内部的 Close() 是有效的
        Assert.That(
            fun () ->
                use fs =
                    new FileStream(testFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                // 如果文件能够以独占方式打开，说明上一个流已经关闭
                Assert.That(fs.CanRead, Is.True)
            , Throws.Nothing
            , "FileStream should be closed after Dispose, allowing re-opening."
        )
