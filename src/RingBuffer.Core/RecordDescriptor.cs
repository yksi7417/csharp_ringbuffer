using System;

namespace RingBuffer.Core;

/// <summary>
/// The on-wire layout of a ring record. Pure arithmetic: no memory, no threads.
///
/// <code>
/// offset +0 : int32  length   (negative = claimed/in-flight, positive = committed)
/// offset +4 : int32  type     (msgTypeId; PaddingMsgTypeId marks a skip record)
/// offset +8 : payload
/// </code>
///
/// See knowledge/concepts/ring-buffer-record-layout.md.
/// </summary>
public static class RecordDescriptor
{
    /// <summary>Bytes of header preceding every payload.</summary>
    public const int HeaderLength = 8;

    /// <summary>
    /// Record alignment. <b>Must be greater than or equal to <see cref="HeaderLength"/>.</b>
    /// That relation is load-bearing: it is what makes a sub-header leftover
    /// impossible when a claim is committed short. See <see cref="PaddingPlan"/>.
    /// </summary>
    public const int Alignment = 8;

    /// <summary>
    /// Marks a record the consumer skips without invoking the handler. Negative,
    /// because a real message type id must be greater than zero.
    /// </summary>
    public const int PaddingMsgTypeId = -1;

    public static int LengthOffset(int recordOffset) => recordOffset;

    public static int TypeOffset(int recordOffset) => recordOffset + sizeof(int);

    public static int EncodedMsgOffset(int recordOffset) => recordOffset + HeaderLength;

    /// <summary>Total aligned record length for a payload of <paramref name="payloadLength"/>.</summary>
    public static int RecordLength(int payloadLength)
    {
        if (payloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payloadLength), payloadLength, "payload length must not be negative");
        }

        return Align.To(HeaderLength + payloadLength, Alignment);
    }

    /// <summary>
    /// A message type id must be greater than zero, which is what frees negative
    /// values to be used as sentinels.
    /// </summary>
    public static void CheckTypeId(int msgTypeId)
    {
        if (msgTypeId < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(msgTypeId), msgTypeId, "message type id must be greater than zero");
        }
    }
}
