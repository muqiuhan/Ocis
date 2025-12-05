
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-rc.2.25502.107
  [Host]   : .NET 10.0.0 (10.0.25.50307), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.50307), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.120 ms** |  **4.295 ms** | **0.2354 ms** |         **-** |     **-** |   **166.37 KB** |
 | BulkGet     | 1000       |       6.353 ms |      6.431 ms |     0.3525 ms |             - |         - |       447.62 KB |
 | **BulkSet** | **10000**  |  **66.546 ms** | **27.219 ms** | **1.4920 ms** |         **-** |     **-** |  **1432.08 KB** |
 | BulkGet     | 10000      |      82.687 ms |     13.063 ms |     0.7160 ms |             - |         - |      4244.58 KB |
 | **BulkSet** | **100000** | **244.495 ms** | **21.818 ms** | **1.1959 ms** | **1000.0000** |     **-** | **14088.39 KB** |
 | BulkGet     | 100000     |     286.858 ms |     25.212 ms |     1.3819 ms |     4000.0000 | 2000.0000 |     42213.39 KB |
