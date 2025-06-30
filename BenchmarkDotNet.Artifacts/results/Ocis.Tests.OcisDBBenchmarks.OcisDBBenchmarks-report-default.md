
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0       | Gen1      | Allocated    |
-------- |------- |-----------:|-----------:|----------:|-----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.675 ms** |  **13.530 ms** | **0.7416 ms** |          **-** |         **-** |    **983.44 KB** |
 BulkGet | 1000   |   8.120 ms |   3.350 ms | 0.1836 ms |          - |         - |   2100.66 KB |
 **BulkSet** | **10000**  |  **59.684 ms** | **142.163 ms** | **7.7925 ms** |  **1000.0000** |         **-** |   **9564.41 KB** |
 BulkGet | 10000  |  72.744 ms |  23.936 ms | 1.3120 ms |  2000.0000 | 1000.0000 |  20862.75 KB |
 **BulkSet** | **100000** | **296.450 ms** |  **67.807 ms** | **3.7167 ms** | **11000.0000** |         **-** |  **95510.63 KB** |
 BulkGet | 100000 | 365.170 ms |  16.816 ms | 0.9217 ms | 25000.0000 | 4000.0000 | 207872.27 KB |
