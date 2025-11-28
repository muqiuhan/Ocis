# Ocis 

[![Build and Test](https://github.com/muqiuhan/Ocis/actions/workflows/build-test.yaml/badge.svg)](https://github.com/muqiuhan/Ocis/actions/workflows/build-test.yaml) 
[![Qodana](https://github.com/muqiuhan/Ocis/actions/workflows/qodana_code_quality.yml/badge.svg)](https://github.com/muqiuhan/Ocis/actions/workflows/qodana_code_quality.yml)

> A cross-platform, robust asynchronous WiscKey storage engine and server.

Ocis is a key-value storage engine implemented based on the [WiscKey](https://www.usenix.org/system/files/conference/fast16/fast16-papers-lu.pdf) paper, written in F#. WiscKey's core idea is key-value separation, storing keys in an LSM-Tree based index structure, while storing the actual values in a separate, append-only Value Log. This design aims to optimize SSD performance by enabling sequential writes and reducing write amplification, thereby achieving low latency and high throughput.

The project includes both an embedded storage engine library (`Ocis`) and a high-performance TCP server (`Ocis.Server`) that provides network access through a custom binary protocol, making it suitable for distributed applications and microservices architectures.

### Project Structure

- **`Ocis/`** - Core WiscKey storage engine library
  - Memtable, SSTable, ValueLog, and WAL implementations
  - Compaction and garbage collection algorithms
  - Embedded database API
- **`Ocis.Server/`** - High-performance TCP server
  - Binary network protocol implementation
  - Connection management and async I/O
  - Server configuration and lifecycle management
  - **`Ocis.Protocol/`** - Ocis.Server SDK with multi-language support
    - Use Fable to provide client support for Ocis.Server in JS/TS/Rust/Python/Dart
- **`Ocis.Tests/`** - Performance benchmarks and core engine tests
- **`Ocis.Server.Tests/`** - Server integration and protocol tests

## Design

*   **Key-Value Separation**: Unlike traditional LSM-Trees that store keys and values together, Ocis only stores `(key, value location in Value Log)` in the Memtable (in-memory mutable structure) and SSTable (on-disk immutable file), writing the actual values to an append-only **Value Log**. This significantly reduces the size of the LSM-Tree and I/O amplification, making it particularly suitable for SSDs.
*   **LSM-Tree Structure**: The project implements Memtable (in-memory mutable structure), SSTable (on-disk immutable file), and Write-Ahead Log (WAL). Through a background Compaction process, SSTables are optimized and garbage collected.
*   **Durability and Recovery**: **WAL (Write-Ahead Log)** ensures the durability of Memtable updates, supports crash recovery, and guarantees data consistency.
*   **Network Server**: Ocis includes a high-performance TCP server implementation that provides network access to the WiscKey storage engine through a custom binary protocol, supporting multiple client connections and CRUD operations.

## Performance Benchmarks

BenchmarkDotNet v0.15.2, Linux Pop!_OS 22.04 LTS
AMD Ryzen 7 6800H with Radeon Graphics 4.79GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100-preview.7.25380.108
  [Host]   : .NET 10.0.0 (10.0.25.38108), X64 AOT AVX2 DEBUG
  ShortRun : .NET 10.0.0 (10.0.25.38108), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 | Method      | Count      |           Mean |         Error |        StdDev |          Gen0 |      Gen1 |       Allocated |
 | ----------- | ---------- | -------------: | ------------: | ------------: | ------------: | --------: | --------------: |
 | **BulkSet** | **1000**   |   **5.256 ms** |  **1.828 ms** | **0.1002 ms** |         **-** |     **-** |   **166.33 KB** |
 | BulkGet     | 1000       |       6.607 ms |      7.182 ms |     0.3936 ms |             - |         - |       471.02 KB |
 | **BulkSet** | **10000**  |  **66.772 ms** | **62.016 ms** | **3.3993 ms** |         **-** |     **-** |  **1432.04 KB** |
 | BulkGet     | 10000      |      79.582 ms |      9.686 ms |     0.5309 ms |             - |         - |      4478.91 KB |
 | **BulkSet** | **100000** | **254.601 ms** | **96.484 ms** | **5.2886 ms** | **1000.0000** |     **-** | **14089.41 KB** |
 | BulkGet     | 100000     |     275.424 ms |     42.625 ms |     2.3364 ms |     5000.0000 | 2000.0000 |      44557.1 KB |


**Note**: These figures represent memory allocated per *operation* during the benchmark run, not the total private memory size of the process. For persistent storage engines, actual memory consumption may vary depending on data volume and internal caching mechanisms.

## Network Server and Protocol

Ocis provides a high-performance TCP server (`Ocis.Server`) that exposes the WiscKey storage engine over the network using a custom binary protocol. The server supports multiple client connections and provides full CRUD operations.

### Server Features

- **High Performance**: Built on .NET's high-performance networking stack with optimized async I/O
- **Multiple Connections**: Supports up to 1000 client connections by default
- **Binary Protocol**: Efficient custom binary protocol with minimal overhead
- **Production Ready**: Includes comprehensive error handling, logging, and graceful shutdown
- **Native AOT Compatible**: Can be compiled to a self-contained native executable

### Starting the Server

```bash
# Basic usage with default settings (port 7379)
dotnet run --project Ocis.Server ./data

# Custom configuration
dotnet run --project Ocis.Server ./data \
  --host 0.0.0.0 \
  --port 7379 \
  --max-connections 1000 \
  --flush-threshold 1000 \
  --log-level Info
```

### Binary Protocol Specification

The Ocis network protocol is a lightweight binary protocol designed for high performance and minimal overhead.

#### Protocol Header

All packets begin with an 18-byte fixed header:

```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                        Magic Number (0x5349434F)              |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|    Version    |   Cmd/Status  |         Total Length          |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                         Key Length                            |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                    Value/Error Length                        |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                          Payload                             |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

#### Field Descriptions

- **Magic Number**: 4 bytes, always `0x5349434F` ("OCIS" in ASCII)
- **Version**: 1 byte, protocol version (currently `0x01`)
- **Command/Status**: 1 byte, command type for requests or status code for responses
- **Total Length**: 4 bytes, total packet length including header
- **Key Length**: 4 bytes, length of the key in payload
- **Value/Error Length**: 4 bytes, length of value (responses) or error message
- **Payload**: Variable length, contains key and value data

#### Command Types

| Command | Value | Description           |
| ------- | ----- | --------------------- |
| SET     | 0x01  | Store key-value pair  |
| GET     | 0x02  | Retrieve value by key |
| DELETE  | 0x03  | Delete key-value pair |

#### Status Codes

| Status    | Value | Description                      |
| --------- | ----- | -------------------------------- |
| SUCCESS   | 0x00  | Operation completed successfully |
| NOT_FOUND | 0x01  | Key not found (GET operations)   |
| ERROR     | 0x02  | Operation failed with error      |

#### Request Format

**SET Request:**
```
Header (18 bytes) + Key (variable) + Value (variable)
```

**GET Request:**
```
Header (18 bytes) + Key (variable)
```

**DELETE Request:**
```
Header (18 bytes) + Key (variable)
```

#### Response Format

**Success Response (with value):**
```
Header (18 bytes) + Value (variable)
```

**Success Response (no value):**
```
Header (18 bytes)
```

**Error Response:**
```
Header (18 bytes) + Error Message (variable)
```

### Client Implementation Example

Here's a simple client implementation example:

```fsharp
// Connect to server
let client = new TcpClient()
client.Connect("127.0.0.1", 7379)
let stream = client.GetStream()

// SET operation
let key = Encoding.UTF8.GetBytes("my_key")
let value = Encoding.UTF8.GetBytes("my_value")
let setRequest = createSetRequest(key, value)
stream.Write(setRequest, 0, setRequest.Length)

// GET operation  
let getRequest = createGetRequest(key)
stream.Write(getRequest, 0, getRequest.Length)
let response = readResponse(stream)
```

### Configuration Options

| Option                      | Default | Description                             |
| --------------------------- | ------- | --------------------------------------- |
| `--host`                    | 0.0.0.0 | Server bind address                     |
| `--port`                    | 7379    | Server port                             |
| `--max-connections`         | 1000    | Maximum number of connections           |
| `--flush-threshold`         | 1000    | Memtable flush threshold                |
| `--l0-compaction-threshold` | 4       | L0 SSTable compaction threshold         |
| `--level-size-multiplier`   | 5       | LSM level size multiplier               |
| `--log-level`               | Info    | Log level (Debug/Info/Warn/Error/Fatal) |

## Native AOT Compilation

Ocis, through .NET 9.0's Native AOT (Ahead-of-Time) compilation, can be published as a self-contained executable without .NET runtime dependencies. This significantly improves application startup speed and runtime performance, and reduces deployment package size, further advancing the goal of "high-quality, high-performance code."

## Build, Run and Test

### Publishing a Self-Contained AOT Executable

To build and publish the Native AOT version of Ocis, run the following command in the project root directory:

```bash
dotnet publish Ocis/Ocis.fsproj -c Release -r <RuntimeIdentifier>
```

Replace `<RuntimeIdentifier>` with your target platform, for example:

* `win-x64` (Windows 64-bit)
* `linux-x64` (Linux 64-bit)
* `osx-x64` (macOS 64-bit)

### Run tests

To run the performance and unit tests included with the project, run in the project root:

```bash
# Run all tests (core engine and server)
dotnet test -c Release

# Run server-specific tests (requires no running server)
dotnet test Ocis.Server.Tests -c Release

# Run performance benchmarks
dotnet run --project Ocis.Tests -- simple/base/advance
```

#### Server Integration Tests

The server tests include:

- **Protocol Tests**: Binary protocol serialization/deserialization
- **Client Tests**: End-to-end client-server communication (requires running server)
- **Integration Tests**: Configuration validation and server lifecycle

To run client tests that require a running server:

```bash
# Terminal 1: Start the server
dotnet run --project Ocis.Server ./test_data

# Terminal 2: Run client tests
dotnet test Ocis.Server.Tests --filter "TestCategory=ClientTests"
```

**Note**: Some integration tests require a running Ocis server instance and will be marked as "Inconclusive" if no server is available.
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
