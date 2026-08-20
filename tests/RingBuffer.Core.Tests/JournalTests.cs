using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RingBuffer.Replay;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>Tasks 3.1 and 3.4.</summary>
public sealed class JournalTests
{
    private static byte[] Write(params byte[][] frames)
    {
        using var ms = new MemoryStream();
        foreach (var f in frames)
        {
            Journal.WriteFrame(ms, f);
        }

        return ms.ToArray();
    }

    private static List<byte[]> Read(byte[] journal)
    {
        using var ms = new MemoryStream(journal);
        return Journal.ReadFrames(ms).ToList();
    }

    [Fact]
    public void Frames_round_trip_in_order()
    {
        var a = "first"u8.ToArray();
        var b = "second frame"u8.ToArray();
        var c = Array.Empty<byte>();

        var got = Read(Write(a, b, c));

        Assert.Equal(3, got.Count);
        Assert.Equal(a, got[0]);
        Assert.Equal(b, got[1]);
        Assert.Empty(got[2]);
    }

    [Fact]
    public void The_length_prefix_is_little_endian()
    {
        // Pinned explicitly: an endianness change would silently invalidate every
        // committed fixture, and this is the cheapest place to catch it.
        var journal = Write(new byte[] { 0xAA, 0xBB, 0xCC });
        Assert.Equal(new byte[] { 0x03, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC }, journal);
    }

    [Fact]
    public void An_empty_journal_reads_as_no_frames()
    {
        Assert.Empty(Read(Array.Empty<byte>()));
    }

    [Fact]
    public void A_truncated_length_prefix_is_rejected()
    {
        Assert.Throws<InvalidDataException>(() => Read(new byte[] { 0x03, 0x00 }));
    }

    [Fact]
    public void A_truncated_frame_body_is_rejected()
    {
        // Silently returning the short frame would let a corrupt fixture pass as a
        // legitimate one.
        Assert.Throws<InvalidDataException>(() => Read(new byte[] { 0x08, 0x00, 0x00, 0x00, 0x01, 0x02 }));
    }

    [Fact]
    public void An_absurd_length_prefix_is_rejected_before_allocating()
    {
        var hostile = new byte[] { 0xFF, 0xFF, 0xFF, 0x7F };
        Assert.Throws<InvalidDataException>(() => Read(hostile));
    }

    [Fact]
    public void Writing_is_byte_deterministic()
    {
        var frames = new[] { "a"u8.ToArray(), "bb"u8.ToArray(), "ccc"u8.ToArray() };
        Assert.Equal(Write(frames), Write(frames));
    }

    [Fact]
    public void The_seeded_clock_is_reproducible_and_monotonic()
    {
        var a = new SeededClock(seed: 7);
        var b = new SeededClock(seed: 7);
        var different = new SeededClock(seed: 8);

        var first = a.NanosSinceEpoch;
        Assert.Equal(first, b.NanosSinceEpoch);
        Assert.NotEqual(first, different.NanosSinceEpoch);

        // Monotonic: a replay that reads the clock twice must not go backwards.
        Assert.True(a.NanosSinceEpoch > first);
    }

    [Fact]
    public void Seeded_ids_are_reproducible_and_distinct_across_seeds()
    {
        var a = new SeededIds(1);
        var b = new SeededIds(1);
        var c = new SeededIds(2);

        Assert.Equal(a.Next(), b.Next());
        Assert.Equal(a.Next(), b.Next());
        Assert.NotEqual(a.Next(), c.Next());
    }
}
