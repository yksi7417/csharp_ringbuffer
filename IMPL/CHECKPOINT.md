# Checkpoint

Where the implementation stands. Updated in the same commit as the task it records.

## Position

**Phases 0–3 complete. Phase 4 — the SPSC ring. Task 4.1 is next.**

`gate.sh full` is green with **14 steps and zero N/A** — the first run in which every step has
a subject. 76 tests pass across three suites, plus 5 conformance cases byte-for-byte.

The acceptance suite exists **before the rings it will exercise**, which is the whole point of
the ATDD ordering: a test written after the implementation encodes what the code does, not
what it should do.

## Next unblocked task

**4.1 — slab allocation** (`NativeMemory.AlignedAlloc`, 4096-aligned, power-of-two capacity).

Phase 4 builds the real SPSC ring against the algebra Phase 2 proved and the corpus Phase 3
committed. When it lands, `--ring spsc` joins the harness and the same 5 cases run against
it unchanged.

### Regression evidence — `cb5de8d`, 2026-08-20T17:42:04Z (working tree dirty)

| Suite | Result |
|---|---|
| `RingBuffer.Acceptance.dll` | 8 passed, 0 failed |
| `RingBuffer.Codecs.Tests.dll` | 9 passed, 0 failed |
| `RingBuffer.Core.Tests.dll` | 59 passed, 0 failed |
| **Total** | **76 passed, 0 failed** |
| `gate.sh full` | full GREEN -- 14 passed, 0 not yet applicable (25s) |

Produced by `scripts/ci/evidence.sh`, which exits non-zero if anything is red — it cannot be
used to record a green checkpoint over a broken tree. It has caught two real failures so far,
both times with every test green and a gate step red.

**Every checkpoint from here carries this block.** See
[the implementation loop](../knowledge/practices/implementation-loop.md).

## Progress

| Phase | Done | Total |
|---|---|---|
| 0 — Toolchain and gate | **13** | 13 |
| 1 — Schemas and codecs | **11** | 11 |
| 2 — Ring algebra | **9** | 9 |
| 3 — ATDD scaffolding | **11** | 11 |
| 4 — SPSC ring | 0 | 15 |
| 5 — MPSC, concurrency, ARM64 | 0 | 11 |
| 6 — Triangulation and corpus | 0 | 6 |
| 7 — Performance | 0 | 6 |
| 8 — Teaching artifacts | 0 | 10 |
| **Total** | **44** | **92** |

## CI

| Run | Result |
|---|---|
| [1](https://github.com/yksi7417/csharp_ringbuffer/actions/runs/32278212505) | green, both legs, ARM64 on a real `ubuntu-24.04-arm` runner |
| [2](https://github.com/yksi7417/csharp_ringbuffer/actions/runs/32279590709) | **red, both legs, two different causes** — see TRAP-8 and TRAP-9 |
| 3 | fix pushed in `7eb634e`; awaiting result |

Run 2 is the important one. It went red **after a green local gate**, which is exactly what
[one gate command](../knowledge/practices/one-gate-command.md) promises cannot happen — and
the promise was never false, only narrower than its wording. The runners carry .NET SDK
10.0.302; this container carries 8.0.130, and VSTest is removed in 10. Same script, same repo,
opposite result. Now pinned by `global.json` + `actions/setup-dotnet`, with `bootstrap.sh`
checking the SDK **major version** rather than mere presence.

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

## Found while building Phase 3

- **The lint found a layout error, not just an allocation.** `banned-members` flagged
  allocations in `RingBuffer.Replay` — which is test apparatus, and which
  [the plan](PLAN.md) always placed under `tests/`. I had put it in `src/` by reflex. Moving
  it made the lint pass with no exemption at all, which is the right shape: the rule did not
  need loosening, the layout needed correcting.
- **Availability is not compatibility.** [F6](../knowledge/findings/f6-package-availability.md)
  listed `xunit.v3` and `Reqnroll.xUnit` as both available, tacitly assuming they compose.
  They do not — Reqnroll pins xUnit v2, and the failure surfaces as
  `CS0103: The name 'Assert' does not exist`, which points at the symbol rather than the
  version mismatch underneath. Recorded as [F7](../knowledge/findings/f7-reqnroll-pins-xunit-v2.md).
- **The differ was verified against a corrupted fixture**, not just a matching one. Flipping
  one byte produced `NewOrderSingle.orderQty (uint32) at block offset 30, byte 0 of 4` with
  the differing byte bracketed in a hex dump of both sides. That is the difference between a
  corpus people use and a corpus people mute.
- **The evidence script caught a real regression on its first working checkpoint.** All 59
  tests were green while `banned-members` was red — exactly the gap between "my change is
  fine" and "the tree is whole" that the separate script exists to close.
- **The compiler enforced the `ref struct` guarantee on me.** `Assert.Throws(() => claim.Commit(5))`
  does not compile: CS8175, a `ref` local cannot be captured by a lambda. So a `Claim`
  genuinely cannot outlive its stack frame, and cannot become a pointer into memory the
  consumer has already reclaimed. The test now uses try/catch and says why.
- **The reference queue's limits are written down, not assumed.** It is an oracle for
  *payload preservation* only. It has no wrap, no padding and no capacity bound, so it can
  never disagree with a real ring about those — those are pinned by the Phase 2 algebra tests
  instead. Claiming more for it would be the most tempting way to get false confidence out of
  triangulation.

## Found while building Phase 2

- **The exhaustive test disproved part of the design.**
  [D1](../knowledge/decisions/d1-claim-commit-with-padding.md) described three leftover cases
  and singled out the sub-header one as "the one that will be got wrong". It is **unreachable
  while `Alignment >= HeaderLength`** — both record lengths are multiples of `Alignment`, so
  their difference is too. There are two cases, not three. The bundle is corrected and the
  constant relation is now asserted at runtime rather than assumed. This is Phase 2 doing
  exactly its job: the cheapest possible place to learn a design was more complicated than it
  needed to be.
- **A hand-written test case was wrong where the exhaustive one was right.** My
  `[InlineData(16, 0, 16, 8)]` should have been `(8, 16)`; the 8-million-pair sweep passed
  while my hand-picked case failed. Kept in the file with a comment, because it is the
  argument for exhaustiveness in miniature.
- **`banned-members` was too blunt and would have taught people to ignore it.** It flagged
  six string interpolations inside exception messages — the throw path, which is not the hot
  path by definition. Demanding an exemption marker on each would train people to sprinkle
  `ALLOW-ALLOC`, which is how a ban quietly stops meaning anything. The rule now exempts
  `throw new ...;` spans, computed over the whole file: my first attempt tracked parens
  incrementally and got multi-line throws wrong.

## Found while building Phase 1

- **A green local gate does not imply a green CI lane unless the toolchain is pinned.**
  TRAP-8, and the most valuable lesson so far: a version that is not pinned is a variable, and
  a variable in a guardrail is a hole.
- **SBE "append-only" is per section, not per file.** A new field must go at the end of the
  *field block*, a new group after the existing groups, and data stays last — the parser
  rejects `field node specified after group or data node`. Growing the field block is exactly
  what `blockLength` and `actingVersion` exist to absorb.
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
- **`--` is illegal inside an XML comment**, and I hit it three times — `.csproj`, `.props`,
  and an SBE schema. Cheap to fix, expensive to diagnose, because no error names the cause.
  Now caught by the `xml-wellformed` gate step (TRAP-10).
- **The zero-allocation harness is not vacuous**, and that was checked: span overloads measure
  exactly 0 bytes per iteration, while `GetSymbol()` measures greater than 0 in the same
  harness. A check that cannot distinguish the two would pass either way.

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
