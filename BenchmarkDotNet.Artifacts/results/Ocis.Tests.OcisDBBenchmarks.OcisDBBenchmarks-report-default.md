
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |          Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | -------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.373 ms** |   **4.641 ms** | **0.2544 ms** |         **-** |     **-** |   **167.42 KB** |
 | BulkGet     | 1000       |       6.545 ms |       5.465 ms |     0.2995 ms |             - |         - |       472.11 KB |
 | **BulkSet** | **10000**  |  **60.356 ms** | **152.673 ms** | **8.3685 ms** |         **-** |     **-** |  **1433.13 KB** |
 | BulkGet     | 10000      |      85.829 ms |      26.899 ms |     1.4744 ms |             - |         - |      4480.01 KB |
 | **BulkSet** | **100000** | **259.009 ms** | **120.832 ms** | **6.6232 ms** | **1000.0000** |     **-** | **14089.45 KB** |
 | BulkGet     | 100000     |     286.011 ms |      45.198 ms |     2.4775 ms |     4000.0000 | 2000.0000 |      41433.2 KB |
