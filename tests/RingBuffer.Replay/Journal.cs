using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace RingBuffer.Replay;

/// <summary>
/// A journal is a flat sequence of length-prefixed frames:
///
/// <code>
/// uint32 frameLength (little-endian)
/// byte[] frame        -- an SBE MessageHeader followed by the message body
/// </code>
///
/// <b>Deliberately not the ring's record format.</b> The journal is a
/// transport-independent serialisation, so a committed fixture stays valid if the
/// ring's internal framing ever changes. Coupling them would mean a ring-layout
/// change invalidated every fixture, which would make the corpus expensive to keep
/// and therefore the first thing abandoned.
///
/// See knowledge/architecture/replay-harness.md.
/// </summary>
public static class Journal
{
    private const int LengthPrefixBytes = 4;

    /// <summary>Guards against a corrupt length prefix turning into a huge allocation.</summary>
    public const int MaxFrameLength = 16 * 1024 * 1024;

    public static void WriteFrame(Stream destination, ReadOnlySpan<byte> frame)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (frame.Length > MaxFrameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame), frame.Length, "frame exceeds the maximum journal frame length");
        }

        Span<byte> prefix = stackalloc byte[LengthPrefixBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)frame.Length);
        destination.Write(prefix);
        destination.Write(frame);
    }

    /// <summary>Reads every frame. Frames are yielded as fresh arrays; this is a test tool, not a hot path.</summary>
    public static IEnumerable<byte[]> ReadFrames(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var prefix = new byte[LengthPrefixBytes]; // ALLOW-ALLOC: replay tooling, file I/O bound
        while (true)
        {
            var got = ReadExactly(source, prefix, LengthPrefixBytes);
            if (got == 0)
            {
                yield break;
            }

            if (got != LengthPrefixBytes)
            {
                throw new InvalidDataException(
                    $"journal truncated: {got} byte(s) of a {LengthPrefixBytes}-byte length prefix");
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
            if (length > MaxFrameLength)
            {
                throw new InvalidDataException(
                    $"journal frame claims {length} bytes, above the {MaxFrameLength}-byte maximum");
            }

            var frame = new byte[length]; // ALLOW-ALLOC: replay tooling, file I/O bound
            if (ReadExactly(source, frame, (int)length) != (int)length)
            {
                throw new InvalidDataException($"journal truncated inside a {length}-byte frame");
            }

            yield return frame;
        }
    }

    private static int ReadExactly(Stream source, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}
