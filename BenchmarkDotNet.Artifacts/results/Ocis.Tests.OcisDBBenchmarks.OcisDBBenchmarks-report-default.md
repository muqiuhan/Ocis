
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |          Error |        StdDev |           Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | -------------: | ------------: | -------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.535 ms** |   **7.076 ms** | **0.3878 ms** |          **-** |     **-** |      **981 KB** |
 | BulkGet     | 1000       |       8.073 ms |       3.922 ms |     0.2150 ms |              - |         - |      2101.15 KB |
 | **BulkSet** | **10000**  |  **57.306 ms** | **119.134 ms** | **6.5302 ms** |  **1000.0000** |     **-** |  **9565.19 KB** |
 | BulkGet     | 10000      |      85.745 ms |      21.797 ms |     1.1948 ms |      2000.0000 | 1000.0000 |     20863.18 KB |
 | **BulkSet** | **100000** | **291.813 ms** |  **28.191 ms** | **1.5452 ms** | **11000.0000** |     **-** | **95513.04 KB** |
 | BulkGet     | 100000     |     370.641 ms |      55.122 ms |     3.0214 ms |     25000.0000 | 4000.0000 |     207872.7 KB |
