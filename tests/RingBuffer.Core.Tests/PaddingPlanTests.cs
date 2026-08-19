using System;
using RingBuffer.Core;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>
/// Tasks 2.4 and 2.5. The padding arithmetic behind
/// <see href="../../knowledge/decisions/d1-claim-commit-with-padding.md">D1</see> is the one
/// part of the design with no reference implementation to check against, which makes it
/// <see href="../../knowledge/risks/risk-register.md">R3</see>.
///
/// Because it is a total function over two ints, it can be checked <em>exhaustively</em>
/// rather than sampled. That is the whole point of doing it here, with no ring, no memory
/// and no threads to obscure a failure.
/// </summary>
public sealed class PaddingPlanTests
{
    private const int Bound = 4096;

    [Fact]
    public void Exhaustive_over_every_claimed_and_actual_pair()
    {
        var checkedPairs = 0L;
        var withPadding = 0L;

        for (var claimed = 0; claimed <= Bound; claimed++)
        {
            var claimedRecord = RecordDescriptor.RecordLength(claimed);

            for (var actual = 0; actual <= claimed; actual++)
            {
                var plan = PaddingPlan.For(claimed, actual);
                checkedPairs++;

                var committedRecord = RecordDescriptor.RecordLength(actual);

                // 1. The committed length is what the payload actually needs.
                Assert.Equal(committedRecord, plan.CommittedLength);

                // 2. Committed plus padding covers the claim EXACTLY. Under-covering
                //    leaves an unreadable gap that desynchronises the consumer for the
                //    rest of the buffer's life; over-covering corrupts the next record.
                Assert.Equal(claimedRecord, plan.CommittedLength + plan.PaddingLength);

                // 3. Everything stays 8-byte aligned.
                Assert.Equal(0, plan.CommittedLength % RecordDescriptor.Alignment);
                Assert.Equal(0, plan.PaddingLength % RecordDescriptor.Alignment);

                if (plan.HasPadding)
                {
                    withPadding++;

                    // 4. A padding record must be able to hold its own header.
                    Assert.True(plan.PaddingLength >= RecordDescriptor.HeaderLength,
                        $"claimed={claimed} actual={actual} padding={plan.PaddingLength}");

                    // 5. It starts exactly where the committed record ends.
                    Assert.Equal(plan.CommittedLength, plan.PaddingOffset);
                }
                else
                {
                    Assert.Equal(0, plan.PaddingOffset);
                    Assert.Equal(claimedRecord, plan.CommittedLength);
                }
            }
        }

        // Guard against the test silently checking nothing.
        Assert.True(checkedPairs > 8_000_000, $"only {checkedPairs} pairs checked");
        Assert.True(withPadding > 0, "no padding case was ever exercised");
    }

    /// <summary>
    /// Task 2.5. The design document described three leftover cases: zero, at least a
    /// header, and <em>less than</em> a header folded into the committed length.
    ///
    /// The third is <b>unreachable by construction</b>, and this test is what
    /// established that. Both record lengths are multiples of <c>Alignment</c>, so their
    /// difference is too; and <c>Alignment >= HeaderLength</c>, so a non-zero difference
    /// always has room for a padding header.
    ///
    /// The knowledge bundle has been corrected accordingly. The case does not need
    /// handling — it needs the relation between the two constants to keep holding.
    /// </summary>
    [Fact]
    public void Leftover_is_never_between_zero_and_one_header()
    {
        Assert.True(RecordDescriptor.Alignment >= RecordDescriptor.HeaderLength,
            "the whole argument rests on this");

        for (var claimed = 0; claimed <= Bound; claimed++)
        {
            for (var actual = 0; actual <= claimed; actual++)
            {
                var leftover = PaddingPlan.For(claimed, actual).PaddingLength;
                Assert.True(leftover == 0 || leftover >= RecordDescriptor.HeaderLength,
                    $"claimed={claimed} actual={actual} produced a {leftover}-byte leftover");
            }
        }
    }

    [Fact]
    public void Committing_the_full_claim_needs_no_padding()
    {
        for (var claimed = 0; claimed <= Bound; claimed++)
        {
            var plan = PaddingPlan.For(claimed, claimed);
            Assert.False(plan.HasPadding);
            Assert.Equal(RecordDescriptor.RecordLength(claimed), plan.CommittedLength);
        }
    }

    [Theory]
    // Same alignment bucket: no leftover at all, despite fewer payload bytes.
    [InlineData(8, 8, 16, 0)]
    [InlineData(8, 5, 16, 0)]
    [InlineData(8, 1, 16, 0)]
    // Crossing a bucket produces exactly one 8-byte padding record.
    [InlineData(9, 1, 16, 8)]
    // claimed=16 reserves Align8(8+16)=24; committing nothing needs Align8(8+0)=8,
    // leaving 16. I first wrote this as (16, 8) and the exhaustive test above was
    // right where my hand-picked case was wrong.
    [InlineData(16, 0, 8, 16)]
    // Committing nothing of a large claim pads the rest.
    [InlineData(64, 0, 8, 64)]
    public void Named_cases(int claimed, int actual, int expectedCommitted, int expectedPadding)
    {
        var plan = PaddingPlan.For(claimed, actual);
        Assert.Equal(expectedCommitted, plan.CommittedLength);
        Assert.Equal(expectedPadding, plan.PaddingLength);
    }

    [Fact]
    public void Rejects_committing_more_than_was_claimed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaddingPlan.For(16, 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaddingPlan.For(0, 1));
    }

    [Fact]
    public void Rejects_negative_lengths()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaddingPlan.For(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaddingPlan.For(16, -1));
    }
}
