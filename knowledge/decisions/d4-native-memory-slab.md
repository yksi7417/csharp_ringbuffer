---
type: Decision
title: "D4: Back the ring with off-heap NativeMemory, 4096-byte aligned"
description: AlignedAlloc rather than a pinned byte[] or a memory-mapped file.
tags: [memory, zero-copy, performance]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

The ring's data region has to live somewhere the GC will not move it while a raw pointer
points into it. Three candidates: a pinned `byte[]`, `NativeMemory`, or a
`MemoryMappedFile`.

# Decision

**`NativeMemory.AlignedAlloc`, aligned to 4096 bytes.**

# Consequences

- Off-heap: immune to GC compaction, never scanned, **no pin to hold**. A pinned `byte[]`
  works but fragments the heap and keeps a `GCHandle` alive for the buffer's lifetime.
- 4096-byte alignment lets us guarantee cache-line placement of the trailer counters, which
  [D5](d5-cache-line-padding.md) depends on.
- This is only reachable because `DirectBuffer` accepts a raw pointer without taking a GC
  handle — see [F4](/findings/f4-directbuffer-native-pointer.md). That finding is what makes
  this decision possible at all.
- The buffer owns unmanaged memory, so it needs `IDisposable` and a finalizer, and a test
  that double-dispose is safe.
- Memory-mapped backing is deferred to [D10](d10-cross-process-deferred.md), not rejected.
