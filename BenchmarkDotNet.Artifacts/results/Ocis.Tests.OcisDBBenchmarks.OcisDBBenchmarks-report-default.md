
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.276 ms** |  **7.500 ms** | **0.4111 ms** |         **-** |     **-** |   **166.36 KB** |
 | BulkGet     | 1000       |       6.225 ms |      3.197 ms |     0.1752 ms |             - |         - |       400.73 KB |
 | **BulkSet** | **10000**  |  **63.294 ms** | **13.292 ms** | **0.7286 ms** |         **-** |     **-** |  **1432.07 KB** |
 | BulkGet     | 10000      |      79.719 ms |     53.906 ms |     2.9548 ms |             - |         - |      3775.82 KB |
 | **BulkSet** | **100000** | **246.479 ms** | **40.782 ms** | **2.2354 ms** | **1000.0000** |     **-** | **14088.38 KB** |
 | BulkGet     | 100000     |     283.456 ms |     54.018 ms |     2.9609 ms |     4000.0000 | 2000.0000 |     37525.88 KB |
