
BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), X64 AOT AVX2 DEBUG
  ShortRun : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method                   | DataSize   | HotDataRatio |           Mean |         Error |       StdDev |            Gen0 |           Gen1 |          Gen2 |     Allocated |
 | ------------------------ | ---------- | ------------ | -------------: | ------------: | -----------: | --------------: | -------------: | ------------: | ------------: |
 | **LayeredWrite**         | **10000**  | **0.2**      |   **359.8 ms** | **297.27 ms** | **16.29 ms** |   **8000.0000** |  **2000.0000** |         **-** |  **70.63 MB** |
 | CrossSSTableRead         | 10000      | 0.2          |       371.9 ms |      55.45 ms |      3.04 ms |       9000.0000 |      3000.0000 |             - |      76.95 MB |
 | RangeQueryAcrossSSTables | 10000      | 0.2          |       352.7 ms |      14.76 ms |      0.81 ms |       8000.0000 |      1000.0000 |             - |      69.83 MB |
 | RealisticWorkload        | 10000      | 0.2          |             NA |            NA |           NA |              NA |             NA |            NA |            NA |
 | PostCompactionRead       | 10000      | 0.2          |     1,358.3 ms |      24.30 ms |      1.33 ms |       9000.0000 |      5000.0000 |             - |      75.17 MB |
 | MemoryEfficiencyTest     | 10000      | 0.2          |       376.9 ms |      53.88 ms |      2.95 ms |      16000.0000 |     11000.0000 |     8000.0000 |      74.84 MB |
 | **LayeredWrite**         | **10000**  | **0.5**      |   **344.5 ms** |  **19.11 ms** |  **1.05 ms** |   **7000.0000** |  **4000.0000** |         **-** |  **63.49 MB** |
 | CrossSSTableRead         | 10000      | 0.5          |       355.7 ms |      35.26 ms |      1.93 ms |       9000.0000 |      5000.0000 |             - |      72.18 MB |
 | RangeQueryAcrossSSTables | 10000      | 0.5          |       344.0 ms |      25.86 ms |      1.42 ms |       7000.0000 |      4000.0000 |             - |      63.53 MB |
 | RealisticWorkload        | 10000      | 0.5          |       356.1 ms |      85.59 ms |      4.69 ms |       8000.0000 |      5000.0000 |             - |      65.63 MB |
 | PostCompactionRead       | 10000      | 0.5          |     1,357.0 ms |      27.11 ms |      1.49 ms |       8000.0000 |      5000.0000 |             - |      65.49 MB |
 | MemoryEfficiencyTest     | 10000      | 0.5          |       367.8 ms |      50.71 ms |      2.78 ms |      15000.0000 |     12000.0000 |     8000.0000 |       69.3 MB |
 | **LayeredWrite**         | **10000**  | **0.8**      |   **349.8 ms** |  **22.25 ms** |  **1.22 ms** |   **7000.0000** |  **3000.0000** |         **-** |  **58.53 MB** |
 | CrossSSTableRead         | 10000      | 0.8          |       361.1 ms |      64.15 ms |      3.52 ms |       8000.0000 |      4000.0000 |             - |      70.53 MB |
 | RangeQueryAcrossSSTables | 10000      | 0.8          |       352.4 ms |       6.21 ms |      0.34 ms |       7000.0000 |      3000.0000 |             - |      58.55 MB |
 | RealisticWorkload        | 10000      | 0.8          |       353.2 ms |      21.27 ms |      1.17 ms |       7000.0000 |      3000.0000 |             - |      60.68 MB |
 | PostCompactionRead       | 10000      | 0.8          |     1,355.9 ms |      21.48 ms |      1.18 ms |       7000.0000 |      3000.0000 |             - |      60.53 MB |
 | MemoryEfficiencyTest     | 10000      | 0.8          |       368.0 ms |      27.61 ms |      1.51 ms |      15000.0000 |     11000.0000 |     8000.0000 |      64.34 MB |
 | **LayeredWrite**         | **50000**  | **0.2**      |   **730.9 ms** | **420.77 ms** | **23.06 ms** |  **54000.0000** | **21000.0000** | **3000.0000** | **433.02 MB** |
 | CrossSSTableRead         | 50000      | 0.2          |       767.1 ms |     669.05 ms |     36.67 ms |      60000.0000 |     23000.0000 |     3000.0000 |     472.06 MB |
 | RangeQueryAcrossSSTables | 50000      | 0.2          |       691.7 ms |     276.32 ms |     15.15 ms |      55000.0000 |     20000.0000 |     2000.0000 |      444.9 MB |
 | RealisticWorkload        | 50000      | 0.2          |       700.4 ms |     226.27 ms |     12.40 ms |      55000.0000 |     21000.0000 |     3000.0000 |     439.93 MB |
 | PostCompactionRead       | 50000      | 0.2          |     1,598.5 ms |      78.50 ms |      4.30 ms |      57000.0000 |     24000.0000 |     4000.0000 |     446.96 MB |
 | MemoryEfficiencyTest     | 50000      | 0.2          |       829.6 ms |   1,051.54 ms |     57.64 ms |      63000.0000 |     31000.0000 |    10000.0000 |     454.44 MB |
 | **LayeredWrite**         | **50000**  | **0.5**      |   **721.0 ms** | **129.78 ms** |  **7.11 ms** |  **58000.0000** | **22000.0000** | **3000.0000** | **457.11 MB** |
 | CrossSSTableRead         | 50000      | 0.5          |       847.6 ms |   1,129.75 ms |     61.93 ms |      62000.0000 |     23000.0000 |     4000.0000 |     492.02 MB |
 | RangeQueryAcrossSSTables | 50000      | 0.5          |       723.0 ms |     384.58 ms |     21.08 ms |      58000.0000 |     23000.0000 |     4000.0000 |      450.9 MB |
 | RealisticWorkload        | 50000      | 0.5          |       729.3 ms |     210.85 ms |     11.56 ms |      55000.0000 |     20000.0000 |     2000.0000 |     441.29 MB |
 | PostCompactionRead       | 50000      | 0.5          |     1,621.5 ms |      55.76 ms |      3.06 ms |      57000.0000 |     24000.0000 |     3000.0000 |     450.45 MB |
 | MemoryEfficiencyTest     | 50000      | 0.5          |       828.1 ms |     254.19 ms |     13.93 ms |      65000.0000 |     31000.0000 |    11000.0000 |     457.25 MB |
 | **LayeredWrite**         | **50000**  | **0.8**      |   **722.7 ms** | **290.59 ms** | **15.93 ms** |  **58000.0000** | **24000.0000** | **3000.0000** | **468.05 MB** |
 | CrossSSTableRead         | 50000      | 0.8          |             NA |            NA |           NA |              NA |             NA |            NA |            NA |
 | RangeQueryAcrossSSTables | 50000      | 0.8          |       736.5 ms |     648.67 ms |     35.56 ms |      59000.0000 |     21000.0000 |     4000.0000 |     460.19 MB |
 | RealisticWorkload        | 50000      | 0.8          |       730.8 ms |     275.79 ms |     15.12 ms |      62000.0000 |     25000.0000 |     5000.0000 |     477.58 MB |
 | PostCompactionRead       | 50000      | 0.8          |     1,625.1 ms |     296.64 ms |     16.26 ms |      56000.0000 |     23000.0000 |     3000.0000 |     448.69 MB |
 | MemoryEfficiencyTest     | 50000      | 0.8          |       841.8 ms |     428.07 ms |     23.46 ms |      67000.0000 |     31000.0000 |    11000.0000 |     483.21 MB |
 | **LayeredWrite**         | **100000** | **0.2**      |         **NA** |        **NA** |       **NA** |          **NA** |         **NA** |        **NA** |        **NA** |
 | CrossSSTableRead         | 100000     | 0.2          |     1,338.9 ms |     314.99 ms |     17.27 ms |     102000.0000 |     41000.0000 |     4000.0000 |     820.28 MB |
 | RangeQueryAcrossSSTables | 100000     | 0.2          |             NA |            NA |           NA |              NA |             NA |            NA |            NA |
 | RealisticWorkload        | 100000     | 0.2          |     1,235.6 ms |     160.03 ms |      8.77 ms |      97000.0000 |     37000.0000 |     4000.0000 |     779.16 MB |
 | PostCompactionRead       | 100000     | 0.2          |     2,022.0 ms |   1,281.32 ms |     70.23 ms |     103000.0000 |     41000.0000 |     5000.0000 |     829.49 MB |
 | MemoryEfficiencyTest     | 100000     | 0.2          |     1,550.0 ms |     945.71 ms |     51.84 ms |     103000.0000 |     43000.0000 |    10000.0000 |     779.09 MB |
 | **LayeredWrite**         | **100000** | **0.5**      | **1,212.5 ms** | **474.64 ms** | **26.02 ms** |  **99000.0000** | **36000.0000** | **4000.0000** | **799.44 MB** |
 | CrossSSTableRead         | 100000     | 0.5          |     1,370.8 ms |     142.10 ms |      7.79 ms |     109000.0000 |     41000.0000 |     4000.0000 |     882.44 MB |
 | RangeQueryAcrossSSTables | 100000     | 0.5          |             NA |            NA |           NA |              NA |             NA |            NA |            NA |
 | RealisticWorkload        | 100000     | 0.5          |             NA |            NA |           NA |              NA |             NA |            NA |            NA |
 | PostCompactionRead       | 100000     | 0.5          |     1,985.6 ms |     563.21 ms |     30.87 ms |      89000.0000 |     36000.0000 |     3000.0000 |     726.93 MB |
 | MemoryEfficiencyTest     | 100000     | 0.5          |     1,485.7 ms |   5,142.63 ms |    281.88 ms |      92000.0000 |     40000.0000 |    11000.0000 |     689.25 MB |
 | **LayeredWrite**         | **100000** | **0.8**      | **1,274.8 ms** | **401.52 ms** | **22.01 ms** | **106000.0000** | **40000.0000** | **4000.0000** | **860.48 MB** |
 | CrossSSTableRead         | 100000     | 0.8          |     1,482.3 ms |   1,355.56 ms |     74.30 ms |     123000.0000 |     48000.0000 |     4000.0000 |     998.54 MB |
 | RangeQueryAcrossSSTables | 100000     | 0.8          |     1,298.9 ms |     151.39 ms |      8.30 ms |     112000.0000 |     42000.0000 |     3000.0000 |     916.32 MB |
 | RealisticWorkload        | 100000     | 0.8          |     1,314.2 ms |   1,294.66 ms |     70.96 ms |     107000.0000 |     40000.0000 |     3000.0000 |        878 MB |
 | PostCompactionRead       | 100000     | 0.8          |     2,041.4 ms |   1,474.29 ms |     80.81 ms |     113000.0000 |     44000.0000 |     7000.0000 |     904.44 MB |
 | MemoryEfficiencyTest     | 100000     | 0.8          |     1,594.2 ms |     867.27 ms |     47.54 ms |     113000.0000 |     44000.0000 |    11000.0000 |     865.11 MB |

Benchmarks with issues:
  AdvancedBenchmarks.RealisticWorkload: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=10000, HotDataRatio=0.2]
  AdvancedBenchmarks.CrossSSTableRead: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=50000, HotDataRatio=0.8]
  AdvancedBenchmarks.LayeredWrite: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=100000, HotDataRatio=0.2]
  AdvancedBenchmarks.RangeQueryAcrossSSTables: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=100000, HotDataRatio=0.2]
  AdvancedBenchmarks.RangeQueryAcrossSSTables: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=100000, HotDataRatio=0.5]
  AdvancedBenchmarks.RealisticWorkload: ShortRun(InvocationCount=1, IterationCount=3, LaunchCount=1, UnrollFactor=1, WarmupCount=3) [DataSize=100000, HotDataRatio=0.5]
