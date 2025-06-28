
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |           Error |         StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | --------------: | -------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **4.740 ms** |   **0.7260 ms** |  **0.0398 ms** |         **-** |     **-** |  **1004.97 KB** |
 | BulkGet     | 1000       |       7.851 ms |       5.4889 ms |      0.3009 ms |             - |         - |      2225.66 KB |
 | **BulkSet** | **10000**  |  **53.310 ms** |  **12.2275 ms** |  **0.6702 ms** |         **-** |     **-** |  **9951.84 KB** |
 | BulkGet     | 10000      |      81.809 ms |     106.9466 ms |      5.8621 ms |             - |         - |     22112.53 KB |
 | **BulkSet** | **100000** | **319.175 ms** | **184.8787 ms** | **10.1338 ms** | **1000.0000** |     **-** | **95508.98 KB** |
 | BulkGet     | 100000     |     413.081 ms |     377.4320 ms |     20.6883 ms |     3000.0000 | 1000.0000 |    216466.01 KB |
