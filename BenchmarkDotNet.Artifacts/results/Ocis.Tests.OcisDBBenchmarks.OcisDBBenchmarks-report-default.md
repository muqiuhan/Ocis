
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev    | Gen0       | Gen1      | Allocated    |
-------- |------- |-----------:|-----------:|----------:|-----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.474 ms** |   **3.227 ms** | **0.1769 ms** |          **-** |         **-** |     **965.7 KB** |
 BulkGet | 1000   |   7.530 ms |   6.723 ms | 0.3685 ms |          - |         - |   2000.33 KB |
 **BulkSet** | **10000**  |  **61.131 ms** |  **57.590 ms** | **3.1567 ms** |  **1000.0000** |         **-** |   **9561.96 KB** |
 BulkGet | 10000  |  78.673 ms | 137.628 ms | 7.5438 ms |  2000.0000 | 1000.0000 |  19608.17 KB |
 **BulkSet** | **100000** | **284.417 ms** |   **7.529 ms** | **0.4127 ms** | **11000.0000** |         **-** |   **95511.5 KB** |
 BulkGet | 100000 | 359.820 ms | 153.102 ms | 8.3920 ms | 23000.0000 | 4000.0000 | 195673.43 KB |
