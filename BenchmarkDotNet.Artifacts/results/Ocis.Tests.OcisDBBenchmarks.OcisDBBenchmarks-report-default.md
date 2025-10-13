
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |           Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | -------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.480 ms** |  **3.535 ms** | **0.1937 ms** |          **-** |     **-** |   **966.56 KB** |
 | BulkGet     | 1000       |       8.617 ms |     17.465 ms |     0.9573 ms |              - |         - |      2001.44 KB |
 | **BulkSet** | **10000**  |  **58.916 ms** | **29.374 ms** | **1.6101 ms** |  **1000.0000** |     **-** |  **9562.02 KB** |
 | BulkGet     | 10000      |     103.432 ms |     42.838 ms |     2.3481 ms |      2000.0000 | 1000.0000 |     19608.22 KB |
 | **BulkSet** | **100000** | **283.876 ms** |  **2.947 ms** | **0.1615 ms** | **11000.0000** |     **-** | **95509.73 KB** |
 | BulkGet     | 100000     |     369.380 ms |    114.501 ms |     6.2762 ms |     23000.0000 | 4000.0000 |    195672.41 KB |
