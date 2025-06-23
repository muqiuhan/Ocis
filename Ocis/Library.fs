module Ocis.WiscKey

open System.Collections.Concurrent
open Ocis.Memtbl
open Ocis.SSTbl
open Ocis.Valog
open Ocis.WAL

type CompactionMessage = Compaction of int

type GcMessage = Gc of int

type WalMessage = Wal of int

/// ImmutableMemTables is used to buffer Memtbl waiting to be flushed to disk.
/// SSTables should be organized by level (Level), usually using Map<int, SSTable list> to represent, where int is the level number.
type OcisDB =
    {
        /// Database file storage directory
        Dir: string
        /// Current writable memory table
        CurrentMemTable: Memtbl
        /// Queue of immutable memory tables waiting to be flushed
        ImmutableMemTables: ConcurrentQueue<Memtbl>
        /// Level -> List of SSTables
        SSTables: Map<int, SSTbl list>
        /// Value log
        ValueLog: Valog
        /// Write-ahead log
        WAL: Wal
        /// A background agent to handle Compaction
        CompactionAgent: MailboxProcessor<CompactionMessage>
        /// A background agent to handle GC
        GCAgent: MailboxProcessor<GcMessage>
        /// A background agent to handle WAL operations
        WALAgent: MailboxProcessor<WalMessage>
    }
