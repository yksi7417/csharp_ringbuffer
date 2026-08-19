using System;
using System.Runtime.InteropServices;
using System.Text;
using Org.SbeTool.Sbe.Dll;
using Probe.Sbe;

unsafe {
    // native slab -> true zero-copy, no GC pinning
    int cap = 4096;
    byte* slab = (byte*)NativeMemory.AllocZeroed((nuint)cap);
    var buf = new DirectBuffer(slab, cap);

    var hdr = new MessageHeader();
    var enc = new MarketDataIncrementalRefresh();
    enc.WrapForEncodeAndApplyHeader(buf, 0, hdr);
    enc.TransactTime = 1_700_000_000_000UL;
    var g = enc.MdEntriesCount(2);
    for (int i = 0; i < 2; i++) {
        var e = g.Next();
        e.MdEntryType = i == 0 ? MdEntryType.BID : MdEntryType.OFFER;
        e.SetSymbol("ESZ6    ");
        e.MdEntryPx.Mantissa = 5432 + i; e.MdEntryPx.Exponent = -2;
        e.MdEntrySize = 10 * (i + 1);
        var p = e.PartiesCount(1);
        p.Next().PartyRole = (byte)(7 + i);
        e.SetText(i == 0 ? "bid" : "ask");
    }
    int len = MessageHeader.Size + enc.Limit - MessageHeader.Size;
    len = enc.Limit;
    Console.WriteLine($"encoded bytes = {len}");

    // decode from the same memory, zero copy
    var dhdr = new MessageHeader(); dhdr.Wrap(buf, 0, 0);
    var dec = new MarketDataIncrementalRefresh();
    dec.WrapForDecodeAndApplyHeader(buf, 0, dhdr);
    Console.WriteLine($"templateId={dhdr.TemplateId} blockLength={dhdr.BlockLength} transactTime={dec.TransactTime}");
    var dg = dec.MdEntries;
    Console.WriteLine($"mdEntries count={dg.Count}");
    while (dg.HasNext) {
        var e = dg.Next();
        var parties = e.Parties;
        int role = -1; while (parties.HasNext) role = parties.Next().PartyRole;
        Console.WriteLine($"  {e.MdEntryType} {e.GetSymbol()} px={e.MdEntryPx.Mantissa}e{e.MdEntryPx.Exponent} sz={e.MdEntrySize} role={role} text={e.GetText()}");
    }
    // hex of first 32 bytes -> determinism check
    var sb = new StringBuilder();
    for (int i = 0; i < 32; i++) sb.Append(slab[i].ToString("x2"));
    Console.WriteLine("head32=" + sb);
    NativeMemory.Free(slab);
}
