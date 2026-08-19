using System;
using RingBuffer.Core;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>Task 2.7. Exhaustive over a small capacity: a message must never straddle the end.</summary>
public sealed class WrapPlanTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(1024)]
    public void Exhaustive_over_every_tail_and_length(int capacity)
    {
        var wrapped = 0;
        var inPlace = 0;

        // Tails are always aligned, because the tail only ever advances by aligned
        // record lengths. Testing unaligned tails would test a state the ring
        // cannot reach.
        for (var tailIndex = 0; tailIndex < capacity; tailIndex += RecordDescriptor.Alignment)
        {
            for (var recordLength = RecordDescriptor.Alignment;
                 recordLength <= capacity;
                 recordLength += RecordDescriptor.Alignment)
            {
                var plan = WrapPlan.For(tailIndex, recordLength, capacity);

                if (plan.Wraps)
                {
                    wrapped++;

                    // Restarts at zero, and the padding fills exactly the tail of
                    // the buffer -- no gap, no overlap.
                    Assert.Equal(0, plan.WriteIndex);
                    Assert.Equal(capacity - tailIndex, plan.PaddingLength);

                    // A padding record must hold its own header.
                    Assert.True(plan.PaddingLength >= RecordDescriptor.HeaderLength);

                    // It only wraps when it genuinely had to.
                    Assert.True(recordLength > capacity - tailIndex);
                }
                else
                {
                    inPlace++;
                    Assert.Equal(tailIndex, plan.WriteIndex);
                    Assert.Equal(0, plan.PaddingLength);

                    // The record fits entirely before the end. This is the
                    // contiguity invariant, stated directly.
                    Assert.True(plan.WriteIndex + recordLength <= capacity);
                }
            }
        }

        Assert.True(wrapped > 0 && inPlace > 0, "both branches must be exercised");
    }

    [Fact]
    public void A_record_exactly_filling_the_remainder_does_not_wrap()
    {
        // The off-by-one that would silently insert a pointless padding record,
        // or worse, overwrite the first record in the buffer.
        var plan = WrapPlan.For(tailIndex: 56, recordLength: 8, capacity: 64);
        Assert.False(plan.Wraps);
        Assert.Equal(56, plan.WriteIndex);
    }

    [Fact]
    public void One_byte_too_long_wraps()
    {
        var plan = WrapPlan.For(tailIndex: 56, recordLength: 16, capacity: 64);
        Assert.True(plan.Wraps);
        Assert.Equal(8, plan.PaddingLength);
        Assert.Equal(0, plan.WriteIndex);
    }

    [Fact]
    public void Rejects_a_non_power_of_two_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WrapPlan.For(0, 8, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => WrapPlan.For(0, 8, 0));
    }

    [Fact]
    public void Rejects_a_record_larger_than_the_buffer()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WrapPlan.For(0, 128, 64));
    }
}
