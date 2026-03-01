module Ocis.Server.Tests.ShutdownAndResilienceTests

open System
open System.Net.Sockets
open NUnit.Framework
open Ocis.Server.Resilience

[<TestFixture>]
type ShutdownAndResilienceTests() =

  [<TestCase(SocketError.TimedOut)>]
  [<TestCase(SocketError.WouldBlock)>]
  [<TestCase(SocketError.TryAgain)>]
  [<TestCase(SocketError.Interrupted)>]
  [<TestCase(SocketError.NoBufferSpaceAvailable)>]
  member _.TransientSocketErrorsAreClassifiedAsTransient(error: SocketError) =
    let ex = SocketException(int error) :> exn
    Assert.That(isTransientAcceptException ex, Is.True)

  [<TestCase(SocketError.AccessDenied)>]
  [<TestCase(SocketError.AddressAlreadyInUse)>]
  [<TestCase(SocketError.ConnectionRefused)>]
  [<TestCase(SocketError.NotSocket)>]
  member _.NonTransientSocketErrorsAreNotClassifiedAsTransient
    (error: SocketError)
    =
    let ex = SocketException(int error) :> exn
    Assert.That(isTransientAcceptException ex, Is.False)

  [<Test>]
  member _.RetryDelayGrowsExponentiallyAndCapsAtMaximum() =
    let baseDelayMs = 25
    let maxDelayMs = 200

    Assert.That(
      computeBoundedRetryDelayMs 0 baseDelayMs maxDelayMs,
      Is.EqualTo 25
    )
    Assert.That(
      computeBoundedRetryDelayMs 1 baseDelayMs maxDelayMs,
      Is.EqualTo 50
    )
    Assert.That(
      computeBoundedRetryDelayMs 2 baseDelayMs maxDelayMs,
      Is.EqualTo 100
    )
    Assert.That(
      computeBoundedRetryDelayMs 3 baseDelayMs maxDelayMs,
      Is.EqualTo 200
    )
    Assert.That(
      computeBoundedRetryDelayMs 4 baseDelayMs maxDelayMs,
      Is.EqualTo 200
    )

  [<Test>]
  member _.RetryDelayHandlesNegativeAttemptByTreatingItAsFirstAttempt() =
    let delay = computeBoundedRetryDelayMs -1 50 1000
    Assert.That(delay, Is.EqualTo 50)

  [<Test>]
  member _.RetryDelayHandlesLargeAttemptWithoutOverflow() =
    let delay = computeBoundedRetryDelayMs 63 25 200
    Assert.That(delay, Is.EqualTo 200)
