---
type: Component
title: RingBuffer.Core
description: The SPSC and MPSC ring buffers behind a shared IRingBuffer contract.
tags: [ring-buffer, component, core]
resource: src/RingBuffer.Core
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: draft
---

# Status

**Not yet implemented.** This describes the intended shape, agreed at review.

# The contract

```csharp
public interface IRingBuffer : IDisposable
{
    int Capacity { get; }
    bool TryClaim(int maxLength, out Claim claim);   // D1, D3: never blocks
    int Read(MessageHandler handler, int messageCountLimit);
}

public readonly ref struct Claim
{
    public Span<byte> Span { get; }     // points INTO the ring slab -- D4
    public void Commit(int actualLength);  // D1: writes true length, pads leftover
    public void Abort();                   // whole claim becomes a padding record
}
```

`Claim` is a `ref struct` so it **cannot escape to the heap**. A claim that outlived its
stack frame would be a pointer into a region the consumer may already have reclaimed; the
compiler prevents it rather than a code review.

# Two implementations

| | `OneToOneRingBuffer` (SPSC) | `ManyToOneRingBuffer` (MPSC) |
|---|---|---|
| Tail update | plain store-release | `Interlocked.CompareExchange` loop |
| Head cache | not needed | required |
| Teaching role | the on-ramp | the real thing |

Both are exercised by **the same conformance suite**, which is what makes `IRingBuffer` a
contract rather than a shape ([D2](/decisions/d2-spsc-and-mpsc.md)).

# Invariants

These hold at all times and are asserted as properties in [L2](/testing/l2-property.md):

- `head <= tail`
- `tail - head <= capacity`
- capacity is a power of two
- every committed record's length is 8-byte aligned
- consumed bytes always equal published bytes

# Related

- [Record and trailer layout](/concepts/ring-buffer-record-layout.md)
- [The publication protocol](/concepts/claim-commit-protocol.md)
- [Padding records](/concepts/padding-records.md)
