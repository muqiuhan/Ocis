module Ocis.Client.SDK.Tests.BinaryTests

open NUnit.Framework
open Ocis.Client.SDK.Binary

[<TestFixture>]
type BinaryTests () =

  [<Test>]
  member this.WriteAndReadUInt32 () =
    let buffer = Array.zeroCreate<byte> 4
    writeUInt32LittleEndian 0x12345678u buffer 0
    let result = readUInt32LittleEndian buffer 0
    Assert.AreEqual (0x12345678u, result)

  [<Test>]
  member this.WriteAndReadUInt32_MaxValue () =
    let buffer = Array.zeroCreate<byte> 4
    writeUInt32LittleEndian 0xFFFFFFFFu buffer 0
    let result = readUInt32LittleEndian buffer 0
    Assert.AreEqual (0xFFFFFFFFu, result)

  [<Test>]
  member this.WriteAndReadUInt32_Zero () =
    let buffer = Array.zeroCreate<byte> 4
    writeUInt32LittleEndian 0u buffer 0
    let result = readUInt32LittleEndian buffer 0
    Assert.AreEqual (0u, result)

  [<Test>]
  member this.WriteAndReadInt32_Positive () =
    let buffer = Array.zeroCreate<byte> 4
    writeInt32LittleEndian 0x12345678 buffer 0
    let result = readInt32LittleEndian buffer 0
    Assert.AreEqual (0x12345678, result)

  [<Test>]
  member this.WriteAndReadInt32_Negative () =
    let buffer = Array.zeroCreate<byte> 4
    writeInt32LittleEndian -1 buffer 0
    let result = readInt32LittleEndian buffer 0
    Assert.AreEqual (-1, result)

  [<Test>]
  member this.WriteAndReadInt32_Zero () =
    let buffer = Array.zeroCreate<byte> 4
    writeInt32LittleEndian 0 buffer 0
    let result = readInt32LittleEndian buffer 0
    Assert.AreEqual (0, result)

  [<Test>]
  member this.WriteAndReadByte () =
    let buffer = Array.zeroCreate<byte> 1
    writeByte 0x42uy buffer 0
    let result = readByte buffer 0
    Assert.AreEqual (0x42uy, result)

  [<Test>]
  member this.WriteAndReadByte_MaxValue () =
    let buffer = Array.zeroCreate<byte> 1
    writeByte 0xFFuy buffer 0
    let result = readByte buffer 0
    Assert.AreEqual (0xFFuy, result)

  [<Test>]
  member this.ReadUInt32AtOffset () =
    let buffer =
      [| 0xAAuy
         0xBBuy
         0xCCuy
         0xDDuy
         0x11uy
         0x22uy
         0x33uy
         0x44uy |]

    let result = readUInt32LittleEndian buffer 4
    Assert.AreEqual (0x44332211u, result)

  [<Test>]
  member this.ReadInt32AtOffset () =
    let buffer =
      [| 0xAAuy
         0x00uy
         0x00uy
         0x00uy
         0xFFuy
         0x00uy
         0x00uy
         0x00uy |]

    let result = readInt32LittleEndian buffer 4
    Assert.AreEqual (0xFF, result)

  [<Test>]
  member this.StringToBytesAndBack () =
    let original = "Hello, 世界"
    let bytes = stringToBytes original
    let result = bytesToString bytes
    Assert.AreEqual (original, result)

  [<Test>]
  member this.StringToBytes_Empty () =
    let bytes = stringToBytes ""
    Assert.IsEmpty (bytes)

  [<Test>]
  member this.BytesToString_Empty () =
    let result = bytesToString [||]
    Assert.AreEqual ("", result)
