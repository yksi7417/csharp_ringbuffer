---
type: Decision
title: "D6: Volatile/Interlocked on unmanaged counters, with an ARM64 CI leg"
description: The .NET memory model is not Java's, and x86-TSO hides the bugs. CI must include ARM64.
tags: [concurrency, memory-model, ci, correctness]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

.NET's memory model differs from Java's in ways that bite exactly here, and the counters live
in unmanaged memory so the ordinary tools do not apply. See
[the .NET memory model concept](/concepts/dotnet-memory-model.md).

# Decision

- **`Volatile.Read` / `Volatile.Write`** for acquire/release — **not** a `volatile`
  field, because the counters are reached through `Unsafe.AsRef<long>(ptr)` and a pointer
  deref cannot carry the modifier.
- **`Interlocked.CompareExchange(ref Unsafe.AsRef<long>(ptr), next, current)`** for the
  tail CAS.
- **CI runs an ARM64 leg on GitHub Actions**, in addition to x64.

# Consequences

- **x86-TSO hides missing barriers; ARM64 does not.** A suite that only runs on x64 will pass
  green while the code is genuinely broken on Apple Silicon and Graviton. This is a real bug
  class, not a hypothetical, and it is invisible to code review.
- [Coyote](/testing/l3-concurrency.md) explores **interleavings**; it does **not** model weak
  memory. The ARM64 leg covers weak memory; it does not explore interleavings. **Neither
  alone is sufficient** and the project runs both.
- GitHub Actions ARM64 runners are confirmed in scope by review. See
  [R4](/risks/index.md).
