module Ocis.Tests.WalCheckpointTests

open System
open System.IO
open System.Text
open NUnit.Framework
open Ocis.OcisDB

[<TestFixture>]
type WalCheckpointTests () =

  let tempRoot =
    Path.Combine (Path.GetTempPath (), "ocis_wal_checkpoint_tests")

  let mutable testDbPath = ""

  let getValueAsString (db : OcisDB) (key : string) : string option =
    match db.Get (Encoding.UTF8.GetBytes key) with
    | Ok (Some value) -> Some (Encoding.UTF8.GetString value)
    | Ok None -> None
    | Error msg ->
      Assert.Fail $"Get failed for '{key}': {msg}"
      None

  [<SetUp>]
  member _.SetUp () =
    if Directory.Exists tempRoot then
      Directory.Delete (tempRoot, true)

    Directory.CreateDirectory tempRoot |> ignore
    testDbPath <- Path.Combine (tempRoot, Guid.NewGuid().ToString ("N"))

  [<TearDown>]
  member _.TearDown () =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()
    System.Threading.Thread.Sleep 50

    if Directory.Exists tempRoot then
      Directory.Delete (tempRoot, true)

  [<Test>]
  member _.Wal_IsResetAfterCheckpointTriggeringFlush () =
    use db =
      match OcisDB.Open (testDbPath, 3) with
      | Ok opened -> opened
      | Error msg ->
        Assert.Fail $"Failed to open DB: {msg}"
        Unchecked.defaultof<OcisDB>

    let walPath = Path.Combine (testDbPath, "wal.log")

    db.Set (Encoding.UTF8.GetBytes "k1", Encoding.UTF8.GetBytes "v1")
    |> ignore

    let sizeBeforeFlush = (FileInfo walPath).Length

    db.Set (Encoding.UTF8.GetBytes "k2", Encoding.UTF8.GetBytes "v2")
    |> ignore

    db.Set (Encoding.UTF8.GetBytes "k3", Encoding.UTF8.GetBytes "v3")
    |> ignore

    let sizeAfterFlush = (FileInfo walPath).Length

    Assert.That (sizeBeforeFlush, Is.GreaterThan 0L)

    Assert.That (
      sizeAfterFlush,
      Is.EqualTo 0L,
      "WAL should be reset after successful checkpoint"
    )

  [<Test>]
  member _.Recovery_RemainsCorrectAfterCheckpointReset () =
    do
      use db =
        match OcisDB.Open (testDbPath, 3) with
        | Ok opened -> opened
        | Error msg ->
          Assert.Fail $"Failed to open DB: {msg}"
          Unchecked.defaultof<OcisDB>

      db.Set (Encoding.UTF8.GetBytes "a", Encoding.UTF8.GetBytes "1")
      |> ignore

      db.Set (Encoding.UTF8.GetBytes "b", Encoding.UTF8.GetBytes "2")
      |> ignore

      db.Set (Encoding.UTF8.GetBytes "c", Encoding.UTF8.GetBytes "3")
      |> ignore

      db.Set (
        Encoding.UTF8.GetBytes "tail",
        Encoding.UTF8.GetBytes "tail-value"
      )
      |> ignore

    use reopened =
      match OcisDB.Open (testDbPath, 3) with
      | Ok opened -> opened
      | Error msg ->
        Assert.Fail $"Failed to reopen DB: {msg}"
        Unchecked.defaultof<OcisDB>

    Assert.That (getValueAsString reopened "a", Is.EqualTo (Some "1"))
    Assert.That (getValueAsString reopened "b", Is.EqualTo (Some "2"))
    Assert.That (getValueAsString reopened "c", Is.EqualTo (Some "3"))

    Assert.That (
      getValueAsString reopened "tail",
      Is.EqualTo (Some "tail-value")
    )

  [<Test>]
  member _.MultipleCheckpointCycles_DoNotCorruptState () =
    do
      use db =
        match OcisDB.Open (testDbPath, 2) with
        | Ok opened -> opened
        | Error msg ->
          Assert.Fail $"Failed to open DB: {msg}"
          Unchecked.defaultof<OcisDB>

      db.Set (Encoding.UTF8.GetBytes "a", Encoding.UTF8.GetBytes "1")
      |> ignore

      db.Set (Encoding.UTF8.GetBytes "b", Encoding.UTF8.GetBytes "1")
      |> ignore

      db.Set (Encoding.UTF8.GetBytes "a", Encoding.UTF8.GetBytes "2")
      |> ignore

      db.Set (Encoding.UTF8.GetBytes "c", Encoding.UTF8.GetBytes "3")
      |> ignore

      db.Delete (Encoding.UTF8.GetBytes "b") |> ignore

      db.Set (Encoding.UTF8.GetBytes "d", Encoding.UTF8.GetBytes "4")
      |> ignore

      db.Set (Encoding.UTF8.GetBytes "tail", Encoding.UTF8.GetBytes "5")
      |> ignore

    use reopened =
      match OcisDB.Open (testDbPath, 2) with
      | Ok opened -> opened
      | Error msg ->
        Assert.Fail $"Failed to reopen DB: {msg}"
        Unchecked.defaultof<OcisDB>

    Assert.That (getValueAsString reopened "a", Is.EqualTo (Some "2"))
    Assert.That (getValueAsString reopened "b", Is.EqualTo None)
    Assert.That (getValueAsString reopened "c", Is.EqualTo (Some "3"))
    Assert.That (getValueAsString reopened "d", Is.EqualTo (Some "4"))
    Assert.That (getValueAsString reopened "tail", Is.EqualTo (Some "5"))

  [<Test>]
  member _.Delete_AfterCheckpoint_RemainsDeletedAfterReopen_StrictMode () =
    do
      use db =
        match OcisDB.Open (testDbPath, 2, durabilityMode = "Strict") with
        | Ok opened -> opened
        | Error msg ->
          Assert.Fail $"Failed to open DB: {msg}"
          Unchecked.defaultof<OcisDB>

      let firstSet =
        db.Set (
          Encoding.UTF8.GetBytes "target",
          Encoding.UTF8.GetBytes "value-1"
        )

      Assert.That (firstSet.IsOk, Is.True)

      let secondSet =
        db.Set (
          Encoding.UTF8.GetBytes "other",
          Encoding.UTF8.GetBytes "value-2"
        )

      Assert.That (secondSet.IsOk, Is.True)

      let deleteResult = db.Delete (Encoding.UTF8.GetBytes "target")
      Assert.That (deleteResult.IsOk, Is.True)

    use reopened =
      match OcisDB.Open (testDbPath, 2, durabilityMode = "Strict") with
      | Ok opened -> opened
      | Error msg ->
        Assert.Fail $"Failed to reopen DB: {msg}"
        Unchecked.defaultof<OcisDB>

    Assert.That (getValueAsString reopened "target", Is.EqualTo None)
    Assert.That (getValueAsString reopened "other", Is.EqualTo (Some "value-2"))
