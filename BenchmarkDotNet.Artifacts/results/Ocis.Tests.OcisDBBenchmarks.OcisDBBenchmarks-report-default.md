
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |           Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | -------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.902 ms** |  **9.831 ms** | **0.5389 ms** |          **-** |     **-** |   **966.63 KB** |
 | BulkGet     | 1000       |       8.091 ms |     22.982 ms |     1.2597 ms |              - |         - |      2000.38 KB |
 | **BulkSet** | **10000**  |  **72.198 ms** | **23.917 ms** | **1.3110 ms** |  **1000.0000** |     **-** |  **9644.81 KB** |
 | BulkGet     | 10000      |      87.928 ms |    115.532 ms |     6.3327 ms |      2000.0000 | 1000.0000 |     19607.16 KB |
 | **BulkSet** | **100000** | **273.698 ms** | **11.942 ms** | **0.6546 ms** | **11000.0000** |     **-** | **95511.63 KB** |
 | BulkGet     | 100000     |     430.295 ms |    786.485 ms |    43.1099 ms |     23000.0000 | 4000.0000 |    195672.41 KB |
