module Ocis.SSTbl

open Ocis.Memtbl

open System.IO

/// FileStream needs to be closed and released correctly after use (using the use keyword).
/// RecordOffsets is a sparse index in memory, pointing to the starting position of each key-value pair in the SSTable file.
/// LowKey and HighKey are used to quickly determine if a key may exist in this SSTable, reducing unnecessary disk lookups.
type SSTbl =
    {
        Path: string
        /// Create timestamp, for version control
        Timestamp: int64
        /// Compaction level
        Level: int
        /// For direct file operations
        FileStream: FileStream
        /// Key offsets in memory
        RecordOffsets: int64 array
        /// Minimum key
        LowKey: byte array
        /// Maximum key
        HighKey: byte array
    }
