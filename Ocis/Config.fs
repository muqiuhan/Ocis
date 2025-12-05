module Ocis.Config

/// <summary>
/// Centralized size limits for length-prefixed entries to prevent
/// pathological allocations on corrupted inputs.
/// </summary>
module Limits =
    /// Maximum allowed size (bytes) for any length-prefixed payload
    /// read from on-disk structures (keys/values/byte arrays).
    /// Adjust cautiously; oversized values risk memory exhaustion.
    let MaxEntrySizeBytes = 64 * 1024 * 1024 // 64MB upper bound
