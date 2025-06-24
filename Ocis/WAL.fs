module Ocis.WAL

open Ocis.Memtbl
open System.IO
open Ocis.ValueLocation

type WalEntry =
    | Set of byte array * ValueLocation
    | Delete of byte array

type Wal =
    { Path: string; FileStream: FileStream }
