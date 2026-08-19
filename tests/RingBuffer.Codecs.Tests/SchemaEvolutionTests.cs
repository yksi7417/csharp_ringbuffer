using System;
using System.Runtime.InteropServices;
using Org.SbeTool.Sbe.Dll;
using Xunit;
using V1 = RingBuffer.Codecs;
using V2 = RingBuffer.Codecs.V2;

namespace RingBuffer.Codecs.Tests;

/// <summary>
/// Task 1.8. A v2 codec must correctly decode bytes written by a v1 codec, using
/// <c>actingVersion</c> from the MessageHeader. This is the mechanism that lets a
/// schema change without invalidating every message ever written, and it is worth
/// far more as an executable test than as a rule people remember.
///
/// See knowledge/decisions/d8-schema-selection.md.
/// </summary>
public sealed unsafe class SchemaEvolutionTests
{
    private static byte* Slab(int capacity)
    {
        var p = (byte*)NativeMemory.AlignedAlloc((nuint)capacity, 4096);
        NativeMemory.Clear(p, (nuint)capacity);
        return p;
    }

    /// <summary>Writes a v1 NewOrderSingle and returns the encoded length.</summary>
    private static int EncodeV1(DirectBuffer buffer)
    {
        var e = new V1.NewOrderSingle();
        e.WrapForEncodeAndApplyHeader(buffer, 0, new V1.MessageHeader());
        e.SetClOrdId("ORD-V1--------------");
        e.SetSymbol("ESZ6    ");
        e.Side = V1.Side.BUY;
        e.OrdType = V1.OrdType.LIMIT;
        e.OrderQty = 100;
        e.Price.Mantissa = 123456;
        e.Price.Exponent = -2;
        e.TransactTime = 1_700_000_000_000_000_000UL;
        var parties = e.PartiesCount(1);
        parties.Next();
        parties.SetPartyId("BROKER-A        ");
        parties.PartyIdSource = V1.PartyIDSource.PROPRIETARY;
        parties.PartyRole = 1;
        e.SetAccount("ACCT-1");
        return e.Limit;
    }

    [Fact]
    public void V2_codec_decodes_v1_bytes()
    {
        const int cap = 1024;
        var slab = Slab(cap);
        try
        {
            var buffer = new DirectBuffer(slab, cap);
            EncodeV1(buffer);

            // Decode with the V2 codec. The header carries v1's version and
            // blockLength; WrapForDecodeAndApplyHeader passes both through as
            // actingVersion / actingBlockLength.
            var header = new V2.MessageHeader();
            header.Wrap(buffer, 0, 0);
            Assert.Equal(0, header.Version);      // written by v1
            Assert.Equal(1, V2.NewOrderSingle.SchemaVersion);  // compiled as v2

            var d = new V2.NewOrderSingle();
            d.WrapForDecodeAndApplyHeader(buffer, 0, header);

            // Everything v1 knew about survives unchanged.
            Assert.Equal("ORD-V1--------------", d.GetClOrdId());
            Assert.Equal("ESZ6    ", d.GetSymbol());
            Assert.Equal(V2.Side.BUY, d.Side);
            Assert.Equal(100u, d.OrderQty);
            Assert.Equal(123456L, d.Price.Mantissa);
            Assert.Equal(1_700_000_000_000_000_000UL, d.TransactTime);

            // The v2-only scalar is absent, and says so rather than returning
            // whatever bytes happen to follow.
            Assert.False(d.MinQtyInActingVersion());
            Assert.Equal(V2.NewOrderSingle.MinQtyNullValue, d.MinQty);

            // The v1 group still decodes.
            var parties = d.Parties;
            Assert.Equal(1, parties.Count);
            parties.Next();
            Assert.Equal("BROKER-A        ", parties.GetPartyId());
            Assert.Equal(1, parties.PartyRole);

            // The v2-only group reads as empty, not as garbage.
            Assert.Equal(0, d.Allocations.Count);

            // And var-data after both groups still lands correctly, which is the
            // part that breaks if actingVersion handling is wrong: the Limit
            // cursor must skip a group that is not there.
            Assert.Equal("ACCT-1", d.GetAccount());
        }
        finally
        {
            NativeMemory.AlignedFree(slab);
        }
    }

    [Fact]
    public void V1_and_v2_agree_on_the_bytes_v1_can_express()
    {
        // A v2 encoder writing only v1 fields must not silently change the layout
        // of the part v1 understands. If this fails, every committed fixture is
        // invalidated by a schema bump.
        const int cap = 1024;
        var a = Slab(cap);
        var b = Slab(cap);
        try
        {
            var v1Len = EncodeV1(new DirectBuffer(a, cap));

            var e = new V2.NewOrderSingle();
            e.WrapForEncodeAndApplyHeader(new DirectBuffer(b, cap), 0, new V2.MessageHeader());
            e.SetClOrdId("ORD-V1--------------");
            e.SetSymbol("ESZ6    ");
            e.Side = V2.Side.BUY;
            e.OrdType = V2.OrdType.LIMIT;
            e.OrderQty = 100;
            e.Price.Mantissa = 123456;
            e.Price.Exponent = -2;
            e.TransactTime = 1_700_000_000_000_000_000UL;

            // The fixed block is v1's prefix plus the appended minQty, so compare
            // only the region v1 defines.
            var v1Block = V1.NewOrderSingle.BlockLength;
            var headerSize = V1.MessageHeader.Size;
            Assert.True(V2.NewOrderSingle.BlockLength > v1Block,
                "v2 must grow the block, otherwise the append did nothing");

            var av = new ReadOnlySpan<byte>(a, headerSize + v1Block).ToArray();
            var bv = new ReadOnlySpan<byte>(b, headerSize + v1Block).ToArray();

            // Bytes 0..7 are the header, which legitimately differs in
            // blockLength and version. Compare the message body only.
            Assert.Equal(av[headerSize..], bv[headerSize..]);
            Assert.True(v1Len > 0);
        }
        finally
        {
            NativeMemory.AlignedFree(a);
            NativeMemory.AlignedFree(b);
        }
    }
}
