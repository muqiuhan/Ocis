module Ocis.Utils.Serialization

open System.IO
open System.Buffers
open System.Collections.Concurrent
open Ocis.ValueLocation

module internal Serialization =

    /// <summary>
    /// Byte array pool for reusing byte arrays to reduce allocations
    /// </summary>
    let private byteArrayPool = ArrayPool<byte>.Shared

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
        let len = reader.ReadInt32()

        if len <= 0 then
            [||] // Return empty array for invalid lengths
        elif len <= 1024 then
            // For small arrays, use pool to reduce allocations
            let rented = byteArrayPool.Rent len
            let read = reader.Read(rented, 0, len)

            if read = len then
                // Create a copy of the exact size to return
                let result = Array.zeroCreate<byte> len
                System.Array.Copy(rented, result, len)
                byteArrayPool.Return rented
                result
            else
                // If read failed, return the rented array as-is (though this shouldn't happen)
                byteArrayPool.Return rented
                let result = Array.zeroCreate<byte> read
                System.Array.Copy(rented, result, read)
                result
        else
            // For large arrays, use the standard method
            reader.ReadBytes len

    /// <summary>
    /// Read a byte array into a provided buffer to avoid allocation.
    /// </summary>
    /// <param name="reader">The BinaryReader to read from.</param>
    /// <param name="buffer">The buffer to read into.</param>
    /// <returns>The number of bytes read.</returns>
    let inline readByteArrayIntoBuffer (reader: BinaryReader) (buffer: byte array) =
        let len = reader.ReadInt32()

        if len > buffer.Length then
            failwith $"Buffer too small: required {len} bytes, buffer size {buffer.Length}"

        reader.Read(buffer, 0, len)

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
