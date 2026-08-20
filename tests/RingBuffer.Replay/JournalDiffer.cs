using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RingBuffer.Replay;

/// <summary>The outcome of comparing two journals.</summary>
public sealed record DiffResult(bool Identical, string Report)
{
    public static DiffResult Same(int frames) =>
        new(true, $"identical: {frames} frame(s), byte for byte");
}

/// <summary>
/// Byte-exact journal comparison with a readable failure report.
///
/// The comparison itself is trivial; the reporting is the work. A diff that says
/// "byte 52 differs" gets muted. A diff that says "NewOrderSingle.orderQty, expected
/// 250, actual 200" gets fixed.
/// </summary>
public sealed class JournalDiffer
{
    private readonly SbeSchemaMap? _schema;

    public JournalDiffer(SbeSchemaMap? schema = null) => _schema = schema;

    public DiffResult Compare(IReadOnlyList<byte[]> expected, IReadOnlyList<byte[]> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var common = Math.Min(expected.Count, actual.Count);
        for (var i = 0; i < common; i++)
        {
            if (!expected[i].AsSpan().SequenceEqual(actual[i]))
            {
                return new DiffResult(false, DescribeFrame(i, expected[i], actual[i]));
            }
        }

        if (expected.Count != actual.Count)
        {
            var report = new StringBuilder();
            report.AppendLine(CultureInfo.InvariantCulture,
                $"frame count differs: expected {expected.Count}, actual {actual.Count}");
            report.AppendLine(CultureInfo.InvariantCulture,
                $"the first {common} frame(s) are identical");

            var extra = expected.Count > actual.Count ? expected : actual;
            var side = expected.Count > actual.Count ? "expected" : "actual";
            report.AppendLine(CultureInfo.InvariantCulture,
                $"first frame present only in {side} (index {common}):");
            report.Append(HexDump(extra[common], 0, "  "));
            return new DiffResult(false, report.ToString());
        }

        return DiffResult.Same(expected.Count);
    }

    private string DescribeFrame(int frameIndex, byte[] expected, byte[] actual)
    {
        var offset = FirstDifference(expected, actual);
        var report = new StringBuilder();

        report.AppendLine(CultureInfo.InvariantCulture,
            $"frame {frameIndex} differs at byte {offset} (0x{offset:X4})");

        var templateId = TemplateIdOf(expected);
        if (_schema is not null && templateId is not null)
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  field: {_schema.Describe(templateId.Value, offset)}");
        }
        else if (_schema is null)
        {
            report.AppendLine("  (no schema supplied, so the field cannot be named)");
        }

        if (offset < expected.Length && offset < actual.Length)
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  expected 0x{expected[offset]:X2}, actual 0x{actual[offset]:X2}");
        }
        else
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  lengths differ: expected {expected.Length} byte(s), actual {actual.Length}");
        }

        // Context either side of the difference, so the surrounding fields are visible.
        var from = Math.Max(0, (offset - 8) & ~0xF);
        report.AppendLine("  expected:");
        report.Append(HexDump(expected, from, "    ", offset));
        report.AppendLine("  actual:");
        report.Append(HexDump(actual, from, "    ", offset));

        return report.ToString();
    }

    private static int FirstDifference(byte[] a, byte[] b)
    {
        var common = Math.Min(a.Length, b.Length);
        for (var i = 0; i < common; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return common;
    }

    private static int? TemplateIdOf(byte[] frame) =>
        frame.Length >= 4 ? BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)) : null;

    private static string HexDump(byte[] data, int from, string indent, int highlight = -1)
    {
        var sb = new StringBuilder();
        var to = Math.Min(data.Length, from + 48);
        for (var line = from; line < to; line += 16)
        {
            sb.Append(indent).Append(CultureInfo.InvariantCulture, $"{line:X4}: ");
            for (var i = line; i < Math.Min(line + 16, data.Length); i++)
            {
                sb.Append(i == highlight ? '[' : ' ');
                sb.Append(CultureInfo.InvariantCulture, $"{data[i]:X2}");
                if (i == highlight)
                {
                    sb.Append(']');
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
