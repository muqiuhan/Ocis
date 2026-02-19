module Ocis.Perf.Tests.AggregationTests

open NUnit.Framework
open Ocis.Perf

[<TestFixture>]
type AggregationTests() =
    let run throughput p50 p95 p99 : RunSummary =
        { Target = "server"
          Operation = "mixed"
          DurationSeconds = 10.0
          Workers = 4
          TotalOperations = 1000L
          SuccessfulOperations = 1000L
          FailedOperations = 0L
          ErrorRate = 0.0
          ThroughputOpsPerSec = throughput
          Latency =
            { P50Ms = p50
              P95Ms = p95
              P99Ms = p99
              MeanMs = p50
              MinMs = p50
              MaxMs = p99 } }

    [<Test>]
    member _.AggregateComputesMedianMinMaxAndCv() =
        let runs =
            [ run 100.0 1.0 4.0 8.0
              run 90.0 1.5 5.0 9.0
              run 110.0 0.5 3.0 7.0 ]

        let summary = Aggregation.summarize runs

        Assert.That(summary.RunCount, Is.EqualTo(3))
        Assert.That(summary.ThroughputMedianOpsPerSec, Is.EqualTo(100.0).Within(0.0001))
        Assert.That(summary.ThroughputMinOpsPerSec, Is.EqualTo(90.0).Within(0.0001))
        Assert.That(summary.ThroughputMaxOpsPerSec, Is.EqualTo(110.0).Within(0.0001))
        Assert.That(summary.ThroughputCoefficientOfVariation, Is.EqualTo(0.08164966).Within(0.000001))
        Assert.That(summary.LatencyP50MedianMs, Is.EqualTo(1.0).Within(0.0001))
        Assert.That(summary.LatencyP95MedianMs, Is.EqualTo(4.0).Within(0.0001))
        Assert.That(summary.LatencyP99MedianMs, Is.EqualTo(8.0).Within(0.0001))

    [<Test>]
    member _.AggregateSingleRunHasZeroCv() =
        let summary = Aggregation.summarize [ run 123.0 2.0 6.0 9.0 ]

        Assert.That(summary.RunCount, Is.EqualTo(1))
        Assert.That(summary.ThroughputCoefficientOfVariation, Is.EqualTo(0.0).Within(0.0001))
