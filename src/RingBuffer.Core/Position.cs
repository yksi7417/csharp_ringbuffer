namespace RingBuffer.Core;

/// <summary>
/// Head and tail are monotonically increasing 64-bit <em>positions</em>, not indices.
/// Keeping them monotonic is what makes <c>tail - head</c> an unambiguous measure of
/// occupancy: if they wrapped, a full buffer and an empty one would look identical.
/// </summary>
public static class Position
{
    /// <summary>Index into the data region for a position. Capacity must be a power of two.</summary>
    public static int IndexOf(long position, int capacity) => (int)(position & (capacity - 1));

    /// <summary>Bytes currently occupied.</summary>
    /// <remarks>
    /// Correct even when the positions have run past <see cref="long.MaxValue"/> and
    /// wrapped to negative: two's-complement subtraction yields the true difference
    /// so long as that difference is itself less than <see cref="long.MaxValue"/>,
    /// which it always is, being bounded by capacity.
    /// </remarks>
    public static long Occupancy(long head, long tail) => tail - head;

    /// <summary>Bytes available for a new claim.</summary>
    public static long Available(long head, long tail, int capacity) => capacity - (tail - head);
}
