---
type: Finding
title: "F5: The full chain works end to end"
description: Schema to shim to C# codecs to .NET 8 to encode into native memory to decode from the same address.
tags: [sbe, zero-copy, proof, critical]
resource: /references/evidence/RoundTripProof.cs
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What was run

A complete round trip, executed rather than reasoned about:

**schema → shim → C# codecs → compiled on .NET 8 against the vendored runtime → encode into
`NativeMemory` → decode from the same address.**

The message exercised a nested repeating group (`parties` inside `mdEntries`), a
variable-length `text` field, a `Decimal64` composite, a char-array `Symbol` and an enum.

# Output

```
encoded bytes = 96
templateId=1 blockLength=8 transactTime=1700000000000
mdEntries count=2
  BID   ESZ6     px=5432e-2 sz=10 role=7 text=bid
  OFFER ESZ6     px=5433e-2 sz=20 role=8 text=ask
head32=08000100010000000068e5cf8b0100001a0002003045535a3620202020381500
```

Reproduction steps and the three probe files are in
[the evidence directory](/references/evidence/index.md).

# Consequence

**The hardest technical risk in the project was retired before implementation started.**

Every downstream decision — [D1](/decisions/d1-claim-commit-with-padding.md),
[D4](/decisions/d4-native-memory-slab.md), [D8](/decisions/d8-schema-selection.md) — rests on
this chain working. It does.

The `head32` hex string is also the first evidence that the wire format is
**byte-deterministic**, which is the premise of the
[replay corpus](/architecture/conformance-corpus.md).
