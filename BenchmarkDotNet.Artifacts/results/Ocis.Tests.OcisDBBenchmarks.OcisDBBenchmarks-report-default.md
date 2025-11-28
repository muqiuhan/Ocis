
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0      | Gen1      | Allocated   |
-------- |------- |-----------:|-----------:|----------:|----------:|----------:|------------:|
 **BulkSet** | **1000**   |   **5.252 ms** |  **10.375 ms** | **0.5687 ms** |         **-** |         **-** |   **166.33 KB** |
 BulkGet | 1000   |   6.274 ms |   1.634 ms | 0.0896 ms |         - |         - |   471.02 KB |
 **BulkSet** | **10000**  |  **52.317 ms** | **178.106 ms** | **9.7626 ms** |         **-** |         **-** |  **1432.04 KB** |
 BulkGet | 10000  |  73.827 ms |  71.156 ms | 3.9003 ms |         - |         - |  4478.91 KB |
 **BulkSet** | **100000** | **226.579 ms** |  **69.396 ms** | **3.8038 ms** | **1000.0000** |         **-** | **14089.41 KB** |
 BulkGet | 100000 | 262.440 ms |  47.264 ms | 2.5907 ms | 5000.0000 | 2000.0000 | 44558.16 KB |
