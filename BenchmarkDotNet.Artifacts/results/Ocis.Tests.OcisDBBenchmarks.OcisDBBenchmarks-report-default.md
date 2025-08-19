
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0       | Gen1      | Gen2      | Allocated    |
-------- |------- |-----------:|-----------:|----------:|-----------:|----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.662 ms** |   **6.186 ms** | **0.3391 ms** |          **-** |         **-** |         **-** |    **981.05 KB** |
 BulkGet | 1000   |   8.007 ms |   5.173 ms | 0.2836 ms |          - |         - |         - |   2092.72 KB |
 **BulkSet** | **10000**  |  **65.032 ms** | **100.395 ms** | **5.5030 ms** |  **1000.0000** |         **-** |         **-** |   **9565.71 KB** |
 BulkGet | 10000  |  90.878 ms |  29.688 ms | 1.6273 ms |  2000.0000 | 1000.0000 |         - |  20784.49 KB |
 **BulkSet** | **100000** | **298.936 ms** |  **61.580 ms** | **3.3754 ms** | **11000.0000** |         **-** |         **-** |  **95580.01 KB** |
 BulkGet | 100000 | 401.850 ms | 116.732 ms | 6.3985 ms | 27000.0000 | 6000.0000 | 2000.0000 | 207093.41 KB |
