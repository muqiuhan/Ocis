module Ocis.Perf.Tests.LatencyStatsTests

open NUnit.Framework
open Ocis.Perf
open Ocis.Perf.Stats

[<TestFixture>]
type LatencyStatsTests () =

  [<Test>]
  member _.ComputePercentilesFromSortedLatencies () =
    let stats = computeLatencyStats [ 1.0 ; 2.0 ; 3.0 ; 4.0 ; 5.0 ]
    Assert.That (stats.P50Ms, Is.EqualTo(3.0).Within (0.0001))
    Assert.That (stats.P95Ms, Is.EqualTo(5.0).Within (0.0001))
    Assert.That (stats.P99Ms, Is.EqualTo(5.0).Within (0.0001))

  [<Test>]
  member _.ComputeThroughputAndErrorRate () =
    let summary =
      summarizeRun "engine" "set" 4 900L 100L 10.0 [ 1.0 ; 2.0 ; 3.0 ]

    Assert.That (summary.ThroughputOpsPerSec, Is.EqualTo(90.0).Within (0.0001))
    Assert.That (summary.ErrorRate, Is.EqualTo(0.1).Within (0.0001))
    Assert.That (summary.TotalOperations, Is.EqualTo (1000L))
