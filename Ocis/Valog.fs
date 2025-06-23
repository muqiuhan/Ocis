module Ocis.Valog

open System.IO

type Valog =
    {
        Path: string
        /// For direct file operations
        FileStream: FileStream
        /// Next write position
        mutable Head: int64
        /// Oldest valid value position (for GC)
        mutable Tail: int64
    }
