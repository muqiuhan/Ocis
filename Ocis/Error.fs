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

/// <summary>
/// Result computation expression builder for functional error handling.
/// Simplifies nested Result.bind operations and makes error handling more readable.
/// </summary>
type ResultBuilder() =
    member _.Return(x: 'T) = Ok x
    member _.ReturnFrom(m: Result<'T, 'E>) = m
    member _.Bind(m: Result<'T, 'E>, f: 'T -> Result<'U, 'E>) = Result.bind f m
    member _.Zero() = Ok()
    member _.Delay(f: unit -> Result<'T, 'E>) = f
    member _.Run(f: unit -> Result<'T, 'E>) = f ()

    member _.Combine(m1: Result<unit, 'E>, m2: unit -> Result<'T, 'E>) =
        match m1 with
        | Ok() -> m2 ()
        | Error e -> Error e

    member _.TryWith(m: unit -> Result<'T, 'E>, h: exn -> Result<'T, 'E>) =
        try
            m ()
        with ex ->
            h ex

    member _.TryFinally(m: unit -> Result<'T, 'E>, compensation: unit -> unit) =
        try
            m ()
        finally
            compensation ()

/// <summary>
/// Result computation expression instance for use in code.
/// </summary>
let result = ResultBuilder()
