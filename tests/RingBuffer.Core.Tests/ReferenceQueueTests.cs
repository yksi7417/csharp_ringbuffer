using System;
using System.Collections.Generic;
using System.Text;
using RingBuffer.Core;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>
/// Task 3.3. The reference queue is the oracle the real rings are checked against,
/// so what matters is that it preserves payloads exactly — in order, exactly once.
/// </summary>
public sealed class ReferenceQueueTests
{
    private static List<(int Type, byte[] Payload)> Drain(IRingBuffer ring, int limit = int.MaxValue)
    {
        var got = new List<(int, byte[])>();
        ring.Read((type, payload) => got.Add((type, payload.ToArray())), limit);
        return got;
    }

    [Fact]
    public void A_committed_message_comes_back_byte_identical()
    {
        using var ring = new ReferenceQueue();
        var payload = "hello ring"u8.ToArray();

        Assert.True(ring.TryClaim(payload.Length, msgTypeId: 7, out var claim));
        payload.CopyTo(claim.Span);
        claim.Commit(payload.Length);

        var got = Drain(ring);
        Assert.Single(got);
        Assert.Equal(7, got[0].Type);
        Assert.Equal(payload, got[0].Payload);
    }

    [Fact]
    public void Messages_come_back_in_order_exactly_once()
    {
        using var ring = new ReferenceQueue();
        for (var i = 0; i < 100; i++)
        {
            var body = Encoding.UTF8.GetBytes($"msg-{i:D3}");
            Assert.True(ring.TryClaim(body.Length, msgTypeId: 1, out var claim));
            body.CopyTo(claim.Span);
            claim.Commit(body.Length);
        }

        var got = Drain(ring);
        Assert.Equal(100, got.Count);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal($"msg-{i:D3}", Encoding.UTF8.GetString(got[i].Payload));
        }

        // Exactly once: a second drain yields nothing.
        Assert.Empty(Drain(ring));
    }

    [Fact]
    public void Committing_short_truncates_to_the_committed_length()
    {
        // The reference queue's stand-in for D1: claim a bound, commit the truth.
        using var ring = new ReferenceQueue();
        Assert.True(ring.TryClaim(64, msgTypeId: 3, out var claim));
        "abc"u8.ToArray().CopyTo(claim.Span);
        claim.Commit(3);

        var got = Drain(ring);
        Assert.Single(got);
        Assert.Equal(3, got[0].Payload.Length);
        Assert.Equal("abc"u8.ToArray(), got[0].Payload);
    }

    [Fact]
    public void An_aborted_claim_never_appears()
    {
        using var ring = new ReferenceQueue();
        Assert.True(ring.TryClaim(16, msgTypeId: 1, out var doomed));
        "nope"u8.ToArray().CopyTo(doomed.Span);
        doomed.Abort();

        Assert.True(ring.TryClaim(16, msgTypeId: 1, out var kept));
        "yes"u8.ToArray().CopyTo(kept.Span);
        kept.Commit(3);

        var got = Drain(ring);
        Assert.Single(got);
        Assert.Equal("yes"u8.ToArray(), got[0].Payload);
    }

    [Fact]
    public void Read_honours_the_message_count_limit()
    {
        using var ring = new ReferenceQueue();
        for (var i = 0; i < 10; i++)
        {
            Assert.True(ring.TryClaim(1, msgTypeId: 1, out var claim));
            claim.Span[0] = (byte)i;
            claim.Commit(1);
        }

        Assert.Equal(3, Drain(ring, 3).Count);
        Assert.Equal(3, Drain(ring, 3).Count);
        Assert.Equal(4, Drain(ring, 99).Count);
        Assert.Empty(Drain(ring));
    }

    [Fact]
    public void An_empty_payload_round_trips()
    {
        using var ring = new ReferenceQueue();
        Assert.True(ring.TryClaim(0, msgTypeId: 5, out var claim));
        claim.Commit(0);

        var got = Drain(ring);
        Assert.Single(got);
        Assert.Empty(got[0].Payload);
    }

    [Fact]
    public void Rejects_an_invalid_message_type_id()
    {
        using var ring = new ReferenceQueue();
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.TryClaim(8, 0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.TryClaim(8, -1, out _));
    }

    [Fact]
    public void Rejects_committing_more_than_was_claimed()
    {
        using var ring = new ReferenceQueue();
        Assert.True(ring.TryClaim(4, msgTypeId: 1, out var claim));

        // Written with try/catch rather than Assert.Throws because a Claim is a
        // ref struct and CANNOT be captured by a lambda -- CS8175. That is the
        // guarantee working: a claim cannot outlive its stack frame, so it can
        // never become a pointer into memory the consumer has already reclaimed.
        try
        {
            claim.Commit(5);
            Assert.Fail("committing more than was claimed must throw");
        }
        catch (ArgumentOutOfRangeException)
        {
            // expected
        }
    }

    [Fact]
    public void A_claim_cannot_escape_its_stack_frame()
    {
        // There is no runtime assertion to make here: the compiler rejects every
        // way of smuggling a Claim out -- boxing it, storing it in a field,
        // capturing it in a lambda, putting it in a list. This test documents that
        // the guarantee is structural, and the CS8175 in the test above is the
        // evidence it is enforced.
        using var ring = new ReferenceQueue();
        Assert.True(ring.TryClaim(8, msgTypeId: 1, out var claim));
        claim.Commit(0);
        Assert.Equal(1, ring.PendingCount);
    }

    [Fact]
    public void Rejects_a_non_power_of_two_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceQueue(100));
    }
}
