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
open Ocis.ValueLocation
open FSharp.Collections

// Messages for agents
type CompactionMessage =
    | FlushMemtable of Memtbl // Message to flush an immutable memtable to SSTable
    | TriggerCompaction // Message to trigger a compaction cycle
    | RecompactionForRemapped of Map<int64, int64> // New message to trigger recompaction for remapped value locations

type GcMessage = | TriggerGC // Message to trigger garbage collection in Valog (Removed parameter)

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
    // Constants for compaction strategy
    static let L0_COMPACTION_THRESHOLD = 5 // Number of Level 0 SSTables to trigger compaction
    static let LEVEL_SIZE_MULTIPLIER = 10 // Each level is approximately 10 times larger than the previous one

    let mutable ssTables = ssTables
    let mutable currentMemtbl = currentMemtbl
    let mutable valog = valog
    let mutable internalPendingRemappedLocations = Map.empty<int64, int64> // Internal mutable field

    member _.Dir = dir

    member _.CurrentMemtbl
        with get () = currentMemtbl
        and set (memtbl: Memtbl) = currentMemtbl <- memtbl

    member _.ImmutableMemtbl = immutableMemtbl

    member _.SSTables
        with get () = ssTables
        and set (tables: Map<int, SSTbl list>) = ssTables <- tables

    member _.ValueLog
        with get () = valog
        and set (v: Valog) = valog <- v

    member _.PendingRemappedLocations // New public property for pending remappings
        with get () = internalPendingRemappedLocations
        and set (m: Map<int64, int64>) = internalPendingRemappedLocations <- m

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
        (dbRef: OcisDB ref, sstblsToMerge: SSTbl list, targetLevel: int, remappedLocations: Map<int64, int64> option)
        : Result<SSTbl option * (byte array * ValueLocation) list, string> =
        try
            // Collect all key-value pairs from the SSTables, ordered by key and then timestamp (descending)
            let allEntries =
                sstblsToMerge
                |> List.collect (fun sstbl ->
                    sstbl |> Seq.map (fun kvp -> kvp.Key, kvp.Value, sstbl.Timestamp) |> Seq.toList)
                |> List.sortBy (fun (key, _, timestamp) -> key, -timestamp) // Sort by key, then by timestamp descending

            let mergedMemtbl = Memtbl()
            let mutable processedKeys = Set.empty<byte array> // Use a set to track processed keys
            let mutable liveEntriesInNewSSTbl = ResizeArray<byte array * ValueLocation>() // To collect live entries for GC

            for (key, valueLoc, _) in allEntries do
                if not (processedKeys.Contains(key)) then // Only process if key hasn't been processed yet
                    // Apply remapping if available
                    let finalValueLoc =
                        match remappedLocations with
                        | Some remap ->
                            match remap.TryGetValue(valueLoc) with
                            | true, newLoc -> newLoc
                            | false, _ -> valueLoc
                        | None -> valueLoc

                    if finalValueLoc <> -1L then // Filter out deletion markers
                        mergedMemtbl.Add(key, finalValueLoc)
                        liveEntriesInNewSSTbl.Add((key, finalValueLoc)) // Collect live entries for GC

                    processedKeys <- processedKeys.Add(key) // Mark key as processed

            if mergedMemtbl.Count > 0 then
                let newSSTblPath =
                    Path.Combine(dbRef.Value.Dir, $"sstbl-{Guid.NewGuid().ToString()}.sst")

                let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

                let flushedSSTblPath =
                    SSTbl.Flush(mergedMemtbl, newSSTblPath, timestamp, targetLevel)

                match SSTbl.Open(flushedSSTblPath) with
                | Some newSSTbl -> Ok(Some newSSTbl, liveEntriesInNewSSTbl |> List.ofSeq) // Return new SSTbl and live entries
                | None -> Error(sprintf "Failed to open newly merged SSTable at %s" flushedSSTblPath)
            else
                Ok(None, []) // No entries to merge, return None and empty list
        with ex ->
            Error(sprintf "Error merging SSTables: %s" ex.Message)

    static member private compact
        (
            dbRef: OcisDB ref,
            level: int,
            sstblsToMerge: SSTbl list,
            targetLevel: int,
            remappedLocations: Map<int64, int64> option
        ) : Result<SSTbl option * (byte array * ValueLocation) list, string> =
        OcisDB.mergeSSTables (dbRef, sstblsToMerge, targetLevel, remappedLocations)

    /// <summary>
    /// Re-compacts an SSTable by updating its value locations based on a remapping map.
    /// This is used after Valog garbage collection to update outdated value pointers.
    /// </summary>
    static member private recompactSSTable
        (dbRef: OcisDB ref, sstbl: SSTbl, remappedLocations: Map<int64, int64>)
        : Result<SSTbl option, string> =
        try
            let recompactedMemtbl = Memtbl()
            let mutable changed = false

            for KeyValue(key, originalValueLoc) in sstbl do
                let newValueLoc =
                    match remappedLocations.TryGetValue(originalValueLoc) with
                    | true, newLoc ->
                        changed <- true
                        newLoc
                    | false, _ -> originalValueLoc // Value not remapped

                recompactedMemtbl.Add(key, newValueLoc)

            if changed || recompactedMemtbl.Count <> 0 then // Only create new SSTbl if something changed or it's not empty after recompaction
                let newSSTblPath =
                    Path.Combine(dbRef.Value.Dir, $"sstbl-recompact-{Guid.NewGuid().ToString()}.sst") // Use a distinct name

                let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                let level = sstbl.Level // Keep the same level

                let flushedSSTblPath =
                    SSTbl.Flush(recompactedMemtbl, newSSTblPath, timestamp, level)

                match SSTbl.Open(flushedSSTblPath) with
                | Some newSSTbl -> Ok(Some newSSTbl)
                | None -> Error(sprintf "Failed to open recompacted SSTable at %s" flushedSSTblPath)
            else
                Ok None // No changes, no new SSTbl needed. Or all entries were deletion markers and didn't get added.
        with ex ->
            Error(sprintf "Error recompacting SSTable %s: %s" sstbl.Path ex.Message)

    /// <summary>
    /// Performs compaction for Level 0 SSTables.
    /// </summary>
    static member private compactLevel0(dbRef: OcisDB ref) : Async<unit> =
        async {
            let level0SSTablesOption = dbRef.Value.SSTables |> Map.tryFind 0

            match level0SSTablesOption with
            | Some level0SSTables when level0SSTables.Length >= L0_COMPACTION_THRESHOLD ->
                // Select SSTables for merging (simplistic strategy: merge all L0 files for now)
                let sstblsToMerge = level0SSTables
                let targetLevel = 1 // Always merge L0 to L1

                match
                    OcisDB.compact (dbRef, 0, sstblsToMerge, targetLevel, Some dbRef.Value.PendingRemappedLocations)
                with
                | Ok(Some newSSTbl, liveEntries) ->
                    // Update the SSTables map
                    do!
                        async { // Perform synchronous side effects
                            let updatedSSTables =
                                dbRef.Value.SSTables
                                |> Map.remove 0 // Remove all old Level 0 SSTables
                                |> Map.change targetLevel (fun currentListOption ->
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
                        //     "Compaction successful: Merged %d SSTables from Level 0 to Level 1."
                        //     sstblsToMerge.Length
                        }

                | Ok(None, _) ->
                    do!
                        async { // Perform synchronous side effects
                            // All entries were deletion markers, remove old SSTables
                            dbRef.Value.SSTables <- dbRef.Value.SSTables |> Map.remove 0

                            sstblsToMerge
                            |> List.iter (fun sstbl ->
                                (sstbl :> IDisposable).Dispose()
                                File.Delete(sstbl.Path))

                        // printfn
                        //     "Compaction successful: Removed %d SSTables from Level 0 (all deletion markers)."
                        //     sstblsToMerge.Length
                        }
                | Error msg -> printfn "Error during Level 0 compaction: %s" msg
            | _ -> () // No compaction needed for Level 0
        }

    /// <summary>
    /// Performs compaction for levels greater than 0 (Leveled Compaction).
    /// This is a simplified version and will need to be expanded for full Leveled Compaction.
    /// </summary>
    static member private compactLevel(dbRef: OcisDB ref, level: int) : Async<unit> =
        async {
            let nextLevel = level + 1

            let currentLevelSSTablesOption = dbRef.Value.SSTables |> Map.tryFind level
            let nextLevelSSTablesOption = dbRef.Value.SSTables |> Map.tryFind nextLevel

            do!
                async { // Outer async block to ensure Async<unit> return
                    match currentLevelSSTablesOption with
                    | Some currentLevelSSTables when not (currentLevelSSTables |> List.isEmpty) ->
                        let targetLevelSize =
                            int64 L0_COMPACTION_THRESHOLD * (pown (int64 LEVEL_SIZE_MULTIPLIER) (level - 1))

                        let currentLevelTotalSize =
                            currentLevelSSTables |> List.sumBy (fun sstbl -> sstbl.Size)

                        if currentLevelTotalSize >= targetLevelSize then
                            let mutable bestSSTblToCompact: SSTbl option = None
                            let mutable maxOverlapSize = -1L

                            for sstbl in currentLevelSSTables do
                                let currentOverlapSize =
                                    match nextLevelSSTablesOption with
                                    | Some nextLevelSSTables ->
                                        nextLevelSSTables
                                        |> List.filter (fun nsstbl -> sstbl.Overlaps(nsstbl))
                                        |> List.sumBy (fun os -> os.Size)
                                        |> int64
                                    | None -> 0L

                                if currentOverlapSize >= maxOverlapSize then
                                    maxOverlapSize <- currentOverlapSize
                                    bestSSTblToCompact <- Some sstbl

                            match bestSSTblToCompact with
                            | Some sstblToCompact ->
                                let overlappingSSTables =
                                    match nextLevelSSTablesOption with
                                    | Some nextLevelSSTables ->
                                        nextLevelSSTables |> List.filter (fun sstbl -> sstblToCompact.Overlaps(sstbl))
                                    | None -> []

                                let sstblsToMerge = sstblToCompact :: overlappingSSTables

                                let compactResult =
                                    OcisDB.compact (
                                        dbRef,
                                        level,
                                        sstblsToMerge,
                                        nextLevel,
                                        Some dbRef.Value.PendingRemappedLocations
                                    )

                                match compactResult with
                                | Ok(newSSTblOption, _) ->
                                    let mutable updatedSSTables = dbRef.Value.SSTables

                                    // Remove sstblToCompact from current level
                                    updatedSSTables <-
                                        updatedSSTables
                                        |> Map.change level (fun currentListOption ->
                                            match currentListOption with
                                            | Some currentList ->
                                                Some(
                                                    currentList |> List.filter (fun s -> s.Path <> sstblToCompact.Path)
                                                )
                                            | None -> None)

                                    // Remove overlappingSSTables from next level
                                    updatedSSTables <-
                                        updatedSSTables
                                        |> Map.change nextLevel (fun nextListOption ->
                                            match nextListOption with
                                            | Some nextList ->
                                                Some(
                                                    nextList
                                                    |> List.filter (fun s ->
                                                        not (
                                                            overlappingSSTables
                                                            |> List.exists (fun os -> os.Path = s.Path)
                                                        ))
                                                )
                                            | None -> None)

                                    match newSSTblOption with
                                    | Some newSSTbl ->
                                        // Add newSSTbl to next level
                                        updatedSSTables <-
                                            updatedSSTables
                                            |> Map.change nextLevel (fun currentListOption ->
                                                match currentListOption with
                                                | Some currentList -> Some(newSSTbl :: currentList)
                                                | None -> Some [ newSSTbl ])
                                    | None -> ()

                                    dbRef.Value.SSTables <- updatedSSTables

                                    sstblsToMerge
                                    |> List.iter (fun sstbl ->
                                        (sstbl :> IDisposable).Dispose()
                                        File.Delete(sstbl.Path))

                                    dbRef.Value.PendingRemappedLocations <- Map.empty<int64, int64> // Clear pending remapped locations
                                    return () // This returns unit for the do! block

                                | Error msg ->
                                    printfn "Error during Level %d compaction: %s" level msg
                                    return () // Returns unit
                            | None ->
                                printfn
                                    "Level %d: No suitable SSTable found for compaction (max overlap size: %d)."
                                    level
                                    maxOverlapSize

                                return () // Returns unit
                        else
                            return () // No compaction needed (size), returns unit
                    | _ -> return () // Level has no SSTables or is empty, returns unit
                } // End of do! async block
        }

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
                            dbRef.Value.SSTables <-
                                dbRef.Value.SSTables
                                |> Map.change level (fun currentListOption ->
                                    match currentListOption with
                                    | Some currentList -> Some(sstbl :: currentList)
                                    | None -> Some [ sstbl ])

                            // After flushing, check if compaction is needed
                            mailbox.Post(TriggerCompaction)
                        | None -> printfn "Error: Failed to open flushed SSTable at %s" flushedSSTblPath
                    with ex ->
                        printfn "Error flushing Memtable to SSTable: %s" ex.Message

                | TriggerCompaction ->
                    do! OcisDB.compactLevel0 dbRef // Try to compact Level 0 first

                    // Iterate through other levels and trigger compaction as needed
                    // For a more robust implementation, define a MAX_LEVEL or dynamically handle it.
                    // For demonstration, let's assume we have Levels 0, 1, 2 for now.
                    for level = 1 to 2 do // Example: compact Levels 1 and 2
                        do! OcisDB.compactLevel (dbRef, level) // Explicitly tuple arguments

                    return ()

                | RecompactionForRemapped remappedLocations ->
                    // printfn "Received recompaction request for %d remapped locations." remappedLocations.Count
                    let mutable updatedSSTables = dbRef.Value.SSTables
                    let mutable recompactedCount = 0

                    for level, sstblList in dbRef.Value.SSTables |> Map.toSeq do
                        let mutable newSSTblList = ResizeArray<SSTbl>()

                        for sstbl in sstblList do
                            match OcisDB.recompactSSTable (dbRef, sstbl, remappedLocations) with
                            | Ok(Some newSSTbl) ->
                                newSSTblList.Add(newSSTbl)
                                (sstbl :> IDisposable).Dispose()
                                File.Delete(sstbl.Path)
                                recompactedCount <- recompactedCount + 1
                            // printfn "Recompacted SSTable %s to %s (Level %d)" sstbl.Path newSSTbl.Path level
                            | Ok None -> newSSTblList.Add(sstbl) // No change, keep original SSTable
                            | Error msg ->
                                printfn "Error recompacting SSTable %s: %s" sstbl.Path msg
                                newSSTblList.Add(sstbl) // On error, keep original SSTable

                        updatedSSTables <- updatedSSTables |> Map.add level (newSSTblList |> List.ofSeq)

                    dbRef.Value.SSTables <- updatedSSTables
                    // printfn "Completed recompaction for Valog GC. Recompacted %d SSTables." recompactedCount
                    return ()
        }

    /// <summary>
    /// Background agent for handling Garbage Collection in Valog.
    /// </summary>
    static member private gcAgentLoop (dbRef: OcisDB ref) (mailbox: MailboxProcessor<GcMessage>) =
        async {
            while true do
                let! msg = mailbox.Receive()

                match msg with
                | TriggerGC ->
                    // printfn "Triggering garbage collection."

                    // Collect all live value locations
                    let mutable liveLocations = Set.empty<int64>

                    // From CurrentMemtbl
                    dbRef.Value.CurrentMemtbl
                    |> Seq.iter (fun (KeyValue(_, valueLoc)) -> liveLocations <- liveLocations.Add(valueLoc))

                    // From ImmutableMemtbl
                    dbRef.Value.ImmutableMemtbl
                    |> Seq.collect (fun memtbl -> memtbl |> Seq.map (fun (KeyValue(_, valueLoc)) -> valueLoc))
                    |> Seq.iter (fun valueLoc -> liveLocations <- liveLocations.Add(valueLoc))

                    // From SSTables
                    dbRef.Value.SSTables
                    |> Map.toSeq
                    |> Seq.collect (fun (_, sstblList) ->
                        sstblList
                        |> Seq.collect (fun sstbl -> sstbl |> Seq.map (fun (KeyValue(_, valueLoc)) -> valueLoc)))
                    |> Seq.iter (fun valueLoc -> liveLocations <- liveLocations.Add(valueLoc))

                    // printfn "Collected %d live value locations." liveLocations.Count

                    // Perform actual garbage collection in Valog
                    let! remappedLocationsOption = Valog.CollectGarbage(dbRef.Value.ValueLog, liveLocations)

                    match remappedLocationsOption with
                    | Some(Ok(newValog, remappedLocations)) ->
                        dbRef.Value.ValueLog <- newValog // Update the Valog instance in OcisDB

                        printfn
                            "Valog GC: Values were moved, %d value locations remapped. Affected SSTables might need re-compaction to update pointers."
                            remappedLocations.Count

                        // Merge new remappedLocations into pendingRemappedLocations
                        for KeyValue(key, newLoc) in remappedLocations do
                            dbRef.Value.PendingRemappedLocations <-
                                dbRef.Value.PendingRemappedLocations.Add(key, newLoc)

                        // Trigger a compaction cycle to handle the remapping in affected SSTables
                        dbRef.Value.CompactionAgent.Post(TriggerCompaction) // Trigger general compaction
                    | Some(Error msg) -> printfn "Valog GC Error: %s" msg
                    | None -> printfn "Valog GC: No values were moved or no remapping needed."

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
