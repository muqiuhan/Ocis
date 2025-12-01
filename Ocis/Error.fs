module Ocis.Errors

type OcisError =
    | InvalidPath of path: string
    | KeyNotFound of key: byte array
    | DeleteOperationFailed of key: byte array * reason: string
    | NullMemtbl
    | InvalidSSTablePath of path: string
    | InvalidSSTableLevel of level: int
    | SSTableFlushError of path: string * reason: string
    | SSTableRecordCountMismatch of expected: int * actual: int
    | SSTableRecordIndexOutOfRange of index: int * count: int
    | IOError of operation: string * path: string * message: string
    | UnauthorizedAccess of path: string
    | SerializationError of message: string
    | ResourceDisposalError of resource: string * message: string

    override this.ToString() : string =
        match this with
        | InvalidPath path -> $"Invalid path: {path}"
        | KeyNotFound key -> $"Key not found: {System.Text.Encoding.UTF8.GetString key}"
        | DeleteOperationFailed(key, reason) ->
            $"Failed to delete key {System.Text.Encoding.UTF8.GetString key}: {reason}"
        | NullMemtbl -> "Memtbl cannot be null"
        | InvalidSSTablePath path -> $"SSTable path cannot be null or empty: {path}"
        | InvalidSSTableLevel level -> $"Invalid SSTable level: {level} (must be >= 0)"
        | SSTableFlushError(path, reason) -> $"Failed to flush SSTable to '{path}': {reason}"
        | SSTableRecordCountMismatch(expected, actual) ->
            $"SSTable record count mismatch: expected {expected}, got {actual}"
        | SSTableRecordIndexOutOfRange(index, count) -> $"SSTable record index {index} out of range (count: {count})"
        | IOError(operation, path, message) -> $"I/O error during {operation} at '{path}': {message}"
        | UnauthorizedAccess path -> $"Access denied: {path}"
        | SerializationError message -> $"Serialization error: {message}"
        | ResourceDisposalError(resource, message) -> $"Failed to dispose {resource}: {message}"

type Result<'T> = Result<'T, OcisError>
