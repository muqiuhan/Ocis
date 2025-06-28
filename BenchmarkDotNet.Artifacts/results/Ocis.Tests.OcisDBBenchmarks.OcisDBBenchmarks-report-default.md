
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error        | StdDev     | Gen0      | Gen1      | Allocated    |
-------- |------- |-----------:|-------------:|-----------:|----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.562 ms** |     **5.898 ms** |  **0.3233 ms** |         **-** |         **-** |   **1004.59 KB** |
 BulkGet | 1000   |   7.216 ms |     1.623 ms |  0.0890 ms |         - |         - |   2225.66 KB |
 **BulkSet** | **10000**  |  **63.340 ms** |   **125.534 ms** |  **6.8809 ms** |         **-** |         **-** |    **9950.2 KB** |
 BulkGet | 10000  |  93.738 ms |    66.794 ms |  3.6612 ms |         - |         - |  22112.53 KB |
 **BulkSet** | **100000** | **365.717 ms** | **1,232.566 ms** | **67.5611 ms** | **1000.0000** |         **-** |  **95509.88 KB** |
 BulkGet | 100000 | 471.836 ms |    93.574 ms |  5.1291 ms | 3000.0000 | 1000.0000 | 216466.01 KB |
