```

BenchmarkDotNet v0.15.2, Linux openSUSE Tumbleweed
AMD Ryzen 7 8845HS w/ Radeon 780M Graphics 5.14GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.301
  [Host]   : .NET 9.0.6 (9.0.625.26613), X64 AOT AVX-512F+CD+BW+DQ+VL+VBMI DEBUG
  ShortRun : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method      | Count      |           Mean |          Error |         StdDev |           Gen0 |      Gen1 |       Allocated |
| ----------- | ---------- | -------------: | -------------: | -------------: | -------------: | --------: | --------------: |
| **BulkSet** | **1000**   |   **9.526 ms** |   **3.969 ms** |  **0.2176 ms** |          **-** |     **-** |  **1004.95 KB** |
| BulkGet     | 1000       |      13.117 ms |      11.373 ms |      0.6234 ms |              - |         - |      2403.21 KB |
| **BulkSet** | **10000**  | **113.793 ms** | **148.250 ms** |  **8.1261 ms** |  **1000.0000** |     **-** |  **9950.24 KB** |
| BulkGet     | 10000      |     122.123 ms |     264.695 ms |     14.5088 ms |      2000.0000 | 1000.0000 |     23907.48 KB |
| **BulkSet** | **100000** | **705.837 ms** | **444.079 ms** | **24.3415 ms** | **11000.0000** |     **-** | **96533.24 KB** |
| BulkGet     | 100000     |     742.808 ms |     247.308 ms |     13.5558 ms |     28000.0000 | 2000.0000 |    234432.63 KB |
