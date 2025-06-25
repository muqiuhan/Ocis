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

Ocis has undergone comprehensive performance testing, demonstrating excellent read and write performance under various workloads. The following performance data was obtained under specific machine configurations (see "Test Environment" below):

| Test Scenario                     | Time for 1000 entries (ms) | Time for 10000 entries (ms) | Time for 50000 entries (ms) |
| :-------------------------------- | :------------------------- | :-------------------------- | :-------------------------- |
| Bulk Set                          | 11.29                      | 113.02                      | 494.67                      |
| Bulk Get                          | 16.57                      | 330.36                      | 1999.72                     |
| Mixed Workload (50% Set, 50% Get) | 8.93                       | 92.63                       | 703.70                      |

**Memory Usage (Private Memory Size)**:

*   Initial Private Memory: ~197.48 MB
*   After inserting 1000 entries: ~186.57 MB (decreased by ~10.91 MB)
*   After inserting 10000 entries: ~183.97 MB (decreased by ~2.60 MB)
*   After inserting 50000 entries: ~189.19 MB (increased by ~5.22 MB)

**Note**: Memory usage shows minor fluctuations after inserting a large amount of data, indicating that the Memtable flush and SSTable write mechanisms effectively control memory peaks. A slight increase in memory with growing data volume is normal, as it involves loading SSTable indexes and other metadata.

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
```

### Test environment

This project was tested and verified in the following environment:

* **Operating system**: Pop!\_OS 22.04 LTS x86\_64 (based on Linux Kernel 6.12.10-76061203-generic)
* **CPU**: AMD Ryzen 7 6800H (16 threads @ 4.7 GHz)
* **Memory**: 14.32 GiB (2xDDR4)

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
