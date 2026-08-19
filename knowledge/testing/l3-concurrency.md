---
type: Test Layer
title: "L3: Concurrency tests"
description: Systematic interleaving exploration with Coyote, plus long-running stress.
tags: [testing, concurrency, coyote]
resource: tests/RingBuffer.Concurrency
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Coyote

[Microsoft Coyote](https://microsoft.github.io/coyote/) takes over thread scheduling and
explores interleavings **systematically**, then replays any failure deterministically.

Targets:

- the MPSC CAS loop under contention
- claim/commit interleaved across producers
- the wrap-while-consuming race

Deterministic replay of a concurrency failure is the whole value. A stress test that fails
once in ten million runs is a rumour; a Coyote trace is a bug report.

# Stress

Separately: N producers × M messages, asserting every message arrives **exactly once** with
an intact payload.

Stress finds different bugs than Coyote — it runs the real scheduler on real cores, at real
speed, with real cache behaviour. It belongs in the **nightly** lane where a long run is
affordable and a slow feedback loop does not matter.

# What this layer does NOT cover

**Weak memory ordering.** Coyote schedules threads; it does not model store reordering. A
missing `Volatile.Write` passes every Coyote test.

That gap is covered by the **ARM64 CI leg** ([D6](/decisions/d6-memory-model-and-arm64.md)),
and the two are not substitutes. See
[the memory model concept](/concepts/dotnet-memory-model.md) for the full division of labour.

Recording this explicitly, because "we have concurrency tests" is exactly the sentence that
would later justify dropping the ARM64 leg as redundant.
