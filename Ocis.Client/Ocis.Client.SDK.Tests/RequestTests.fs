module Ocis.Client.SDK.Tests.RequestTests

open NUnit.Framework
open System
open Ocis.Server.ProtocolSpec
open Ocis.Client.SDK.Request

[<TestFixture>]
type RequestTests () =

  [<Test>]
  member this.CreateSetRequest_VerifyHeader () =
    let key = "testKey"
    let value = [| 0x00uy ; 0x01uy ; 0x02uy ; 0x03uy |]
    let bytes = createSetRequest key value

    Assert.GreaterOrEqual (bytes.Length, HEADER_SIZE)

    let magicNumber = System.BitConverter.ToUInt32 (bytes, 0)
    Assert.AreEqual (MAGIC_NUMBER, magicNumber)

    let version = bytes.[4]
    Assert.AreEqual (PROTOCOL_VERSION, version)

    let commandType = bytes.[5]
    Assert.AreEqual (byte CommandType.Set, commandType)

  [<Test>]
  member this.CreateSetRequest_VerifyKeyAndValue () =
    let key = "myKey"
    let value = [| 0xDEuy ; 0xADuy ; 0xBEuy ; 0xEFuy |]
    let bytes = createSetRequest key value

    let keyLength = System.BitConverter.ToInt32 (bytes, 10)
    let valueLength = System.BitConverter.ToInt32 (bytes, 14)

    Assert.AreEqual (key.Length, keyLength)
    Assert.AreEqual (value.Length, valueLength)

    let keyInPacket =
      bytes.[HEADER_SIZE .. HEADER_SIZE + keyLength - 1]

    let keyStr = System.Text.Encoding.UTF8.GetString (keyInPacket)
    Assert.AreEqual (key, keyStr)

    let valueInPacket = bytes.[HEADER_SIZE + keyLength ..]
    CollectionAssert.AreEqual (value, valueInPacket)

  [<Test>]
  member this.CreateSetRequest_WithEmptyValue () =
    let key = "emptyVal"
    let value = [||]
    let bytes = createSetRequest key value

    let valueLength = System.BitConverter.ToInt32 (bytes, 14)
    Assert.AreEqual (0, valueLength)

  [<Test>]
  member this.CreateGetRequest_VerifyHeader () =
    let key = "testKey"
    let bytes = createGetRequest key

    Assert.GreaterOrEqual (bytes.Length, HEADER_SIZE)

    let magicNumber = System.BitConverter.ToUInt32 (bytes, 0)
    Assert.AreEqual (MAGIC_NUMBER, magicNumber)

    let commandType = bytes.[5]
    Assert.AreEqual (byte CommandType.Get, commandType)

  [<Test>]
  member this.CreateGetRequest_NoValue () =
    let key = "someKey"
    let bytes = createGetRequest key

    let valueLength = System.BitConverter.ToInt32 (bytes, 14)
    Assert.AreEqual (0, valueLength)

  [<Test>]
  member this.CreateGetRequest_VerifyKey () =
    let key = "getKey123"
    let bytes = createGetRequest key

    let keyLength = System.BitConverter.ToInt32 (bytes, 10)
    Assert.AreEqual (key.Length, keyLength)

    let keyInPacket =
      bytes.[HEADER_SIZE .. HEADER_SIZE + keyLength - 1]

    let keyStr = System.Text.Encoding.UTF8.GetString (keyInPacket)
    Assert.AreEqual (key, keyStr)

  [<Test>]
  member this.CreateDeleteRequest_VerifyHeader () =
    let key = "deleteKey"
    let bytes = createDeleteRequest key

    Assert.GreaterOrEqual (bytes.Length, HEADER_SIZE)

    let magicNumber = System.BitConverter.ToUInt32 (bytes, 0)
    Assert.AreEqual (MAGIC_NUMBER, magicNumber)

    let commandType = bytes.[5]
    Assert.AreEqual (byte CommandType.Delete, commandType)

  [<Test>]
  member this.CreateDeleteRequest_NoValue () =
    let key = "toDelete"
    let bytes = createDeleteRequest key

    let valueLength = System.BitConverter.ToInt32 (bytes, 14)
    Assert.AreEqual (0, valueLength)

  [<Test>]
  member this.CreateSetRequest_UnicodeKey () =
    let key = "键"
    let value = [| 0x01uy |]
    let bytes = createSetRequest key value

    let keyLength = System.BitConverter.ToInt32 (bytes, 10)

    let expectedKeyLength =
      System.Text.Encoding.UTF8.GetByteCount (key)

    Assert.AreEqual (expectedKeyLength, keyLength)

  [<Test>]
  member this.CreateSetRequest_LongKey () =
    let key = String ('k', 1000)
    let value = [| 0xFFuy |]
    let bytes = createSetRequest key value

    let keyLength = System.BitConverter.ToInt32 (bytes, 10)
    Assert.AreEqual (1000, keyLength)

  [<Test>]
  member this.CreateSetRequest_LongValue () =
    let key = "longval"
    let value = Array.init 10000 (fun i -> byte (i % 256))
    let bytes = createSetRequest key value

    let valueLength = System.BitConverter.ToInt32 (bytes, 14)
    Assert.AreEqual (10000, valueLength)

  [<Test>]
  member this.TotalPacketLength_Calculation () =
    let key = "key"
    let value = [| 0x00uy ; 0x01uy ; 0x02uy |]
    let bytes = createSetRequest key value

    let totalLength = System.BitConverter.ToInt32 (bytes, 6)
    let expected = HEADER_SIZE + key.Length + value.Length
    Assert.AreEqual (expected, totalLength)
