using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Org.SbeTool.Sbe.Dll;
using RingBuffer.Codecs;
using RingBuffer.Replay;

namespace RingBuffer.CorpusBuilder;

/// <summary>A conformance case: a name, why it exists, and the frames it feeds in.</summary>
public sealed record CorpusCase(string Name, string Why, Func<List<byte[]>> Frames);

/// <summary>
/// Declarative descriptions of the corpus cases.
///
/// <c>case.md</c> is generated from <see cref="CorpusCase.Why"/> rather than written
/// separately, so a fixture cannot exist without a recorded reason. A fixture whose
/// purpose nobody wrote down cannot be reviewed, and cannot be correctly updated
/// later — a reviewer has no way to tell a fix from a regression.
/// </summary>
public static class Cases
{
    private const int Scratch = 8192;

    public static IReadOnlyList<CorpusCase> All { get; } = new[]
    {
        new CorpusCase(
            "single-new-order",
            "The floor. One NewOrderSingle with one party, exercising the fixed block, a "
            + "single-level repeating group, and var-length data in one message.",
            () => new List<byte[]> { NewOrderSingle(partyCount: 1, account: "ACCT-1") }),

        new CorpusCase(
            "multiple-messages",
            "Sequencing. Five orders in a row must come out in the order they went in, "
            + "which is the property a ring is most likely to break under load.",
            () =>
            {
                var frames = new List<byte[]>();
                for (var i = 0; i < 5; i++)
                {
                    frames.Add(NewOrderSingle(partyCount: 1, account: $"ACCT-{i}"));
                }

                return frames;
            }),

        new CorpusCase(
            "empty-repeating-group",
            "Count 0 is the group boundary nobody writes by hand. A decoder that assumes "
            + "at least one entry reads var-data from the wrong offset, silently.",
            () => new List<byte[]> { NewOrderSingle(partyCount: 0, account: "ACCT-EMPTY") }),

        new CorpusCase(
            "nested-groups-uneven",
            "The shared-Limit trap. MarketDataIncrementalRefresh nests NoPartyIDs inside "
            + "NoMDEntries, with UNEVEN inner counts, so a decoder that assumes a fixed "
            + "inner size desynchronises rather than merely returning one wrong value.",
            () => new List<byte[]> { MarketData(entryCount: 3) }),

        new CorpusCase(
            "var-data-size-boundaries",
            "Variable-length data at 0, 1, and exactly-aligned lengths. These are where a "
            + "length prefix off by one stops being visible in a round-trip test.",
            () => new List<byte[]>
            {
                NewOrderSingle(partyCount: 1, account: ""),
                NewOrderSingle(partyCount: 1, account: "A"),
                NewOrderSingle(partyCount: 1, account: "12345678"),
            }),
    };

    private static unsafe byte[] NewOrderSingle(int partyCount, string account)
    {
        var slab = (byte*)NativeMemory.AlignedAlloc(Scratch, 4096);
        try
        {
            NativeMemory.Clear(slab, Scratch);
            var buffer = new DirectBuffer(slab, Scratch);

            var e = new Codecs.NewOrderSingle();
            e.WrapForEncodeAndApplyHeader(buffer, 0, new MessageHeader());
            e.SetClOrdId("ORD-0000000000000001");
            e.SetSymbol("ESZ6    ");
            e.Side = Side.BUY;
            e.OrdType = OrdType.LIMIT;
            e.OrderQty = 250;
            e.Price.Mantissa = 445125;
            e.Price.Exponent = -2;
            e.TransactTime = 1_700_000_000_000_000_000UL;

            var parties = e.PartiesCount(partyCount);
            for (var i = 0; i < partyCount; i++)
            {
                parties.Next();
                parties.SetPartyId($"BROKER-{i}        "[..16]);
                parties.PartyIdSource = PartyIDSource.PROPRIETARY;
                parties.PartyRole = (byte)(i + 1);
            }

            e.SetAccount(account);
            return new ReadOnlySpan<byte>(slab, e.Limit).ToArray();
        }
        finally
        {
            NativeMemory.AlignedFree(slab);
        }
    }

    private static unsafe byte[] MarketData(int entryCount)
    {
        var slab = (byte*)NativeMemory.AlignedAlloc(Scratch, 4096);
        try
        {
            NativeMemory.Clear(slab, Scratch);
            var buffer = new DirectBuffer(slab, Scratch);

            var e = new MarketDataIncrementalRefresh();
            e.WrapForEncodeAndApplyHeader(buffer, 0, new MessageHeader());
            e.TransactTime = 1_700_000_000_000_000_000UL;

            var entries = e.MdEntriesCount(entryCount);
            for (var i = 0; i < entryCount; i++)
            {
                entries.Next();
                entries.MdUpdateAction = MDUpdateAction.NEW;
                entries.MdEntryType = i == 0 ? MDEntryType.BID : MDEntryType.OFFER;
                entries.SetSymbol("ESZ6    ");
                entries.MdEntryPx.Mantissa = 445100 + i;
                entries.MdEntryPx.Exponent = -2;
                entries.MdEntrySize = 10 * (i + 1);

                // Uneven on purpose: 1, 2, 0.
                var inner = i switch { 0 => 1, 1 => 2, _ => 0 };
                var parties = entries.PartiesCount(inner);
                for (var j = 0; j < inner; j++)
                {
                    parties.Next();
                    parties.SetPartyId($"PARTY-{i}{j}         "[..16]);
                    parties.PartyRole = (byte)(j + 1);
                }

                entries.SetText($"entry-{i}");
            }

            return new ReadOnlySpan<byte>(slab, e.Limit).ToArray();
        }
        finally
        {
            NativeMemory.AlignedFree(slab);
        }
    }
}
