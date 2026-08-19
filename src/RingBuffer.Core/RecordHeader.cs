using System;
using System.Buffers.Binary;

namespace RingBuffer.Core;

/// <summary>
/// Reads and writes the 8-byte record header. Operates on a <see cref="Span{T}"/>
/// so it can be tested without a ring, and used against ring memory unchanged.
/// </summary>
public static class RecordHeader
{
    /// <summary>
    /// Writes the header with a <b>negative</b> length, marking the record claimed
    /// but not yet readable. This is always the first write of a publication.
    /// </summary>
    public static void WriteClaimed(Span<byte> record, int recordLength, int msgTypeId)
    {
        if (recordLength < RecordDescriptor.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordLength), recordLength, "a record must be at least a header long");
        }

        BinaryPrimitives.WriteInt32LittleEndian(record, -recordLength);
        BinaryPrimitives.WriteInt32LittleEndian(record[sizeof(int)..], msgTypeId);
    }

    /// <summary>
    /// Writes the positive length that publishes the record. In the real ring this
    /// is a store-release and is the single commit point; here it is the plain
    /// arithmetic underneath.
    /// </summary>
    public static void WriteCommitted(Span<byte> record, int recordLength)
    {
        if (recordLength < RecordDescriptor.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordLength), recordLength, "a record must be at least a header long");
        }

        BinaryPrimitives.WriteInt32LittleEndian(record, recordLength);
    }

    /// <summary>Writes a complete padding record: committed, and typed as skippable.</summary>
    public static void WritePadding(Span<byte> record, int recordLength)
    {
        if (recordLength < RecordDescriptor.HeaderLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordLength), recordLength, "padding must be at least a header long");
        }

        BinaryPrimitives.WriteInt32LittleEndian(record[sizeof(int)..], RecordDescriptor.PaddingMsgTypeId);
        BinaryPrimitives.WriteInt32LittleEndian(record, recordLength);
    }

    public static int ReadLength(ReadOnlySpan<byte> record) =>
        BinaryPrimitives.ReadInt32LittleEndian(record);

    public static int ReadTypeId(ReadOnlySpan<byte> record) =>
        BinaryPrimitives.ReadInt32LittleEndian(record[sizeof(int)..]);

    /// <summary>A record is readable only once its length has gone positive.</summary>
    public static bool IsCommitted(ReadOnlySpan<byte> record) => ReadLength(record) > 0;

    /// <summary>True for a record the consumer must skip without invoking the handler.</summary>
    public static bool IsPadding(ReadOnlySpan<byte> record) =>
        ReadTypeId(record) == RecordDescriptor.PaddingMsgTypeId;
}
