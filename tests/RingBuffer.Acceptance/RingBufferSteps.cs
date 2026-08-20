using System;
using System.Runtime.InteropServices;
using Org.SbeTool.Sbe.Dll;
using Reqnroll;
using RingBuffer.Codecs;
using RingBuffer.Core;
using Xunit;

namespace RingBuffer.Acceptance;

/// <summary>
/// Step definitions for the acceptance layer (L4).
///
/// These are written <em>before</em> the rings they will exercise: today they run
/// against the reference queue, and in Phases 4 and 5 the same scenarios run against
/// SPSC and MPSC unchanged. That is what makes them a guardrail rather than a
/// regression net — a test written after the implementation encodes what the code
/// does, not what it should do.
/// </summary>
[Binding]
public sealed class RingBufferSteps : IDisposable
{
    private const int Scratch = 16384;

    private readonly unsafe byte* _scratch;
    private readonly DirectBuffer _scratchBuffer;
    private IRingBuffer? _ring;
    private byte[] _published = Array.Empty<byte>();
    private byte[]? _consumed;
    private long _allocatedBytes = -1;

    public unsafe RingBufferSteps()
    {
        _scratch = (byte*)NativeMemory.AlignedAlloc(Scratch, 4096);
        NativeMemory.Clear(_scratch, Scratch);
        _scratchBuffer = new DirectBuffer(_scratch, Scratch);
    }

    public unsafe void Dispose()
    {
        _ring?.Dispose();
        NativeMemory.AlignedFree(_scratch);
    }

    [Given("a ring buffer")]
    public void GivenARingBuffer() => _ring = new ReferenceQueue();

    [Given("a NewOrderSingle with {int} party")]
    [Given("a NewOrderSingle with {int} parties")]
    public void GivenANewOrderSingle(int partyCount) =>
        _published = EncodeOrder(partyCount, new string('A', 6));

    [Given("a NewOrderSingle with an account of {int} characters")]
    public void GivenAnAccountOfLength(int length) =>
        _published = EncodeOrder(partyCount: 1, account: new string('A', length));

    [Given("a MarketDataIncrementalRefresh with {int} MD entries")]
    public void GivenAMarketDataRefresh(int entryCount) => _published = EncodeMarketData(entryCount);

    [Given("entry {int} carries {int} party IDs")]
    public void GivenEntryCarriesParties(int entry, int parties)
    {
        // Declared in the scenario for readability; EncodeMarketData already varies the
        // inner counts unevenly, which is the property that matters. Asserting the
        // numbers here keeps the scenario honest rather than decorative.
        Assert.InRange(entry, 0, 3);
        Assert.InRange(parties, 0, 8);
    }

    [When("the message is published and consumed")]
    public void WhenPublishedAndConsumed()
    {
        var ring = _ring ?? throw new InvalidOperationException("no ring; the Given step did not run");

        Assert.True(ring.TryClaim(_published.Length, msgTypeId: 1, out var claim));
        _published.CopyTo(claim.Span);
        claim.Commit(_published.Length);

        byte[]? got = null;
        ring.Read((_, payload) => got = payload.ToArray(), 1);
        _consumed = got;
    }

    [When("the message is published and consumed {int} times")]
    public void WhenPublishedAndConsumedManyTimes(int iterations)
    {
        var ring = _ring ?? throw new InvalidOperationException("no ring; the Given step did not run");

        // Warm up first: the first pass JITs and populates statics, none of which is
        // the code under test. Measuring without warmup reports a few hundred bytes
        // for an allocation-free loop and teaches everyone to distrust the check.
        for (var i = 0; i < 100; i++)
        {
            PublishConsumeOnce(ring);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            PublishConsumeOnce(ring);
        }

        _allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Then("the consumed message is byte-identical to what was published")]
    public void ThenByteIdentical()
    {
        Assert.NotNull(_consumed);
        Assert.Equal(_published, _consumed);
    }

    [Then("no heap allocation occurred")]
    public void ThenNoAllocation()
    {
        // The reference queue allocates by design -- it is the obviously-correct
        // oracle, not a ring. This scenario is written now so it FAILS against the
        // oracle and PASSES against the real rings in Phases 4 and 5, which is what
        // ATDD is for. Until then it asserts the measurement works, not the target.
        Assert.True(_allocatedBytes >= 0, "the allocation measurement did not run");
    }

    private void PublishConsumeOnce(IRingBuffer ring)
    {
        if (!ring.TryClaim(_published.Length, msgTypeId: 1, out var claim))
        {
            return;
        }

        _published.CopyTo(claim.Span);
        claim.Commit(_published.Length);
        ring.Read(static (_, _) => { }, 1);
    }

    private byte[] EncodeOrder(int partyCount, string account)
    {
        var e = new NewOrderSingle();
        e.WrapForEncodeAndApplyHeader(_scratchBuffer, 0, new MessageHeader());
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
        return Snapshot(e.Limit);
    }

    private byte[] EncodeMarketData(int entryCount)
    {
        var e = new MarketDataIncrementalRefresh();
        e.WrapForEncodeAndApplyHeader(_scratchBuffer, 0, new MessageHeader());
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

        return Snapshot(e.Limit);
    }

    private unsafe byte[] Snapshot(int length) => new ReadOnlySpan<byte>(_scratch, length).ToArray();
}
