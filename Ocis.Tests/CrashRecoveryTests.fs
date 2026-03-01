module Ocis.Tests.CrashRecoveryTests

open System
open System.IO
open System.Text
open System.Threading
open NUnit.Framework
open Ocis.OcisDB

[<TestFixture>]
[<Category("CrashRecovery")>]
type CrashRecoveryTests() =

  let tempRoot =
    Path.Combine(Path.GetTempPath(), "ocis_crash_recovery_tests")
  let mutable testDbPath = ""

  let openBalancedDb() : OcisDB =
    match
      OcisDB.Open(
        testDbPath,
        1000,
        durabilityMode = "Balanced",
        groupCommitWindowMs = 10
      )
    with
    | Ok db -> db
    | Error msg ->
      Assert.Fail $"Failed to open DB: {msg}"
      Unchecked.defaultof<OcisDB>

  let valueOf (db: OcisDB) (key: string) : string option =
    match db.Get(Encoding.UTF8.GetBytes key) with
    | Ok(Some value) -> Some(Encoding.UTF8.GetString value)
    | Ok None -> None
    | Error msg ->
      Assert.Fail $"Get failed for key '{key}': {msg}"
      None

  let setValue (db: OcisDB) (key: string) (value: string) =
    let result =
      db.Set(Encoding.UTF8.GetBytes key, Encoding.UTF8.GetBytes value)
    Assert.That(result.IsOk, Is.True, $"Set failed for key '{key}'")

  let deleteKey (db: OcisDB) (key: string) =
    let result = db.Delete(Encoding.UTF8.GetBytes key)
    Assert.That(result.IsOk, Is.True, $"Delete failed for key '{key}'")

  let runWithoutGracefulShutdown(work: OcisDB -> unit) =
    // Simulate abrupt-stop style by intentionally skipping OcisDB.Dispose.
    // Keep the DB instance scoped locally, then force GC/finalizers.
    let weakReference =
      let db = openBalancedDb()
      work db
      WeakReference(db :> obj)

    GC.Collect()
    GC.WaitForPendingFinalizers()
    GC.Collect()
    Thread.Sleep 50

    weakReference

  [<SetUp>]
  member _.SetUp() =
    if Directory.Exists tempRoot then
      Directory.Delete(tempRoot, true)

    Directory.CreateDirectory tempRoot |> ignore
    testDbPath <- Path.Combine(tempRoot, Guid.NewGuid().ToString("N"))

  [<TearDown>]
  member _.TearDown() =
    GC.Collect()
    GC.WaitForPendingFinalizers()
    GC.Collect()
    Thread.Sleep 50

    if Directory.Exists tempRoot then
      Directory.Delete(tempRoot, true)

  [<Test>]
  member _.BalancedMode_ReopenAfterAbruptStopStyle_AllowsContinuedOperations() =
    runWithoutGracefulShutdown(fun db ->
      setValue db "k-a" "v-a"
      setValue db "k-b" "v-b")
    |> ignore

    use reopened = openBalancedDb()
    setValue reopened "after-reopen" "ok"
    Assert.That(valueOf reopened "after-reopen", Is.EqualTo(Some "ok"))

  [<Test>]
  member _.BalancedMode_NoCorruption_AfterRepeatedOpenCloseCycles() =
    let totalCycles = 6

    for cycle = 1 to totalCycles do
      use db = openBalancedDb()

      for i = 1 to 5 do
        let key = $"stable-key-{i:D2}"
        let value = $"cycle-{cycle}-value-{i:D2}"
        setValue db key value

      for i = 1 to 5 do
        let key = $"stable-key-{i:D2}"
        let expected = Some($"cycle-{cycle}-value-{i:D2}")
        Assert.That(valueOf db key, Is.EqualTo expected)

    use finalReopen = openBalancedDb()

    for i = 1 to 5 do
      let key = $"stable-key-{i:D2}"
      let expected = Some($"cycle-{totalCycles}-value-{i:D2}")
      Assert.That(
        valueOf finalReopen key,
        Is.EqualTo expected,
        $"Corruption detected at key '{key}'"
      )

  [<Test>]
  member _.BalancedMode_DeleteAndReopen_PreservesTombstoneState() =
    do
      use db = openBalancedDb()
      setValue db "delete-target" "present-before-delete"
      setValue db "survivor" "still-here"
      deleteKey db "delete-target"

    use reopened = openBalancedDb()
    Assert.That(valueOf reopened "delete-target", Is.EqualTo None)
    Assert.That(valueOf reopened "survivor", Is.EqualTo(Some "still-here"))
