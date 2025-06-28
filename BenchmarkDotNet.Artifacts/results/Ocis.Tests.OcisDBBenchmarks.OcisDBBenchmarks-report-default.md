
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0       | Gen1      | Allocated    |
-------- |------- |-----------:|-----------:|----------:|-----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.447 ms** |   **9.397 ms** | **0.5151 ms** |          **-** |         **-** |    **991.72 KB** |
 BulkGet | 1000   |   7.873 ms |   3.937 ms | 0.2158 ms |          - |         - |   2131.91 KB |
 **BulkSet** | **10000**  |  **61.278 ms** | **103.579 ms** | **5.6775 ms** |  **1000.0000** |         **-** |   **9579.94 KB** |
 BulkGet | 10000  |  85.196 ms |  57.864 ms | 3.1717 ms |  2000.0000 | 1000.0000 |  21175.25 KB |
 **BulkSet** | **100000** | **295.885 ms** |  **32.813 ms** | **1.7986 ms** | **11000.0000** |         **-** |  **95527.39 KB** |
 BulkGet | 100000 | 386.592 ms |  71.542 ms | 3.9214 ms | 25000.0000 | 4000.0000 | 210997.27 KB |
