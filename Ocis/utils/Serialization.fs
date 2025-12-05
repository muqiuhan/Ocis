module Ocis.Utils.Serialization

open System.IO
open System.Buffers
open Ocis.ValueLocation
open Ocis.Config

module internal Serialization =

    /// <summary>
    /// Byte array pool for reusing byte arrays to reduce allocations
    /// </summary>
    let private byteArrayPool = ArrayPool<byte>.Shared

    /// <summary>
    /// Validate a length value against configured maximum and optional remaining payload bytes.
    /// remainingPayloadOpt should represent bytes available for the payload *after* consuming the length field itself.
    let internal validateLengthValue (len: int) (remainingPayloadOpt: int64 option) (fieldName: string) =
        if len < 0 then
            raise (InvalidDataException($"Serialization error ({fieldName}): negative length {len}"))

        if len > Limits.MaxEntrySizeBytes then
            raise (
                InvalidDataException(
                    $"Serialization error ({fieldName}): length {len} exceeds max {Limits.MaxEntrySizeBytes}"
                )
            )

        match remainingPayloadOpt with
        | Some remaining when remaining >= 0L && int64 len > remaining ->
            raise (
                InvalidDataException(
                    $"Serialization error ({fieldName}): length {len} exceeds remaining bytes {remaining}"
                )
            )
        | _ -> len

    /// Read an int32 length and validate it using optional remaining payload bytes.
    let internal readLengthAndValidate (reader: BinaryReader) (fieldName: string) (remainingPayloadOpt: int64 option) =
        let len = reader.ReadInt32()
        validateLengthValue len remainingPayloadOpt fieldName

    /// <summary>
    /// Write a byte array to a BinaryWriter. First write length (int32), then write the byte array itself.
    /// </summary>
    /// <param name="writer">The BinaryWriter to write to.</param>
    /// <param name="data">The byte array to write.</param>
    let inline writeByteArray (writer: BinaryWriter) (data: byte array) =
        writer.Write data.Length
        writer.Write data

    /// <summary>
    /// Read a byte array from a BinaryReader. First read length (int32), then read the specified length of byte array.
    /// Uses ArrayPool to reduce allocations for frequently used sizes.
    /// </summary>
    /// <param name="reader">The BinaryReader to read from.</param>
    /// <returns>The byte array read from the BinaryReader.</returns>
    let inline readByteArray (reader: BinaryReader) =
        let len = readLengthAndValidate reader "byte array" None

        if len = 0 then
            [||]
        elif len <= 1024 then
            // For small arrays, use pool to reduce allocations
            let rented = byteArrayPool.Rent len

            try
                // Ensure complete read in case of partial reads
                let mutable totalRead = 0

                while totalRead < len do
                    let read = reader.Read(rented, totalRead, len - totalRead)

                    if read = 0 then
                        failwith $"Unexpected end of stream: expected {len} bytes, got {totalRead}"

                    totalRead <- totalRead + read

                // Create a copy of the exact size to return
                let result = Array.zeroCreate<byte> len
                System.Array.Copy(rented, 0, result, 0, len)
                result
            finally
                byteArrayPool.Return rented
        else
            // For large arrays, ensure complete read
            let result = Array.zeroCreate<byte> len
            let mutable totalRead = 0

            while totalRead < len do
                let read = reader.Read(result, totalRead, len - totalRead)

                if read = 0 then
                    failwith $"Unexpected end of stream: expected {len} bytes, got {totalRead}"

                totalRead <- totalRead + read

            result

    /// <summary>
    /// Read a byte array into a provided buffer to avoid allocation.
    /// Ensures complete read even if partial reads occur.
    /// </summary>
    /// <param name="reader">The BinaryReader to read from.</param>
    /// <param name="buffer">The buffer to read into.</param>
    /// <returns>The number of bytes read (should equal len).</returns>
    let inline readByteArrayIntoBuffer (reader: BinaryReader) (buffer: byte array) =
        let len = readLengthAndValidate reader "byte array into buffer" None

        if len > buffer.Length then
            failwith $"Buffer too small: required {len} bytes, buffer size {buffer.Length}"

        // Ensure complete read
        let mutable totalRead = 0

        while totalRead < len do
            let read = reader.Read(buffer, totalRead, len - totalRead)

            if read = 0 then
                failwith $"Unexpected end of stream: expected {len} bytes, got {totalRead}"

            totalRead <- totalRead + read

        totalRead

    /// <summary>
    /// Read a byte array key for comparison purposes, using ArrayPool to minimize allocations.
    /// Returns a byte array that should be used immediately for comparison and then discarded.
    /// For SSTable binary search optimization.
    /// </summary>
    /// <param name="reader">The BinaryReader to read from.</param>
    /// <returns>The key byte array (may be from pool, should be used immediately).</returns>
    let inline readKeyForComparison (reader: BinaryReader) : byte array =
        let len = readLengthAndValidate reader "key for comparison" None

        if len = 0 then
            [||]
        elif len <= 1024 then
            // Use ArrayPool for small keys
            let rented = byteArrayPool.Rent len

            try
                let mutable totalRead = 0

                while totalRead < len do
                    let read = reader.Read(rented, totalRead, len - totalRead)

                    if read = 0 then
                        failwith $"Unexpected end of stream: expected {len} bytes for key, got {totalRead}"

                    totalRead <- totalRead + read
                // Create a copy for safety (comparison may hold reference)
                let result = Array.zeroCreate<byte> len
                System.Array.Copy(rented, 0, result, 0, len)
                result
            finally
                byteArrayPool.Return rented
        else
            // For large keys, read directly
            let result = Array.zeroCreate<byte> len
            let mutable totalRead = 0

            while totalRead < len do
                let read = reader.Read(result, totalRead, len - totalRead)

                if read = 0 then
                    failwith $"Unexpected end of stream: expected {len} bytes for key, got {totalRead}"

                totalRead <- totalRead + read

            result

    /// <summary>
    /// Write a ValueLocation (int64) to a BinaryWriter.
    /// </summary>
    /// <param name="writer">The BinaryWriter to write to.</param>
    /// <param name="loc">The ValueLocation to write.</returns>
    let inline writeValueLocation (writer: BinaryWriter) (loc: ValueLocation) = writer.Write loc

    /// <summary>
    /// Read a ValueLocation (int64) from a BinaryReader.
    /// </summary>
    let inline readValueLocation (reader: BinaryReader) = reader.ReadInt64()
