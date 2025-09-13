
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |          Error |        StdDev |           Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | -------------: | ------------: | -------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **4.994 ms** |  **11.806 ms** | **0.6471 ms** |          **-** |     **-** |   **901.86 KB** |
 | BulkGet     | 1000       |       7.234 ms |      15.306 ms |     0.8390 ms |              - |         - |      2014.07 KB |
 | **BulkSet** | **10000**  |  **57.268 ms** | **141.362 ms** | **7.7485 ms** |  **1000.0000** |     **-** |  **8940.95 KB** |
 | BulkGet     | 10000      |      86.693 ms |      25.060 ms |     1.3736 ms |      2000.0000 | 1000.0000 |     19903.25 KB |
 | **BulkSet** | **100000** | **281.590 ms** |  **23.895 ms** | **1.3098 ms** | **10000.0000** |     **-** | **89267.73 KB** |
 | BulkGet     | 100000     |     359.215 ms |      49.439 ms |     2.7099 ms |     24000.0000 | 3000.0000 |    198792.57 KB |
