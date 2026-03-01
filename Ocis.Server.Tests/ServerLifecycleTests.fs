module Ocis.Server.Tests.ServerLifecycleTests

open System
open System.IO
open System.Net.Sockets
open System.Reflection
open System.Threading
open System.Threading.Tasks
open NUnit.Framework
open Ocis.OcisDB
open Ocis.Server.DbDispatcher
open Ocis.Server.Server

[<TestFixture>]
type ServerLifecycleTests() =

  let testDbDir =
    Path.Combine(
      Path.GetTempPath(),
      $"ocis_server_lifecycle_tests_{Guid.NewGuid():N}"
    )

  let openDbOrFail dir =
    match OcisDB.Open(dir, 1000) with
    | Ok opened -> opened
    | Result.Error msg ->
      Assert.Fail $"Failed to open db: {msg}"
      Unchecked.defaultof<OcisDB>

  let tryGetListener(server: TcpServer) =
    let flags = BindingFlags.Instance ||| BindingFlags.NonPublic

    typeof<TcpServer>.GetFields(flags)
    |> Array.tryPick(fun field ->
      if field.FieldType = typeof<TcpListener option> then
        match field.GetValue(server) with
        | :? (TcpListener option) as listenerOpt -> listenerOpt
        | _ -> None
      else
        None)

  let waitUntilRunningWithListener (server: TcpServer) timeoutMs =
    let deadline = DateTime.UtcNow.AddMilliseconds(float timeoutMs)

    let rec loop() =
      match server.State, tryGetListener server with
      | ServerState.Running, Some listener -> listener
      | _ when DateTime.UtcNow >= deadline ->
        Assert.Fail
          "Timed out waiting for server to start accepting connections"
        Unchecked.defaultof<TcpListener>
      | _ ->
        Thread.Sleep 20
        loop()

    loop()

  let waitForCompletion (task: Task) timeoutMs =
    let completed =
      Task.WhenAny(task, Task.Delay(timeoutMs: int)).GetAwaiter().GetResult()

    Object.ReferenceEquals(completed, task)

  [<SetUp>]
  member _.SetUp() =
    if not(Directory.Exists testDbDir) then
      Directory.CreateDirectory testDbDir |> ignore

  [<TearDown>]
  member _.TearDown() =
    try
      if Directory.Exists testDbDir then
        Directory.Delete(testDbDir, true)
    with _ ->
      ()

  [<Test>]
  member _.FatalAcceptLoopExceptionFaultsServerTask() =
    let db = openDbOrFail testDbDir
    use db = db
    use dispatcher = new OcisDbDispatcher(db, 64)

    let server =
      new TcpServer(
        {Host = "127.0.0.1"
         Port = 0
         MaxConnections = 8
         ReceiveTimeout = 2000
         SendTimeout = 2000},
        dispatcher
      )

    use serverDisposable = server :> IDisposable

    let startTask = server.StartAsync() |> Async.StartAsTask

    try
      let listener = waitUntilRunningWithListener server 2000
      listener.Stop()

      let completedInTime = waitForCompletion startTask 2000
      Assert.That(
        completedInTime,
        Is.True,
        "Expected StartAsync task to fault after fatal accept-loop exception"
      )
      Assert.That(
        startTask.IsFaulted,
        Is.True,
        "Expected StartAsync task to fault"
      )

      match server.State with
      | ServerState.Error _ -> Assert.Pass()
      | other -> Assert.Fail $"Expected Error state, got {other}"
    finally
      try
        server.StopAsync() |> Async.RunSynchronously
      with _ ->
        ()

  [<Test>]
  member _.StopAsyncCleansUpWhenStateIsError() =
    let db = openDbOrFail testDbDir
    use db = db
    use dispatcher = new OcisDbDispatcher(db, 64)

    let server =
      new TcpServer(
        {Host = "127.0.0.1"
         Port = 0
         MaxConnections = 8
         ReceiveTimeout = 2000
         SendTimeout = 2000},
        dispatcher
      )

    use serverDisposable = server :> IDisposable

    let startTask = server.StartAsync() |> Async.StartAsTask

    try
      let listener = waitUntilRunningWithListener server 2000
      listener.Stop()

      let completedInTime = waitForCompletion startTask 2000
      Assert.That(
        completedInTime,
        Is.True,
        "Expected StartAsync task to complete after listener failure"
      )
      Assert.That(
        startTask.IsFaulted,
        Is.True,
        "Expected StartAsync task to fault"
      )

      server.StopAsync() |> Async.RunSynchronously

      Assert.That(server.State, Is.EqualTo ServerState.Stopped)
    finally
      try
        server.StopAsync() |> Async.RunSynchronously
      with _ ->
        ()
