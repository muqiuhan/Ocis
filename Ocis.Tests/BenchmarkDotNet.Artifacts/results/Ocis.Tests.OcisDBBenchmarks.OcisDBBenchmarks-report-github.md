```

BenchmarkDotNet v0.15.2, Linux openSUSE Tumbleweed
AMD Ryzen 7 8845HS w/ Radeon 780M Graphics 5.14GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.301
  [Host]   : .NET 9.0.6 (9.0.625.26613), X64 AOT AVX-512F+CD+BW+DQ+VL+VBMI DEBUG
  ShortRun : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method  | Count  | Mean       | Error      | StdDev    | Gen0       | Gen1      | Allocated    |
|-------- |------- |-----------:|-----------:|----------:|-----------:|----------:|-------------:|
| **BulkSet** | **1000**   |   **9.629 ms** |  **23.165 ms** |  **1.270 ms** |          **-** |         **-** |   **1005.73 KB** |
| BulkGet | 1000   |  12.701 ms |  19.301 ms |  1.058 ms |          - |         - |   2403.21 KB |
| **BulkSet** | **10000**  | **100.723 ms** | **110.024 ms** |  **6.031 ms** |  **1000.0000** |         **-** |   **9955.27 KB** |
| BulkGet | 10000  | 118.462 ms |  41.716 ms |  2.287 ms |  2000.0000 | 1000.0000 |  23907.48 KB |
| **BulkSet** | **100000** | **650.189 ms** | **171.918 ms** |  **9.423 ms** | **11000.0000** |         **-** |  **95508.79 KB** |
| BulkGet | 100000 | 734.871 ms | 260.082 ms | 14.256 ms | 28000.0000 | 2000.0000 | 234432.63 KB |
