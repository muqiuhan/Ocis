module Ocis.OcisDB

open System.Collections.Concurrent
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

// Messages for agents
type CompactionMessage =
  | FlushMemtable of Memtbl // Message to flush an immutable memtable to SSTable
  | TriggerCompaction // Message to trigger a compaction cycle
  | RecompactionForRemapped of Map<int64, int64> // New message to trigger recompaction for remapped value locations
  | ParallelCompaction of int // Message to trigger parallel compaction for a specific level

type FlushMessage = // New message type for FlushAgent
  | FlushMemtable of Memtbl

// Compaction strategy types
type CompactionPriority =
  | High // Level 0 SSTables or size-triggered
  | Medium // Overlap-triggered
  | Low // Background compaction

type SSTableCandidate =
  { SSTable : SSTbl
    Priority : CompactionPriority
    OverlapSize : int64
    Score : float } // Combined score for selection

type GcMessage = | TriggerGC // Message to trigger garbage collection in Valog (Removed parameter)

/// <summary>
/// OcisDB represents the main WiscKey storage engine instance.
/// It orchestrates operations across MemTables, SSTables, ValueLog, and WAL.
/// </summary>
type OcisDB
  (
    dir : string,
    currentMemtbl : Memtbl,
    immutableMemtbl : ConcurrentQueue<Memtbl>,
    ssTables : Map<int, SSTbl list>,
    valog : Valog,
    wal : Wal,
    compactionAgent : MailboxProcessor<CompactionMessage>,
    flushAgent : MailboxProcessor<FlushMessage>, // New parameter for flush agent
    gcAgent : MailboxProcessor<GcMessage>,
    flushThreshold : int
  )
  =
  /// Number of Level 0 SSTables to trigger compaction.
  /// For write-intensive applications, this value can be appropriately increased to reduce the merge frequency.
  /// For read-intensive applications, this value can be appropriately reduced to reduce the scan range of L0
  static let mutable L0_COMPACTION_THRESHOLD = 4

  /// Each level is approximately 10 times larger than the previous one
  static let mutable LEVEL_SIZE_MULTIPLIER = 5

  /// Maximum number of parallel compaction tasks
  static let mutable MAX_PARALLEL_COMPACTIONS = 3

  /// Compaction score threshold for triggering compaction
  static let mutable COMPACTION_SCORE_THRESHOLD = 1.0

  let mutable ssTables = ssTables
  let mutable currentMemtbl = currentMemtbl
  let mutable valog = valog
  let mutable internalPendingRemappedLocations = Map.empty<int64, int64> // Internal mutable field

  static member L0CompactionThreshold
    with get () = L0_COMPACTION_THRESHOLD
    and set (threshold : int) = L0_COMPACTION_THRESHOLD <- threshold

  static member LevelSizeMultiplier
    with get () = LEVEL_SIZE_MULTIPLIER
    and set (multiplier : int) = LEVEL_SIZE_MULTIPLIER <- multiplier

  static member MaxParallelCompactions
    with get () = MAX_PARALLEL_COMPACTIONS
    and set (max : int) = MAX_PARALLEL_COMPACTIONS <- max

  static member CompactionScoreThreshold
    with get () = COMPACTION_SCORE_THRESHOLD
    and set (threshold : float) = COMPACTION_SCORE_THRESHOLD <- threshold

  /// <summary>
  /// Calculate compaction priority for an SSTable based on various factors
  /// </summary>
  static member private calculateCompactionPriority
    (sstbl : SSTbl, level : int, nextLevelSSTables : SSTbl list option)
    : CompactionPriority
    =
    if level = 0 then
      High // Level 0 always has high priority
    else
      match nextLevelSSTables with
      | Some nextSSTables ->
        let overlapSize =
          nextSSTables
          |> List.filter sstbl.Overlaps
          |> List.sumBy _.Size
          |> int64

        let levelSize =
          int64 L0_COMPACTION_THRESHOLD
          * (pown (int64 LEVEL_SIZE_MULTIPLIER) (level - 1))

        let sizeRatio = float sstbl.Size / float levelSize

        if overlapSize > 0L && sizeRatio > 0.8 then High
        elif overlapSize > 0L then Medium
        else Low
      | None -> Low

  /// <summary>
  /// Calculate a score for SSTable compaction priority
  /// Higher score means higher priority for compaction
  /// </summary>
  static member private calculateCompactionScore
    (sstbl : SSTbl, level : int, nextLevelSSTables : SSTbl list option)
    : float
    =
    let ageScore =
      float sstbl.Timestamp
      / float (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ())

    let sizeScore = float sstbl.Size / 1024.0 / 1024.0 // Size in MB

    let overlapScore =
      match nextLevelSSTables with
      | Some nextSSTables ->
        nextSSTables
        |> List.filter sstbl.Overlaps
        |> List.sumBy _.Size
        |> float
        |> fun x -> x / 1024.0 / 1024.0 // Convert to MB
      | None -> 0.0

    // Weighted combination of factors
    let levelWeight = if level = 0 then 10.0 else 1.0
    ageScore * 0.3 + sizeScore * 0.3 + overlapScore * 0.4 + levelWeight

  /// <summary>
  /// Select best SSTables for compaction using improved strategy
  /// </summary>
  static member private selectSSTablesForCompaction
    (dbRef : OcisDB ref, level : int)
    : SSTableCandidate list
    =
    let currentLevelSSTables = dbRef.Value.SSTables |> Map.tryFind level
    let nextLevelSSTables = dbRef.Value.SSTables |> Map.tryFind (level + 1)

    match currentLevelSSTables with
    | Some sstables when not (sstables |> List.isEmpty) ->
      sstables
      |> List.map (fun sstbl ->
        let priority =
          OcisDB.calculateCompactionPriority (sstbl, level, nextLevelSSTables)

        let overlapSize =
          match nextLevelSSTables with
          | Some nextSSTables ->
            nextSSTables
            |> List.filter sstbl.Overlaps
            |> List.sumBy _.Size
            |> int64
          | None -> 0L

        let score =
          OcisDB.calculateCompactionScore (sstbl, level, nextLevelSSTables)

        { SSTable = sstbl
          Priority = priority
          OverlapSize = overlapSize
          Score = score })
      |> List.sortByDescending (fun candidate ->
        candidate.Score, candidate.Priority)
    | _ -> []

  /// <summary>
  /// Find SSTables that contain remapped value locations for selective recompaction
  /// </summary>
  static member private findSSTablesWithRemappedValues
    (dbRef : OcisDB ref, remappedLocations : Map<int64, int64>)
    : SSTbl list
    =
    let remappedValueSet =
      remappedLocations |> Map.toSeq |> Seq.map fst |> Set.ofSeq

    dbRef.Value.SSTables
    |> Map.toSeq
    |> Seq.collect (fun (_, sstblList) ->
      sstblList
      |> List.filter (fun sstbl ->
        sstbl
        |> Seq.exists (fun (KeyValue (_, valueLoc)) ->
          remappedValueSet.Contains valueLoc)))
    |> Seq.toList

  member _.Dir = dir

  member _.CurrentMemtbl
    with get () = currentMemtbl
    and private set (memtbl : Memtbl) = currentMemtbl <- memtbl

  member _.ImmutableMemtbl = immutableMemtbl

  member _.SSTables
    with get () = ssTables
    and private set (tables : Map<int, SSTbl list>) = ssTables <- tables

  member _.ValueLog
    with get () = valog
    and private set (v : Valog) = valog <- v

  member _.PendingRemappedLocations // New public property for pending remappings
    with get () = internalPendingRemappedLocations
    and private set (m : Map<int64, int64>) =
      internalPendingRemappedLocations <- m

  member _.WAL = wal
  member _.CompactionAgent = compactionAgent
  member _.FlushAgent = flushAgent // New public member
  member _.GCAgent = gcAgent

  // Implement IDisposable for OcisDB to ensure all underlying resources are properly closed
  interface IDisposable with
    member this.Dispose () =
      // Stop agents first
      compactionAgent.Dispose ()
      this.GCAgent.Dispose ()
      this.FlushAgent.Dispose () // Dispose the new flush agent
      // Close file streams
      this.ValueLog.Close ()
      this.WAL.Close ()
      // Dispose all SSTables
      this.SSTables
      |> Map.iter (fun _ sstblList ->
        sstblList |> List.iter (fun sstbl -> (sstbl :> IDisposable).Dispose ()))

  /// <summary>
  /// Merges multiple SSTables into a new SSTable.
  /// Handles duplicate keys by keeping the entry with the latest timestamp.
  /// Filters out deletion markers (-1L).
  /// </summary>
  static member private mergeSSTables
    (
      dbRef : OcisDB ref,
      sstblsToMerge : SSTbl list,
      targetLevel : int,
      remappedLocations : Map<int64, int64> option
    )
    : Result<SSTbl option * (byte array * ValueLocation) list, string>
    =
    try
      // Collect all key-value pairs from the SSTables, ordered by key and then timestamp (descending)
      let allEntries =
        sstblsToMerge
        |> List.collect (fun sstbl ->
          sstbl
          |> Seq.map (fun kvp -> kvp.Key, kvp.Value, sstbl.Timestamp)
          |> Seq.toList)
        |> List.sortBy (fun (key, _, timestamp) -> key, -timestamp) // Sort by key, then by timestamp descending

      let mergedMemtbl = Memtbl ()
      let mutable processedKeys = Set.empty<byte array> // Use a set to track processed keys

      // Pre-allocate array for live entries to reduce allocations
      // Estimate capacity based on input SSTables size
      let estimatedCapacity =
        sstblsToMerge |> List.sumBy (fun sstbl -> sstbl.Size |> int)

      let liveEntriesArray =
        Array.zeroCreate<byte array * ValueLocation> estimatedCapacity

      let mutable liveEntriesCount = 0

      for key, valueLoc, _ in allEntries do
        if not (processedKeys.Contains key) then // Only process if key hasn't been processed yet
          // Apply remapping if available
          let finalValueLoc =
            match remappedLocations with
            | Some remap ->
              match remap.TryGetValue valueLoc with
              | true, newLoc -> newLoc
              | false, _ -> valueLoc
            | None -> valueLoc

          if finalValueLoc <> -1L then // Filter out deletion markers
            mergedMemtbl.Add (key, finalValueLoc)

            if liveEntriesCount < estimatedCapacity then
              liveEntriesArray[liveEntriesCount] <- (key, finalValueLoc)
              liveEntriesCount <- liveEntriesCount + 1
          // If array is full, we'll handle this by creating a smaller final array

          processedKeys <- processedKeys.Add key // Mark key as processed

      // Create final array with actual live entries
      let liveEntriesInNewSSTbl =
        if liveEntriesCount = estimatedCapacity then
          liveEntriesArray
        else
          liveEntriesArray |> Array.take liveEntriesCount

      if mergedMemtbl.Count > 0 then
        let newSSTblPath =
          Path.Combine (
            dbRef.Value.Dir,
            $"sstbl-{Guid.NewGuid().ToString ()}.sst"
          )

        let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ()

        let flushedSSTblPath =
          SSTbl.Flush (mergedMemtbl, newSSTblPath, timestamp, targetLevel)

        match SSTbl.Open flushedSSTblPath with
        | Some newSSTbl ->
          Ok (Some newSSTbl, liveEntriesInNewSSTbl |> Array.toList) // Return new SSTbl and live entries
        | None ->
          Error $"Failed to open newly merged SSTable at {flushedSSTblPath}"
      else
        Ok (None, []) // No entries to merge, return None and empty list
    with ex ->
      Error $"Error merging SSTables: {ex.Message}"

  static member private compact
    (
      dbRef : OcisDB ref,
      level : int,
      sstblsToMerge : SSTbl list,
      targetLevel : int,
      remappedLocations : Map<int64, int64> option
    )
    : Result<SSTbl option * (byte array * ValueLocation) list, string>
    =
    OcisDB.mergeSSTables (dbRef, sstblsToMerge, targetLevel, remappedLocations)

  /// <summary>
  /// Re-compacts an SSTable by updating its value locations based on a remapping map.
  /// This is used after Valog garbage collection to update outdated value pointers.
  /// </summary>
  static member private recompactSSTable
    (dbRef : OcisDB ref, sstbl : SSTbl, remappedLocations : Map<int64, int64>)
    : Result<SSTbl option, string>
    =
    try
      let recompactedMemtbl = Memtbl ()
      let mutable changed = false

      for KeyValue (key, originalValueLoc) in sstbl do
        let newValueLoc =
          match remappedLocations.TryGetValue originalValueLoc with
          | true, newLoc ->
            changed <- true
            newLoc
          | false, _ -> originalValueLoc // Value not remapped

        recompactedMemtbl.Add (key, newValueLoc)

      if changed || recompactedMemtbl.Count <> 0 then // Only create new SSTbl if something changed or it's not empty after recompaction
        let newSSTblPath =
          Path.Combine (
            dbRef.Value.Dir,
            $"sstbl-recompact-{Guid.NewGuid().ToString ()}.sst"
          ) // Use a distinct name

        let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ()
        let level = sstbl.Level // Keep the same level

        let flushedSSTblPath =
          SSTbl.Flush (recompactedMemtbl, newSSTblPath, timestamp, level)

        match SSTbl.Open flushedSSTblPath with
        | Some newSSTbl -> Ok (Some newSSTbl)
        | None ->
          Error $"Failed to open recompacted SSTable at {flushedSSTblPath}"
      else
        Ok None // No changes, no new SSTbl needed. Or all entries were deletion markers and didn't get added.
    with ex ->
      Error $"Error recompacting SSTable {sstbl.Path}: {ex.Message}"

  /// <summary>
  /// Performs optimized compaction for Level 0 SSTables with intelligent selection.
  /// </summary>
  static member private compactLevel0 (dbRef : OcisDB ref) : Async<unit> =
    async {
      let level0SSTablesOption = dbRef.Value.SSTables |> Map.tryFind 0

      match level0SSTablesOption with
      | Some level0SSTables when
        level0SSTables.Length >= L0_COMPACTION_THRESHOLD
        ->
        // Use intelligent selection for Level 0
        let candidates = OcisDB.selectSSTablesForCompaction (dbRef, 0)

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
            OcisDB.compact (
              dbRef,
              0,
              sstblsToMerge,
              targetLevel,
              Some dbRef.Value.PendingRemappedLocations
            )
          with
          | Ok (newSSTblOption, liveEntries) ->
            do!
              OcisDB.updateSSTablesAfterCompaction (
                dbRef,
                newSSTblOption,
                sstblsToMerge,
                []
              )
          | Error msg -> Logger.Error $"Error during Level 0 compaction: {msg}"
        else
          Logger.Debug
            "Level 0 compaction: No suitable SSTables found for compaction"
      | _ -> () // No compaction needed for Level 0
    }

  /// <summary>
  /// Helper to update SSTables map and dispose/delete old SSTable files after compaction.
  /// </summary>
  static member private updateSSTablesAfterCompaction
    (
      dbRef : OcisDB ref,
      newSSTblOption : SSTbl option,
      sstblsToRemoveFromSource : SSTbl list,
      sstblsToRemoveFromTarget : SSTbl list
    )
    : Async<unit>
    =
    async {
      let mutable updatedSSTables = dbRef.Value.SSTables

      // Remove SSTables from source level
      if not (sstblsToRemoveFromSource |> List.isEmpty) then
        let sourceLevel = sstblsToRemoveFromSource.Head.Level // Assuming all SSTables in the list are from the same level

        updatedSSTables <-
          updatedSSTables
          |> Map.change sourceLevel (fun currentListOption ->
            match currentListOption with
            | Some currentList ->
              Some (
                currentList
                |> List.filter (fun s ->
                  not (
                    sstblsToRemoveFromSource
                    |> List.exists (fun oldS -> oldS.Path = s.Path)
                  ))
              )
            | None -> None) // If source level list is empty, remove the key from the map

      // Remove SSTables from target level (if different from source and not empty)
      if not (sstblsToRemoveFromTarget |> List.isEmpty) then
        let targetLevel = sstblsToRemoveFromTarget.Head.Level // Assuming all SSTables in the list are from the same level

        if
          Some targetLevel
          <> (if not (sstblsToRemoveFromSource |> List.isEmpty) then
                Some sstblsToRemoveFromSource.Head.Level
              else
                None)
        then // Only remove from target if it's a different level than source
          updatedSSTables <-
            updatedSSTables
            |> Map.change targetLevel (fun currentListOption ->
              match currentListOption with
              | Some currentList ->
                Some (
                  currentList
                  |> List.filter (fun s ->
                    not (
                      sstblsToRemoveFromTarget
                      |> List.exists (fun oldS -> oldS.Path = s.Path)
                    ))
                )
              | None -> None) // If target level list is empty, remove the key from the map

      match newSSTblOption with
      | Some newSSTbl ->
        let targetLevel = newSSTbl.Level // Use the level of the new SSTbl

        updatedSSTables <-
          updatedSSTables
          |> Map.change targetLevel (fun currentListOption ->
            match currentListOption with
            | Some currentList -> Some (newSSTbl :: currentList)
            | None -> Some [ newSSTbl ])
      | None -> ()

      dbRef.Value.SSTables <- updatedSSTables

      (sstblsToRemoveFromSource @ sstblsToRemoveFromTarget)
      |> List.iter (fun sstbl ->
        (sstbl :> IDisposable).Dispose ()
        File.Delete sstbl.Path)

      match newSSTblOption with
      | Some newSSTbl ->
        Logger.Debug
          $"Compaction successful: Merged {sstblsToRemoveFromSource.Length + sstblsToRemoveFromTarget.Length} SSTables to Level {newSSTbl.Level}. New SSTable: {newSSTbl.Path}"
      | None ->
        Logger.Debug
          $"Compaction successful: Removed {sstblsToRemoveFromSource.Length + sstblsToRemoveFromTarget.Length} SSTables (all deletion markers or no new SSTbl)."
    }

  /// <summary>
  /// Performs optimized compaction for levels greater than 0 using intelligent SSTable selection.
  /// </summary>
  static member private compactLevel
    (dbRef : OcisDB ref, level : int)
    : Async<unit>
    =
    async {
      let nextLevel = level + 1

      // Use intelligent selection strategy
      let candidates = OcisDB.selectSSTablesForCompaction (dbRef, level)

      if candidates.IsEmpty then
        Logger.Debug $"Level {level}: No SSTables available for compaction"
        return ()

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
        return ()

      let currentLevelSSTablesOption = dbRef.Value.SSTables |> Map.tryFind level

      let nextLevelSSTablesOption =
        dbRef.Value.SSTables |> Map.tryFind nextLevel

      let targetLevelSize =
        int64 L0_COMPACTION_THRESHOLD
        * (pown (int64 LEVEL_SIZE_MULTIPLIER) (level - 1))

      let currentLevelTotalSize =
        match currentLevelSSTablesOption with
        | Some sstables -> sstables |> List.sumBy _.Size
        | None -> 0L

      // Only proceed if level size threshold is exceeded or we have high priority candidates
      let hasHighPriority =
        selectedCandidates |> List.exists (fun c -> c.Priority = High)

      if currentLevelTotalSize >= targetLevelSize || hasHighPriority then
        for candidate in selectedCandidates do
          let sstblToCompact = candidate.SSTable

          let overlappingSSTables =
            match nextLevelSSTablesOption with
            | Some nextLevelSSTables ->
              nextLevelSSTables |> List.filter sstblToCompact.Overlaps
            | None -> []

          let sstblsToMerge = sstblToCompact :: overlappingSSTables

          Logger.Debug
            $"Level {level} compaction: Compacting SSTable with score {candidate.Score:F2}, overlap: {candidate.OverlapSize / 1024L / 1024L}MB"

          let compactResult =
            OcisDB.compact (
              dbRef,
              level,
              sstblsToMerge,
              nextLevel,
              Some dbRef.Value.PendingRemappedLocations
            )

          match compactResult with
          | Ok (newSSTblOption, _) ->
            do!
              OcisDB.updateSSTablesAfterCompaction (
                dbRef,
                newSSTblOption,
                [ sstblToCompact ],
                overlappingSSTables
              )
          | Error msg ->
            Logger.Error $"Error during Level {level} compaction: {msg}"
      else
        Logger.Debug
          $"Level {level}: Size threshold not met ({currentLevelTotalSize} < {targetLevelSize}) and no high priority candidates"
    }

  /// <summary>
  /// Background agent for handling Memtable flushing to SSTables and Compaction with parallel processing.
  /// </summary>
  static member private compactionAgentLoop
    (dbRef : OcisDB ref)
    (mailbox : MailboxProcessor<CompactionMessage>)
    =
    async {
      while true do
        let! msg = mailbox.Receive ()

        match msg with
        | TriggerCompaction ->
          // Parallel compaction for improved throughput
          let! compactionTasks =
            async {
              // Always try Level 0 first (highest priority)
              let level0Task =
                async {
                  do! OcisDB.compactLevel0 dbRef
                  return 0 // Return level number for logging
                }

              // Create parallel tasks for other levels
              let otherLevelTasks =
                [ 1..5 ] // Support up to level 5
                |> List.map (fun level ->
                  async {
                    do! OcisDB.compactLevel (dbRef, level)
                    return level
                  })

              // Execute tasks with controlled parallelism
              let allTasks = level0Task :: otherLevelTasks
              return! Async.Parallel (allTasks, MAX_PARALLEL_COMPACTIONS)
            }

          let levelsStr = System.String.Join (", ", compactionTasks)
          Logger.Debug $"Parallel compaction completed for levels: {levelsStr}"
          return ()

        | ParallelCompaction level ->
          // Handle parallel compaction for a specific level
          Logger.Debug $"Starting parallel compaction for level {level}"
          do! OcisDB.compactLevel (dbRef, level)
          return ()

        | RecompactionForRemapped remappedLocations ->
          Logger.Debug
            $"Received recompaction request for {remappedLocations.Count} remapped locations."

          // Selective recompaction: only recompact SSTables that contain remapped values
          let sstablesToRecompact =
            OcisDB.findSSTablesWithRemappedValues (dbRef, remappedLocations)

          if sstablesToRecompact.IsEmpty then
            Logger.Debug
              "No SSTables contain remapped values, skipping recompaction."

            return ()

          Logger.Debug
            $"Found {sstablesToRecompact.Length} SSTables containing remapped values for selective recompaction."

          let mutable updatedSSTables = dbRef.Value.SSTables
          let mutable recompactedCount = 0

          // Group SSTables by level for efficient processing
          let sstablesByLevel =
            sstablesToRecompact
            |> List.groupBy (fun sstbl -> sstbl.Level)
            |> Map.ofList

          // Parallel recompaction within each level
          let! recompactionTasks =
            async {
              let tasks =
                sstablesByLevel
                |> Map.toList
                |> List.map (fun (level, levelSSTables) ->
                  async {
                    let levelSSTablesArray = Array.ofList levelSSTables

                    let newSSTblArray =
                      Array.zeroCreate<SSTbl> levelSSTablesArray.Length

                    let mutable newSSTblCount = 0
                    let mutable levelRecompactedCount = 0

                    for sstbl in levelSSTablesArray do
                      match
                        OcisDB.recompactSSTable (
                          dbRef,
                          sstbl,
                          remappedLocations
                        )
                      with
                      | Ok (Some newSSTbl) ->
                        newSSTblArray[newSSTblCount] <- newSSTbl
                        newSSTblCount <- newSSTblCount + 1
                        (sstbl :> IDisposable).Dispose ()
                        File.Delete sstbl.Path
                        levelRecompactedCount <- levelRecompactedCount + 1

                        Logger.Debug
                          $"Recompacted SSTable {sstbl.Path} to {newSSTbl.Path} (Level {level})"
                      | Ok None ->
                        newSSTblArray[newSSTblCount] <- sstbl // No change, keep original SSTable
                        newSSTblCount <- newSSTblCount + 1
                      | Error msg ->
                        Logger.Error
                          $"Error recompacting SSTable {sstbl.Path}: {msg}"

                        newSSTblArray[newSSTblCount] <- sstbl // On error, keep original SSTable
                        newSSTblCount <- newSSTblCount + 1

                    let finalSSTblList =
                      if newSSTblCount = levelSSTablesArray.Length then
                        newSSTblArray |> Array.toList
                      else
                        newSSTblArray
                        |> Array.take newSSTblCount
                        |> Array.toList

                    return (level, finalSSTblList, levelRecompactedCount)
                  })

              return! Async.Parallel (tasks, MAX_PARALLEL_COMPACTIONS)
            }

          // Update SSTables map with recompacted results
          for (level, finalSSTblList, levelRecompactedCount) in
            recompactionTasks do
            updatedSSTables <- updatedSSTables |> Map.add level finalSSTblList
            recompactedCount <- recompactedCount + levelRecompactedCount

          dbRef.Value.SSTables <- updatedSSTables

          Logger.Debug
            $"Completed selective recompaction for Valog GC. Recompacted {recompactedCount} out of {sstablesToRecompact.Length} affected SSTables."

          return ()
        | CompactionMessage.FlushMemtable _ ->
          failwith "FlushMemtable should be handled by flushAgentLoop"
    }

  /// <summary>
  /// Background agent for handling Memtable flushing to SSTables.
  /// </summary>
  static member private flushAgentLoop
    (dbRef : OcisDB ref)
    (mailbox : MailboxProcessor<FlushMessage>)
    =
    async {
      while true do
        let! msg = mailbox.Receive ()

        match msg with
        | FlushMemtable memtblToFlush ->
          // Logic to flush memtblToFlush to a new SSTable
          let newSSTblPath =
            Path.Combine (
              dbRef.Value.Dir,
              $"sstbl-{Guid.NewGuid().ToString ()}.sst"
            )

          let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ()
          let level = 0 // Always flush to Level 0

          try
            let flushedSSTblPath =
              SSTbl.Flush (memtblToFlush, newSSTblPath, timestamp, level)

            match SSTbl.Open flushedSSTblPath with
            | Some sstbl ->
              // Update the SSTables map in the OcisDB instance
              dbRef.Value.SSTables <-
                dbRef.Value.SSTables
                |> Map.change level (fun currentListOption ->
                  match currentListOption with
                  | Some currentList -> Some (sstbl :: currentList)
                  | None -> Some [ sstbl ])

              // After flushing, notify compaction agent to trigger compaction if needed
              // This is a simple trigger, more advanced logic could be added here later
              dbRef.Value.CompactionAgent.Post TriggerCompaction
            | None ->
              Logger.Error
                $"Error: Failed to open flushed SSTable at {flushedSSTblPath}"
          with ex ->
            Logger.Error $"Error flushing Memtable to SSTable: {ex.Message}"
    }

  /// <summary>
  /// Background agent for handling Garbage Collection in Valog.
  /// </summary>
  static member private gcAgentLoop
    (dbRef : OcisDB ref)
    (mailbox : MailboxProcessor<GcMessage>)
    =
    async {
      while true do
        let! msg = mailbox.Receive ()

        match msg with
        | TriggerGC ->
          Logger.Debug "Triggering garbage collection."

          // Collect all live value locations
          let liveLocations = OcisDB.GetLiveLocations dbRef // Use the new helper function

          Logger.Debug $"Collected {liveLocations.Count} live value locations."

          // Perform actual garbage collection in Valog
          let! remappedLocationsOption =
            Valog.CollectGarbage (dbRef.Value.ValueLog, liveLocations)

          match remappedLocationsOption with
          | Some (Ok (newValog, remappedLocations)) ->
            dbRef.Value.ValueLog <- newValog // Update the Valog instance in OcisDB

            Logger.Debug
              $"Valog GC: Values were moved, {remappedLocations.Count} value locations remapped. Affected SSTables might need re-compaction to update pointers."

            // Merge new remappedLocations into pendingRemappedLocations
            for KeyValue (key, newLoc) in remappedLocations do
              dbRef.Value.PendingRemappedLocations <-
                dbRef.Value.PendingRemappedLocations.Add (key, newLoc)

            // Trigger a compaction cycle to handle the remapping in affected SSTables
            dbRef.Value.CompactionAgent.Post TriggerCompaction // Trigger general compaction
          | Some (Error msg) -> Logger.Error $"Valog GC Error: {msg}"
          | None ->
            Logger.Debug
              "Valog GC: No values were moved or no remapping needed."

    }

  /// <summary>
  /// Helper function to collect all live value locations from MemTables and SSTables.
  /// </summary>
  static member private GetLiveLocations (dbRef : OcisDB ref) : Set<int64> =
    let mutable liveLocations = Set.empty<int64>

    // From CurrentMemtbl
    dbRef.Value.CurrentMemtbl
    |> Seq.iter (fun (KeyValue (_, valueLoc)) ->
      liveLocations <- liveLocations.Add valueLoc)

    // From ImmutableMemtbl
    dbRef.Value.ImmutableMemtbl
    |> Seq.collect (fun memtbl ->
      memtbl |> Seq.map (fun (KeyValue (_, valueLoc)) -> valueLoc))
    |> Seq.iter (fun valueLoc -> liveLocations <- liveLocations.Add valueLoc)

    // From SSTables
    dbRef.Value.SSTables
    |> Map.toSeq
    |> Seq.collect (fun (_, sstblList) ->
      sstblList
      |> Seq.collect (fun sstbl ->
        sstbl |> Seq.map (fun (KeyValue (_, valueLoc)) -> valueLoc)))
    |> Seq.iter (fun valueLoc -> liveLocations <- liveLocations.Add valueLoc)

    liveLocations

  /// <summary>
  /// Opens an existing WiscKeyDB or creates a new one if it doesn't exist.
  /// </summary>
  /// <param name="dir">The directory where the database files are stored.</param>
  /// <returns>A Result type: Ok OcisDB instance if successful, otherwise an Error string.</returns>
  static member Open
    (dir : string, flushThreshold : int)
    : Result<OcisDB, string>
    =
    try
      // Ensure the directory exists
      if not (Directory.Exists dir) then Directory.CreateDirectory dir |> ignore

      let valogPath = Path.Combine (dir, "valog.vlog")

      Valog.Create valogPath
      |> Result.bind (fun valog -> // Chain Result operations
        let walPath = Path.Combine (dir, "wal.log")

        Wal.Create walPath
        |> Result.bind (fun wal ->
          // Replay WAL to reconstruct CurrentMemTable
          let initialMemtbl = Memtbl () // Use Memtbl() for a new instance
          let replayedEntries = Wal.Replay walPath // Use WAL.Wal to specify the type

          for entry in replayedEntries do
            match entry with
            | WalEntry.Set (key, valueLoc) ->
              initialMemtbl.Add (key, valueLoc)
            | WalEntry.Delete key -> initialMemtbl.Delete key

          // Load existing SSTables
          let mutable loadedSSTables = Map.empty<int, SSTbl list> // 修正 Map.empty
          let sstblFiles = Directory.GetFiles (dir, "sstbl-*.sst") // Assuming SSTables follow this naming convention

          for filePath in sstblFiles do
            match SSTbl.Open filePath with
            | Some sstbl ->
              loadedSSTables <-
                loadedSSTables
                |> Map.change sstbl.Level (fun currentListOption ->
                  match currentListOption with
                  | Some currentList -> Some (sstbl :: currentList)
                  | None -> Some [ sstbl ])
            | None ->
              Logger.Warn
                $"Failed to open SSTable file: {filePath}. It might be corrupted."

          // Initialize agents (MailboxProcessors)
          // Create a mutable reference to OcisDB to allow agents to update its state
          let dbRef = ref Unchecked.defaultof<OcisDB> // Placeholder, will be updated after OcisDB instance is created

          let compactionAgent =
            MailboxProcessor.Start (OcisDB.compactionAgentLoop dbRef)

          let flushAgent =
            MailboxProcessor.Start (OcisDB.flushAgentLoop dbRef) // Initialize new flush agent

          let gcAgent = MailboxProcessor.Start (OcisDB.gcAgentLoop dbRef)

          let ocisDB =
            new OcisDB (
              dir,
              initialMemtbl,
              ConcurrentQueue<Memtbl> (),
              loadedSSTables,
              valog,
              wal,
              compactionAgent,
              flushAgent, // Pass new flush agent
              gcAgent,
              flushThreshold
            )

          dbRef.Value <- ocisDB // Update the mutable reference

          Ok ocisDB))
    with ex ->
      Error $"Failed to open WiscKeyDB: {ex.Message}"

  /// <summary>
  /// Sets a key-value pair in the database.
  /// </summary>
  /// <param name="key">The key to set.</param>
  /// <param name="value">The value to associate with the key.</param>
  /// <returns>An AsyncResult: Ok unit if successful, otherwise an Error string.</returns>
  member this.Set
    (key : byte array, value : byte array)
    : Async<Result<unit, string>>
    =
    async {
      try
        // 1. Write to ValueLog
        let valueLocation = valog.Append (key, value)

        // 2. Write to WAL
        wal.Append (WalEntry.Set (key, valueLocation))

        // 3. Write to CurrentMemTable
        currentMemtbl.Add (key, valueLocation)

        // 4. Check MemTable size and trigger flush if needed
        do! this.CheckAndFlushMemtable ()

        return Ok ()
      with ex ->
        return Error $"Failed to set key-value pair: {ex.Message}"
    }

  /// <summary>
  /// Helper function to check MemTable size and trigger flush if needed.
  /// </summary>
  member private this.CheckAndFlushMemtable () : Async<unit> =
    async {
      let memtblCount = currentMemtbl.Count

      if memtblCount >= flushThreshold then
        let frozenMemtbl = currentMemtbl
        immutableMemtbl.Enqueue frozenMemtbl
        currentMemtbl <- Memtbl () // Create a new empty MemTable
        this.FlushAgent.Post (FlushMessage.FlushMemtable frozenMemtbl)
    }

  /// <summary>
  /// Gets the value associated with the given key.
  /// </summary>
  /// <param name="key">The key to retrieve.</param>
  /// <returns>An AsyncResult: Ok (Some value) if found, Ok None if not found, otherwise an Error string.</returns>
  member this.Get
    (key : byte array)
    : Async<Result<byte array option, string>>
    =
    async {
      try
        let resolveValue = this.ResolveValueLocation // Use the new helper function

        // 1. Search CurrentMemTable
        match this.CurrentMemtbl.TryGet key with
        | Some valueLocation ->
          return! async { return resolveValue valueLocation }
        | None ->
          // 2. Search ImmutableMemTables (from newest to oldest)
          match this.SearchImmutableMemtables key with // Use the new helper function
          | Some valueLocation ->
            return! async { return resolveValue valueLocation }
          | None ->
            // 3. Search SSTables (from Level 0 upwards, newest within each level)
            match this.SearchSSTables key with // Use the new helper function
            | Some valueLocation ->
              return! async { return resolveValue valueLocation }
            | None ->
              // If not found anywhere
              return Ok None
      with ex ->
        return
          Error
            $"Failed to get value for key {Encoding.UTF8.GetString key}: {ex.Message}"
    }

  /// <summary>
  /// Helper function to resolve ValueLocation to actual value or error.
  /// </summary>
  member private this.ResolveValueLocation
    (valueLocation : int64)
    : Result<byte array option, string>
    =
    if valueLocation = -1L then
      Ok None // Deletion marker
    else
      match this.ValueLog.Read valueLocation with
      | Some (_, value) -> Ok (Some value)
      | None ->
        Error $"Failed to read value from Valog at location {valueLocation}."

  /// <summary>
  /// Helper function to search ImmutableMemTables for a given key.
  /// </summary>
  member private this.SearchImmutableMemtables
    (key : byte array)
    : ValueLocation option
    =
    let memtbls = this.ImmutableMemtbl.GetEnumerator ()
    let mutable found = None

    while memtbls.MoveNext () && found.IsNone do
      found <- memtbls.Current.TryGet key

    found

  /// <summary>
  /// Helper function to search SSTables for a given key.
  /// </summary>
  member private this.SearchSSTables (key : byte array) : ValueLocation option =
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
  /// <returns>An AsyncResult: Ok unit if successful, otherwise an Error string.</returns>
  member this.Delete (key : byte array) : Async<Result<unit, string>> =
    async {
      try
        // 1. Write deletion marker to WAL
        this.WAL.Append (WalEntry.Delete key)

        // 2. Add deletion marker to CurrentMemTable
        currentMemtbl.Delete key // Memtbl.Delete already adds -1L

        // Check MemTable size and trigger flush if needed (same logic as Set)
        do! this.CheckAndFlushMemtable ()

        return Ok ()
      with ex ->
        return
          Error
            $"Failed to delete key {Encoding.UTF8.GetString key}: {ex.Message}"
    }
