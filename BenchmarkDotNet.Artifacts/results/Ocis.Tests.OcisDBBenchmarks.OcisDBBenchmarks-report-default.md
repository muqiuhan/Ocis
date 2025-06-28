
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0       | Gen1      | Allocated    |
-------- |------- |-----------:|-----------:|----------:|-----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.579 ms** |  **15.130 ms** | **0.8294 ms** |          **-** |         **-** |    **965.91 KB** |
 BulkGet | 1000   |   7.796 ms |   4.015 ms | 0.2201 ms |          - |         - |   2131.91 KB |
 **BulkSet** | **10000**  |  **56.973 ms** |  **68.309 ms** | **3.7442 ms** |  **1000.0000** |         **-** |   **9582.12 KB** |
 BulkGet | 10000  |  87.802 ms |  20.457 ms | 1.1213 ms |  2000.0000 | 1000.0000 |  21175.25 KB |
 **BulkSet** | **100000** | **306.510 ms** |  **39.882 ms** | **2.1861 ms** | **11000.0000** |         **-** |  **95509.41 KB** |
 BulkGet | 100000 | 378.065 ms | 128.308 ms | 7.0330 ms | 25000.0000 | 4000.0000 | 210997.27 KB |
