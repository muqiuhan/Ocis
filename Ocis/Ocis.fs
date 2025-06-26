module Ocis.OcisDB

open System.Collections.Concurrent
open Ocis.Memtbl
open Ocis.SSTbl
open Ocis.Valog
open Ocis.WAL
open System.IO
open System
open System.Text
open Ocis.Utils.ByteArrayComparer

// Messages for agents
type CompactionMessage =
    | FlushMemtable of Memtbl // Message to flush an immutable memtable to SSTable
    | TriggerCompaction // Message to trigger a compaction cycle

type GcMessage = | TriggerGC // Message to trigger garbage collection in Valog

/// <summary>
/// OcisDB represents the main WiscKey storage engine instance.
/// It orchestrates operations across MemTables, SSTables, ValueLog, and WAL.
/// </summary>
type OcisDB
    (
        dir: string,
        currentMemtbl: Memtbl,
        immutableMemtbl: ConcurrentQueue<Memtbl>,
        ssTables: Map<int, SSTbl list>,
        valog: Valog,
        wal: Wal,
        compactionAgent: MailboxProcessor<CompactionMessage>,
        gcAgent: MailboxProcessor<GcMessage>,
        flushThreshold: int
    ) =
    let mutable ssTables = ssTables
    let mutable currentMemtbl = currentMemtbl

    member _.Dir = dir

    member _.CurrentMemtbl
        with get () = currentMemtbl
        and set (memtbl: Memtbl) = currentMemtbl <- memtbl

    member _.ImmutableMemtbl = immutableMemtbl

    member _.SSTables
        with get () = ssTables
        and set (tables: Map<int, SSTbl list>) = ssTables <- tables

    member _.ValueLog = valog
    member _.WAL = wal
    member _.CompactionAgent = compactionAgent
    member _.GCAgent = gcAgent

    // Implement IDisposable for OcisDB to ensure all underlying resources are properly closed
    interface System.IDisposable with
        member this.Dispose() =
            // Stop agents first
            compactionAgent.Dispose()
            this.GCAgent.Dispose()
            // Close file streams
            this.ValueLog.Close()
            this.WAL.Close()
            // Dispose all SSTables
            this.SSTables
            |> Map.iter (fun _ sstblList -> sstblList |> List.iter (fun sstbl -> (sstbl :> IDisposable).Dispose()))

    /// <summary>
    /// Merges multiple SSTables into a new SSTable.
    /// Handles duplicate keys by keeping the entry with the latest timestamp.
    /// Filters out deletion markers (-1L).
    /// </summary>
    static member private mergeSSTables
        (dbRef: OcisDB ref, sstblsToMerge: SSTbl list, targetLevel: int)
        : Result<SSTbl option, string> =
        try
            // Collect all key-value pairs from the SSTables, ordered by key and then timestamp (descending)
            let allEntries =
                sstblsToMerge
                |> List.collect (fun sstbl ->
                    sstbl |> Seq.map (fun kvp -> kvp.Key, kvp.Value, sstbl.Timestamp) |> Seq.toList)
                |> List.sortBy (fun (key, _, timestamp) -> key, -timestamp) // Sort by key, then by timestamp descending

            let mergedMemtbl = Memtbl()
            let mutable processedKeys = Set.empty<byte array> // Use a set to track processed keys

            for (key, valueLoc, _) in allEntries do
                if not (processedKeys.Contains(key)) then // Only process if key hasn't been processed yet
                    if valueLoc <> -1L then // Filter out deletion markers
                        mergedMemtbl.Add(key, valueLoc)

                    processedKeys <- processedKeys.Add(key) // Mark key as processed

            if mergedMemtbl.Count > 0 then
                let newSSTblPath =
                    Path.Combine(dbRef.Value.Dir, $"sstbl-{Guid.NewGuid().ToString()}.sst")

                let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

                let flushedSSTblPath =
                    SSTbl.Flush(mergedMemtbl, newSSTblPath, timestamp, targetLevel)

                match SSTbl.Open(flushedSSTblPath) with
                | Some newSSTbl -> Ok(Some newSSTbl)
                | None -> Error(sprintf "Failed to open newly merged SSTable at %s" flushedSSTblPath)
            else
                Ok None // No entries to merge or all were deletion markers
        with ex ->
            Error(sprintf "Error merging SSTables: %s" ex.Message)

    /// <summary>
    /// Background agent for handling Memtable flushing to SSTables and Compaction.
    /// </summary>
    static member private compactionAgentLoop (dbRef: OcisDB ref) (mailbox: MailboxProcessor<CompactionMessage>) =
        async {
            while true do
                let! msg = mailbox.Receive()

                match msg with
                | FlushMemtable memtblToFlush ->
                    // Logic to flush memtblToFlush to a new SSTable
                    // For simplicity, let's assume Level 0 for now.
                    let newSSTblPath =
                        Path.Combine(dbRef.Value.Dir, $"sstbl-{Guid.NewGuid().ToString()}.sst")

                    let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    let level = 0 // Always flush to Level 0

                    try
                        let flushedSSTblPath = SSTbl.Flush(memtblToFlush, newSSTblPath, timestamp, level)

                        match SSTbl.Open(flushedSSTblPath) with
                        | Some sstbl ->
                            // Update the SSTables map in the OcisDB instance
                            dbRef.Value.SSTables <-  // 直接赋值给可变属性
                                dbRef.Value.SSTables
                                |> Map.change level (fun currentListOption ->
                                    match currentListOption with
                                    | Some currentList -> Some(sstbl :: currentList)
                                    | None -> Some [ sstbl ])

                            // printfn "Memtable flushed to SSTable: %s" flushedSSTblPath
                            // After flushing, check if compaction is needed
                            mailbox.Post(TriggerCompaction)
                        | None -> printfn "Error: Failed to open flushed SSTable at %s" flushedSSTblPath
                    with ex ->
                        printfn "Error flushing Memtable to SSTable: %s" ex.Message

                | TriggerCompaction ->
                    do!
                        async { // Wrap the entire compaction logic in an async block
                            let level0SSTablesOption = dbRef.Value.SSTables |> Map.tryFind 0

                            match level0SSTablesOption with
                            | Some level0SSTables when level0SSTables.Length >= 5 ->
                                // Select the first 5 SSTables for merging (simplistic strategy)
                                let sstblsToMerge = level0SSTables |> List.take 5
                                let remainingSSTables = level0SSTables |> List.skip 5

                                match OcisDB.mergeSSTables (dbRef, sstblsToMerge, 1) with // Merge to Level 1
                                | Ok(Some newSSTbl) ->
                                    do // Perform synchronous side effects
                                        let updatedSSTables =
                                            dbRef.Value.SSTables
                                            |> Map.add 0 remainingSSTables // Keep remaining Level 0 SSTables
                                            |> Map.change 1 (fun currentListOption ->
                                                match currentListOption with
                                                | Some currentList -> Some(newSSTbl :: currentList)
                                                | None -> Some [ newSSTbl ])

                                        dbRef.Value.SSTables <- updatedSSTables

                                        // Dispose and delete old SSTable files
                                        sstblsToMerge
                                        |> List.iter (fun sstbl ->
                                            (sstbl :> IDisposable).Dispose()
                                            File.Delete(sstbl.Path))

                                        // printfn
                                        //     "Compaction successful: Merged %d SSTables to Level 1."
                                        //     sstblsToMerge.Length
                                        ()

                                | Ok None ->
                                    do // Perform synchronous side effects
                                        let updatedSSTables = dbRef.Value.SSTables |> Map.add 0 remainingSSTables
                                        dbRef.Value.SSTables <- updatedSSTables

                                        sstblsToMerge
                                        |> List.iter (fun sstbl ->
                                            (sstbl :> IDisposable).Dispose()
                                            File.Delete(sstbl.Path))

                                        // printfn
                                        //     "Compaction successful: Removed %d SSTables (all deletion markers)."
                                        //     sstblsToMerge.Length
                                        ()

                                | Error msg ->
                                    do // Perform synchronous side effects
                                        printfn "Error during compaction: %s" msg
                            | _ ->
                                // No compaction needed or not enough SSTables in Level 0
                                return () // Explicitly return unit in an async block
                        }
        // In a real implementation, this would involve selecting SSTables,
        // merging them, and updating the SSTables map.
        }

    /// <summary>
    /// Background agent for handling Garbage Collection in Valog.
    /// </summary>
    static member private gcAgentLoop (dbRef: OcisDB ref) (mailbox: MailboxProcessor<GcMessage>) =
        async {
            while true do
                let! msg = mailbox.Receive()

                match msg with
                | TriggerGC -> printfn "Triggering garbage collection (simplified)."
        // This is where Valog GC logic would go.
        // This typically happens during compaction or as a separate process
        // that identifies unreferenced values in the Valog and reclaims space.
        }

    /// <summary>
    /// Opens an existing WiscKeyDB or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="dir">The directory where the database files are stored.</param>
    /// <returns>A Result type: Ok OcisDB instance if successful, otherwise an Error string.</returns>
    static member Open(dir: string, flushThreshold: int) : Result<OcisDB, string> =
        try
            // Ensure the directory exists
            if not (Directory.Exists(dir)) then
                Directory.CreateDirectory(dir) |> ignore

            let valogPath = Path.Combine(dir, "valog.vlog")

            Valog.Create(valogPath)
            |> Result.bind (fun valog -> // Chain Result operations
                let walPath = Path.Combine(dir, "wal.log")

                WAL.Wal.Create(walPath)
                |> Result.bind (fun wal ->
                    // Replay WAL to reconstruct CurrentMemTable
                    let initialMemtbl = Memtbl() // Use Memtbl() for a new instance
                    let replayedEntries = WAL.Wal.Replay(walPath) // Use WAL.Wal to specify the type

                    for entry in replayedEntries do
                        match entry with
                        | WalEntry.Set(key, valueLoc) -> initialMemtbl.Add(key, valueLoc)
                        | WalEntry.Delete(key) -> initialMemtbl.Delete(key)

                    // Load existing SSTables
                    let mutable loadedSSTables = Map.empty<int, SSTbl list> // 修正 Map.empty
                    let sstblFiles = Directory.GetFiles(dir, "sstbl-*.sst") // Assuming SSTables follow this naming convention

                    for filePath in sstblFiles do
                        match SSTbl.Open(filePath) with
                        | Some sstbl ->
                            loadedSSTables <-
                                loadedSSTables
                                |> Map.change sstbl.Level (fun currentListOption ->
                                    match currentListOption with
                                    | Some currentList -> Some(sstbl :: currentList)
                                    | None -> Some [ sstbl ])
                        | None -> printfn "Warning: Failed to open SSTable file: %s. It might be corrupted." filePath

                    // Initialize agents (MailboxProcessors)
                    // Create a mutable reference to OcisDB to allow agents to update its state
                    let dbRef = ref Unchecked.defaultof<OcisDB> // Placeholder, will be updated after OcisDB instance is created

                    let compactionAgent = MailboxProcessor.Start(OcisDB.compactionAgentLoop dbRef)
                    let gcAgent = MailboxProcessor.Start(OcisDB.gcAgentLoop dbRef)

                    let ocisDB =
                        new OcisDB(
                            dir,
                            initialMemtbl,
                            ConcurrentQueue<Memtbl>(),
                            loadedSSTables,
                            valog,
                            wal,
                            compactionAgent,
                            gcAgent,
                            flushThreshold
                        )

                    dbRef.Value <- ocisDB // Update the mutable reference

                    Ok ocisDB))
        with ex ->
            Error(sprintf "Failed to open WiscKeyDB: %s" ex.Message)

    /// <summary>
    /// Sets a key-value pair in the database.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>An AsyncResult: Ok unit if successful, otherwise an Error string.</returns>
    member _.Set(key: byte array, value: byte array) : Async<Result<unit, string>> =
        async {
            try
                // 1. Write to ValueLog
                let valueLocation = valog.Append(key, value)

                // 2. Write to WAL
                wal.Append(WalEntry.Set(key, valueLocation))

                // 3. Write to CurrentMemTable
                currentMemtbl.Add(key, valueLocation)

                // 4. Check MemTable size and trigger flush if needed
                // For simplicity, we'll use a fixed size for now (e.g., 100 entries for testing)
                // In a real system, this would be based on memory usage, not entry count.
                // Memtbl does not directly expose a Count property, so we might need to add one or iterate.
                // For now, let's assume we can determine its 'size' indirectly or add a Count property.
                // Adding a simple workaround for demonstration. In Memtbl.fs, we'd add `member _.Count = memtbl.Count`
                let memtblCount = currentMemtbl.Count

                if memtblCount >= flushThreshold then
                    let frozenMemtbl = currentMemtbl
                    immutableMemtbl.Enqueue(frozenMemtbl)
                    currentMemtbl <- Memtbl() // Create a new empty MemTable
                    compactionAgent.Post(FlushMemtable frozenMemtbl) // Notify compaction agent to flush

                return Ok()
            with ex ->
                return Error(sprintf "Failed to set key-value pair: %s" ex.Message)
        }

    /// <summary>
    /// Gets the value associated with the given key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>An AsyncResult: Ok (Some value) if found, Ok None if not found, otherwise an Error string.</returns>
    member this.Get(key: byte array) : Async<Result<byte array option, string>> =
        async {
            try
                // Helper function to resolve ValueLocation to actual value or error
                let resolveValue (valueLocation: int64) : Result<byte array option, string> =
                    if valueLocation = -1L then
                        Ok None // Deletion marker
                    else
                        match this.ValueLog.Read(valueLocation) with
                        | Some(_, value) -> Ok(Some value)
                        | None ->
                            Error(
                                sprintf
                                    "Failed to read value from Valog at location %d for key %s."
                                    valueLocation
                                    (Encoding.UTF8.GetString(key))
                            )

                // 1. Search CurrentMemTable
                match this.CurrentMemtbl.TryGet(key) with
                | Some valueLocation -> return! async { return (resolveValue valueLocation) }
                | None ->
                    // 2. Search ImmutableMemTables (from newest to oldest)
                    let immutableLocationOption =
                        this.ImmutableMemtbl
                        |> Seq.rev
                        |> Seq.tryPick (fun memtbl -> memtbl.TryGet(key))

                    match immutableLocationOption with
                    | Some valueLocation -> return! async { return (resolveValue valueLocation) }
                    | None ->
                        // 3. Search SSTables (from Level 0 upwards, newest within each level)
                        let sstblLocationOption =
                            this.SSTables
                            |> Map.toSeq
                            |> Seq.sortBy (fun (lvl, _) -> lvl)
                            |> Seq.tryPick (fun (_, sstblList) ->
                                sstblList
                                |> List.sortByDescending (fun s -> s.Timestamp)
                                |> List.tryPick (fun sstbl -> sstbl.TryGet(key)))

                        match sstblLocationOption with
                        | Some valueLocation -> return! async { return (resolveValue valueLocation) }
                        | None ->
                            // If not found anywhere
                            return Ok None
            with ex ->
                return Error(sprintf "Failed to get value for key %s: %s" (Encoding.UTF8.GetString(key)) ex.Message)

        }

    /// <summary>
    /// Deletes a key from the database.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <returns>An AsyncResult: Ok unit if successful, otherwise an Error string.</returns>
    member this.Delete(key: byte array) : Async<Result<unit, string>> =
        async {
            try
                // 1. Write deletion marker to WAL
                this.WAL.Append(WalEntry.Delete(key))

                // 2. Add deletion marker to CurrentMemTable
                currentMemtbl.Delete(key) // Memtbl.Delete already adds -1L

                // Check MemTable size and trigger flush if needed (same logic as Set)
                // Assuming Memtbl has a Count property or similar mechanism to get its size.
                let memtblCount = currentMemtbl.Count

                if memtblCount >= flushThreshold then
                    let frozenMemtbl = currentMemtbl
                    immutableMemtbl.Enqueue(frozenMemtbl)
                    currentMemtbl <- Memtbl() // Create a new empty MemTable
                    compactionAgent.Post(FlushMemtable frozenMemtbl) // Notify compaction agent to flush

                return Ok()
            with ex ->
                return Error(sprintf "Failed to delete key %s: %s" (Encoding.UTF8.GetString(key)) ex.Message)
        }
