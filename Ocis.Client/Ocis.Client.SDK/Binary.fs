module Ocis.Client.SDK.Binary

open System

let private getUint32 (b0 : byte) (b1 : byte) (b2 : byte) (b3 : byte) : uint32 =
  (uint32 b0)
  ||| ((uint32 b1) <<< 8)
  ||| ((uint32 b2) <<< 16)
  ||| ((uint32 b3) <<< 24)

let private getInt32 (b0 : byte) (b1 : byte) (b2 : byte) (b3 : byte) : int32 =
  (int32 b0)
  ||| ((int32 b1) <<< 8)
  ||| ((int32 b2) <<< 16)
  ||| ((int32 b3) <<< 24)

let readUInt32LittleEndian (buffer : byte[]) (offset : int) : uint32 =
  getUint32
    buffer.[offset]
    buffer.[offset + 1]
    buffer.[offset + 2]
    buffer.[offset + 3]

let readInt32LittleEndian (buffer : byte[]) (offset : int) : int32 =
  getInt32
    buffer.[offset]
    buffer.[offset + 1]
    buffer.[offset + 2]
    buffer.[offset + 3]

let readByte (buffer : byte[]) (offset : int) : byte = buffer.[offset]

let writeUInt32LittleEndian
  (value : uint32)
  (buffer : byte[])
  (offset : int)
  : unit
  =
  buffer.[offset] <- byte value
  buffer.[offset + 1] <- byte (value >>> 8)
  buffer.[offset + 2] <- byte (value >>> 16)
  buffer.[offset + 3] <- byte (value >>> 24)

let writeInt32LittleEndian
  (value : int32)
  (buffer : byte[])
  (offset : int)
  : unit
  =
  let u = uint32 value
  buffer.[offset] <- byte u
  buffer.[offset + 1] <- byte (u >>> 8)
  buffer.[offset + 2] <- byte (u >>> 16)
  buffer.[offset + 3] <- byte (u >>> 24)

let writeByte (value : byte) (buffer : byte[]) (offset : int) : unit =
  buffer.[offset] <- value

let stringToBytes (str : string) : byte[] =
  System.Text.Encoding.UTF8.GetBytes (str)

let bytesToString (bytes : byte[]) : string =
  System.Text.Encoding.UTF8.GetString (bytes)
