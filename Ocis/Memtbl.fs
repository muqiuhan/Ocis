module Ocis.Memtbl

open System
open System.Collections.Generic
open Ocis.Utils.ByteArrayComparer
open Ocis.ValueLocation

/// <summary>
/// Memtbl (Memory Table) represents the memory buffer in WiscKey storage engine.
/// It stores the mapping from key to Value Log position, and automatically maintains the order of keys.
/// </summary>
/// <remarks>
/// According to the core idea of WiscKey paper (Lu et al. 2017), Memtbl only stores keys and value locations,
/// rather than actual values, to reduce memory usage and I/O amplification.
/// When Memtbl reaches a certain size, it will be "frozen" and flushed to SSTable on disk.
/// Reference: [https://www.usenix.org/system/files/conference/fast16/fast16-papers-lu.pdf]
///
/// Thread Safety: This implementation is thread-safe for concurrent read/write operations.
/// </remarks>
type Memtbl () =
  let memtbl =
    SortedDictionary<byte array, ValueLocation>
      ByteArrayComparer.ComparerInstance

  // Lock object for thread synchronization
  let lockObj = obj ()

  interface IEnumerable<KeyValuePair<byte array, ValueLocation>> with
    member _.GetEnumerator () =
      memtbl.GetEnumerator ()
      :> IEnumerator<KeyValuePair<byte array, ValueLocation>>

  interface Collections.IEnumerable with
    member _.GetEnumerator () =
      memtbl.GetEnumerator () :> Collections.IEnumerator

  member _.Count = lock lockObj (fun () -> memtbl.Count)

  /// <summary>
  /// Add or update a key-value pair in Memtbl.
  /// Thread-safe implementation using locks.
  /// </summary>
  /// <param name="key">The key to add or update.</param>
  /// <param name="valueLocation">The value location to associate with the key.</param>
  member _.Add (key : byte array, valueLocation : ValueLocation) : unit =
    lock lockObj (fun () -> memtbl[key] <- valueLocation)

  /// <summary>
  /// Get the value location of the given key from Memtbl.
  /// Thread-safe implementation using locks.
  /// </summary>
  /// <param name="key">The key to find.</param>
  /// <returns>
  /// An Option type: Some ValueLocation if the key is found, otherwise None.
  /// This follows the idiomatic way of handling possible missing values in F#.
  /// </returns>
  member _.TryGet (key : byte array) : ValueLocation option =
    lock lockObj (fun () ->
      match memtbl.TryGetValue key with
      | true, value -> Some value
      | false, _ -> None)

  /// <summary>
  /// Delete a key from Memtbl.
  /// In LSM-Tree structure, deletion operations are usually not immediately removing data,
  /// but instead inserting a record with a special value location (e.g., -1) to represent deletion.
  /// This "deletion marker" will be recognized and removed in subsequent Compaction processes.
  /// Thread-safe implementation using locks.
  /// </summary>
  /// <param name="key">The key to delete.</param>
  /// <returns>The updated Memtbl instance, which contains a deletion marker record.</returns>
  member this.SafeDelete (key : byte array) : unit =
    lock lockObj (fun () ->
      match this.TryGet key with
      | Some _ ->
        match memtbl.Remove key with
        | true -> memtbl.Add (key, -1L)
        | false -> failwith "Failed to delete key"
      | None -> failwith "Key not found")

  /// <summary>
  /// Delete a key from Memtbl.
  /// Thread-safe implementation using locks.
  /// </summary>
  /// <param name="key">The key to delete.</param>
  member _.Delete (key : byte array) : unit =
    lock lockObj (fun () -> memtbl[key] <- -1L)
