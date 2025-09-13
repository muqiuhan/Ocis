module Ocis.SSTbl

open Ocis.Memtbl
open Ocis.Utils.ByteArrayComparer
open Ocis.Utils.Serialization
open Ocis.ValueLocation
open System.IO
open System.Collections.Generic
open System.Collections
open Ocis.Utils.Logger

/// FileStream needs to be closed and released correctly after use (using the use keyword).
/// RecordOffsets is a sparse index in memory, pointing to the starting position of each key-value pair in the SSTable file.
/// LowKey and HighKey are used to quickly determine if a key may exist in this SSTable, reducing unnecessary disk lookups.
type SSTbl
  (
    path : string,
    timestamp : int64,
    level : int,
    fileStream : FileStream,
    reader : BinaryReader,
    recordOffsets : int64 array,
    lowKey : byte array,
    highKey : byte array
  )
  =

  member _.Path = path
  member _.Timestamp = timestamp
  member _.Level = level
  member _.FileStream = fileStream
  member _.Reader = reader
  member _.RecordOffsets = recordOffsets
  member _.LowKey = lowKey
  member _.HighKey = highKey
  member _.Size = fileStream.Length

  /// <summary>
  /// Checks if the key range of this SSTable overlaps with another SSTable.
  /// </summary>
  /// <param name="other">The other SSTable to compare with.</param>
  /// <returns>True if the key ranges overlap, false otherwise.</returns>
  member this.Overlaps (other : SSTbl) : bool =
    let comparer = ByteArrayComparer.ComparerInstance
    // Check for overlap: (this.LowKey <= other.HighKey AND this.HighKey >= other.LowKey)
    // OR (other.LowKey <= this.HighKey AND other.HighKey >= this.LowKey)
    let noOverlap =
      (comparer.Compare (this.HighKey, other.LowKey) < 0)
      || (comparer.Compare (other.HighKey, this.LowKey) < 0)

    not noOverlap

  // Implement IDisposable interface, ensure that the FileStream is closed when the SSTbl object is no longer used.
  interface System.IDisposable with
    member this.Dispose () =
      this.Reader.Dispose ()
      this.FileStream.Dispose ()

  // Implement IEnumerable<KeyValuePair<byte array, ValueLocation>>
  interface IEnumerable<KeyValuePair<byte array, ValueLocation>> with
    member this.GetEnumerator () =
      // Helper function to read a key-value pair from the stream at a given offset
      let readKeyValuePair
        (stream : FileStream, reader : BinaryReader, offset : int64)
        : KeyValuePair<byte array, ValueLocation>
        =
        lock stream (fun () ->
          stream.Seek (offset, SeekOrigin.Begin) |> ignore
          // Use BinaryReader to read data. The last parameter 'true' of the constructor means that the underlying file stream (valog.FileStream) will not be closed after the BinaryReader is disposed.

          let key = Serialization.readByteArray reader
          let valueLocation = Serialization.readValueLocation reader
          KeyValuePair<byte array, ValueLocation> (key, valueLocation))

      // Create an enumerator that reads key-value pairs from the SSTable file
      (seq {
        for offset in this.RecordOffsets do
          yield readKeyValuePair (this.FileStream, this.Reader, offset)
      })
        .GetEnumerator ()

  // Implement Collections.IEnumerable (non-generic version)
  interface IEnumerable with
    member this.GetEnumerator () =
      (this :> IEnumerable<KeyValuePair<byte array, ValueLocation>>)
        .GetEnumerator ()
      :> IEnumerator

  /// <summary>
  /// Flush the data from Memtbl to a new SSTable file.
  /// </summary>
  /// <param name="memtbl">The Memtbl instance to flush.</param>
  /// <param name="path">The path of the new SSTable file.</param>
  /// <param name="timestamp">The creation timestamp of the SSTable.</param>
  /// <param name="level">The compression level of the SSTable.</param>
  /// <returns>The path of the created SSTable file.</returns>
  static member Flush
    (memtbl : Memtbl, path : string, timestamp : int64, level : int)
    : string
    =
    use fileStream =
      new FileStream (path, FileMode.Create, FileAccess.Write, FileShare.None)

    use writer = new BinaryWriter (fileStream)

    // Pre-allocate capacity based on memtbl size to reduce allocations
    let memtblCount = memtbl.Count
    let recordOffsets = Array.zeroCreate<int64> memtblCount
    let mutable recordIndex = 0
    let mutable currentLowKey : byte array = null
    let mutable currentHighKey : byte array = null

    // 1. Write data block (Data Block)
    memtbl
    |> Seq.iter (fun (KeyValue (key, valueLocation)) ->
      if currentLowKey = null then // Record the first key as LowKey
        currentLowKey <- key

      currentHighKey <- key // Update HighKey to the current key on each iteration

      recordOffsets[recordIndex] <- fileStream.Position // Record the starting offset of the current key-value pair in the file
      recordIndex <- recordIndex + 1
      Serialization.writeByteArray writer key
      Serialization.writeValueLocation writer valueLocation)

    // Handle empty Memtbl cases, ensure LowKey and HighKey are not null
    let actualLowKey = currentLowKey |> Option.ofObj |> Option.defaultValue [||]

    let actualHighKey =
      currentHighKey |> Option.ofObj |> Option.defaultValue [||]

    // 2. Write index block (Index Block)
    // The starting offset of the index block
    let indexBlockStartOffset = fileStream.Position

    for offset in recordOffsets do
      writer.Write offset // Write the offset of each key-value pair

    // 3. Write footer/metadata block (Footer/Metadata Block)
    // The starting offset of the footer
    let footerStartOffset = fileStream.Position
    writer.Write timestamp
    writer.Write level
    Serialization.writeByteArray writer actualLowKey
    Serialization.writeByteArray writer actualHighKey
    writer.Write indexBlockStartOffset // Write the starting offset of the index block
    writer.Write recordIndex // Write the number of entries in the index block
    let footerSize = fileStream.Position - footerStartOffset // Calculate the total size of the footer
    writer.Write (footerSize |> int32) // Write the total size of the footer (int32)

    path // Return the path of the generated SSTable file

  /// <summary>
  /// Open an existing SSTable file and load its metadata and in-memory index.
  /// </summary>
  /// <param name="path">The path of the SSTable file.</param>
  /// <returns>An Option type: Some SSTbl if successful, otherwise None.</returns>
  static member Open (path : string) : SSTbl option =
    let mutable fileStream : FileStream = null // Declare a mutable variable to close the stream in case of an exception
    let mutable reader : BinaryReader = null

    try
      fileStream <-
        new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read)
      // BinaryReader uses the leaveOpen: true parameter to ensure that the FileStream is not closed when the BinaryReader is disposed
      reader <- new BinaryReader (fileStream, System.Text.Encoding.UTF8, true)

      // 1. Read footer (Footer)
      // First, move the file pointer back 4 bytes to read the footerSize (int32) at the end of the file
      fileStream.Seek (-4L, SeekOrigin.End) |> ignore
      let footerSize = reader.ReadInt32 () // Read the size of the footer content (excluding footerSize itself)

      // Now, move the file pointer back to the starting position of the footer content (timestamp).
      // The total amount of backtracking is footerSize + 4 bytes occupied by footerSize itself.
      fileStream.Seek (-(footerSize + 4 |> int64), SeekOrigin.End) |> ignore

      let timestamp = reader.ReadInt64 ()
      let level = reader.ReadInt32 ()
      let lowKey = Serialization.readByteArray reader
      let highKey = Serialization.readByteArray reader
      let indexBlockStartOffset = reader.ReadInt64 ()
      let indexEntriesCount = reader.ReadInt32 ()
      reader.ReadInt32 () |> ignore // Read and discard the footerSize field that we have already read

      // 2. Read index block (Index Block)
      fileStream.Seek (indexBlockStartOffset, SeekOrigin.Begin) |> ignore // Jump to the starting position of the index block
      let recordOffsets = Array.zeroCreate<int64> indexEntriesCount

      for i = 0 to indexEntriesCount - 1 do
        recordOffsets[i] <- reader.ReadInt64 ()

      Some (
        new SSTbl (
          path,
          timestamp,
          level,
          fileStream,
          reader,
          recordOffsets,
          lowKey,
          highKey
        )
      )
    with
    | :? FileNotFoundException ->
      if reader <> null then reader.Dispose ()

      if fileStream <> null then fileStream.Dispose () // Ensure the stream is closed when the file is not found

      None
    | ex ->
      Logger.Error $"Error opening SSTable '{path}': {ex.Message}"

      if reader <> null then reader.Dispose ()

      if fileStream <> null then fileStream.Dispose () // Ensure the stream is closed when other exceptions occur

      None

  /// <summary>
  /// Get the value location of the given key from SSTbl.
  /// </summary>
  /// <param name="sstbl">The SSTbl instance.</param>
  /// <param name="key">The key to search for.</param>
  /// <returns>
  /// An Option type: Some ValueLocation if found, otherwise None.
  /// </returns>
  member _.TryGet (key : byte array) : ValueLocation option =
    if recordOffsets.Length = 0 then
      None // Empty SSTable

    // Quick range check, if the key is outside the range of SSTable's LowKey and HighKey, return None
    elif
      ByteArrayComparer.ComparerInstance.Compare (key, lowKey) < 0
      || ByteArrayComparer.ComparerInstance.Compare (key, highKey) > 0
    then
      None

    else
      // Binary search on the RecordOffsets array in memory.
      // Since RecordOffsets only stores offsets, we need to read the actual key from the file for each probe.
      let mutable low = 0
      let mutable high = recordOffsets.Length - 1
      let mutable found = false
      let mutable valueLocation = -1L

      while low <= high && not found do
        let midIdx = low + (high - low) / 2
        let currentOffset = recordOffsets[midIdx]

        // Move the read/write position of the FileStream to the current offset
        fileStream.Seek (currentOffset, SeekOrigin.Begin) |> ignore
        // Use BinaryReader to read data, with leaveOpen: true to ensure the underlying FileStream is not closed
        let currentKey = Serialization.readByteArray reader // Read the key at the current position
        let cmp = ByteArrayComparer.ComparerInstance.Compare (key, currentKey) // Compare the target key and the current key

        if cmp = 0 then
          // Found an exact match
          valueLocation <- Serialization.readValueLocation reader // Read the corresponding value location
          found <- true
        elif cmp < 0 then
          high <- midIdx - 1 // Target key is less than current key, continue searching in the left half
        else
          low <- midIdx + 1 // Target key is greater than current key, continue searching in the right half

      if found then Some valueLocation else None

  /// <summary>
  /// Close the underlying file stream of the SSTbl instance.
  /// It is recommended to use the `use` keyword and the `IDisposable` implementation of SSTbl to automatically manage the lifecycle of the stream.
  /// </summary>
  member this.Close () : unit = this.FileStream.Close ()
