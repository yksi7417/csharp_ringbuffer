using System;

namespace RingBuffer.Core;

/// <summary>Alignment arithmetic. Total over its documented domain.</summary>
public static class Align
{
    /// <summary>
    /// Rounds <paramref name="value"/> up to the next multiple of
    /// <paramref name="alignment"/>, which must be a power of two.
    /// </summary>
    /// <remarks>
    /// Overflow is checked rather than wrapped. A silently negative length would
    /// be read as an uncommitted record and stall the consumer forever, which is
    /// a far worse failure than an exception at the call site.
    /// </remarks>
    public static int To(int value, int alignment)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "must not be negative");
        }

        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alignment), alignment, "must be a positive power of two");
        }

        if (value > int.MaxValue - (alignment - 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "aligning would overflow Int32");
        }

        return (value + (alignment - 1)) & ~(alignment - 1);
    }

    /// <summary>True when <paramref name="value"/> is a positive power of two.</summary>
    public static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}
