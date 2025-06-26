```

BenchmarkDotNet v0.15.2, Linux openSUSE Tumbleweed
AMD Ryzen 7 8845HS w/ Radeon 780M Graphics 5.14GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.301
  [Host]   : .NET 9.0.6 (9.0.625.26613), X64 AOT AVX-512F+CD+BW+DQ+VL+VBMI DEBUG
  ShortRun : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method  | Count  | Mean         | Error        | StdDev      | Gen0        | Gen1      | Allocated  |
|-------- |------- |-------------:|-------------:|------------:|------------:|----------:|-----------:|
| **BulkSet** | **1000**   |     **9.039 ms** |    **10.121 ms** |   **0.5548 ms** |           **-** |         **-** |    **1.12 MB** |
| BulkGet | 1000   |    13.136 ms |     9.329 ms |   0.5114 ms |           - |         - |    2.76 MB |
| **BulkSet** | **10000**  |    **84.730 ms** |    **10.539 ms** |   **0.5777 ms** |   **1000.0000** |         **-** |   **11.24 MB** |
| BulkGet | 10000  |   142.414 ms |   344.475 ms |  18.8818 ms |   6000.0000 | 1000.0000 |   51.78 MB |
| **BulkSet** | **100000** |   **633.025 ms** |   **179.312 ms** |   **9.8287 ms** |  **13000.0000** | **1000.0000** |  **108.59 MB** |
| BulkGet | 100000 | 5,826.877 ms | 3,263.841 ms | 178.9021 ms | 323000.0000 | 2000.0000 | 2578.38 MB |
