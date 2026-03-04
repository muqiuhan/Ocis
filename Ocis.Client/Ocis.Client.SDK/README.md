# Ocis.Client.SDK

Fable-based cross-platform SDK for Ocis Server. This SDK provides request construction and response parsing functionality that can be compiled to multiple target languages.

## Features

- **Type-safe request construction**: Create protocol packets with type safety
- **Response parsing**: Parse server responses into typed results
- **Multi-language support**: Generate SDKs for TypeScript, Python, and other languages

## Installation

```bash
dotnet add package Ocis.Client.SDK
```

Or reference the project directly:

```xml
<ProjectReference Include="Ocis.Client/Ocis.Client.SDK/Ocis.Client.SDK.fsproj" />
```

## Usage

### F# / .NET

```fsharp
open Ocis.Client.SDK.Request
open Ocis.Client.SDK.Response

// Create a SET request
let requestBytes = Request.createSetRequest "my-key" (System.Text.Encoding.UTF8.GetBytes "my-value")

// Create a GET request
let getRequest = Request.createGetRequest "my-key"

// Parse a response
let response = Response.parseResponse responseBytes
match response with
| Response.ParseSuccess pkt ->
    match Response.toClientResultValue response with
    | Response.Success value ->
        printfn "Got value: %A" value
    | Response.NotFound ->
        printfn "Key not found"
    | Response.Error msg ->
        printfn "Error: %s" msg
| _ ->
    printfn "Failed to parse response"
```

### TypeScript

Generate TypeScript SDK:

```bash
cd Ocis.Client/Ocis.Client.SDK
fable . --lang typescript -o ../dist/ts --extension .ts
```

Then in your TypeScript project:

```typescript
import { Request, Response } from 'ocis-client-sdk'

// Create a SET request
const requestBytes = Request.createSetRequest("my-key", new TextEncoder().encode("my-value"))

// Create a GET request
const getRequest = Request.createGetRequest("my-key")

// Parse a response (user must provide their own transport)
// The SDK only provides request construction and response parsing
const response = Response.parseResponse(responseBytes)
```

### Python

Generate Python SDK:

```bash
cd Ocis.Client/Ocis.Client.SDK
fable . --lang python -o ../dist/py
```

Then in your Python project:

```python
from ocis_client_sdk import Request, Response

# Create a SET request
request_bytes = Request.create_set_request("my-key", b"my-value")

# Create a GET request
get_request = Request.create_get_request("my-key")

# Parse a response
response = Response.parse_response(response_bytes)
```

## Architecture

The SDK is designed to be transport-agnostic. It provides:

1. **Request construction**: Functions to build protocol packets
2. **Response parsing**: Functions to parse raw bytes into typed responses

Users are responsible for implementing their own transport layer (TCP, HTTP, WebSocket, etc.)

## Multi-language Generation

This project uses Fable to generate SDKs for multiple languages:

| Language | Command | Output Directory |
|----------|---------|-----------------|
| TypeScript | `fable . --lang typescript -o ../dist/ts --extension .ts` | `dist/ts/` |
| Python | `fable . --lang python -o ../dist/py` | `dist/py/` |
| JavaScript | `fable . --lang javascript -o ../dist/js` | `dist/js/` |

### Requirements

- .NET 8.0 or higher
- Fable CLI: `dotnet tool install -g fable`

## Project Structure

```
Ocis.Client/
├── Ocis.Client.SDK/
│   ├── Ocis.Client.SDK.fsproj    # Project file
│   ├── ProtocolSpec.fs           # Protocol type definitions
│   ├── Binary.fs                 # Cross-platform binary operations
│   ├── Protocol.fs               # Serialization/deserialization
│   ├── Request.fs                # Request construction
│   └── Response.fs               # Response parsing
└── dist/                          # Generated SDKs (create manually)
    ├── ts/                        # TypeScript SDK
    ├── py/                        # Python SDK
    └── js/                        # JavaScript SDK
```

## Binary Protocol

The SDK uses a binary protocol with the following structure:

### Request Packet (18 bytes header + payload)
- Magic Number: 4 bytes (uint32, little-endian)
- Version: 1 byte
- Command Type: 1 byte
- Total Packet Length: 4 bytes (int32, little-endian)
- Key Length: 4 bytes (int32, little-endian)
- Value Length: 4 bytes (int32, little-endian)
- Key: variable length
- Value: variable length (optional)

### Response Packet (18 bytes header + payload)
- Magic Number: 4 bytes (uint32, little-endian)
- Version: 1 byte
- Status Code: 1 byte
- Total Packet Length: 4 bytes (int32, little-endian)
- Value Length: 4 bytes (int32, little-endian)
- Error Message Length: 4 bytes (int32, little-endian)
- Value: variable length (optional)
- Error Message: variable length (optional)

## License

MIT