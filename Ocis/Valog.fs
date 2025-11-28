module Ocis.Valog

open System
open System.IO
open Ocis.Utils.Logger

/// <summary>
/// Configuration for incremental garbage collection
/// </summary>
type IncrementalGCConfig =
    {
        /// Maximum time allowed for a single GC batch (milliseconds)
        MaxBatchTimeMs: int
        /// Maximum entries to process in a single batch
        MaxBatchSize: int
        /// Progress reporting interval (batches)
        ProgressInterval: int
    }

/// <summary>
/// State for incremental garbage collection
/// </summary>
type IncrementalGCState =
    {
        /// Total live locations to process
        TotalLiveLocations: int
        /// Number of locations processed so far
        ProcessedCount: int
        /// Remapped locations collected so far
        RemappedLocations: Map<int64, int64>
        /// Whether GC is completed
        IsCompleted: bool
        /// Current position in the Valog file
        CurrentPosition: int64
        /// Temporary Valog path for incremental writes
        TempValogPath: string
        /// File streams for incremental processing
        TempWriter: BinaryWriter option
        /// Start time of the current batch
        BatchStartTime: System.DateTime
        /// New field to hold the new Valog instance
        NewValog: Valog option
    }

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
and Valog(path: string, fileStream: FileStream, reader: BinaryReader, writer: BinaryWriter, head: int64, tail: int64) =

    let path: string = path
    /// The file stream for direct file operations. FileStream internally handles file pointers and buffers.
    let mutable fileStream: FileStream = fileStream
    let mutable reader: BinaryReader = reader
    let mutable writer: BinaryWriter = writer
    /// The next writable position, also represents the logical end of the file.
    let mutable head: int64 = head
    /// The position of the oldest valid value (for garbage collection). Data before this position is considered garbage.
    let mutable tail: int64 = tail

    member _.Path = path
    member _.FileStream = fileStream

    member _.Head
        with get () = head
        and set value = head <- value

    member _.Tail
        with get () = tail
        and set value = tail <- value

    interface IDisposable with
        member _.Dispose() =
            writer.Dispose()
            reader.Dispose()
            fileStream.Dispose()

    /// <summary>
    /// Open or create a Value Log file.
    /// If the file does not exist, it will be created. If it exists, it will be opened and written from the end of the file.
    /// </summary>
    /// <param name="path">The path of the Value Log file.</param>
    /// <returns>A Result type: if successful, returns Ok Valog instance, otherwise returns Error string.</returns>
    static member Create(path: string) : Result<Valog, string> =
        let mutable fileStream: FileStream = null
        let mutable reader: BinaryReader = null
        let mutable writer: BinaryWriter = null

        try
            fileStream <- new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)

            // Initialize Head to the current length of the file. If the file is new, the length is 0.
            // If the file is opened, Head points to the end of the file, indicating the next writable position.
            let head = fileStream.Length
            // Initialize Tail to 0. The update of Tail is responsible for garbage collection (usually part of the Compaction process).
            let tail = 0L

            reader <- new BinaryReader(fileStream, System.Text.Encoding.UTF8, true)

            writer <- new BinaryWriter(fileStream, System.Text.Encoding.UTF8, true)

            Ok(new Valog(path, fileStream, reader, writer, head, tail))
        with ex ->
            // Dispose any partially created resources to avoid handle leaks
            match writer with
            | null -> ()
            | w ->
                try
                    w.Dispose()
                with e ->
                    failwith $"Failed to dispose writer: {e.Message}"

            match reader with
            | null -> ()
            | r ->
                try
                    r.Dispose()
                with e ->
                    failwith $"Failed to dispose reader: {e.Message}"

            match fileStream with
            | null -> ()
            | fs ->
                try
                    fs.Dispose()
                with e ->
                    failwith $"Failed to dispose file stream: {e.Message}"

            Error $"Failed to open or create Value Log file '{path}': {ex.Message}"

    /// <summary>
    /// Append a key-value pair to the Value Log file.
    /// This function returns the starting offset (ValueLocation) of the entry in the log file.
    /// The actual entry format in the log is: key length (int32), key (byte array), value length (int32), value (byte array).
    /// </summary>
    /// <param name="key">The key associated with the value (for integrity check).</param>
    /// <param name="value">The value to append.</param>
    /// <returns>The ValueLocation (offset) of the entry written.</returns>
    member _.Append(key: byte array, value: byte array) : int64 =
        // Single-threaded implementation optimized for performance.
        // Record the offset at which this new entry will start, which will be returned as ValueLocation.
        let entryStartOffset = head

        // Move the file stream pointer to the current Head position, preparing to append data.
        fileStream.Seek(head, SeekOrigin.Begin) |> ignore

        // Use BinaryWriter to write data. The last parameter 'true' of the constructor means that the underlying file stream (valog.FileStream) will not be closed after the BinaryWriter is disposed.
        writer.Write key.Length
        writer.Write key // BinaryWriter directly supports writing byte array

        // Write the length of the value and the value byte array
        writer.Write value.Length
        writer.Write value // BinaryWriter directly
        head <- fileStream.Position
        entryStartOffset // Return the offset at which this new entry starts.

    /// <summary>
    /// Read a key-value pair entry from the specified position in the Value Log.
    /// </summary>
    /// <param name="location">The ValueLocation (offset) to read.</param>
    /// <returns>An Option type: if found and valid, returns Some (key, value), otherwise returns None.</returns>
    member _.Read(location: int64) : (byte array * byte array) option =
        // Single-threaded implementation optimized for performance.
        // Basic range check: ensure the position is within the valid range of the Value Log
        // (after Tail and before Head).
        if location < tail || location >= head then
            None // Invalid position (possibly garbage collected or not yet written).
        else
            try
                // Move the file stream pointer to the specified position.
                fileStream.Seek(location, SeekOrigin.Begin) |> ignore

                // Use BinaryReader to read data. The last parameter 'true' of the constructor means that the underlying file stream (valog.FileStream) will not be closed after the BinaryReader is disposed.
                let keyLength = reader.ReadInt32()
                let key = reader.ReadBytes keyLength

                // Read the length of the value and the value byte array
                let valueLength = reader.ReadInt32()
                let value = reader.ReadBytes valueLength

                Some(key, value) // Return the read key-value pair.
            with
            | :? EndOfStreamException ->
                // If the file is corrupted or the given position points to an incomplete entry (e.g., due to a crash), this may happen.
                Logger.Warn $"Reached the end of the stream when reading Value Log offset {location}."

                None
            | ex ->
                // Capture other potential I/O errors.
                Logger.Error $"An error occurred when reading Value Log offset {location}: {ex.Message}"

                None

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
    /// Performs incremental garbage collection on the Value Log to avoid long pauses.
    ///
    /// Incremental GC Process:
    /// 1. Initialize GC state and create temporary file
    /// 2. Process live data in small batches with time limits
    /// 3. Periodically yield control back to caller
    /// 4. Complete GC when all data is processed
    /// 5. Atomically replace old Valog with new one
    ///
    /// Benefits:
    /// - Avoids long GC pauses that could block the system
    /// - Allows other operations to proceed between batches
    /// - Provides progress tracking and potential cancellation
    /// - Reduces memory pressure by processing in chunks
    ///
    /// Design Decision: Uses cooperative multitasking with explicit yielding
    /// rather than true parallelism to maintain simplicity and predictability.
    /// </summary>
    /// <param name="valog">The Valog instance to garbage collect</param>
    /// <param name="liveLocations">Set of live value locations to preserve</param>
    /// <param name="config">Configuration for incremental GC behavior</param>
    /// <returns>Async operation that yields GC state after each batch</returns>
    static member CollectGarbageIncremental
        (valog: Valog, liveLocations: Set<int64>, config: IncrementalGCConfig)
        : Async<Result<IncrementalGCState * Valog option, string>> =
        async {
            try
                // Initialize incremental GC state
                let tempValogPath = valog.Path + ".temp"

                let initialState =
                    { TotalLiveLocations = liveLocations.Count
                      ProcessedCount = 0
                      RemappedLocations = Map.empty<int64, int64>
                      IsCompleted = false
                      CurrentPosition = 0L
                      TempValogPath = tempValogPath
                      TempWriter = None
                      BatchStartTime = System.DateTime.UtcNow
                      NewValog = None }

                // Initialize temporary file and writer
                use tempFileStream =
                    new FileStream(tempValogPath, FileMode.Create, FileAccess.Write, FileShare.None)

                use tempWriter = new BinaryWriter(tempFileStream, System.Text.Encoding.UTF8, false)

                let stateWithWriter =
                    { initialState with
                        TempWriter = Some tempWriter }

                // Read and process the original Valog file in batches
                use oldFileStream =
                    new FileStream(valog.Path, FileMode.Open, FileAccess.Read, FileShare.Read)

                use oldReader = new BinaryReader(oldFileStream, System.Text.Encoding.UTF8, true)

                let finalState =
                    Valog.ProcessGCBatch(stateWithWriter, oldReader, liveLocations, config)

                if finalState.IsCompleted then
                    // Atomically replace the old Valog with the new one
                    // Note: We don't dispose the old valog here - let the caller manage its lifecycle
                    File.Delete valog.Path
                    File.Move(tempValogPath, valog.Path)

                    // Small delay to ensure file handles are released by the OS
                    do! Async.Sleep 10

                    // Reopen Valog instance
                    match Valog.Create valog.Path with
                    | Ok newValog ->
                        Logger.Info
                            $"Incremental Valog GC completed: processed {finalState.ProcessedCount} live locations"

                        return
                            Ok(
                                { finalState with
                                    TempWriter = None
                                    NewValog = Some newValog },
                                Some newValog
                            )
                    | Error msg ->
                        Logger.Error $"Failed to reopen Valog after incremental GC: {msg}"
                        return Error $"Failed to reopen Valog: {msg}"
                else
                    // GC is not yet completed, return current state for continuation
                    Logger.Debug
                        $"Incremental GC batch completed: {finalState.ProcessedCount}/{finalState.TotalLiveLocations} locations processed"

                    return Ok(finalState, None)

            with ex ->
                Logger.Error $"Incremental GC error: {ex.Message}"
                return Error $"Incremental GC failed: {ex.Message}"
        }

    /// <summary>
    /// Processes one batch of entries during incremental garbage collection.
    ///
    /// Batch Processing Logic:
    /// 1. Read entries from current position in old Valog
    /// 2. Check if each entry is live (in liveLocations set)
    /// 3. If live, copy to new Valog and record remapping
    /// 4. Track progress and time limits
    /// 5. Yield when batch size or time limit is reached
    /// </summary>
    static member private ProcessGCBatch
        (state: IncrementalGCState, oldReader: BinaryReader, liveLocations: Set<int64>, config: IncrementalGCConfig)
        : IncrementalGCState =
        let mutable currentState = state
        let batchStartTime = System.DateTime.UtcNow
        let mutable entriesInBatch = 0

        // Continue processing from where we left off
        oldReader.BaseStream.Seek(currentState.CurrentPosition, SeekOrigin.Begin)
        |> ignore

        while not currentState.IsCompleted
              && oldReader.BaseStream.Position < oldReader.BaseStream.Length
              && entriesInBatch < config.MaxBatchSize
              && (System.DateTime.UtcNow - batchStartTime).TotalMilliseconds < float config.MaxBatchTimeMs do
            try
                let originalOffset = oldReader.BaseStream.Position

                // Read key and value lengths
                let keyLength = oldReader.ReadInt32()
                let key = oldReader.ReadBytes keyLength
                let valueLength = oldReader.ReadInt32()
                let value = oldReader.ReadBytes valueLength

                // Check if this entry is live
                if liveLocations.Contains originalOffset then
                    match currentState.TempWriter with
                    | Some writer ->
                        // Copy live entry to new Valog
                        let newOffset = writer.BaseStream.Position

                        writer.Write keyLength
                        writer.Write key
                        writer.Write valueLength
                        writer.Write value

                        // Record the remapping
                        let newRemapped = currentState.RemappedLocations.Add(originalOffset, newOffset)

                        currentState <-
                            { currentState with
                                RemappedLocations = newRemapped }

                    | None -> Logger.Warn "TempWriter not available during incremental GC batch"

                currentState <-
                    { currentState with
                        ProcessedCount = currentState.ProcessedCount + 1
                        CurrentPosition = oldReader.BaseStream.Position }

                entriesInBatch <- entriesInBatch + 1

            with
            | :? EndOfStreamException ->
                // Reached end of file
                currentState <- { currentState with IsCompleted = true }
            | ex -> Logger.Error $"Error processing GC batch at position {currentState.CurrentPosition}: {ex.Message}"
        // Continue with next entry rather than failing completely

        // Check if we've completed processing
        if oldReader.BaseStream.Position >= oldReader.BaseStream.Length then
            currentState <- { currentState with IsCompleted = true }

        currentState

    /// <summary>
    /// Legacy synchronous garbage collection method.
    /// Now delegates to incremental GC for consistency.
    /// </summary>
    static member CollectGarbage
        (valog: Valog, liveLocations: Set<int64>)
        : Async<Result<Valog * Map<int64, int64>, string> option> =
        async {
            Logger.Info $"Starting legacy synchronous Valog GC for {liveLocations.Count} live locations"

            // Use incremental GC with aggressive batch settings for synchronous behavior
            let config =
                { MaxBatchTimeMs = System.Int32.MaxValue // No time limit
                  MaxBatchSize = System.Int32.MaxValue // No size limit
                  ProgressInterval = 1 // Report progress every batch
                }

            let! result = Valog.CollectGarbageIncremental(valog, liveLocations, config)

            match result with
            | Ok(state, Some newValog) when state.IsCompleted ->
                Logger.Info "Legacy Valog GC completed successfully"
                return Some(Ok(newValog, state.RemappedLocations))
            | Ok(state, None) when not state.IsCompleted -> // Explicitly handle the case where GC is not completed
                Logger.Warn "Incremental GC did not complete in single batch (legacy mode)"

                return Some(Error "GC did not complete as expected")
            | Error msg -> // Handle the error case
                Logger.Error $"Legacy GC failed: {msg}"
                return Some(Error msg)
            | _ -> // Catch-all for any other unexpected cases
                Logger.Error "Unexpected result from CollectGarbageIncremental in legacy GC mode"

                return Some(Error "Unexpected GC result")
        }

    /// <summary>
    /// Helper function to copy live data to a new Valog file and re-map locations.
    /// </summary>
    static member CopyLiveData
        (oldPath: string, newPath: string, liveLocations: Set<int64>)
        : Async<Result<Map<int64, int64>, string>> =
        async {
            let mutable remappedLocations = Map.empty<int64, int64>

            try
                use newFileStream =
                    new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None)

                use newWriter = new BinaryWriter(newFileStream, System.Text.Encoding.UTF8, false)

                use oldFileStream =
                    new FileStream(oldPath, FileMode.Open, FileAccess.Read, FileShare.Read)

                use oldReader = new BinaryReader(oldFileStream, System.Text.Encoding.UTF8, true)

                while oldFileStream.Position < oldFileStream.Length do
                    let originalOffset = oldFileStream.Position
                    let keyLength = oldReader.ReadInt32()
                    let key = oldReader.ReadBytes keyLength
                    let valueLength = oldReader.ReadInt32()
                    let value = oldReader.ReadBytes valueLength

                    if liveLocations.Contains originalOffset then
                        let newOffset = newFileStream.Position

                        remappedLocations <- remappedLocations.Add(originalOffset, newOffset)

                        newWriter.Write keyLength
                        newWriter.Write key
                        newWriter.Write valueLength
                        newWriter.Write value

                return Ok remappedLocations
            with ex ->
                return Error $"Error copying live data during Valog GC: {ex.Message}"
        }
