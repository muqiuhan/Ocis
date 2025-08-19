module Ocis.Utils.Serialization

open System.IO
open Ocis.ValueLocation

module internal Serialization =

  /// <summary>
  /// Write a byte array to a BinaryWriter. First write length (int32), then write the byte array itself.
  /// </summary>
  /// <param name="writer">The BinaryWriter to write to.</param>
  /// <param name="data">The byte array to write.</param>
  let inline writeByteArray (writer : BinaryWriter) (data : byte array) =
    writer.Write data.Length
    writer.Write data

  /// <summary>
  /// Read a byte array from a BinaryReader. First read length (int32), then read the specified length of byte array.
  /// </summary>
  /// <param name="reader">The BinaryReader to read from.</param>
  /// <returns>The byte array read from the BinaryReader.</returns>
  let inline readByteArray (reader : BinaryReader) =
    let len = reader.ReadInt32 ()
    reader.ReadBytes len

  /// <summary>
  /// Write a ValueLocation (int64) to a BinaryWriter.
  /// </summary>
  /// <param name="writer">The BinaryWriter to write to.</param>
  /// <param name="loc">The ValueLocation to write.</returns>
  let inline writeValueLocation (writer : BinaryWriter) (loc : ValueLocation) =
    writer.Write loc

  /// <summary>
  /// Read a ValueLocation (int64) from a BinaryReader.
  /// </summary>
  let inline readValueLocation (reader : BinaryReader) = reader.ReadInt64 ()
