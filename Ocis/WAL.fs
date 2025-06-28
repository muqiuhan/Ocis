module Ocis.WAL

open System
open System.IO
open Ocis.ValueLocation
open Ocis.Utils.Serialization

type WalEntry =
    | Set of byte array * ValueLocation
    | Delete of byte array

/// <summary>
/// Wal (Write Ahead Log) records all modifications to Memtbl to ensure durability and crash recovery.
/// </summary>
/// <remarks>
/// All write operations must first be recorded in the WAL before being applied to Memtbl. If the system crashes,
/// the WAL can be replayed to restore Memtbl's state.
/// </remarks>
type Wal(path: string, fileStream: FileStream) =

    let path: string = path
    let fileStream: FileStream = fileStream

    member _.Path = path
    member _.FileStream = fileStream

    // Implements IDisposable to ensure the FileStream is properly closed
    interface System.IDisposable with
        member this.Dispose() = this.FileStream.Dispose()

    /// <summary>
    /// Opens or creates a WAL file.
    /// </summary>
    /// <param name="path">The path of the WAL file.</param>
    /// <returns>A Result type: returns Ok Wal instance if successful, otherwise returns an Error string.</returns>
    static member Create(path: string) : Result<Wal, string> =
        try
            let fileStream =
                new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)
            // Move the file pointer to the end of the file, ready to append new log entries
            fileStream.Seek(0L, SeekOrigin.End) |> ignore
            Ok(new Wal(path, fileStream))
        with ex ->
            Error(sprintf "Failed to open or create WAL file '%s': %s" path ex.Message)

    /// <summary>
    /// Appends a WalEntry to the WAL file.
    /// The format of each WalEntry depends on its type (Set or Delete).
    /// </summary>
    /// <param name="entry">The WalEntry to append.</param>
    member _.Append(entry: WalEntry) : unit =
        lock fileStream (fun () ->
            use writer = new BinaryWriter(fileStream, System.Text.Encoding.UTF8, true) // true means the underlying stream will not be closed

            match entry with
            | WalEntry.Set(key, valueLocation) ->
                writer.Write(0uy) // 0 indicates Set type
                Serialization.writeByteArray writer key
                Serialization.writeValueLocation writer valueLocation
            | WalEntry.Delete(key) ->
                writer.Write(1uy) // 1 indicates Delete type
                Serialization.writeByteArray writer key

        // fileStream.Flush() // Ensure data is written to disk (Moved to OcisDB for batched flushing)
        )

    /// <summary>
    /// Reads and replays all WalEntries from the WAL file.
    /// This method is used to reconstruct Memtbl during crash recovery.
    /// </summary>
    /// <param name="path">The path of the WAL file.</param>
    /// <returns>A sequence containing all WalEntries.</returns>
    static member Replay(path: string) : seq<WalEntry> =
        seq {
            if not (File.Exists(path)) then
                yield! Seq.empty
            else
                try
                    use fileStream =
                        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)

                    use reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, true)

                    while fileStream.Position < fileStream.Length do
                        let entryType = reader.ReadByte()

                        match entryType with
                        | 0uy -> // Set type
                            let key = Serialization.readByteArray reader
                            let valueLocation = Serialization.readValueLocation reader
                            yield WalEntry.Set(key, valueLocation)
                        | 1uy -> // Delete type
                            let key = Serialization.readByteArray reader
                            yield WalEntry.Delete(key)
                        | _ ->
                            // Encountered unknown or corrupted entry, skip or log error
                            printfn $"Warning: Encountered unknown WAL entry type {int entryType} in WAL file '{path}'."
                // Attempt to skip the current entry, but for simplicity, we break here. A more robust skipping logic is needed in production systems.
                with
                | :? EndOfStreamException ->
                    // File might be incomplete, or reached end of stream
                    printfn
                        $"Warning: Reached end of stream when replaying WAL file '{path}', file might be incomplete."
                | ex -> printfn $"Error: An error occurred when replaying WAL file '{path}': {ex.Message}"
        }

    /// <summary>
    /// Forces any buffered write data to be written to the underlying file, and then to the device.
    /// This is crucial for data persistence.
    /// </summary>
    member _.Flush() : unit = fileStream.Flush()

    /// <summary>
    /// Closes the WAL file stream.
    /// It is recommended to use the 'use' keyword with the Wal type, as it implements IDisposable.
    /// </summary>
    member _.Close() : unit = fileStream.Close()


    /// <summary>
    /// Explicitly dispose the WAL.
    /// </summary>
    /// <remarks>
    /// This is an internal method that is used to dispose the WAL explicitly.
    /// It is recommended to use the 'use' keyword with the Wal type, as it implements IDisposable.
    /// </remarks>
    [<Obsolete "This function should not be called directly.">]
    member inline this.Dispose() = (this :> IDisposable).Dispose()
