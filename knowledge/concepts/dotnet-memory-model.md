---
type: Concept
title: The .NET memory model, as it applies to unmanaged counters
description: Why volatile fields do not help here, and why x64 CI is not enough.
tags: [concurrency, memory-model, correctness]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The problem with the obvious tools

The ring's counters live in **unmanaged memory**, reached through
`Unsafe.AsRef<long>(ptr)`. That rules out the tool most people reach for:

- **`volatile` field modifier — unusable.** It applies to a field declaration. A pointer
  dereference is not a field, so there is nothing to mark.
- **`Volatile.Read` / `Volatile.Write` — use these.** They take a `ref`, so
  `Volatile.Write(ref Unsafe.AsRef<long>(ptr), value)` works.
- **`Interlocked.CompareExchange(ref Unsafe.AsRef<long>(ptr), next, current)`** for the
  tail CAS.

# Why x64 CI is not enough

**x86-TSO does not reorder store-store or load-load.** A missing `Volatile.Write` on the
commit step ([the publication protocol](claim-commit-protocol.md)) is therefore a **no-op bug
on x64**: the code is wrong, and every test passes.

On ARM64 the same code reorders and the consumer reads a committed record with a
half-written payload.

So a green x64 suite is not evidence of correct ordering. It is evidence that the tests ran
on hardware that cannot expose the bug. This is why
[D6](/decisions/d6-memory-model-and-arm64.md) mandates an ARM64 CI leg — Apple Silicon and
Graviton are both ordinary deployment targets.

# Two tools, two different failure modes

| Tool | Finds | Misses |
|---|---|---|
| [Coyote](/testing/l3-concurrency.md) | bad **interleavings** — the CAS loop losing an update, a wrap racing a consume | weak memory; it schedules threads, it does not model reordering |
| ARM64 CI leg | missing **barriers** | interleavings that the run happened not to hit |

**Neither alone is sufficient.** The project runs both, and the reason is recorded here so
nobody later removes one as redundant.
