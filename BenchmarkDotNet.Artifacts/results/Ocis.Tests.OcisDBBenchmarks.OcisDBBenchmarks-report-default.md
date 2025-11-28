
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.256 ms** |  **1.828 ms** | **0.1002 ms** |         **-** |     **-** |   **166.33 KB** |
 | BulkGet     | 1000       |       6.607 ms |      7.182 ms |     0.3936 ms |             - |         - |       471.02 KB |
 | **BulkSet** | **10000**  |  **66.772 ms** | **62.016 ms** | **3.3993 ms** |         **-** |     **-** |  **1432.04 KB** |
 | BulkGet     | 10000      |      79.582 ms |      9.686 ms |     0.5309 ms |             - |         - |      4478.91 KB |
 | **BulkSet** | **100000** | **254.601 ms** | **96.484 ms** | **5.2886 ms** | **1000.0000** |     **-** | **14089.41 KB** |
 | BulkGet     | 100000     |     275.424 ms |     42.625 ms |     2.3364 ms |     5000.0000 | 2000.0000 |      44557.1 KB |
