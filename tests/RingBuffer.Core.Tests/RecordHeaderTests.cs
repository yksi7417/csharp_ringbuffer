using System;
using RingBuffer.Core;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>Tasks 2.1 and 2.2.</summary>
public sealed class RecordHeaderTests
{
    [Fact]
    public void Claimed_then_committed_round_trips()
    {
        Span<byte> record = stackalloc byte[64];

        RecordHeader.WriteClaimed(record, recordLength: 40, msgTypeId: 7);

        // The defining property: a claimed record is NOT readable. A consumer that
        // treated a negative length as a small record would read a partially
        // written message as if it were complete.
        Assert.False(RecordHeader.IsCommitted(record));
        Assert.Equal(-40, RecordHeader.ReadLength(record));
        Assert.Equal(7, RecordHeader.ReadTypeId(record));

        RecordHeader.WriteCommitted(record, recordLength: 40);

        Assert.True(RecordHeader.IsCommitted(record));
        Assert.Equal(40, RecordHeader.ReadLength(record));
        Assert.Equal(7, RecordHeader.ReadTypeId(record));
        Assert.False(RecordHeader.IsPadding(record));
    }

    [Fact]
    public void Padding_is_committed_and_skippable()
    {
        Span<byte> record = stackalloc byte[16];
        RecordHeader.WritePadding(record, recordLength: 16);

        Assert.True(RecordHeader.IsCommitted(record));
        Assert.True(RecordHeader.IsPadding(record));
        Assert.Equal(16, RecordHeader.ReadLength(record));
    }

    [Fact]
    public void A_zeroed_record_is_not_committed()
    {
        // Consumed bytes are zeroed before the head advances. That is what stops a
        // lapped buffer being reinterpreted as valid records, and it only works if
        // an all-zero header reads as uncommitted.
        Span<byte> record = stackalloc byte[16];
        Assert.False(RecordHeader.IsCommitted(record));
    }

    [Fact]
    public void Padding_type_id_can_never_collide_with_a_real_message()
    {
        Assert.True(RecordDescriptor.PaddingMsgTypeId < 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecordDescriptor.CheckTypeId(RecordDescriptor.PaddingMsgTypeId));
        RecordDescriptor.CheckTypeId(1);
    }

    [Fact]
    public void Offsets_are_where_the_layout_says()
    {
        Assert.Equal(100, RecordDescriptor.LengthOffset(100));
        Assert.Equal(104, RecordDescriptor.TypeOffset(100));
        Assert.Equal(108, RecordDescriptor.EncodedMsgOffset(100));
    }

    [Fact]
    public void Alignment_is_exhaustive_and_total()
    {
        for (var value = 0; value <= 100_000; value++)
        {
            var aligned = Align.To(value, RecordDescriptor.Alignment);
            Assert.True(aligned >= value);
            Assert.Equal(0, aligned % RecordDescriptor.Alignment);
            Assert.True(aligned - value < RecordDescriptor.Alignment);
            Assert.Equal(aligned, Align.To(aligned, RecordDescriptor.Alignment)); // idempotent
        }
    }

    [Fact]
    public void Alignment_rejects_overflow_rather_than_wrapping()
    {
        // A silently negative length would read as an uncommitted record and stall
        // the consumer forever -- much worse than throwing at the call site.
        Assert.Throws<ArgumentOutOfRangeException>(() => Align.To(int.MaxValue, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => Align.To(int.MaxValue - 6, 8));
        Assert.Equal(int.MaxValue - 7, Align.To(int.MaxValue - 7, 8));
    }

    [Fact]
    public void Alignment_rejects_a_non_power_of_two()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Align.To(8, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Align.To(8, 0));
    }

    [Fact]
    public void Record_length_covers_header_plus_payload()
    {
        Assert.Equal(8, RecordDescriptor.RecordLength(0));
        Assert.Equal(16, RecordDescriptor.RecordLength(1));
        Assert.Equal(16, RecordDescriptor.RecordLength(8));
        Assert.Equal(24, RecordDescriptor.RecordLength(9));
    }
}
