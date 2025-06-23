module Ocis.Memtbl

/// Use -1 to represent a deleted record
type ValueLocation = int64

type Memtbl = Map<byte array, ValueLocation>
