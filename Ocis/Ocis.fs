module Ocis.OcisDB

open Ocis.Memtbl
open Ocis.SSTbl
open Ocis.Valog
open Ocis.WAL
open System.IO
open System
open System.Text
open Ocis.ValueLocation
open FSharp.Collections
open Ocis.Utils.Logger

// Compaction strategy types - simplified for single-threaded operation
type CompactionPriority =
    | High // Level 0 SSTables or size-triggered
    | Medium // Overlap-triggered
    | Low // Background compaction

type SSTableCandidate =
    { SSTable: SSTbl
      Priority: CompactionPriority
      OverlapSize: int64
      Score: float } // Combined score for selection

/// <summary>
/// OcisDB represents the main WiscKey storage engine instance.
/// Single-threaded implementation optimized for maximum performance.
/// It orchestrates operations across MemTables, SSTables, ValueLog, and WAL.
/// </summary>
type OcisDB
    (
        dir: string,
        currentMemtbl: Memtbl,
        immutableMemtbls: Memtbl list,
        ssTables: Map<int, SSTbl list>,
        valog: Valog,
        wal: Wal,
        flushThreshold: int
    ) =
    /// Number of Level 0 SSTables to trigger compaction.
    /// For write-intensive applications, this value can be appropriately increased to reduce the merge frequency.
    /// For read-intensive applications, this value can be appropriately reduced to reduce the scan range of L0
    static let mutable L0_COMPACTION_THRESHOLD = 4

    /// Each level is approximately 10 times larger than the previous one
    static let mutable LEVEL_SIZE_MULTIPLIER = 5

    /// Compaction score threshold for triggering compaction
    static let mutable COMPACTION_SCORE_THRESHOLD = 1.0

    let mutable ssTables = ssTables
    let mutable currentMemtbl = currentMemtbl
    let mutable immutableMemtbls = immutableMemtbls // Changed from immutableMemtbl
    let mutable valog = valog
    let mutable internalPendingRemappedLocations = Map.empty<int64, int64> // Internal mutable field

    static member L0CompactionThreshold
        with get () = L0_COMPACTION_THRESHOLD
        and set (threshold: int) = L0_COMPACTION_THRESHOLD <- threshold

    static member LevelSizeMultiplier
        with get () = LEVEL_SIZE_MULTIPLIER
        and set (multiplier: int) = LEVEL_SIZE_MULTIPLIER <- multiplier

    static member CompactionScoreThreshold
        with get () = COMPACTION_SCORE_THRESHOLD
        and set (threshold: float) = COMPACTION_SCORE_THRESHOLD <- threshold

    /// <summary>
    /// Calculates compaction priority for an SSTable based on multiple factors.
    ///
    /// Priority Determination Logic:
    /// 1. Level 0 SSTables always get High priority because they are the most recently written
    ///    and can have significant overlap with each other, affecting read performance.
    ///
    /// 2. For other levels, priority is determined by:
    ///    - Overlap with next level: SSTables that heavily overlap with the next level
    ///      create more I/O during reads and should be compacted sooner.
    ///    - Size ratio: SSTables that are much larger than expected for their level
    ///      indicate they need to be split down to lower levels.
    ///
    /// Design Decision: This multi-factor approach ensures we prioritize compaction
    /// for SSTables that will provide the most benefit in terms of read performance
    /// and space efficiency.
    /// </summary>
    /// <param name="sstbl">The SSTable to evaluate</param>
    /// <param name="level">The current level of the SSTable</param>
    /// <param name="nextLevelSSTables">SSTables in the next level (for overlap calculation)</param>
    /// <returns>The calculated priority level</returns>
    static member private calculateCompactionPriority
        (sstbl: SSTbl, level: int, nextLevelSSTables: SSTbl list option)
        : CompactionPriority =
        if level = 0 then
            // Level 0 SSTables are always high priority because:
            // - They are the most recently written data
            // - Multiple L0 SSTables can have significant key overlap
            // - They directly impact read performance
            High
        else
            match nextLevelSSTables with
            | Some nextSSTables ->
                // Calculate overlap size with next level SSTables
                let overlapSize =
                    nextSSTables
                    |> List.filter sstbl.Overlaps // Check key range overlap
                    |> List.sumBy _.Size // Sum sizes of overlapping SSTables
                    |> int64

                // Calculate expected size for this level
                // Level size grows exponentially: L1 = threshold, L2 = threshold * multiplier, etc.
                let levelSize =
                    int64 L0_COMPACTION_THRESHOLD * (pown (int64 LEVEL_SIZE_MULTIPLIER) (level - 1))

                let sizeRatio = float sstbl.Size / float levelSize

                // Priority decision logic:
                // High: Significant overlap AND this SSTable is oversized (>80% of expected level size)
                // Medium: Has some overlap but not oversized
                // Low: No overlap with next level
                if overlapSize > 0L && sizeRatio > 0.8 then High
                elif overlapSize > 0L then Medium
                else Low
            | None ->
                // No next level SSTables means this is the deepest level
                Low

    /// <summary>
    /// Calculates a comprehensive score for SSTable compaction priority.
    ///
    /// Score Components (weighted combination):
    /// 1. Age Score (30%): Based on SSTable creation timestamp relative to current time.
    ///    Older SSTables get lower scores (less urgent to compact).
    ///    Formula: timestamp / current_time (normalized 0-1)
    ///
    /// 2. Size Score (30%): SSTable size in MB. Larger SSTables get higher scores
    ///    because they have more data to potentially compact.
    ///
    /// 3. Overlap Score (40%): Total size of overlapping SSTables in next level (MB).
    ///    Higher overlap indicates more I/O amplification during reads,
    ///    making compaction more beneficial.
    ///
    /// 4. Level Weight: Level 0 SSTables get 10x weight multiplier because
    ///    they directly impact read performance due to potential overlap.
    ///
    /// Design Decision: The scoring system balances multiple factors to ensure
    /// compaction resources are allocated to SSTables that will provide the most benefit.
    /// The overlap factor gets the highest weight because it directly correlates
    /// with read performance improvement.
    /// </summary>
    /// <param name="sstbl">The SSTable to score</param>
    /// <param name="level">The current level of the SSTable</param>
    /// <param name="nextLevelSSTables">SSTables in the next level for overlap calculation</param>
    /// <returns>A score between 0 and infinity, higher scores indicate higher priority</returns>
    static member private calculateCompactionScore
        (sstbl: SSTbl, level: int, nextLevelSSTables: SSTbl list option)
        : float =
        // Age score: normalized timestamp (older = lower score)
        let ageScore =
            float sstbl.Timestamp
            / float (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())

        // Size score: SSTable size in MB (larger = higher score)
        let sizeScore = float sstbl.Size / 1024.0 / 1024.0

        // Overlap score: total size of overlapping SSTables in next level (MB)
        let overlapScore =
            match nextLevelSSTables with
            | Some nextSSTables ->
                nextSSTables
                |> List.filter sstbl.Overlaps
                |> List.sumBy _.Size
                |> float
                |> fun x -> x / 1024.0 / 1024.0
            | None -> 0.0

        // Level weight: Level 0 gets priority boost
        let levelWeight = if level = 0 then 10.0 else 1.0

        // Weighted combination: overlap gets highest weight (40%) because
        // it directly correlates with read performance improvement
        ageScore * 0.3 + sizeScore * 0.3 + overlapScore * 0.4 + levelWeight

    /// <summary>
    /// Selects the best SSTables for compaction using a sophisticated multi-factor strategy.
    ///
    /// Selection Process:
    /// 1. Evaluate all SSTables in the specified level
    /// 2. Calculate priority and score for each SSTable based on:
    ///    - Key range overlap with next level SSTables
    ///    - SSTable size relative to level expectations
    ///    - SSTable age (creation timestamp)
    ///    - Level-specific priority rules
    /// 3. Sort candidates by score (descending) and priority (High > Medium > Low)
    ///
    /// Design Decision: This approach ensures we select SSTables that will provide
    /// the maximum benefit when compacted, balancing read performance improvement,
    /// write amplification reduction, and space efficiency.
    ///
    /// Performance: The evaluation is done once per compaction cycle, making it
    /// efficient even for large numbers of SSTables.
    /// </summary>
    /// <param name="dbRef">Reference to the OcisDB instance</param>
    /// <param name="level">The level to select SSTables from</param>
    /// <returns>List of SSTable candidates sorted by compaction priority</returns>
    static member private selectSSTablesForCompaction(dbRef: OcisDB ref, level: int) : SSTableCandidate list =
        let currentLevelSSTables = dbRef.Value.SSTables |> Map.tryFind level
        let nextLevelSSTables = dbRef.Value.SSTables |> Map.tryFind (level + 1)

        match currentLevelSSTables with
        | Some sstables when not (sstables |> List.isEmpty) ->
            Logger.Debug $"Evaluating {sstables.Length} SSTables in level {level} for compaction"

            let candidates =
                sstables
                |> List.map (fun sstbl ->
                    // Calculate compaction priority based on multiple factors
                    let priority = OcisDB.calculateCompactionPriority (sstbl, level, nextLevelSSTables)

                    // Calculate overlap size for additional context
                    let overlapSize =
                        match nextLevelSSTables with
                        | Some nextSSTables -> nextSSTables |> List.filter sstbl.Overlaps |> List.sumBy _.Size |> int64
                        | None -> 0L

                    // Calculate comprehensive compaction score
                    let score = OcisDB.calculateCompactionScore (sstbl, level, nextLevelSSTables)

                    { SSTable = sstbl
                      Priority = priority
                      OverlapSize = overlapSize
                      Score = score })

            // Sort by score (descending), then by priority (High > Medium > Low)
            // This ensures the most beneficial SSTables are compacted first
            let sortedCandidates =
                candidates
                |> List.sortByDescending (fun candidate -> candidate.Score, candidate.Priority)

            Logger.Debug $"Selected {sortedCandidates.Length} SSTable candidates for level {level}"

            sortedCandidates
        | _ ->
            Logger.Debug $"No SSTables found in level {level} for compaction"
            []

    /// <summary>
    /// Find SSTables that contain remapped value locations for selective recompaction
    /// </summary>
    static member private findSSTablesWithRemappedValues
        (dbRef: OcisDB ref, remappedLocations: Map<int64, int64>)
        : SSTbl list =
        let remappedValueSet = remappedLocations |> Map.toSeq |> Seq.map fst |> Set.ofSeq

        dbRef.Value.SSTables
        |> Map.toSeq
        |> Seq.collect (fun (_, sstblList) ->
            sstblList
            |> List.filter (fun sstbl ->
                sstbl
                |> Seq.exists (fun (KeyValue(_, valueLoc)) -> remappedValueSet.Contains valueLoc)))
        |> Seq.toList

    member _.Dir = dir

    member _.CurrentMemtbl
        with get () = currentMemtbl
        and private set (memtbl: Memtbl) = currentMemtbl <- memtbl

    member _.ImmutableMemtbls = immutableMemtbls

    member _.SSTables
        with get () = ssTables
        and private set (tables: Map<int, SSTbl list>) = ssTables <- tables

    member _.ValueLog
        with get () = valog
        and private set (v: Valog) = valog <- v

    member _.PendingRemappedLocations // New public property for pending remappings
        with get () = internalPendingRemappedLocations
        and private set (m: Map<int64, int64>) = internalPendingRemappedLocations <- m

    member _.WAL = wal

    // Implement IDisposable for OcisDB to ensure all underlying resources are properly closed
    interface IDisposable with
        member this.Dispose() =
            // Flush WAL before closing to ensure all data is persisted
            this.WAL.Flush()
            // Close file streams
            this.ValueLog.Close()
            this.WAL.Close()
            // Dispose all SSTables
            this.SSTables
            |> Map.iter (fun _ sstblList -> sstblList |> List.iter (fun sstbl -> (sstbl :> IDisposable).Dispose()))

    /// <summary>
    /// Merges multiple SSTables into a new SSTable.
    /// This is the main entry point for SSTable merging that orchestrates the entire merge process.
    /// The process involves: collecting entries, merging/deduplicating, applying remapping, and creating the final SSTable.
    ///
    /// Design Decision: We use timestamp-based conflict resolution (latest wins) because:
    /// 1. It ensures deterministic behavior in distributed scenarios
    /// 2. It's consistent with LSM-tree semantics
    /// </summary>
    static member private mergeSSTables
        (dbRef: OcisDB ref, sstblsToMerge: SSTbl list, targetLevel: int, remappedLocations: Map<int64, int64> option)
        : Result<SSTbl option * (byte array * ValueLocation) list, string> =
        try
            Logger.Debug $"Starting SSTable merge: {sstblsToMerge.Length} SSTables to target level {targetLevel}"

            // Step 1: Collect all entries from input SSTables
            let allEntries = OcisDB.collectEntriesFromSSTables sstblsToMerge

            // Step 2: Merge and deduplicate entries based on key and timestamp
            let mergedEntries = OcisDB.mergeAndDeduplicateEntries allEntries

            // Step 3: Apply value location remapping if provided (used during garbage collection)
            let remappedEntries = OcisDB.applyRemappingToEntries mergedEntries remappedLocations

            // Step 4: Create the final merged SSTable
            OcisDB.createMergedSSTable dbRef remappedEntries targetLevel

        with ex ->
            Logger.Error $"Error during SSTable merge process: {ex.Message}"
            Error $"Error merging SSTables: {ex.Message}"

    /// <summary>
    /// Step 1: Collects all key-value entries from the specified SSTables.
    /// This function extracts entries from multiple SSTables and prepares them for merging.
    ///
    /// Performance Note: We collect all entries first to enable efficient sorting and deduplication.
    /// Memory usage scales with the total size of input SSTables.
    /// </summary>
    static member private collectEntriesFromSSTables
        (sstblsToMerge: SSTbl list)
        : (byte array * ValueLocation * int64) list =
        Logger.Debug $"Collecting entries from {sstblsToMerge.Length} SSTables"

        sstblsToMerge
        |> List.collect (fun sstbl -> sstbl |> Seq.map (fun kvp -> kvp.Key, kvp.Value, sstbl.Timestamp) |> Seq.toList)
        |> List.sortBy (fun (key, _, timestamp) -> key, -timestamp) // Sort by key asc, timestamp desc

    /// <summary>
    /// Step 2: Merges and deduplicates entries based on key and timestamp.
    /// For each unique key, keeps only the entry with the most recent timestamp.
    ///
    /// Algorithm: Single-pass deduplication using a set to track processed keys.
    /// Time Complexity: O(N) where N is the total number of entries.
    /// Space Complexity: O(K) for processed keys set, where K is the number of unique keys.
    /// </summary>
    static member private mergeAndDeduplicateEntries
        (allEntries: (byte array * ValueLocation * int64) list)
        : (byte array * ValueLocation) list =
        Logger.Debug $"Merging and deduplicating {allEntries.Length} entries"

        let mutable processedKeys = Set.empty<byte array>
        let mutable mergedEntries = []

        for key, valueLoc, _ in allEntries do
            if not (processedKeys.Contains key) then
                // First occurrence of this key (due to descending timestamp sort)
                mergedEntries <- (key, valueLoc) :: mergedEntries
                processedKeys <- processedKeys.Add key

        // Reverse to maintain key order
        List.rev mergedEntries

    /// <summary>
    /// Step 3: Applies value location remapping to entries.
    /// This is used during Valog garbage collection to update pointers to moved values.
    ///
    /// Design Decision: Remapping is optional to avoid overhead when not needed.
    /// Deletion markers (-1L) are preserved as they indicate logical deletions.
    /// </summary>
    static member private applyRemappingToEntries
        (entries: (byte array * ValueLocation) list)
        (remappedLocations: Map<int64, int64> option)
        : (byte array * ValueLocation) list =
        match remappedLocations with
        | Some remap when not remap.IsEmpty ->
            Logger.Debug $"Applying remapping to {entries.Length} entries"

            entries
            |> List.map (fun (key, valueLoc) ->
                if valueLoc = -1L then
                    // Preserve deletion markers
                    (key, valueLoc)
                else
                    match remap.TryGetValue valueLoc with
                    | true, newLoc -> (key, newLoc)
                    | false, _ -> (key, valueLoc))
        | _ -> entries

    /// <summary>
    /// Step 4: Creates the final merged SSTable from processed entries.
    /// Handles memory allocation optimization and file I/O.
    ///
    /// Performance Optimization: Pre-allocates arrays based on estimated capacity
    /// to reduce memory allocations during the merge process.
    /// </summary>
    static member private createMergedSSTable
        (dbRef: OcisDB ref)
        (entries: (byte array * ValueLocation) list)
        (targetLevel: int)
        : Result<SSTbl option * (byte array * ValueLocation) list, string> =
        if entries.IsEmpty then
            Logger.Debug "No entries to merge, returning empty result"
            Ok(None, [])
        else
            try
                // Create MemTable for the merged entries
                let mergedMemtbl = Memtbl()
                entries |> List.iter mergedMemtbl.Add

                // Generate unique filename for the new SSTable
                let newSSTblPath =
                    Path.Combine(dbRef.Value.Dir, $"sstbl-{Guid.NewGuid().ToString()}.sst")

                let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

                Logger.Debug $"Flushing {entries.Length} entries to SSTable at level {targetLevel}"

                // Flush to disk
                let flushedSSTblPath =
                    SSTbl.Flush(mergedMemtbl, newSSTblPath, timestamp, targetLevel)

                // Open and validate the new SSTable
                match SSTbl.Open flushedSSTblPath with
                | Some newSSTbl ->
                    Logger.Debug $"Successfully created merged SSTable: {newSSTbl.Path}"
                    Ok(Some newSSTbl, entries)
                | None ->
                    Logger.Error $"Failed to open newly created SSTable: {flushedSSTblPath}"

                    Error $"Failed to open newly merged SSTable at {flushedSSTblPath}"
            with ex ->
                Logger.Error $"Error creating merged SSTable: {ex.Message}"
                Error $"Error creating merged SSTable: {ex.Message}"

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
                    match remappedLocations.TryGetValue originalValueLoc with
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

                match SSTbl.Open flushedSSTblPath with
                | Some newSSTbl -> Ok(Some newSSTbl)
                | None -> Error $"Failed to open recompacted SSTable at {flushedSSTblPath}"
            else
                Ok None // No changes, no new SSTbl needed. Or all entries were deletion markers and didn't get added.
        with ex ->
            Error $"Error recompacting SSTable {sstbl.Path}: {ex.Message}"

    /// <summary>
    /// Performs optimized compaction for Level 0 SSTables with intelligent selection.
    /// </summary>
    /// <summary>
    /// Performs optimized compaction for Level 0 SSTables.
    /// </summary>
    member this.CompactLevel0() : unit =
        let level0SSTablesOption = this.SSTables |> Map.tryFind 0

        match level0SSTablesOption with
        | Some level0SSTables when level0SSTables.Length >= L0_COMPACTION_THRESHOLD ->
            // Use intelligent selection for Level 0
            let candidates = OcisDB.selectSSTablesForCompaction ((ref this), 0)

            // Select top candidates with high priority or score above threshold
            let selectedCandidates =
                candidates
                |> List.filter (fun c ->
                    match c.Priority with
                    | High -> true
                    | Medium -> c.Score >= COMPACTION_SCORE_THRESHOLD
                    | Low -> false)
                |> List.truncate (max 2 (level0SSTables.Length / 2)) // Merge at most half of L0 SSTables

            if not selectedCandidates.IsEmpty then
                let sstblsToMerge = selectedCandidates |> List.map _.SSTable
                let targetLevel = 1

                Logger.Debug
                    $"Level 0 compaction: Selected {sstblsToMerge.Length} out of {level0SSTables.Length} SSTables for compaction"

                match
                    OcisDB.compact ((ref this), 0, sstblsToMerge, targetLevel, Some this.PendingRemappedLocations)
                with
                | Ok(newSSTblOption, liveEntries) ->
                    this.UpdateSSTablesAfterCompaction(newSSTblOption, sstblsToMerge, [])
                | Error msg -> Logger.Error $"Error during Level 0 compaction: {msg}"
            else
                Logger.Debug "Level 0 compaction: No suitable SSTables found for compaction"
        | _ -> () // No compaction needed for Level 0

    /// <summary>
    /// Helper to update SSTables map and dispose/delete old SSTable files after compaction.
    /// </summary>
    /// <summary>
    /// Updates SSTables map and disposes/deletes old SSTable files after compaction.
    /// </summary>
    member this.UpdateSSTablesAfterCompaction
        (newSSTblOption: SSTbl option, sstblsToRemoveFromSource: SSTbl list, sstblsToRemoveFromTarget: SSTbl list)
        : unit =
        let mutable updatedSSTables = this.SSTables

        // Remove SSTables from source level
        if not (sstblsToRemoveFromSource |> List.isEmpty) then
            let sourceLevel = sstblsToRemoveFromSource.Head.Level // Assuming all SSTables in the list are from the same level

            updatedSSTables <-
                updatedSSTables
                |> Map.change sourceLevel (fun currentListOption ->
                    match currentListOption with
                    | Some currentList ->
                        Some(
                            currentList
                            |> List.filter (fun s ->
                                not (sstblsToRemoveFromSource |> List.exists (fun oldS -> oldS.Path = s.Path)))
                        )
                    | None -> None) // If source level list is empty, remove the key from the map

        // Remove SSTables from target level (if different from source and not empty)
        if not (sstblsToRemoveFromTarget |> List.isEmpty) then
            let targetLevel = sstblsToRemoveFromTarget.Head.Level // Assuming all SSTables in the list are from the same level

            if
                Some targetLevel
                <> if not (sstblsToRemoveFromSource |> List.isEmpty) then
                       Some sstblsToRemoveFromSource.Head.Level
                   else
                       None
            then // Only remove from target if it's a different level than source
                updatedSSTables <-
                    updatedSSTables
                    |> Map.change targetLevel (fun currentListOption ->
                        match currentListOption with
                        | Some currentList ->
                            Some(
                                currentList
                                |> List.filter (fun s ->
                                    not (sstblsToRemoveFromTarget |> List.exists (fun oldS -> oldS.Path = s.Path)))
                            )
                        | None -> None) // If target level list is empty, remove the key from the map

        match newSSTblOption with
        | Some newSSTbl ->
            let targetLevel = newSSTbl.Level // Use the level of the new SSTbl

            updatedSSTables <-
                updatedSSTables
                |> Map.change targetLevel (fun currentListOption ->
                    match currentListOption with
                    | Some currentList -> Some(newSSTbl :: currentList)
                    | None -> Some [ newSSTbl ])
        | None -> ()

        this.SSTables <- updatedSSTables

        (sstblsToRemoveFromSource @ sstblsToRemoveFromTarget)
        |> List.iter (fun sstbl ->
            (sstbl :> IDisposable).Dispose()
            File.Delete sstbl.Path)

        match newSSTblOption with
        | Some newSSTbl ->
            Logger.Debug
                $"Compaction successful: Merged {sstblsToRemoveFromSource.Length + sstblsToRemoveFromTarget.Length} SSTables to Level {newSSTbl.Level}. New SSTable: {newSSTbl.Path}"
        | None ->
            Logger.Debug
                $"Compaction successful: Removed {sstblsToRemoveFromSource.Length + sstblsToRemoveFromTarget.Length} SSTables (all deletion markers or no new SSTbl)."

    /// <summary>
    /// Performs optimized compaction for levels greater than 0 using intelligent SSTable selection.
    /// </summary>
    /// <summary>
    /// Performs optimized compaction for levels greater than 0.
    /// </summary>
    member this.CompactLevel(level: int) : unit =
        let nextLevel = level + 1

        // Use intelligent selection strategy
        let candidates = OcisDB.selectSSTablesForCompaction ((ref this), level)

        if candidates.IsEmpty then
            Logger.Debug $"Level {level}: No SSTables available for compaction"
        else
            // Select best candidates for compaction
            let selectedCandidates =
                candidates
                |> List.filter (fun c ->
                    match c.Priority with
                    | High -> true
                    | Medium -> c.Score >= COMPACTION_SCORE_THRESHOLD
                    | Low -> false)
                |> List.truncate 3 // Limit to 3 SSTables per compaction to reduce write amplification

            if selectedCandidates.IsEmpty then
                Logger.Debug $"Level {level}: No suitable SSTables found for compaction"
            else
                let currentLevelSSTablesOption = this.SSTables |> Map.tryFind level
                let nextLevelSSTablesOption = this.SSTables |> Map.tryFind nextLevel

                let targetLevelSize =
                    int64 L0_COMPACTION_THRESHOLD * (pown (int64 LEVEL_SIZE_MULTIPLIER) (level - 1))

                let currentLevelTotalSize =
                    match currentLevelSSTablesOption with
                    | Some sstables -> sstables |> List.sumBy _.Size
                    | None -> 0L

                // Only proceed if level size threshold is exceeded or we have high priority candidates
                let hasHighPriority = selectedCandidates |> List.exists (fun c -> c.Priority = High)

                if currentLevelTotalSize >= targetLevelSize || hasHighPriority then
                    for candidate in selectedCandidates do
                        let sstblToCompact = candidate.SSTable

                        let overlappingSSTables =
                            match nextLevelSSTablesOption with
                            | Some nextLevelSSTables -> nextLevelSSTables |> List.filter sstblToCompact.Overlaps
                            | None -> []

                        let sstblsToMerge = sstblToCompact :: overlappingSSTables

                        Logger.Debug
                            $"Level {level} compaction: Compacting SSTable with score {candidate.Score:F2}, overlap: {candidate.OverlapSize / 1024L / 1024L}MB"

                        let compactResult =
                            OcisDB.compact (
                                (ref this),
                                level,
                                sstblsToMerge,
                                nextLevel,
                                Some this.PendingRemappedLocations
                            )

                        match compactResult with
                        | Ok(newSSTblOption, _) ->
                            this.UpdateSSTablesAfterCompaction(newSSTblOption, [ sstblToCompact ], overlappingSSTables)
                        | Error msg -> Logger.Error $"Error during Level {level} compaction: {msg}"
                else
                    Logger.Debug
                        $"Level {level}: Size threshold not met ({currentLevelTotalSize} < {targetLevelSize}) and no high priority candidates"

    /// <summary>
    /// Performs compaction operations in single-threaded mode for maximum performance.
    /// </summary>
    member this.PerformCompaction() : unit =
        // Always try Level 0 first (highest priority)
        this.CompactLevel0()

        // Process other levels sequentially
        for level in 1..5 do // Support up to level 5
            this.CompactLevel level

    /// <summary>
    /// Performs recompaction for remapped value locations.
    /// </summary>
    member this.PerformRecompactionForRemapped(remappedLocations: Map<int64, int64>) : unit =
        Logger.Debug $"Starting recompaction request for {remappedLocations.Count} remapped locations."

        // Selective recompaction: only recompact SSTables that contain remapped values
        let sstablesToRecompact =
            OcisDB.findSSTablesWithRemappedValues ((ref this), remappedLocations)

        if sstablesToRecompact.IsEmpty then
            Logger.Debug "No SSTables contain remapped values, skipping recompaction."
        else
            Logger.Debug
                $"Found {sstablesToRecompact.Length} SSTables containing remapped values for selective recompaction."

            let mutable updatedSSTables = this.SSTables
            let mutable recompactedCount = 0

            // Group SSTables by level for efficient processing
            let sstablesByLevel =
                sstablesToRecompact |> List.groupBy (fun sstbl -> sstbl.Level) |> Map.ofList

            // Process each level sequentially
            for KeyValue(level, levelSSTables) in sstablesByLevel do
                let levelSSTablesArray = Array.ofList levelSSTables
                let newSSTblArray = Array.zeroCreate<SSTbl> levelSSTablesArray.Length
                let mutable newSSTblCount = 0
                let mutable levelRecompactedCount = 0

                for sstbl in levelSSTablesArray do
                    match OcisDB.recompactSSTable ((ref this), sstbl, remappedLocations) with
                    | Ok(Some newSSTbl) ->
                        newSSTblArray[newSSTblCount] <- newSSTbl
                        newSSTblCount <- newSSTblCount + 1
                        (sstbl :> IDisposable).Dispose()
                        File.Delete sstbl.Path
                        levelRecompactedCount <- levelRecompactedCount + 1

                        Logger.Debug $"Recompacted SSTable {sstbl.Path} to {newSSTbl.Path} (Level {level})"
                    | Ok None ->
                        newSSTblArray[newSSTblCount] <- sstbl // No change, keep original SSTable
                        newSSTblCount <- newSSTblCount + 1
                    | Error msg ->
                        Logger.Error $"Error recompacting SSTable {sstbl.Path}: {msg}"
                        newSSTblArray[newSSTblCount] <- sstbl // On error, keep original SSTable
                        newSSTblCount <- newSSTblCount + 1

                let finalSSTblList =
                    if newSSTblCount = levelSSTablesArray.Length then
                        newSSTblArray |> Array.toList
                    else
                        newSSTblArray |> Array.take newSSTblCount |> Array.toList

                updatedSSTables <- updatedSSTables |> Map.add level finalSSTblList
                recompactedCount <- recompactedCount + levelRecompactedCount

            this.SSTables <- updatedSSTables

            Logger.Debug
                $"Completed selective recompaction for Valog GC. Recompacted {recompactedCount} out of {sstablesToRecompact.Length} affected SSTables."

    /// <summary>
    /// Flushes a memtable to SSTable synchronously.
    /// </summary>
    member this.FlushMemtableToSSTable(memtblToFlush: Memtbl) : unit =
        // Ensure WAL is flushed to disk before flushing memtable to SSTable
        // This guarantees durability: if system crashes, WAL can be replayed to recover data
        wal.Flush()

        let newSSTblPath = Path.Combine(this.Dir, $"sstbl-{Guid.NewGuid().ToString()}.sst")

        let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        let level = 0 // Always flush to Level 0

        try
            let flushedSSTblPath = SSTbl.Flush(memtblToFlush, newSSTblPath, timestamp, level)

            match SSTbl.Open flushedSSTblPath with
            | Some sstbl ->
                // Update the SSTables map in the OcisDB instance
                this.SSTables <-
                    this.SSTables
                    |> Map.change level (fun currentListOption ->
                        match currentListOption with
                        | Some currentList -> Some(sstbl :: currentList)
                        | None -> Some [ sstbl ])

                // After flushing, trigger compaction if needed
                this.PerformCompaction()
            | None -> Logger.Error $"Error: Failed to open flushed SSTable at {flushedSSTblPath}"
        with ex ->
            Logger.Error $"Error flushing Memtable to SSTable: {ex.Message}"

    /// <summary>
    /// Background agent for handling Garbage Collection in Valog with enhanced features.
    ///
    /// Enhanced GC Features:
    /// 1. Smart Tail position updates based on SSTable timestamps
    /// 2. Incremental garbage collection support (when configured)
    /// 3. Better progress tracking and error handling
    /// 4. Automatic triggering based on Tail advancement
    /// </summary>
    /// <summary>
    /// Performs garbage collection synchronously in single-threaded mode.
    /// </summary>
    member this.PerformGarbageCollection() : unit =
        Async.RunSynchronously
        <| async {
            Logger.Info "Starting garbage collection"

            try
                // Step 1: Update Valog Tail position using smart strategy
                Logger.Debug "Updating Valog Tail position"
                this.UpdateValogTail()

                // Step 2: Collect all live value locations
                let liveLocations = OcisDB.GetLiveLocations(ref this)

                Logger.Info $"Collected {liveLocations.Count} live value locations"

                if liveLocations.IsEmpty then
                    Logger.Info "No live locations found, skipping garbage collection"
                else
                    // Step 3: Perform garbage collection using synchronous method
                    Logger.Info "Using synchronous garbage collection"

                    let! remappedLocationsOption = Valog.CollectGarbage(this.ValueLog, liveLocations)

                    match remappedLocationsOption with
                    | Some(Ok(newValog, remappedLocations)) ->
                        // Store reference to old valog for disposal
                        let oldValog = this.ValueLog

                        // Update to new valog
                        this.ValueLog <- newValog

                        Logger.Info $"GC completed: {remappedLocations.Count} locations remapped"

                        // Dispose the old valog after successful replacement
                        try
                            (oldValog :> IDisposable).Dispose()
                            Logger.Debug "Old Valog disposed after successful GC"
                        with ex ->
                            Logger.Warn $"Error disposing old Valog after GC: {ex.Message}"

                        // Handle remapped locations
                        for KeyValue(oldLoc, newLoc) in remappedLocations do
                            this.PendingRemappedLocations <- this.PendingRemappedLocations.Add(oldLoc, newLoc)

                        // Trigger compaction to update affected SSTables
                        this.PerformRecompactionForRemapped remappedLocations

                    | Some(Error msg) -> Logger.Error $"GC failed: {msg}"
                    | None -> Logger.Info "GC: No values needed to be moved"

            with ex ->
                Logger.Error $"GC error: {ex.Message}"
                Logger.Error $"Stack trace: {ex.StackTrace}"
        }

    /// <summary>
    /// Calculates the optimal Tail position for Valog based on SSTable timestamps.
    ///
    /// Smart Tail Update Strategy:
    /// The Tail represents the position before which all data is considered garbage.
    /// Instead of keeping Tail at 0, we can advance it based on the oldest SSTable
    /// that might still reference data in the Valog.
    ///
    /// Algorithm:
    /// 1. Find the oldest SSTable across all levels
    /// 2. Find the minimum value location referenced by that SSTable
    /// 3. Set Tail to that minimum location (minus safety margin)
    /// 4. This allows earlier parts of Valog to be garbage collected
    ///
    /// Benefits:
    /// - Reduces the amount of data that needs to be processed during GC
    /// - Allows more aggressive garbage collection
    /// - Improves overall system performance by reducing Valog size
    ///
    /// Safety Considerations:
    /// - Keep a safety margin to account for potential timing issues
    /// - Only advance Tail if we have high confidence no older data is needed
    /// - Conservative approach: never advance beyond what's absolutely safe
    /// </summary>
    /// <param name="dbRef">Reference to the OcisDB instance</param>
    /// <returns>The calculated optimal Tail position</returns>
    static member private CalculateOptimalTailPosition(dbRef: OcisDB ref) : int64 =
        try
            // Safety margin: don't advance Tail too close to actually referenced data
            let safetyMargin = 1024L * 1024L // 1MB safety margin

            // Find all SSTables across all levels
            let allSSTables =
                dbRef.Value.SSTables
                |> Map.toSeq
                |> Seq.collect (fun (_, sstblList) -> sstblList)
                |> Seq.toList

            if allSSTables.IsEmpty then
                Logger.Debug "No SSTables found, keeping Tail at 0"
                0L
            else
                // Find the oldest SSTable (by timestamp)
                let oldestSSTable = allSSTables |> List.minBy (fun sstbl -> sstbl.Timestamp)

                Logger.Debug $"Oldest SSTable timestamp: {oldestSSTable.Timestamp}, level: {oldestSSTable.Level}"

                // Find the minimum value location in the oldest SSTable
                let minValueLocation =
                    oldestSSTable
                    |> Seq.map (fun (KeyValue(_, valueLoc)) -> valueLoc)
                    |> Seq.filter (fun loc -> loc <> -1L) // Exclude deletion markers
                    |> Seq.min

                // Apply safety margin and ensure we don't go below 0
                let optimalTail = max 0L (minValueLocation - safetyMargin)

                Logger.Debug
                    $"Calculated optimal Tail position: {optimalTail} (min location: {minValueLocation}, safety margin: {safetyMargin})"

                optimalTail

        with ex ->
            Logger.Warn $"Error calculating optimal Tail position: {ex.Message}, keeping current Tail"

            dbRef.Value.ValueLog.Tail

    /// <summary>
    /// Updates the Valog Tail position using smart timestamp-based strategy.
    ///
    /// This function should be called periodically to advance the Tail and allow
    /// more aggressive garbage collection.
    /// </summary>
    member this.UpdateValogTail() : unit =
        try
            let currentTail = this.ValueLog.Tail
            let dbRef = ref this // Create a reference to self for static method
            let optimalTail = OcisDB.CalculateOptimalTailPosition dbRef

            if optimalTail > currentTail then
                Logger.Info
                    $"Advancing Valog Tail from {currentTail} to {optimalTail} ({optimalTail - currentTail} bytes freed)"

                this.ValueLog.Tail <- optimalTail

                // Trigger GC if the advancement is significant
                let advancementThreshold = 10L * 1024L * 1024L // 10MB threshold

                if optimalTail - currentTail > advancementThreshold then
                    Logger.Info "Significant Tail advancement detected, triggering garbage collection"
                    this.PerformGarbageCollection()
            else
                Logger.Debug $"Tail position unchanged (current: {currentTail}, optimal: {optimalTail})"

        with ex ->
            Logger.Error $"Error updating Valog Tail: {ex.Message}"

    /// <summary>
    /// Helper function to collect all live value locations from MemTables and SSTables.
    ///
    /// Performance Optimization:
    /// This function is critical for garbage collection efficiency.
    /// We use Set to automatically deduplicate locations and enable fast lookups.
    /// </summary>
    static member private GetLiveLocations(dbRef: OcisDB ref) : Set<int64> =
        let mutable liveLocations = Set.empty<int64>

        // From CurrentMemtbl
        dbRef.Value.CurrentMemtbl
        |> Seq.iter (fun (KeyValue(_, valueLoc)) ->
            if valueLoc <> -1L then // Exclude deletion markers
                liveLocations <- liveLocations.Add valueLoc)

        // From ImmutableMemtbls
        dbRef.Value.ImmutableMemtbls
        |> Seq.collect (fun memtbl -> memtbl |> Seq.map (fun (KeyValue(_, valueLoc)) -> valueLoc))
        |> Seq.filter (fun valueLoc -> valueLoc <> -1L) // Exclude deletion markers
        |> Seq.iter (fun valueLoc -> liveLocations <- liveLocations.Add valueLoc)

        // From SSTables
        dbRef.Value.SSTables
        |> Map.toSeq
        |> Seq.collect (fun (_, sstblList) ->
            sstblList
            |> Seq.collect (fun sstbl -> sstbl |> Seq.map (fun (KeyValue(_, valueLoc)) -> valueLoc)))
        |> Seq.filter (fun valueLoc -> valueLoc <> -1L) // Exclude deletion markers
        |> Seq.iter (fun valueLoc -> liveLocations <- liveLocations.Add valueLoc)

        Logger.Debug $"Collected {liveLocations.Count} live value locations for GC"
        liveLocations

    /// <summary>
    /// Opens an existing WiscKeyDB or creates a new one if it doesn't exist.
    /// </summary>
    /// <param name="dir">The directory where the database files are stored.</param>
    /// <returns>A Result type: Ok OcisDB instance if successful, otherwise an Error string.</returns>
    static member Open(dir: string, flushThreshold: int) : Result<OcisDB, string> =
        try
            // Validate inputs
            if System.String.IsNullOrWhiteSpace dir then
                Error "Directory path cannot be null or empty"
            elif flushThreshold <= 0 then
                Error $"Invalid flush threshold: {flushThreshold}. Must be greater than 0"
            else
                Logger.Info $"Opening OcisDB at directory: {dir}"

                // Ensure the directory exists
                let createDirResult =
                    try
                        if not (Directory.Exists dir) then
                            Directory.CreateDirectory dir |> ignore
                            Logger.Info $"Created database directory: {dir}"

                        Ok()
                    with
                    | :? System.IO.IOException as ioEx ->
                        Error $"Failed to create database directory '{dir}': {ioEx.Message}"
                    | :? System.UnauthorizedAccessException ->
                        Error $"Access denied creating database directory '{dir}'"

                match createDirResult with
                | Error msg -> Error msg
                | Ok() ->

                    // Create Valog
                    let valogPath = Path.Combine(dir, "valog.vlog")
                    Logger.Debug $"Creating Valog at: {valogPath}"

                    Valog.Create valogPath
                    |> Result.bind (fun valog ->
                        Logger.Debug "Valog created successfully"

                        // Create WAL
                        let walPath = Path.Combine(dir, "wal.log")
                        Logger.Debug $"Creating WAL at: {walPath}"

                        Wal.Create walPath
                        |> Result.bind (fun wal ->
                            Logger.Debug "WAL created successfully"

                            // Replay WAL to reconstruct CurrentMemTable
                            let initialMemtbl = Memtbl()
                            Logger.Debug "Replaying WAL entries..."

                            let replayedEntries = Wal.Replay walPath
                            let mutable replayCount = 0

                            for entry in replayedEntries do
                                match entry with
                                | WalEntry.Set(key, valueLoc) ->
                                    initialMemtbl.Add(key, valueLoc)
                                    replayCount <- replayCount + 1
                                | WalEntry.Delete key ->
                                    initialMemtbl.Delete key
                                    replayCount <- replayCount + 1

                            Logger.Info $"Replayed {replayCount} WAL entries into MemTable"

                            // Load existing SSTables
                            let mutable loadedSSTables = Map.empty<int, SSTbl list>
                            let mutable loadedCount = 0
                            let mutable failedCount = 0

                            try
                                let sstblFiles = Directory.GetFiles(dir, "sstbl-*.sst")

                                Logger.Info $"Found {sstblFiles.Length} SSTable files to load"

                                for filePath in sstblFiles do
                                    try
                                        Logger.Debug $"Loading SSTable: {filePath}"

                                        match SSTbl.Open filePath with
                                        | Some sstbl ->
                                            loadedSSTables <-
                                                loadedSSTables
                                                |> Map.change sstbl.Level (fun currentListOption ->
                                                    match currentListOption with
                                                    | Some currentList -> Some(sstbl :: currentList)
                                                    | None -> Some [ sstbl ])

                                            loadedCount <- loadedCount + 1

                                            Logger.Debug
                                                $"Loaded SSTable: {Path.GetFileName filePath} (Level {sstbl.Level})"
                                        | None ->
                                            Logger.Warn
                                                $"Failed to open SSTable file: {filePath}. File may be corrupted or inaccessible."

                                            failedCount <- failedCount + 1
                                    with ex ->
                                        Logger.Error $"Error loading SSTable '{filePath}': {ex.Message}"

                                        failedCount <- failedCount + 1

                                if failedCount > 0 then
                                    Logger.Warn
                                        $"Failed to load {failedCount} out of {sstblFiles.Length} SSTable files"

                            with
                            | :? System.IO.DirectoryNotFoundException ->
                                Logger.Warn $"SSTable directory not found: {dir}"
                            | :? System.IO.IOException as ioEx ->
                                Logger.Error $"I/O error accessing SSTable directory: {ioEx.Message}"
                            | ex -> Logger.Error $"Unexpected error loading SSTables: {ex.Message}"

                            Logger.Info
                                $"Successfully loaded {loadedCount} SSTables across {loadedSSTables.Count} levels"

                            // Create OcisDB instance without agents
                            Logger.Debug "Creating OcisDB instance"

                            let ocisDB =
                                new OcisDB(
                                    dir,
                                    initialMemtbl,
                                    [], // Start with empty immutable memtables list
                                    loadedSSTables,
                                    valog,
                                    wal,
                                    flushThreshold
                                )

                            Logger.Debug "OcisDB instance created successfully"

                            Logger.Info
                                $"OcisDB opened successfully at {dir} with {initialMemtbl.Count} MemTable entries and {loadedCount} SSTables"

                            Ok ocisDB))
        with
        | :? System.IO.PathTooLongException -> Error $"Database path too long: '{dir}'"
        | :? System.UnauthorizedAccessException -> Error $"Access denied to database directory: '{dir}'"
        | :? System.IO.IOException as ioEx when ioEx.Message.Contains "disk" || ioEx.Message.Contains "full" ->
            Error "Disk is full - cannot create database files"
        | :? System.OutOfMemoryException -> Error "Out of memory - database may be too large to load"
        | ex ->
            Logger.Error $"Unexpected error opening OcisDB: {ex.Message}"
            Error $"Failed to open WiscKeyDB: {ex.Message}"

    /// <summary>
    /// Ensures Valog is available, reopening it if necessary after GC.
    /// </summary>
    member private this.EnsureValogAvailable() : Result<unit, string> =
        try
            // Check if Valog is in an invalid state (null, disposed, or file closed)
            let valogInvalid =
                obj.ReferenceEquals(this.ValueLog, null)
                || (try
                        // Try to check if the FileStream is closed/disposed
                        not this.ValueLog.FileStream.CanRead
                    with _ ->
                        true) // If we can't check, assume it's invalid

            if valogInvalid then
                Logger.Debug "Valog is invalid (null or disposed), reopening with retry logic..."

                let valogPath = Path.Combine(this.Dir, "valog.vlog")

                // Use a lock to ensure only one thread can recreate Valog
                lock this (fun () ->
                    // Double-check after acquiring lock
                    let stillInvalid =
                        obj.ReferenceEquals(this.ValueLog, null)
                        || (try
                                not this.ValueLog.FileStream.CanRead
                            with _ ->
                                true)

                    if stillInvalid then
                        // Retry logic with exponential backoff
                        let rec retryOpen attempts =
                            if attempts >= 10 then // Increased from 5 to 10
                                Logger.Error $"Failed to reopen Valog after {attempts} attempts"

                                Error $"Failed to reopen Valog after {attempts} attempts"
                            else
                                match Valog.Create valogPath with
                                | Ok newValog ->
                                    this.ValueLog <- newValog
                                    Logger.Info "Valog reopened successfully after GC"
                                    Ok()
                                | Error msg ->
                                    Logger.Warn $"Failed to reopen Valog (attempt {attempts + 1}): {msg}"
                                    // Wait before retry with longer exponential backoff
                                    System.Threading.Thread.Sleep(200 * (1 <<< attempts)) // Increased from 100ms
                                    retryOpen (attempts + 1)

                        retryOpen 0
                    else
                        Ok())
            else
                Ok()
        with ex ->
            Logger.Error $"Error ensuring Valog availability: {ex.Message}"
            Error $"Valog availability check failed: {ex.Message}"

    /// <summary>
    /// Sets a key-value pair in the database.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>A Result: Ok unit if successful, otherwise an Error string.</returns>
    member this.Set(key: byte array, value: byte array) : Result<unit, string> =
        try
            // Ensure Valog is available
            match this.EnsureValogAvailable() with
            | Error msg -> Error msg
            | Ok() ->
                // 1. Write to ValueLog
                let valueLocation = valog.Append(key, value)

                // 2. Write to WAL
                wal.Append(WalEntry.Set(key, valueLocation))

                // 3. Write to CurrentMemTable
                currentMemtbl.Add(key, valueLocation)

                // 4. Check MemTable size and trigger flush if needed
                this.CheckAndFlushMemtable()

                Ok()
        with ex ->
            Error $"Failed to set key-value pair: {ex.Message}"

    /// <summary>
    /// Helper function to check MemTable size and trigger flush if needed.
    /// </summary>
    member private this.CheckAndFlushMemtable() : unit =
        let memtblCount = currentMemtbl.Count

        if memtblCount >= flushThreshold then
            let frozenMemtbl = currentMemtbl
            immutableMemtbls <- frozenMemtbl :: immutableMemtbls // Add to immutable list
            currentMemtbl <- Memtbl() // Create a new empty MemTable
            this.FlushMemtableToSSTable frozenMemtbl

    /// <summary>
    /// Gets the value associated with the given key.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>A Result: Ok (Some value) if found, Ok None if not found, otherwise an Error string.</returns>
    member this.Get(key: byte array) : Result<byte array option, string> =
        try
            // Ensure Valog is available
            match this.EnsureValogAvailable() with
            | Error msg -> Error msg
            | Ok() ->
                let resolveValue = this.ResolveValueLocation // Use the new helper function

                // 1. Search CurrentMemTable
                match this.CurrentMemtbl.TryGet key with
                | Some valueLocation -> resolveValue valueLocation
                | None ->
                    // 2. Search ImmutableMemTables (from newest to oldest)
                    match this.SearchImmutableMemtables key with // Use the new helper function
                    | Some valueLocation -> resolveValue valueLocation
                    | None ->
                        // 3. Search SSTables (from Level 0 upwards, newest within each level)
                        match this.SearchSSTables key with // Use the new helper function
                        | Some valueLocation -> resolveValue valueLocation
                        | None ->
                            // If not found anywhere
                            Ok None
        with ex ->
            Error $"Failed to get value for key {Encoding.UTF8.GetString key}: {ex.Message}"

    /// <summary>
    /// Helper function to resolve ValueLocation to actual value or error.
    /// </summary>
    member private this.ResolveValueLocation(valueLocation: int64) : Result<byte array option, string> =
        if valueLocation = -1L then
            Ok None // Deletion marker
        else
            // Ensure Valog is available before reading
            match this.EnsureValogAvailable() with
            | Error msg -> Error msg
            | Ok() ->
                match this.ValueLog.Read valueLocation with
                | Some(_, value) -> Ok(Some value)
                | None -> Error $"Failed to read value from Valog at location {valueLocation}."

    /// <summary>
    /// Helper function to search ImmutableMemTables for a given key.
    /// </summary>
    member private this.SearchImmutableMemtables(key: byte array) : ValueLocation option =
        // Search from newest to oldest (list is prepended)
        List.tryPick (fun (memtbl: Memtbl) -> memtbl.TryGet key) this.ImmutableMemtbls

    /// <summary>
    /// Helper function to search SSTables for a given key.
    /// </summary>
    member private this.SearchSSTables(key: byte array) : ValueLocation option =
        this.SSTables
        |> Map.toSeq
        |> Seq.sortBy fst // Sort by level (0, 1, 2...)
        |> Seq.tryPick (fun (_, sstblList) ->
            sstblList
            |> List.sortByDescending _.Timestamp // Search newest first within each level
            |> List.tryPick (fun sstbl -> sstbl.TryGet key))

    /// <summary>
    /// Deletes a key from the database.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <returns>A Result: Ok unit if successful, otherwise an Error string.</returns>
    member this.Delete(key: byte array) : Result<unit, string> =
        try
            // 1. Write deletion marker to WAL
            this.WAL.Append(WalEntry.Delete key)

            // 2. Add deletion marker to CurrentMemTable
            currentMemtbl.Delete key // Memtbl.Delete already adds -1L

            // Check MemTable size and trigger flush if needed (same logic as Set)
            this.CheckAndFlushMemtable()

            Ok()
        with ex ->
            Error $"Failed to delete key {Encoding.UTF8.GetString key}: {ex.Message}"
