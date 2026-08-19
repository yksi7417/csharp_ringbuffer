# Checkpoint

Where the implementation stands. Updated in the same commit as the task it records.

## Position

**Phase 0 — Toolchain and gate. Task 0.1 is next.**

Nothing in [`PLAN.md`](PLAN.md) is claimed yet. The repository contains the
[knowledge bundle](../knowledge/index.md), the OKF validator, and this plan.

## Next unblocked task

**0.1 — `scripts/bootstrap.sh`.** No prerequisites.

## Progress

| Phase | Done | Total |
|---|---|---|
| 0 — Toolchain and gate | 0 | 13 |
| 1 — Schemas and codecs | 0 | 11 |
| 2 — Ring algebra | 0 | 9 |
| 3 — ATDD scaffolding | 0 | 11 |
| 4 — SPSC ring | 0 | 15 |
| 5 — MPSC, concurrency, ARM64 | 0 | 11 |
| 6 — Triangulation and corpus | 0 | 6 |
| 7 — Performance | 0 | 6 |
| 8 — Teaching artifacts | 0 | 10 |
| **Total** | **0** | **92** |

## Verified before planning

These were executed in a container, not assumed. They are why the plan commits to a specific
API shape rather than sketching one:

| Claim | Where |
|---|---|
| SBE 1.39.0 generates C# with nested groups, via the shim | [F2](../knowledge/findings/f2-csharp-codegen-requires-shim.md), [F5](../knowledge/findings/f5-end-to-end-roundtrip-proof.md) |
| `NativeMemory.AlignedAlloc` gives a 4096-aligned off-heap slab | probe, feeds 4.1 |
| `Interlocked.CompareExchange(ref Unsafe.AsRef<long>(ptr), …)` works on unmanaged memory | probe, feeds 5.2 |
| A `Claim` ref struct exposes a `Span` pointing into the slab | probe, feeds 3.2 |
| Zero copy is assertable as address identity, via `Unsafe.AsPointer(ref MemoryMarshal.GetReference(span))` | probe, feeds 4.5 |

Note for 3.2/4.5: `&claim.Span[0]` **does not compile** — you cannot take the address of a
`ref struct` property. Use `MemoryMarshal.GetReference`. Found the hard way; recorded so the
next person does not.
