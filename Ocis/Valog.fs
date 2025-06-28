module Ocis.Valog

open System
open System.IO
open Ocis.ValueLocation

/// <summary>
/// Valog (Value Log) represents the append-only log file in WiscKey storage engine that stores actual values.
/// It is the core part of the key-value separation design, and all values are written to this file in order.
/// </summary>
/// <remarks>
/// Valog file is append-only, aiming to utilize the sequential write performance of SSD.
/// Head points to the next writable position.
/// Tail points to the oldest valid value (for garbage collection).
/// Each entry contains the key and its length, so that it can be verified when reading.
/// </remarks>
type Valog(path: string, fileStream: FileStream, head: int64, tail: int64) =

    let path: string = path
    /// The file stream for direct file operations. FileStream internally handles file pointers and buffers.
    let mutable fileStream: FileStream = fileStream
    /// The next writable position, also represents the logical end of the file.
    let mutable head: int64 = head
    /// The position of the oldest valid value (for garbage collection). Data before this position is considered garbage.
    let mutable tail: int64 = tail

    member _.Path = path
    member _.FileStream = fileStream

    member private _.SetFileStream(fs) = fileStream <- fs

    member _.Head
        with get () = head
        and set value = head <- value

    member _.Tail
        with get () = tail
        and set value = tail <- value

    interface IDisposable with
        member _.Dispose() = fileStream.Dispose()

    /// <summary>
    /// Open or create a Value Log file.
    /// If the file does not exist, it will be created. If it exists, it will be opened and written from the end of the file.
    /// </summary>
    /// <param name="path">The path of the Value Log file.</param>
    /// <returns>A Result type: if successful, returns Ok Valog instance, otherwise returns Error string.</returns>
    static member Create(path: string) : Result<Valog, string> =
        try
            // FileMode.OpenOrCreate: if the file exists, it will be opened. Otherwise, a new file will be created.
            // FileAccess.ReadWrite: read and write permissions are required.
            // FileShare.ReadWrite: allows other processes or threads to share read and write files, and a lock mechanism is needed to coordinate when accessed concurrently.
            let fileStream =
                new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)

            // Initialize Head to the current length of the file. If the file is new, the length is 0.
            // If the file is opened, Head points to the end of the file, indicating the next writable position.
            let head = fileStream.Length
            // Initialize Tail to 0. The update of Tail is responsible for garbage collection (usually part of the Compaction process).
            let tail = 0L

            Ok(new Valog(path, fileStream, head, tail))
        with ex ->
            Error(sprintf "Failed to open or create Value Log file '%s': %s" path ex.Message)

    /// <summary>
    /// Append a key-value pair to the Value Log file.
    /// This function returns the starting offset (ValueLocation) of the entry in the log file.
    /// The actual entry format in the log is: key length (int32), key (byte array), value length (int32), value (byte array).
    /// </summary>
    /// <param name="key">The key associated with the value (for integrity check).</param>
    /// <param name="value">The value to append.</param>
    /// <returns>The ValueLocation (offset) of the entry written.</returns>
    member _.Append(key: byte array, value: byte array) : int64 =
        // Use a lock to ensure that the write operation to the file stream is thread-safe, maintaining the consistency of the Head offset.
        lock fileStream (fun () ->
            // Record the offset at which this new entry will start, which will be returned as ValueLocation.
            let entryStartOffset = head

            // Move the file stream pointer to the current Head position, preparing to append data.
            fileStream.Seek(head, SeekOrigin.Begin) |> ignore

            // Use BinaryWriter to write data. The last parameter 'true' of the constructor means that the underlying file stream (valog.FileStream) will not be closed after the BinaryWriter is disposed.
            use writer = new BinaryWriter(fileStream, System.Text.Encoding.UTF8, true)

            // Write the length of the key and the key byte array
            writer.Write(key.Length)
            writer.Write(key) // BinaryWriter directly supports writing byte array

            // Write the length of the value and the value byte array
            writer.Write(value.Length)
            writer.Write(value) // BinaryWriter directly
            head <- fileStream.Position
            entryStartOffset // Return the offset at which this new entry starts.
        )

    /// <summary>
    /// Read a key-value pair entry from the specified position in the Value Log.
    /// </summary>
    /// <param name="location">The ValueLocation (offset) to read.</param>
    /// <returns>An Option type: if found and valid, returns Some (key, value), otherwise returns None.</returns>
    member _.Read(location: int64) : (byte array * byte array) option =
        // Use a lock to ensure that the read operation to the file stream is thread-safe, preventing race conditions of the file pointer when accessed concurrently.
        lock fileStream (fun () ->
            // Basic range check: ensure the position is within the valid range of the Value Log
            // (after Tail and before Head).
            if location < tail || location >= head then
                None // Invalid position (possibly garbage collected or not yet written).
            else
                try
                    // Move the file stream pointer to the specified position.
                    fileStream.Seek(location, SeekOrigin.Begin) |> ignore

                    // Use BinaryReader to read data. The last parameter 'true' of the constructor means that the underlying file stream (valog.FileStream) will not be closed after the BinaryReader is disposed.
                    use reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, true)

                    // Read the length of the key and the key byte array
                    let keyLength = reader.ReadInt32()
                    let key = reader.ReadBytes(keyLength)

                    // Read the length of the value and the value byte array
                    let valueLength = reader.ReadInt32()
                    let value = reader.ReadBytes(valueLength)

                    Some(key, value) // Return the read key-value pair.
                with
                | :? EndOfStreamException ->
                    // If the file is corrupted or the given position points to an incomplete entry (e.g., due to a crash), this may happen.
                    printfn $"Warning: Reached the end of the stream when reading Value Log offset {location}."
                    None
                | ex ->
                    // Capture other potential I/O errors.
                    printfn $"Error: An error occurred when reading Value Log offset {location}: {ex.Message}"
                    None)

    /// <summary>
    /// Force any buffered write data to be written to the underlying file, and then to the device.
    /// This is crucial for data persistence.
    /// </summary>
    member _.Flush() : unit =
        // Usually, to ensure data persistence to stable storage, flush needs to be followed by fsync.
        // However, the WiscKey paper mentions that WAL provides atomicity and consistency, and Value Log writes can be buffered.
        // Here, a complete fsync may offset the performance advantage of buffering.
        // For simplicity and
        fileStream.Flush()

    /// <summary>
    /// Close the Value Log file stream.
    /// It is recommended to use the 'use' keyword with the Valog type, as it implements IDisposable.
    /// </summary>
    member _.Close() : unit = fileStream.Close()

    /// <summary>
    /// Explicitly dispose the Value Log.
    /// </summary>
    /// <remarks>
    /// This is an internal method that is used to dispose the Value Log explicitly.
    /// It is recommended to use the 'use' keyword with the Valog type, as it implements IDisposable.
    /// </remarks>
    [<Obsolete "This function should not be called directly.">]
    member inline this.Dispose() = (this :> IDisposable).Dispose()

    /// <summary>
    /// Performs garbage collection on the Value Log.
    /// This is a simplified placeholder. A full implementation would involve:
    /// 1. Identifying all live value locations across all SSTables and Memtables.
    /// 2. Copying live data to a new Valog file.
    /// 3. Deleting the old Valog file and renaming the new one.
    /// 4. Updating SSTable value locations if necessary (highly complex).
    /// </summary>
    /// <param name="valog">The Valog instance.</param>
    /// <param name="liveLocations">A set of ValueLocations that are currently live across all SSTables and Memtables.</param>
    static member CollectGarbage
        (valog: Valog, liveLocations: Set<int64>)
        : Async<Result<Valog * Map<int64, int64>, string> option> =
        async {
            printfn $"Valog GC: Started. Received {liveLocations.Count} live locations."

            let tempValogPath = valog.Path + ".temp"
            let mutable remappedLocations = Map.empty<int64, int64>

            try
                // Close the old file stream before moving/deleting it
                valog.Close()

                use newFileStream =
                    new FileStream(tempValogPath, FileMode.Create, FileAccess.Write, FileShare.None)

                use newWriter = new BinaryWriter(newFileStream, System.Text.Encoding.UTF8, false) // False means BinaryWriter owns the stream

                // Reopen the old Valog file for reading to copy live data
                use oldFileStream =
                    new FileStream(valog.Path, FileMode.Open, FileAccess.Read, FileShare.Read)

                use oldReader = new BinaryReader(oldFileStream, System.Text.Encoding.UTF8, true)

                while oldFileStream.Position < oldFileStream.Length do
                    let originalOffset = oldFileStream.Position
                    let keyLength = oldReader.ReadInt32()
                    let key = oldReader.ReadBytes(keyLength)
                    let valueLength = oldReader.ReadInt32()
                    let value = oldReader.ReadBytes(valueLength)

                    if liveLocations.Contains(originalOffset) then
                        let newOffset = newFileStream.Position
                        remappedLocations <- remappedLocations.Add(originalOffset, newOffset)

                        newWriter.Write(keyLength)
                        newWriter.Write(key)
                        newWriter.Write(valueLength)
                        newWriter.Write(value)

                // Replace old valog with new one
                File.Delete(valog.Path)
                File.Move(tempValogPath, valog.Path)

                // Reopen the Valog instance with the new file to get its updated state
                match Valog.Create(valog.Path) with
                | Ok newValog ->
                    printfn "Valog GC: Successfully replaced Valog file."
                    return Some(Ok(newValog, remappedLocations))
                | Error msg ->
                    printfn $"Valog GC Error: Failed to re-open Valog after GC: {msg}"
                    return Some(Error msg)

            with ex ->
                printfn $"Valog GC Error: {ex.Message}"
                // Clean up temp file in case of error
                if File.Exists(tempValogPath) then
                    File.Delete(tempValogPath)

                return Some(Error ex.Message)
        }
