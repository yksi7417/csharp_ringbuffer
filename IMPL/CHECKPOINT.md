# Checkpoint

Where the implementation stands. Updated in the same commit as the task it records.

## Position

**Phase 0 complete. Phase 1 in progress — 6 of 11 done. Task 1.7 is next.**

Schemas generate codecs that round-trip nested repeating groups through off-heap memory, and
`gate.sh full` is green with 10 steps passing. Only `conformance` is still N/A, waiting on the
Phase 3 corpus.

## Next unblocked task

**1.7 — `schemas/fix-sbe-v2.xml`**, appending a field and a group to v1. Needs 1.3 (done).
Then 1.8 (evolution test), 1.9–1.10 (zero-allocation harness and assertions), 1.11
(banned-members lint).

## Progress

| Phase | Done | Total |
|---|---|---|
| 0 — Toolchain and gate | **13** | 13 |
| 1 — Schemas and codecs | **6** | 11 |
| 2 — Ring algebra | 0 | 9 |
| 3 — ATDD scaffolding | 0 | 11 |
| 4 — SPSC ring | 0 | 15 |
| 5 — MPSC, concurrency, ARM64 | 0 | 11 |
| 6 — Triangulation and corpus | 0 | 6 |
| 7 — Performance | 0 | 6 |
| 8 — Teaching artifacts | 0 | 10 |
| **Total** | **19** | **92** |

## CI

[Run 1](https://github.com/yksi7417/csharp_ringbuffer/actions/runs/32278212505) — **both legs
green**, `fast on x64` and `fast on arm64`, the latter on a real `ubuntu-24.04-arm` runner.
Bootstrap works on both architectures.

**This does not retire [R4](../knowledge/risks/risk-register.md).** The ARM64 leg existing and
running is confirmed; that it *catches* a weak-memory bug is not, and cannot be until there is
concurrent code to break. Task **5.11** is what retires R4: remove a `Volatile.Write` and
require it to go green on x64 and red on ARM64. Marking R4 retired now would be exactly the
mistake [green gate is evidence, not proof](../knowledge/practices/green-gate-is-evidence.md)
warns about.

🟡 **0.2** remains unverified for the same reason: a SessionStart hook can only be confirmed
from a fresh session.

## Guards proven failing, not just passing

Per [green gate is evidence, not proof](../knowledge/practices/green-gate-is-evidence.md), a
check nobody has watched fail is not yet a check. These were each broken deliberately and
observed going red:

| Guard | Broken by | Result |
|---|---|---|
| `okf-validate` | missing `type`, broken link, orphaned concept, frontmatter in a non-root index, non-ISO log heading, bad `status` | 6/6 caught |
| `vendored-sbe` | editing a vendored file; adding an unlisted file | both caught, exit 1 |
| `no-lock` | a `lock` statement in `src/` | caught, exit 1 |
| `deferred-work` | orphan `DEFERRED:` marker; entry missing **Why** | both caught |
| `dispatch` (TRAP-3) | a lane listing a step with no `step_` function | caught, exit 1 |

## Found while building Phase 1

- **A build succeeded having compiled none of its generated sources.** TRAP-7, and the most
  instructive failure so far — two independent faults each sufficient to produce a convincing
  green: MSBuild evaluates the `Compile` glob before `BeforeCompile` targets run, and the
  generator writes into a namespace subdirectory that a non-recursive glob missed. The
  generator was working the whole time. Guard: `EnableDefaultCompileItems` off, sources added
  inside the target, and an `Error` when the item list is empty — verified by deleting the
  sources and watching the build go red.
- **The codegen-freshness check could not be a `git status` check.** Generated sources are
  gitignored, and git does not report ignored files, so the check would have passed
  *unconditionally* — worse than a check that misses a case. Replaced with a
  hash / regenerate / compare in `codegen_fresh.sh`, verified failing on both a changed schema
  and a hand-edited `.g.cs`.
- **`--` is illegal inside an XML comment**, and I hit it twice in `.csproj`/`.props` files
  before scrubbing them all. Cheap to fix, confusing to diagnose: MSBuild reports it as
  "project file could not be loaded".

## Found while building Phase 0

- **`deferred_work.py` failed on its first run** — correctly. Trap ids and deferred-work ids
  were both `T-n`, so a documentation reference to trap `T-4` was indistinguishable from a
  live marker. Traps are now `TRAP-n`. Recorded as TRAP-6 in [`docs/TRAPS.md`](../docs/TRAPS.md).
- **The pre-push hook earned itself on its first use.** It blocked the Phase 0 push: writing
  TRAP-6 re-tripped the very check it describes, because documenting a marker means writing
  one. The scanner cannot tell prose about a marker from a marker, so the fix is an authoring
  rule rather than a loosened check — loosening it would let a real orphaned marker in a
  `.md` file through.
- **`shellcheck` found four real defects** on its first run (unchecked `cd`, unquoted `case`
  patterns). Fixed; the step is clean.
- **Vendored runtime is 1,851 lines across 7 files** and builds on net8.0 with **0 warnings**,
  exactly as [F3](../knowledge/findings/f3-sbe-dll-nuget-stale.md) predicted.
- **`N/A` had to be distinct from `PASS`.** A gate whose steps mostly have no subject yet
  would otherwise report a full green over an empty tree. Recorded as TRAP-5.

## Verified before planning

Executed in a container, not assumed:

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
