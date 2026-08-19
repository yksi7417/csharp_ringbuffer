using System;

namespace RingBuffer.Core;

/// <summary>
/// What to do with the space left over when a producer commits less than it claimed.
///
/// This is the arithmetic behind D1, the project's one genuine extension beyond
/// Agrona. SBE variable-length data means the encoded length is unknown until
/// encoding finishes, but zero copy means space must be claimed before encoding
/// starts — so a claim is a bound, and a commit is the truth.
///
/// Deliberately a pure total function over two integers: no ring, no memory, no
/// threads. That is what lets it be tested exhaustively rather than sampled, and
/// it is why R3 can be retired before any concurrency exists to obscure a failure.
///
/// See knowledge/decisions/d1-claim-commit-with-padding.md.
/// </summary>
public readonly record struct PaddingPlan(int CommittedLength, int PaddingOffset, int PaddingLength)
{
    /// <summary>True when a padding record must be written after the committed one.</summary>
    public bool HasPadding => PaddingLength > 0;

    /// <summary>
    /// Plans the commit of <paramref name="actualPayloadLength"/> bytes into a
    /// record that reserved room for <paramref name="claimedPayloadLength"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leftover is always either zero or at least
    /// <see cref="RecordDescriptor.HeaderLength"/> bytes — never in between. Both
    /// the claimed and the committed record lengths are multiples of
    /// <see cref="RecordDescriptor.Alignment"/>, so their difference is too; and
    /// <c>Alignment &gt;= HeaderLength</c>, so any non-zero difference is large
    /// enough to hold a padding header.
    /// </para>
    /// <para>
    /// That relation is the whole reason a sub-header leftover cannot arise. It is
    /// asserted below rather than assumed, because it silently stops holding the
    /// moment someone sets <c>Alignment</c> below <c>HeaderLength</c>.
    /// </para>
    /// </remarks>
    public static PaddingPlan For(int claimedPayloadLength, int actualPayloadLength)
    {
        if (claimedPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(claimedPayloadLength), claimedPayloadLength, "must not be negative");
        }

        if (actualPayloadLength < 0 || actualPayloadLength > claimedPayloadLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actualPayloadLength), actualPayloadLength,
                $"must be between 0 and the claimed length ({claimedPayloadLength})");
        }

        var claimedRecord = RecordDescriptor.RecordLength(claimedPayloadLength);
        var committedRecord = RecordDescriptor.RecordLength(actualPayloadLength);
        var leftover = claimedRecord - committedRecord;

        if (leftover == 0)
        {
            return new PaddingPlan(committedRecord, PaddingOffset: 0, PaddingLength: 0);
        }

        // Unreachable while Alignment >= HeaderLength. Kept as a live assertion,
        // not a comment, because the guarantee is structural and easy to break.
        if (leftover < RecordDescriptor.HeaderLength)
        {
            throw new InvalidOperationException(
                $"leftover of {leftover} byte(s) cannot hold a {RecordDescriptor.HeaderLength}-byte " +
                $"padding header. This is unreachable while Alignment ({RecordDescriptor.Alignment}) " +
                $">= HeaderLength ({RecordDescriptor.HeaderLength}); if you changed either, the " +
                "claim/commit design needs revisiting, not this exception suppressing.");
        }

        return new PaddingPlan(committedRecord, PaddingOffset: committedRecord, PaddingLength: leftover);
    }
}
