
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |          Error |        StdDev |         Median |           Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | -------------: | ------------: | -------------: | -------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.050 ms** |  **10.200 ms** | **0.5591 ms** |   **4.771 ms** |          **-** |     **-** |   **904.99 KB** |
 | BulkGet     | 1000       |      10.380 ms |      83.112 ms |     4.5557 ms |       8.045 ms |              - |         - |       2027.1 KB |
 | **BulkSet** | **10000**  |  **56.238 ms** | **169.132 ms** | **9.2707 ms** |  **54.248 ms** |  **1000.0000** |     **-** |  **8884.41 KB** |
 | BulkGet     | 10000      |      86.917 ms |      38.931 ms |     2.1340 ms |      87.217 ms |      2000.0000 | 1000.0000 |     19916.28 KB |
 | **BulkSet** | **100000** | **284.932 ms** |   **5.302 ms** | **0.2906 ms** | **285.002 ms** | **10000.0000** |     **-** | **88479.29 KB** |
 | BulkGet     | 100000     |     360.183 ms |      91.767 ms |     5.0301 ms |     358.736 ms |     24000.0000 | 3000.0000 |    198805.74 KB |
