
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method  | Count  | Mean       | Error      | StdDev     | Gen0      | Gen1      | Allocated    |
-------- |------- |-----------:|-----------:|-----------:|----------:|----------:|-------------:|
 **BulkSet** | **1000**   |   **5.260 ms** |   **6.974 ms** |  **0.3822 ms** |         **-** |         **-** |   **1004.97 KB** |
 BulkGet | 1000   |   8.111 ms |  14.175 ms |  0.7770 ms |         - |         - |   2405.34 KB |
 **BulkSet** | **10000**  |  **52.994 ms** |   **4.194 ms** |  **0.2299 ms** |         **-** |         **-** |    **9951.9 KB** |
 BulkGet | 10000  |  94.405 ms |  25.957 ms |  1.4228 ms |         - |         - |  23909.41 KB |
 **BulkSet** | **100000** | **323.336 ms** | **335.272 ms** | **18.3774 ms** | **1000.0000** |         **-** |  **95510.19 KB** |
 BulkGet | 100000 | 418.403 ms | 386.883 ms | 21.2063 ms | 3000.0000 | 1000.0000 | 234434.76 KB |
