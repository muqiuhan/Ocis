```

BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
| ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
| **BulkSet** | **1000**   |   **4.856 ms** |  **7.835 ms** | **0.4294 ms** |         **-** |     **-** |  **1004.97 KB** |
| BulkGet     | 1000       |       7.431 ms |      8.464 ms |     0.4639 ms |             - |         - |      2403.23 KB |
| **BulkSet** | **10000**  |  **52.731 ms** | **19.333 ms** | **1.0597 ms** |         **-** |     **-** |  **9951.96 KB** |
| BulkGet     | 10000      |      92.312 ms |     19.018 ms |     1.0424 ms |             - |         - |      23907.3 KB |
| **BulkSet** | **100000** | **288.006 ms** | **82.798 ms** | **4.5384 ms** | **1000.0000** |     **-** | **95578.96 KB** |
| BulkGet     | 100000     |     390.799 ms |    115.698 ms |     6.3418 ms |     3000.0000 | 1000.0000 |    234432.65 KB |
