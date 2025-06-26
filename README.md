# Ocis [![Build and Test](https://github.com/muqiuhan/Ocis/actions/workflows/build-test.yaml/badge.svg)](https://github.com/muqiuhan/Ocis/actions/workflows/build-test.yaml)

> A cross-platform, robust asynchronous WiscKey storage engine.

Ocis is a key-value storage engine implemented based on the [WiscKey](https://www.usenix.org/system/files/conference/fast16/fast16-papers-lu.pdf) paper, written in F#. WiscKey's core idea is key-value separation, storing keys in an LSM-Tree based index structure, while storing the actual values in a separate, append-only Value Log. This design aims to optimize SSD performance by enabling sequential writes and reducing write amplification, thereby achieving low latency and high throughput.

## Design

*   **Key-Value Separation**: Unlike traditional LSM-Trees that store keys and values together, Ocis only stores `(key, value location in Value Log)` in the Memtable (in-memory mutable structure) and SSTable (on-disk immutable file), writing the actual values to an append-only **Value Log**. This significantly reduces the size of the LSM-Tree and I/O amplification, making it particularly suitable for SSDs.
*   **LSM-Tree Structure**: The project implements Memtable (in-memory mutable structure), SSTable (on-disk immutable file), and Write-Ahead Log (WAL). Through a background Compaction process, SSTables are optimized and garbage collected.
*   **Durability and Recovery**: **WAL (Write-Ahead Log)** ensures the durability of Memtable updates, supports crash recovery, and guarantees data consistency.

## F#

Ocis leverages the features of the F# language to build a robust and efficient storage engine:

*   **Functional and Immutability**: Core data structures like SSTable utilize F#'s immutability, simplifying concurrency and state management.
*   **Type Safety**: Extensive use of F#'s `Result` and `Option` types for error handling and representing potentially missing values enhances code robustness and readability.
*   **Concurrency Model**: Utilizes F#'s `MailboxProcessor` (Agent) to implement independent background tasks, such as Memtable flushing to SSTable, and future planned Compaction and Value Log garbage collection, effectively managing concurrent operations without blocking the main thread.
*   **High-Performance I/O**: Efficient byte array operations and file I/O are achieved by directly manipulating `FileStream` and `BinaryWriter/Reader`, combined with optimizations like `System.Memory.Span<T>`.

## Performance Benchmarks

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

**Note**: These figures represent memory allocated per *operation* during the benchmark run, not the total private memory size of the process. For persistent storage engines, actual memory consumption may vary depending on data volume and internal caching mechanisms.

## Native AOT Compilation

Ocis, through .NET 9.0's Native AOT (Ahead-of-Time) compilation, can be published as a self-contained executable without .NET runtime dependencies. This significantly improves application startup speed and runtime performance, and reduces deployment package size, further advancing the goal of "high-quality, high-performance code."

## Build, Run and Test

### Publishing a Self-Contained AOT Executable

To build and publish the Native AOT version of Ocis, run the following command in the project root directory:

```bash
dotnet publish Ocis/Ocis.fsproj -c Release -r <RuntimeIdentifier> --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true -p:PublishAot=true -p:EnableTrimAnalyzer=true
```

Replace `<RuntimeIdentifier>` with your target platform, for example:

* `win-x64` (Windows 64-bit)
* `linux-x64` (Linux 64-bit)
* `osx-x64` (macOS 64-bit)

### Run tests

To run the performance and unit tests included with the project, run in the project root:

```bash
dotnet test -c Release

# benchmark
dotnet run --project Ocis.Tests
```
---

## [LICENSE](./LICENSE)

```
Copyright (c) 2025 Somhairle H. Marisol

All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright notice,
      this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright notice,
      this list of conditions and the following disclaimer in the documentation
      and/or other materials provided with the distribution.
    * Neither the name of Ocis nor the names of its contributors
      may be used to endorse or promote products derived from this software
      without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```