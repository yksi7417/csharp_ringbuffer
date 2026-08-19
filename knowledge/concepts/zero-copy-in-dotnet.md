---
type: Concept
title: What "zero copy" actually means here
description: The specific claim being made, how it is verified, and the ways it is easy to lose.
tags: [zero-copy, memory, performance, core]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The claim

**One write of the message bytes, at their final address. One read, from that same address.**

The producer's SBE encoder writes fields directly into the ring slab. The consumer's decoder
reads them from the same memory. There is no intermediate `byte[]`, no
serialize-then-publish step, and no copy on consume.

# How it is verified, not asserted

- **S1 (zero copy):** a test compares the address the encoder wrote to against
  `slab base + claimed offset`. Identity, not similarity.
- **S2 (zero allocation):** `GC.GetAllocatedBytesForCurrentThread()` deltas around a
  steady-state publish/consume loop must be **exactly 0**, plus BenchmarkDotNet
  `MemoryDiagnoser` showing Gen0 = 0.

Allocation count is deterministic, unlike latency, so it can **block a PR** without being
flaky. It is gated hard in the fast lane.

# Three ways to lose it by accident

1. **Reaching for the ergonomic overload.** `GetText()` allocates a string;
   `GetText(Span<byte>)` does not. The nicer-looking call is the wrong one. See
   [F4](/findings/f4-directbuffer-native-pointer.md).
2. **Boxing a struct** by passing it through an interface or a non-generic delegate. A
   message handler typed `Action<object>` silently allocates per message.
3. **Capturing a closure** in the consume loop. A lambda that captures a local becomes a heap
   allocation per call.

All three are invisible in review and immediately visible to the allocation gate. This is why
the gate exists rather than a coding guideline.

# What zero copy does *not* mean

It does not mean the data is never in two places. The CPU will pull it into cache, and on a
cross-process ring the OS maps it into two address spaces. The claim is about **software
copies on the data path**, not about physical uniqueness.
