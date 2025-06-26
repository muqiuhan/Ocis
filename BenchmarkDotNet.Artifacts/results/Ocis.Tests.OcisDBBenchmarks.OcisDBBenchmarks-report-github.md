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
| **BulkSet** | **1000**   |   **5.397 ms** |  **9.551 ms** | **0.5235 ms** |         **-** |     **-** |  **1004.59 KB** |
| BulkGet     | 1000       |       7.804 ms |      6.541 ms |     0.3585 ms |             - |         - |      2405.34 KB |
| **BulkSet** | **10000**  |  **53.176 ms** |  **2.070 ms** | **0.1135 ms** |         **-** |     **-** |  **9972.74 KB** |
| BulkGet     | 10000      |      93.963 ms |     26.592 ms |     1.4576 ms |             - |         - |     23909.41 KB |
| **BulkSet** | **100000** | **305.264 ms** | **57.092 ms** | **3.1294 ms** | **1000.0000** |     **-** | **95510.55 KB** |
| BulkGet     | 100000     |     419.953 ms |    346.164 ms |    18.9744 ms |     3000.0000 | 1000.0000 |    234434.76 KB |
