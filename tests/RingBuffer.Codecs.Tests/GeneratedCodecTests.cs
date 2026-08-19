using System;
using System.Runtime.InteropServices;
using System.Text;
using Org.SbeTool.Sbe.Dll;
using RingBuffer.Codecs;
using Xunit;

namespace RingBuffer.Codecs.Tests;

/// <summary>
/// Task 1.6. The assertion is that generated code <em>compiles and round-trips</em>,
/// not that files appeared. A generator that writes nothing exits 0 (TRAP-1), and a
/// build can succeed having compiled none of its output (TRAP-7) -- so the only
/// honest test of the codegen step is to encode and decode a real message.
/// </summary>
public sealed unsafe class GeneratedCodecTests
{
    /// <summary>Off-heap slab, as the real ring will use (D4). Never moved, never pinned.</summary>
    private static byte* Slab(int capacity)
    {
        var p = (byte*)NativeMemory.AlignedAlloc((nuint)capacity, 4096);
        NativeMemory.Clear(p, (nuint)capacity);
        return p;
    }

    [Fact]
    public void NewOrderSingle_round_trips_through_native_memory()
    {
        const int cap = 1024;
        var slab = Slab(cap);
        try
        {
            var buffer = new DirectBuffer(slab, cap);

            var encoder = new NewOrderSingle();
            encoder.WrapForEncodeAndApplyHeader(buffer, 0, new MessageHeader());
            encoder.SetClOrdId("ORD-0000000000000001");
            encoder.SetSymbol("ESZ6    ");
            encoder.Side = Side.BUY;
            encoder.OrdType = OrdType.LIMIT;
            encoder.OrderQty = 250;
            encoder.Price.Mantissa = 445125;
            encoder.Price.Exponent = -2;
            encoder.TransactTime = 1_700_000_000_123_456_789UL;

            var parties = encoder.PartiesCount(2);
            parties.Next();
            parties.SetPartyId("BROKER-A        ");
            parties.PartyIdSource = PartyIDSource.PROPRIETARY;
            parties.PartyRole = 1;
            parties.Next();
            parties.SetPartyId("CLIENT-42       ");
            parties.PartyIdSource = PartyIDSource.BIC;
            parties.PartyRole = 3;

            encoder.SetAccount("ACCT-9");

            var header = new MessageHeader();
            header.Wrap(buffer, 0, 0);
            Assert.Equal(NewOrderSingle.TemplateId, header.TemplateId);
            Assert.Equal(NewOrderSingle.SchemaId, header.SchemaId);

            var decoder = new NewOrderSingle();
            decoder.WrapForDecodeAndApplyHeader(buffer, 0, header);

            Assert.Equal("ORD-0000000000000001", decoder.GetClOrdId());
            Assert.Equal("ESZ6    ", decoder.GetSymbol());
            Assert.Equal(Side.BUY, decoder.Side);
            Assert.Equal(OrdType.LIMIT, decoder.OrdType);
            Assert.Equal(250u, decoder.OrderQty);
            Assert.Equal(445125L, decoder.Price.Mantissa);
            Assert.Equal(-2, decoder.Price.Exponent);
            Assert.Equal(1_700_000_000_123_456_789UL, decoder.TransactTime);

            var decoded = decoder.Parties;
            Assert.Equal(2, decoded.Count);
            decoded.Next();
            Assert.Equal("BROKER-A        ", decoded.GetPartyId());
            Assert.Equal(PartyIDSource.PROPRIETARY, decoded.PartyIdSource);
            Assert.Equal(1, decoded.PartyRole);
            decoded.Next();
            Assert.Equal("CLIENT-42       ", decoded.GetPartyId());
            Assert.Equal(PartyIDSource.BIC, decoded.PartyIdSource);
            Assert.Equal(3, decoded.PartyRole);

            // Var-data is read AFTER the group, because both advance the same
            // Limit cursor on the parent message.
            Assert.Equal("ACCT-9", decoder.GetAccount());
        }
        finally
        {
            NativeMemory.AlignedFree(slab);
        }
    }

    [Fact]
    public void Nested_repeating_groups_round_trip()
    {
        // The case the schema exists for (D8). The inner group advances the parent
        // message's shared Limit, so a decoder that reads out of order silently
        // returns garbage rather than throwing.
        const int cap = 4096;
        var slab = Slab(cap);
        try
        {
            var buffer = new DirectBuffer(slab, cap);

            var encoder = new MarketDataIncrementalRefresh();
            encoder.WrapForEncodeAndApplyHeader(buffer, 0, new MessageHeader());
            encoder.TransactTime = 1_700_000_000_000_000_000UL;

            var entries = encoder.MdEntriesCount(3);
            for (var i = 0; i < 3; i++)
            {
                entries.Next();
                entries.MdUpdateAction = MDUpdateAction.NEW;
                entries.MdEntryType = i == 0 ? MDEntryType.BID : MDEntryType.OFFER;
                entries.SetSymbol("ESZ6    ");
                entries.MdEntryPx.Mantissa = 445100 + i;
                entries.MdEntryPx.Exponent = -2;
                entries.MdEntrySize = 10 * (i + 1);

                // Entry 1 carries two parties, the others one. Uneven counts are
                // what catch a decoder that assumes a fixed inner size.
                var innerCount = i == 1 ? 2 : 1;
                var inner = entries.PartiesCount(innerCount);
                for (var j = 0; j < innerCount; j++)
                {
                    inner.Next();
                    inner.SetPartyId($"PARTY-{i}{j}         "[..16]);
                    inner.PartyRole = (byte)(j + 1);
                }

                entries.SetText($"entry-{i}");
            }

            var header = new MessageHeader();
            header.Wrap(buffer, 0, 0);
            var decoder = new MarketDataIncrementalRefresh();
            decoder.WrapForDecodeAndApplyHeader(buffer, 0, header);

            Assert.Equal(1_700_000_000_000_000_000UL, decoder.TransactTime);
            var outer = decoder.MdEntries;
            Assert.Equal(3, outer.Count);

            for (var i = 0; i < 3; i++)
            {
                outer.Next();
                Assert.Equal(MDUpdateAction.NEW, outer.MdUpdateAction);
                Assert.Equal(i == 0 ? MDEntryType.BID : MDEntryType.OFFER, outer.MdEntryType);
                Assert.Equal("ESZ6    ", outer.GetSymbol());
                Assert.Equal(445100 + i, outer.MdEntryPx.Mantissa);
                Assert.Equal(10 * (i + 1), outer.MdEntrySize);

                var expectedInner = i == 1 ? 2 : 1;
                var inner = outer.Parties;
                Assert.Equal(expectedInner, inner.Count);
                for (var j = 0; j < expectedInner; j++)
                {
                    inner.Next();
                    Assert.Equal($"PARTY-{i}{j}         "[..16], inner.GetPartyId());
                    Assert.Equal(j + 1, inner.PartyRole);
                }

                Assert.Equal($"entry-{i}", outer.GetText());
            }
        }
        finally
        {
            NativeMemory.AlignedFree(slab);
        }
    }

    [Fact]
    public void Encoding_is_byte_deterministic()
    {
        // The premise of the whole conformance-corpus strategy (L4): the same
        // logical message must produce the same bytes every time.
        static byte[] Encode()
        {
            const int cap = 512;
            var slab = (byte*)NativeMemory.AlignedAlloc(cap, 4096);
            try
            {
                NativeMemory.Clear(slab, cap);
                var buffer = new DirectBuffer(slab, cap);
                var e = new NewOrderSingle();
                e.WrapForEncodeAndApplyHeader(buffer, 0, new MessageHeader());
                e.SetClOrdId("ORD-0000000000000001");
                e.SetSymbol("ESZ6    ");
                e.Side = Side.SELL;
                e.OrdType = OrdType.MARKET;
                e.OrderQty = 7;
                e.Price.Mantissa = 1;
                e.Price.Exponent = 0;
                e.TransactTime = 42;
                e.PartiesCount(0);
                e.SetAccount("A");
                return new ReadOnlySpan<byte>(slab, e.Limit).ToArray();
            }
            finally
            {
                NativeMemory.AlignedFree(slab);
            }
        }

        Assert.Equal(Encode(), Encode());
    }

    [Fact]
    public void Empty_repeating_group_round_trips()
    {
        // Count 0 is the group boundary nobody writes by hand.
        const int cap = 512;
        var slab = Slab(cap);
        try
        {
            var buffer = new DirectBuffer(slab, cap);
            var e = new NewOrderSingle();
            e.WrapForEncodeAndApplyHeader(buffer, 0, new MessageHeader());
            e.SetClOrdId("ORD-EMPTY           ");
            e.SetSymbol("NQZ6    ");
            e.Side = Side.BUY;
            e.OrdType = OrdType.MARKET;
            e.OrderQty = 1;
            e.Price.Mantissa = 0;
            e.Price.Exponent = 0;
            e.TransactTime = 1;
            e.PartiesCount(0);
            e.SetAccount("");

            var header = new MessageHeader();
            header.Wrap(buffer, 0, 0);
            var d = new NewOrderSingle();
            d.WrapForDecodeAndApplyHeader(buffer, 0, header);

            Assert.Equal(0, d.Parties.Count);
            Assert.Equal("", d.GetAccount());
        }
        finally
        {
            NativeMemory.AlignedFree(slab);
        }
    }
}
