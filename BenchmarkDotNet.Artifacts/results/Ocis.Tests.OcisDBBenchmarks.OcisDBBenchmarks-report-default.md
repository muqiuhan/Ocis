
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-rc.2.25502.107
  [Host]   : .NET 10.0.0 (10.0.25.50307), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.50307), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0      | Gen1      | Allocated   |
-------- |------- |-----------:|-----------:|----------:|----------:|----------:|------------:|
 **BulkSet** | **1000**   |   **5.016 ms** |   **4.739 ms** | **0.2598 ms** |         **-** |         **-** |   **166.37 KB** |
 BulkGet | 1000   |   8.450 ms |  44.833 ms | 2.4574 ms |         - |         - |   447.62 KB |
 **BulkSet** | **10000**  |  **63.776 ms** |  **14.909 ms** | **0.8172 ms** |         **-** |         **-** |  **1432.08 KB** |
 BulkGet | 10000  |  82.843 ms |  18.176 ms | 0.9963 ms |         - |         - |  4244.58 KB |
 **BulkSet** | **100000** | **245.704 ms** | **118.720 ms** | **6.5074 ms** | **1000.0000** |         **-** | **14088.39 KB** |
 BulkGet | 100000 | 286.427 ms |  23.933 ms | 1.3119 ms | 4000.0000 | 2000.0000 | 42213.39 KB |
