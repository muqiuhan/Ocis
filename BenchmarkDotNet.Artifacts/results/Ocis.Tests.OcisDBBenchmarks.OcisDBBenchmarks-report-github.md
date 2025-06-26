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
| **BulkSet** | **1000**   |   **5.175 ms** |  **7.699 ms** | **0.4220 ms** |         **-** |     **-** |  **1004.17 KB** |
| BulkGet     | 1000       |       7.613 ms |      7.236 ms |     0.3966 ms |             - |         - |      2404.78 KB |
| **BulkSet** | **10000**  |  **54.183 ms** |  **6.949 ms** | **0.3809 ms** |         **-** |     **-** |   **9960.7 KB** |
| BulkGet     | 10000      |      92.701 ms |     28.271 ms |     1.5496 ms |             - |         - |     23908.84 KB |
| **BulkSet** | **100000** | **299.638 ms** | **90.582 ms** | **4.9651 ms** | **1000.0000** |     **-** | **95510.49 KB** |
| BulkGet     | 100000     |     397.035 ms |     16.598 ms |     0.9098 ms |     3000.0000 | 1000.0000 |     234434.2 KB |
