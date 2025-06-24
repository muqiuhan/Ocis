module Ocis.Utils.ByteArrayComparer

open System.Collections.Generic

/// <summary>
/// Custom byte array comparer.
/// F# Map requires a reliable comparer to correctly sort and find byte array keys,
/// because the default comparison is based on reference rather than content.
/// </summary>
type ByteArrayComparer() =
    interface IComparer<byte array> with
        /// <summary>
        /// Compare two byte arrays.
        /// </summary>
        /// <param name="lhs">Left byte array.</param>
        /// <param name="rhs">Right byte array.</param>
        /// <returns>
        /// If lhs is less than rhs, return a negative number;
        /// If lhs is greater than rhs, return a positive number;
        /// If both are equal, return 0.
        /// </returns>
        member _.Compare(lhs: byte array, rhs: byte array) =
            // Compare length first, return result if different
            let lenCmp = lhs.Length.CompareTo(rhs.Length)

            if lenCmp <> 0 then
                lenCmp
            else
                // If length is the same, compare byte by byte
                // Use tail recursion to replace the loop with break
                let rec compareBytes (idx: int) =
                    if idx = lhs.Length then
                        0 // All bytes are the same, arrays are equal
                    else
                        let byteCmp = lhs.[idx].CompareTo(rhs.[idx])

                        if byteCmp <> 0 then
                            byteCmp // Found different byte, return comparison result
                        else
                            compareBytes (idx + 1) // Continue to compare next byte

                compareBytes 0 // Start from the first byte


    member inline this.Compare(lhs: byte array, rhs: byte array) =
        (this :> IComparer<byte array>).Compare(lhs, rhs)


module ByteArrayComparer =
    let ComparerInstance = new ByteArrayComparer()
