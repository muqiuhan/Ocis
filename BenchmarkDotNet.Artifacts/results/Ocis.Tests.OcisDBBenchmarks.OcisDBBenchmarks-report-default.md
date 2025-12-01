
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.292 ms** |  **4.158 ms** | **0.2279 ms** |         **-** |     **-** |   **167.42 KB** |
 | BulkGet     | 1000       |       9.379 ms |     67.558 ms |     3.7031 ms |             - |         - |       472.11 KB |
 | **BulkSet** | **10000**  |  **68.885 ms** | **38.012 ms** | **2.0836 ms** |         **-** |     **-** |  **1433.13 KB** |
 | BulkGet     | 10000      |      89.408 ms |     13.683 ms |     0.7500 ms |             - |         - |      4480.01 KB |
 | **BulkSet** | **100000** | **267.612 ms** | **68.448 ms** | **3.7519 ms** | **1000.0000** |     **-** | **14089.45 KB** |
 | BulkGet     | 100000     |     295.853 ms |     74.385 ms |     4.0773 ms |     5000.0000 | 2000.0000 |      44558.2 KB |
