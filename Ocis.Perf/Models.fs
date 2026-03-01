namespace Ocis.Perf

type TargetMode =
  | Engine
  | Server

type OperationMode =
  | Set
  | Get
  | Mixed

type BenchmarkConfig =
  {Target: TargetMode
   Operation: OperationMode
   DurationSeconds: int
   WarmupSeconds: int
   RepeatCount: int
   Workers: int
   AllowUnsafeEngineConcurrency: bool
   KeyCount: int
   ValueBytes: int
   DataDir: string
   Host: string
   Port: int
   FlushThreshold: int
   DurabilityMode: string
   GroupCommitWindowMs: int
   GroupCommitBatchSize: int
   ClearCacheBeforeRun: bool
   ColdStart: bool
   PreloadKeyCount: int
   SkipPreload: bool
   OutputDir: string
   OutputTag: string}

type LatencyStats =
  {P50Ms: float
   P95Ms: float
   P99Ms: float
   MaxMs: float
   MinMs: float
   MeanMs: float}

type RunSummary =
  {Target: string
   Operation: string
   DurationSeconds: float
   Workers: int
   TotalOperations: int64
   SuccessfulOperations: int64
   FailedOperations: int64
   ErrorRate: float
   ThroughputOpsPerSec: float
   Latency: LatencyStats}

type AggregateSummary =
  {RunCount: int
   ThroughputMedianOpsPerSec: float
   ThroughputMinOpsPerSec: float
   ThroughputMaxOpsPerSec: float
   ThroughputCoefficientOfVariation: float
   LatencyP50MedianMs: float
   LatencyP95MedianMs: float
   LatencyP99MedianMs: float}
