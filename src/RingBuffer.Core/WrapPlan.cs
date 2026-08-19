using System;

namespace RingBuffer.Core;

/// <summary>
/// Whether a record fits before the end of the data region, or must be preceded by
/// a padding record and restarted at index zero.
///
/// A message must be contiguous: a decoder cannot straddle the end of the buffer.
/// Pure arithmetic, for the same reason as <see cref="PaddingPlan"/>.
/// </summary>
public readonly record struct WrapPlan(bool Wraps, int PaddingLength, int WriteIndex)
{
    /// <summary>
    /// Plans placement of a record of <paramref name="recordLength"/> bytes when the
    /// tail sits at <paramref name="tailIndex"/> within a buffer of
    /// <paramref name="capacity"/> bytes.
    /// </summary>
    public static WrapPlan For(int tailIndex, int recordLength, int capacity)
    {
        if (!Align.IsPowerOfTwo(capacity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity), capacity, "capacity must be a positive power of two");
        }

        if (tailIndex < 0 || tailIndex >= capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tailIndex), tailIndex, $"must be within [0, {capacity})");
        }

        if (recordLength <= 0 || recordLength > capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordLength), recordLength, $"must be within (0, {capacity}]");
        }

        var toEndOfBuffer = capacity - tailIndex;
        if (recordLength <= toEndOfBuffer)
        {
            return new WrapPlan(Wraps: false, PaddingLength: 0, WriteIndex: tailIndex);
        }

        // The padding fills the rest of the buffer. It is always at least a header
        // long for the same reason as in PaddingPlan: tailIndex and capacity are
        // both multiples of Alignment, and Alignment >= HeaderLength.
        return new WrapPlan(Wraps: true, PaddingLength: toEndOfBuffer, WriteIndex: 0);
    }
}
