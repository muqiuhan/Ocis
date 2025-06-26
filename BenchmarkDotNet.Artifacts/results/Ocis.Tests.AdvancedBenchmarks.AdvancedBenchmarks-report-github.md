```

BenchmarkDotNet v0.15.2, Linux openSUSE Tumbleweed
AMD Ryzen 7 8845HS w/ Radeon 780M Graphics 5.14GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.301
  [Host]   : .NET 9.0.6 (9.0.625.26613), X64 AOT AVX-512F+CD+BW+DQ+VL+VBMI DEBUG
  ShortRun : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                   | DataSize   | HotDataRatio |           Mean |           Error |       StdDev |           Gen0 |          Gen1 |      Gen2 |     Allocated |
| ------------------------ | ---------- | ------------ | -------------: | --------------: | -----------: | -------------: | ------------: | --------: | ------------: |
| **LayeredWrite**         | **10000**  | **0.2**      |   **419.0 ms** |    **71.57 ms** |  **3.92 ms** |  **3000.0000** | **1000.0000** |     **-** |   **27.2 MB** |
| CrossSSTableRead         | 10000      | 0.2          |       426.4 ms |       102.49 ms |      5.62 ms |      4000.0000 |     1000.0000 |         - |      33.29 MB |
| RangeQueryAcrossSSTables | 10000      | 0.2          |             NA |              NA |           NA |             NA |            NA |        NA |            NA |
| RealisticWorkload        | 10000      | 0.2          |       413.4 ms |       158.12 ms |      8.67 ms |      3000.0000 |             - |         - |       29.8 MB |
| PostCompactionRead       | 10000      | 0.2          |     1,421.0 ms |        62.45 ms |      3.42 ms |      4000.0000 |     2000.0000 |         - |      32.16 MB |
| MemoryEfficiencyTest     | 10000      | 0.2          |             NA |              NA |           NA |             NA |            NA |        NA |            NA |
| **LayeredWrite**         | **10000**  | **0.5**      |   **415.3 ms** |    **59.80 ms** |  **3.28 ms** |  **3000.0000** | **1000.0000** |     **-** |   **27.6 MB** |
| CrossSSTableRead         | 10000      | 0.5          |       419.5 ms |       124.01 ms |      6.80 ms |      4000.0000 |             - |         - |      37.74 MB |
| RangeQueryAcrossSSTables | 10000      | 0.5          |       408.2 ms |       178.53 ms |      9.79 ms |      3000.0000 |     1000.0000 |         - |      27.03 MB |
| RealisticWorkload        | 10000      | 0.5          |       413.1 ms |        68.45 ms |      3.75 ms |      3000.0000 |             - |         - |      29.54 MB |
| PostCompactionRead       | 10000      | 0.5          |     1,423.6 ms |        47.08 ms |      2.58 ms |      4000.0000 |     2000.0000 |         - |      32.02 MB |
| MemoryEfficiencyTest     | 10000      | 0.5          |       433.6 ms |       112.05 ms |      6.14 ms |     12000.0000 |    10000.0000 | 8000.0000 |      37.66 MB |
| **LayeredWrite**         | **10000**  | **0.8**      |   **410.7 ms** |    **60.19 ms** |  **3.30 ms** |  **3000.0000** | **1000.0000** |     **-** |  **27.18 MB** |
| CrossSSTableRead         | 10000      | 0.8          |       426.4 ms |        46.23 ms |      2.53 ms |      5000.0000 |     1000.0000 |         - |      43.38 MB |
| RangeQueryAcrossSSTables | 10000      | 0.8          |       411.7 ms |        19.35 ms |      1.06 ms |      3000.0000 |     1000.0000 |         - |      27.24 MB |
| RealisticWorkload        | 10000      | 0.8          |       416.2 ms |        85.35 ms |      4.68 ms |      3000.0000 |             - |         - |      30.82 MB |
| PostCompactionRead       | 10000      | 0.8          |     1,428.7 ms |       147.87 ms |      8.11 ms |      4000.0000 |     1000.0000 |         - |      31.88 MB |
| MemoryEfficiencyTest     | 10000      | 0.8          |       434.4 ms |        39.74 ms |      2.18 ms |     12000.0000 |    10000.0000 | 8000.0000 |      37.52 MB |
| **LayeredWrite**         | **50000**  | **0.2**      |   **824.1 ms** |   **227.16 ms** | **12.45 ms** | **18000.0000** | **5000.0000** |     **-** | **147.56 MB** |
| CrossSSTableRead         | 50000      | 0.2          |       859.6 ms |       236.92 ms |     12.99 ms |     24000.0000 |     8000.0000 | 1000.0000 |     188.67 MB |
| RangeQueryAcrossSSTables | 50000      | 0.2          |       865.9 ms |       560.44 ms |     30.72 ms |     18000.0000 |     5000.0000 |         - |     148.52 MB |
| RealisticWorkload        | 50000      | 0.2          |       901.6 ms |       544.34 ms |     29.84 ms |     18000.0000 |     5000.0000 |         - |     152.51 MB |
| PostCompactionRead       | 50000      | 0.2          |     1,892.1 ms |     1,067.17 ms |     58.49 ms |     19000.0000 |     6000.0000 |         - |     155.62 MB |
| MemoryEfficiencyTest     | 50000      | 0.2          |       911.2 ms |       128.19 ms |      7.03 ms |     27000.0000 |    14000.0000 | 8000.0000 |     162.46 MB |
| **LayeredWrite**         | **50000**  | **0.5**      |   **818.9 ms** |   **341.40 ms** | **18.71 ms** | **18000.0000** | **5000.0000** |     **-** | **148.92 MB** |
| CrossSSTableRead         | 50000      | 0.5          |       873.4 ms |       129.81 ms |      7.12 ms |     28000.0000 |     8000.0000 | 1000.0000 |     218.22 MB |
| RangeQueryAcrossSSTables | 50000      | 0.5          |       810.2 ms |       148.32 ms |      8.13 ms |     18000.0000 |     5000.0000 |         - |     148.41 MB |
| RealisticWorkload        | 50000      | 0.5          |       826.9 ms |       298.97 ms |     16.39 ms |     18000.0000 |     5000.0000 |         - |     152.35 MB |
| PostCompactionRead       | 50000      | 0.5          |     1,843.0 ms |       204.26 ms |     11.20 ms |     19000.0000 |     6000.0000 |         - |     154.93 MB |
| MemoryEfficiencyTest     | 50000      | 0.5          |       919.4 ms |       380.41 ms |     20.85 ms |     27000.0000 |    14000.0000 | 8000.0000 |     161.19 MB |
| **LayeredWrite**         | **50000**  | **0.8**      |   **807.2 ms** |   **185.81 ms** | **10.18 ms** | **18000.0000** | **5000.0000** |     **-** | **148.33 MB** |
| CrossSSTableRead         | 50000      | 0.8          |       913.9 ms |       536.72 ms |     29.42 ms |     31000.0000 |     9000.0000 | 1000.0000 |     242.95 MB |
| RangeQueryAcrossSSTables | 50000      | 0.8          |             NA |              NA |           NA |             NA |            NA |        NA |            NA |
| RealisticWorkload        | 50000      | 0.8          |       813.6 ms |       172.22 ms |      9.44 ms |     18000.0000 |     5000.0000 |         - |     151.29 MB |
| PostCompactionRead       | 50000      | 0.8          |     1,820.5 ms |       134.50 ms |      7.37 ms |     18000.0000 |     6000.0000 |         - |     154.24 MB |
| MemoryEfficiencyTest     | 50000      | 0.8          |       926.6 ms |       176.28 ms |      9.66 ms |     27000.0000 |    14000.0000 | 8000.0000 |     160.38 MB |
| **LayeredWrite**         | **100000** | **0.2**      | **1,367.9 ms** | **1,062.74 ms** | **58.25 ms** | **37000.0000** | **7000.0000** |     **-** |  **305.8 MB** |
| CrossSSTableRead         | 100000     | 0.2          |     1,507.9 ms |     1,017.48 ms |     55.77 ms |     48000.0000 |    13000.0000 | 1000.0000 |     384.79 MB |
| RangeQueryAcrossSSTables | 100000     | 0.2          |     1,399.7 ms |     1,127.70 ms |     61.81 ms |     37000.0000 |     7000.0000 |         - |     305.81 MB |
| RealisticWorkload        | 100000     | 0.2          |     1,376.1 ms |       539.86 ms |     29.59 ms |     38000.0000 |     8000.0000 |         - |     312.63 MB |
| PostCompactionRead       | 100000     | 0.2          |     2,366.9 ms |       259.19 ms |     14.21 ms |     38000.0000 |    10000.0000 |         - |      314.8 MB |
| MemoryEfficiencyTest     | 100000     | 0.2          |     1,636.6 ms |     1,235.96 ms |     67.75 ms |     46000.0000 |    19000.0000 | 9000.0000 |     322.41 MB |
| **LayeredWrite**         | **100000** | **0.5**      | **1,340.5 ms** |   **334.31 ms** | **18.32 ms** | **37000.0000** | **8000.0000** |     **-** | **307.12 MB** |
| CrossSSTableRead         | 100000     | 0.5          |     1,578.9 ms |     1,195.74 ms |     65.54 ms |     54000.0000 |    13000.0000 | 1000.0000 |     434.59 MB |
| RangeQueryAcrossSSTables | 100000     | 0.5          |     1,355.8 ms |       250.50 ms |     13.73 ms |     37000.0000 |     8000.0000 |         - |     306.53 MB |
| RealisticWorkload        | 100000     | 0.5          |     1,325.0 ms |        51.16 ms |      2.80 ms |     39000.0000 |     9000.0000 | 1000.0000 |     313.33 MB |
| PostCompactionRead       | 100000     | 0.5          |     2,402.1 ms |       768.53 ms |     42.13 ms |     38000.0000 |     9000.0000 |         - |     313.43 MB |
| MemoryEfficiencyTest     | 100000     | 0.5          |     1,545.9 ms |     1,471.10 ms |     80.64 ms |     46000.0000 |    16000.0000 | 9000.0000 |     317.37 MB |
| **LayeredWrite**         | **100000** | **0.8**      | **1,342.0 ms** |   **532.25 ms** | **29.17 ms** | **37000.0000** | **7000.0000** |     **-** | **305.19 MB** |
| CrossSSTableRead         | 100000     | 0.8          |     1,781.7 ms |     2,172.96 ms |    119.11 ms |     61000.0000 |    17000.0000 | 1000.0000 |     486.62 MB |
| RangeQueryAcrossSSTables | 100000     | 0.8          |     1,415.4 ms |     1,861.78 ms |    102.05 ms |     38000.0000 |    10000.0000 | 1000.0000 |     303.57 MB |
| RealisticWorkload        | 100000     | 0.8          |     1,500.5 ms |       349.34 ms |     19.15 ms |     38000.0000 |     8000.0000 |         - |     311.98 MB |
| PostCompactionRead       | 100000     | 0.8          |     2,409.6 ms |       368.53 ms |     20.20 ms |     39000.0000 |    11000.0000 | 1000.0000 |     312.06 MB |
| MemoryEfficiencyTest     | 100000     | 0.8          |             NA |              NA |           NA |             NA |            NA |        NA |            NA |

Benchmarks with issues:
  AdvancedBenchmarks.RangeQueryAcrossSSTables: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=10000, HotDataRatio=0.2]
  AdvancedBenchmarks.MemoryEfficiencyTest: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=10000, HotDataRatio=0.2]
  AdvancedBenchmarks.RangeQueryAcrossSSTables: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=50000, HotDataRatio=0.8]
  AdvancedBenchmarks.MemoryEfficiencyTest: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=100000, HotDataRatio=0.8]
