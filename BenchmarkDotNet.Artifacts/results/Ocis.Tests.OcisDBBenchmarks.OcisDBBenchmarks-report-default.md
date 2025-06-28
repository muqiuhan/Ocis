
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.053 ms** |  **4.494 ms** | **0.2463 ms** |         **-** |     **-** |   **966.69 KB** |
 | BulkGet     | 1000       |       7.303 ms |      7.866 ms |     0.4312 ms |             - |         - |      2131.91 KB |
 | **BulkSet** | **10000**  |  **53.924 ms** | **45.550 ms** | **2.4967 ms** |         **-** |     **-** |  **9561.06 KB** |
 | BulkGet     | 10000      |      74.843 ms |    312.557 ms |    17.1323 ms |             - |         - |     21175.04 KB |
 | **BulkSet** | **100000** | **294.837 ms** | **48.903 ms** | **2.6806 ms** | **1000.0000** |     **-** | **95510.19 KB** |
 | BulkGet     | 100000     |     380.365 ms |     47.300 ms |     2.5927 ms |     3000.0000 | 1000.0000 |    210997.05 KB |
