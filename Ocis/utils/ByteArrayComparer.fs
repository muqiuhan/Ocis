module Ocis.Utils.ByteArrayComparer

open System.Collections.Generic
open System

/// <summary>
/// Custom byte array comparer.
/// F# Map requires a reliable comparer to correctly sort and find byte array keys,
/// because the default comparison is based on reference rather than content.
/// </summary>
type ByteArrayComparer () =
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
        member _.Compare (lhs : byte array, rhs : byte array) =
            System.ReadOnlySpan<byte>(lhs).SequenceCompareTo (System.ReadOnlySpan<byte> (rhs))

    member inline this.Compare (lhs : byte array, rhs : byte array) = (this :> IComparer<byte array>).Compare (lhs, rhs)

    static member ComparerInstance = new ByteArrayComparer ()
