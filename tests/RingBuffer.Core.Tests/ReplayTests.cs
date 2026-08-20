using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RingBuffer.Core;
using RingBuffer.Replay;
using Xunit;

namespace RingBuffer.Core.Tests;

/// <summary>Tasks 3.5, 3.6 and 3.7.</summary>
public sealed class ReplayTests
{
    private static readonly string SchemaPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "schemas", "fix-sbe.xml");

    private static ReplayEngine Engine() => new(() => new ReferenceQueue());

    private static byte[] Frame(params byte[] body) => body;

    [Fact]
    public void Replay_preserves_frames_exactly_and_in_order()
    {
        var input = new List<byte[]>
        {
            Frame(1, 2, 3),
            Frame(4, 5),
            Array.Empty<byte>(),
            Frame(6),
        };

        var output = Engine().Run(input);

        Assert.Equal(input.Count, output.Count);
        for (var i = 0; i < input.Count; i++)
        {
            Assert.Equal(input[i], output[i]);
        }
    }

    [Fact]
    public void Replay_is_deterministic_across_runs()
    {
        // The premise of the whole corpus strategy: same input, same bytes, always.
        var input = Enumerable.Range(0, 50).Select(i => new byte[] { (byte)i, (byte)(i * 2) }).ToList();

        var a = Engine().Run(input, seed: 7);
        var b = Engine().Run(input, seed: 7);

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i], b[i]);
        }
    }

    [Fact]
    public void The_cli_contract_is_a_pure_function_of_the_two_paths()
    {
        var dir = Directory.CreateTempSubdirectory("replay-test");
        try
        {
            var inputPath = Path.Combine(dir.FullName, "input.sbe");
            var outputPath = Path.Combine(dir.FullName, "output.sbe");

            using (var s = File.Create(inputPath))
            {
                Journal.WriteFrame(s, new byte[] { 0xDE, 0xAD });
                Journal.WriteFrame(s, new byte[] { 0xBE, 0xEF, 0x01 });
            }

            Engine().Run(inputPath, outputPath, seed: 0);

            // Running it again over the same input must produce byte-identical output.
            var first = File.ReadAllBytes(outputPath);
            Engine().Run(inputPath, outputPath, seed: 0);
            Assert.Equal(first, File.ReadAllBytes(outputPath));

            using var read = File.OpenRead(outputPath);
            var frames = Journal.ReadFrames(read).ToList();
            Assert.Equal(2, frames.Count);
            Assert.Equal(new byte[] { 0xDE, 0xAD }, frames[0]);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Identical_journals_compare_equal()
    {
        var differ = new JournalDiffer();
        var a = new List<byte[]> { new byte[] { 1, 2 }, new byte[] { 3 } };
        var b = new List<byte[]> { new byte[] { 1, 2 }, new byte[] { 3 } };

        var result = differ.Compare(a, b);
        Assert.True(result.Identical);
        Assert.Contains("2 frame(s)", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void A_differing_byte_is_located_by_frame_and_offset()
    {
        var differ = new JournalDiffer();
        var expected = new List<byte[]> { new byte[] { 1, 2, 3 }, new byte[] { 9, 9, 9, 9 } };
        var actual = new List<byte[]> { new byte[] { 1, 2, 3 }, new byte[] { 9, 9, 8, 9 } };

        var result = differ.Compare(expected, actual);

        Assert.False(result.Identical);
        Assert.Contains("frame 1", result.Report, StringComparison.Ordinal);
        Assert.Contains("byte 2", result.Report, StringComparison.Ordinal);
        Assert.Contains("expected 0x09, actual 0x08", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void A_frame_count_mismatch_is_reported_distinctly()
    {
        var differ = new JournalDiffer();
        var expected = new List<byte[]> { new byte[] { 1 }, new byte[] { 2 } };
        var actual = new List<byte[]> { new byte[] { 1 } };

        var result = differ.Compare(expected, actual);

        Assert.False(result.Identical);
        Assert.Contains("frame count differs", result.Report, StringComparison.Ordinal);
        Assert.Contains("expected 2, actual 1", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void The_schema_map_computes_fixed_block_offsets()
    {
        var map = new SbeSchemaMap(SchemaPath);

        var nos = map.Message(1);
        Assert.NotNull(nos);
        Assert.Equal("NewOrderSingle", nos!.Name);

        // clOrdId char[20], symbol char[8], side char, ordType char, orderQty uint32,
        // price Decimal64 (int64 + int8 = 9), transactTime uint64.
        var fields = nos.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
        Assert.Equal(0, fields["clOrdId"].Offset);
        Assert.Equal(20, fields["clOrdId"].Length);
        Assert.Equal(20, fields["symbol"].Offset);
        Assert.Equal(28, fields["side"].Offset);
        Assert.Equal(29, fields["ordType"].Offset);
        Assert.Equal(30, fields["orderQty"].Offset);
        Assert.Equal(34, fields["price"].Offset);
        Assert.Equal(9, fields["price"].Length);
        Assert.Equal(43, fields["transactTime"].Offset);
        Assert.Equal(51, nos.BlockLength);
    }

    [Fact]
    public void The_differ_names_the_field_not_just_the_offset()
    {
        // This is the difference between a corpus people use and one they mute.
        var map = new SbeSchemaMap(SchemaPath);

        // Header is 8 bytes; orderQty sits at block offset 30, so frame offset 38.
        Assert.Contains("NewOrderSingle.orderQty", map.Describe(templateId: 1, frameOffset: 38),
            StringComparison.Ordinal);
        Assert.Contains("messageHeader.templateId", map.Describe(templateId: 1, frameOffset: 2),
            StringComparison.Ordinal);
        Assert.Contains("groups / var-data", map.Describe(templateId: 1, frameOffset: 8 + 51 + 4),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_differ_report_names_the_field_when_given_a_schema()
    {
        var map = new SbeSchemaMap(SchemaPath);
        var differ = new JournalDiffer(map);

        // Build two frames differing inside orderQty (frame offset 38).
        var expected = new byte[64];
        expected[2] = 1; // templateId = 1, little-endian
        var actual = (byte[])expected.Clone();
        expected[38] = 0xFA;
        actual[38] = 0xC8;

        var result = differ.Compare(new[] { expected }, new[] { actual });

        Assert.False(result.Identical);
        Assert.Contains("orderQty", result.Report, StringComparison.Ordinal);
    }
}
