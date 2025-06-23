module Ocis.Memtbl

open System
open System.Collections.Generic

/// <summary>
/// The value location in Value Log (byte offset).
/// -1 is used to represent a deleted record.
/// </summary>
type ValueLocation = int64

/// <summary>
/// Custom byte array comparer.
/// F# Map requires a reliable comparer to correctly sort and find byte array keys,
/// because the default comparison is based on reference rather than content.
/// </summary>
type private ByteArrayComparer() =
    interface IComparer<byte array> with
        /// <summary>
        /// Compare two byte arrays.
        /// </summary>
        /// <param name="lhs">Left byte array.</param>
        /// <param name="rhs">Right byte array.</param>
        /// <returns>
        /// If lhs is less than rhs, return a negative number;
        /// If lhs is greater than rhs, return a positive number;
        /// If both are equal, return 0.
        /// </returns>
        member _.Compare(lhs: byte array, rhs: byte array) =
            // Compare length first, return result if different
            let lenCmp = lhs.Length.CompareTo(rhs.Length)

            if lenCmp <> 0 then
                lenCmp
            else
                // If length is the same, compare byte by byte
                // Use tail recursion to replace the loop with break
                let rec compareBytes (idx: int) =
                    if idx = lhs.Length then
                        0 // All bytes are the same, arrays are equal
                    else
                        let byteCmp = lhs.[idx].CompareTo(rhs.[idx])

                        if byteCmp <> 0 then
                            byteCmp // Found different byte, return comparison result
                        else
                            compareBytes (idx + 1) // Continue to compare next byte

                compareBytes 0 // Start from the first byte

/// <summary>
/// Memtbl (Memory Table) represents the memory buffer in WiscKey storage engine.
/// It stores the mapping from key to Value Log position, and automatically maintains the order of keys.
/// </summary>
/// <remarks>
/// According to the core idea of WiscKey paper (Lu et al. 2017), Memtbl only stores keys and value locations,
/// rather than actual values, to reduce memory usage and I/O amplification.
/// When Memtbl reaches a certain size, it will be "frozen" and flushed to SSTable on disk.
/// Reference: [https://www.usenix.org/system/files/conference/fast16/fast16-papers-lu.pdf]
/// </remarks>
type Memtbl() =
    let memtbl = SortedDictionary<byte array, ValueLocation>(new ByteArrayComparer())

    interface IEnumerable<KeyValuePair<byte array, ValueLocation>> with
        member this.GetEnumerator() =
            memtbl.GetEnumerator() :> IEnumerator<KeyValuePair<byte array, ValueLocation>>

    interface Collections.IEnumerable with
        member this.GetEnumerator() =
            memtbl.GetEnumerator() :> Collections.IEnumerator

    /// <summary>
    /// Get the value location of the given key from Memtbl.
    /// </summary>
    /// <param name="key">The key to find.</param>
    /// <returns>
    /// An Option type: Some ValueLocation if the key is found, otherwise None.
    /// This follows the idiomatic way of handling possible missing values in F#.
    /// </returns>
    member this.TryGet(key: byte array) : ValueLocation option =
        match memtbl.TryGetValue(key) with
        | true, value -> Some value
        | false, _ -> None

    /// <summary>
    /// Delete a key from Memtbl.
    /// In LSM-Tree structure, deletion operations are usually not immediately removing data,
    /// but instead inserting a record with a special value location (e.g., -1) to represent deletion.
    /// This "deletion marker" will be recognized and removed in subsequent Compaction processes.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <returns>The updated Memtbl instance, which contains a deletion marker record.</returns>
    member this.Delete(key: byte array) : Memtbl =
        match this.TryGet(key) with
        | Some _ ->
            match memtbl.Remove(key) with
            | true ->
                memtbl.Add(key, -1L)
                this
            | false -> failwith "Failed to delete key"
        | None -> failwith "Key not found"
