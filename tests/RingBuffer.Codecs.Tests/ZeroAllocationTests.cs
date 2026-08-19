using System;
using System.Runtime.InteropServices;
using System.Text;
using Org.SbeTool.Sbe.Dll;
using RingBuffer.Codecs;
using RingBuffer.TestSupport;
using Xunit;

namespace RingBuffer.Codecs.Tests;

/// <summary>
/// Task 1.10. The span overloads must allocate nothing; the string overloads
/// demonstrably do. The ergonomic call is the wrong one, which is why this is a
/// gate rather than a coding guideline (F4, R5).
/// </summary>
public sealed unsafe class ZeroAllocationTests : IDisposable
{
    private const int Capacity = 4096;
    private readonly byte* _slab;
    private readonly DirectBuffer _buffer;

    // Flyweights and scratch are allocated ONCE, as the hot path will do.
    private readonly NewOrderSingle _encoder = new();
    private readonly NewOrderSingle _decoder = new();
    private readonly MessageHeader _header = new();
    private readonly byte[] _scratch = new byte[64];

    public ZeroAllocationTests()
    {
        _slab = (byte*)NativeMemory.AlignedAlloc(Capacity, 4096);
        NativeMemory.Clear(_slab, Capacity);
        _buffer = new DirectBuffer(_slab, Capacity);
    }

    public void Dispose() => NativeMemory.AlignedFree(_slab);

    [Fact]
    public void Encoding_with_span_overloads_allocates_nothing()
    {
        var clOrdId = "ORD-0000000000000001"u8.ToArray();
        var symbol = "ESZ6    "u8.ToArray();
        var partyId = "BROKER-A        "u8.ToArray();
        var account = "ACCT-9"u8.ToArray();

        var bytes = Allocation.BytesPerIteration(() =>
        {
            _encoder.WrapForEncodeAndApplyHeader(_buffer, 0, _header);
            _encoder.SetClOrdId(clOrdId);
            _encoder.SetSymbol(symbol);
            _encoder.Side = Side.BUY;
            _encoder.OrdType = OrdType.LIMIT;
            _encoder.OrderQty = 250;
            _encoder.Price.Mantissa = 445125;
            _encoder.Price.Exponent = -2;
            _encoder.TransactTime = 1_700_000_000UL;

            var parties = _encoder.PartiesCount(1);
            parties.Next();
            parties.SetPartyId(partyId);
            parties.PartyIdSource = PartyIDSource.PROPRIETARY;
            parties.PartyRole = 1;

            _encoder.SetAccount(account);
        });

        Assert.Equal(0, bytes);
    }

    [Fact]
    public void Decoding_with_span_overloads_allocates_nothing()
    {
        // Seed one message to decode repeatedly.
        _encoder.WrapForEncodeAndApplyHeader(_buffer, 0, _header);
        _encoder.SetClOrdId("ORD-0000000000000001"u8.ToArray());
        _encoder.SetSymbol("ESZ6    "u8.ToArray());
        _encoder.Side = Side.SELL;
        _encoder.OrdType = OrdType.MARKET;
        _encoder.OrderQty = 5;
        _encoder.Price.Mantissa = 1;
        _encoder.Price.Exponent = 0;
        _encoder.TransactTime = 7;
        var seed = _encoder.PartiesCount(1);
        seed.Next();
        seed.SetPartyId("BROKER-A        "u8.ToArray());
        seed.PartyIdSource = PartyIDSource.BIC;
        seed.PartyRole = 2;
        _encoder.SetAccount("ACCT-9"u8.ToArray());

        var sink = 0L;
        var bytes = Allocation.BytesPerIteration(() =>
        {
            _header.Wrap(_buffer, 0, 0);
            _decoder.WrapForDecodeAndApplyHeader(_buffer, 0, _header);

            sink += _decoder.OrderQty;
            sink += _decoder.Price.Mantissa;
            sink += (long)_decoder.TransactTime;
            sink += _decoder.Symbol[0];          // ReadOnlySpan<byte>, no copy

            var parties = _decoder.Parties;
            while (parties.HasNext)
            {
                sink += parties.Next().PartyRole;
            }

            sink += _decoder.GetAccount(_scratch); // span overload, no allocation
        });

        Assert.Equal(0, bytes);
        Assert.True(sink != 0);
    }

    [Fact]
    public void The_string_overloads_allocate_which_is_why_they_are_banned_on_the_hot_path()
    {
        // Not a complaint about the API -- a demonstration that the ergonomic call
        // is the wrong one, so the banned-members lint has something to point at.
        _encoder.WrapForEncodeAndApplyHeader(_buffer, 0, _header);
        _encoder.SetClOrdId("ORD-0000000000000001");
        _encoder.SetSymbol("ESZ6    ");
        _encoder.Side = Side.BUY;
        _encoder.OrdType = OrdType.LIMIT;
        _encoder.OrderQty = 1;
        _encoder.Price.Mantissa = 1;
        _encoder.Price.Exponent = 0;
        _encoder.TransactTime = 1;
        _encoder.PartiesCount(0);
        _encoder.SetAccount("A");

        var sink = string.Empty;
        var bytes = Allocation.BytesPerIteration(() =>
        {
            _header.Wrap(_buffer, 0, 0);
            _decoder.WrapForDecodeAndApplyHeader(_buffer, 0, _header);
            sink = _decoder.GetSymbol();   // allocates a string, every call
        });

        Assert.True(bytes > 0,
            "GetSymbol() should allocate; if it no longer does, the banned-members lint can be relaxed");
        Assert.NotEmpty(sink);
    }
}
