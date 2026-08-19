---
type: Finding
title: "F4: DirectBuffer wraps a raw pointer with no GC handle"
description: The API that makes the zero-copy claim reachable rather than aspirational.
tags: [sbe, zero-copy, memory, critical]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What was observed

The decisive API in `DirectBuffer.cs`:

```csharp
public DirectBuffer(byte* pBuffer, int bufferLength)
public void Wrap(byte* pBuffer, int bufferLength)   // sets _needToFreeGCHandle = false
```

It wraps a raw pointer with **no GC handle and no pinning**. `Wrap(byte[])` also exists but
pins via `GCHandle.Alloc(..., GCHandleType.Pinned)`, which we avoid.

Generated accessors are span-based where it matters:

```csharp
public ReadOnlySpan<byte> Symbol { get; }     // decode, no allocation
public Span<byte> SymbolAsSpan();             // encode, no allocation
public int SetText(ReadOnlySpan<byte> src);   // var-length, no allocation
public string GetText();                      // convenience -- ALLOCATES
```

# Consequence

- The ring slab can be off-heap `NativeMemory` — never moved, never scanned, no pin held.
  This is what makes success criteria S1 (zero copy) and S2 (zero allocation) achievable
  rather than aspirational, and it is the basis of [D4](/decisions/d4-native-memory-slab.md).
- **The ergonomic call is the allocating one.** `GetText()` reads better than
  `GetText(Span<byte>)` and will be reached for by reflex. The hot path uses the span
  overloads; the `string` overloads are for tests and diagnostics only, and that needs a
  lint rule on banned members rather than a code-review habit. Tracked as
  [R5](/risks/index.md).
