using RingBuffer.Core;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>Tasks 2.8 and 2.9. Includes the arithmetic at the 64-bit boundary.</summary>
public sealed class PositionTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(1024)]
    public void Index_wraps_within_capacity(int capacity)
    {
        for (long position = 0; position < capacity * 3L; position += RecordDescriptor.Alignment)
        {
            var index = Position.IndexOf(position, capacity);
            Assert.InRange(index, 0, capacity - 1);
            Assert.Equal(position % capacity, index);
        }
    }

    [Fact]
    public void Available_is_capacity_minus_occupancy()
    {
        const int capacity = 1024;
        for (long head = 0; head < 4096; head += 8)
        {
            for (long occupied = 0; occupied <= capacity; occupied += 8)
            {
                var tail = head + occupied;
                Assert.Equal(occupied, Position.Occupancy(head, tail));
                Assert.Equal(capacity - occupied, Position.Available(head, tail, capacity));
            }
        }
    }

    /// <summary>
    /// Task 2.9. Unreachable in practice — at a billion messages a second this is
    /// centuries away — and trivially reachable in a test by seeding the positions
    /// directly. Untested arithmetic that "cannot happen" is where these buffers
    /// actually break.
    /// </summary>
    [Fact]
    public void Arithmetic_survives_the_signed_64_bit_boundary()
    {
        const int capacity = 1024;

        // Straddle the wrap: head just below long.MaxValue, tail just past it.
        var head = long.MaxValue - 512;
        var tail = head + 256;
        Assert.Equal(256, Position.Occupancy(head, tail));
        Assert.Equal(capacity - 256, Position.Available(head, tail, capacity));

        // Tail has wrapped to negative; the difference is still correct, because
        // two's-complement subtraction yields the true gap whenever that gap is
        // smaller than long.MaxValue -- and it is always bounded by capacity.
        tail = head + 1024;
        Assert.True(tail < 0, "the test must actually cross the boundary");
        Assert.Equal(1024, Position.Occupancy(head, tail));
        Assert.Equal(0, Position.Available(head, tail, capacity));

        // Both wrapped.
        head = long.MinValue + 16;
        tail = head + 64;
        Assert.Equal(64, Position.Occupancy(head, tail));

        // Indexing stays in range across the boundary. long.MinValue & (cap-1) is
        // still correct because capacity is a power of two and the mask ignores
        // the sign bit.
        for (var i = 0; i < 64; i++)
        {
            var position = long.MaxValue - 32 + i;
            Assert.InRange(Position.IndexOf(position, capacity), 0, capacity - 1);
        }
    }
}
