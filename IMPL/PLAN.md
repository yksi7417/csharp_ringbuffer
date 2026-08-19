# Implementation plan

Nine phases, 92 tasks, plus one deferred. Every design question is settled — see
[decisions](../knowledge/decisions/index.md). This is sequencing, not design.

**How to work this plan:** [the implementation loop](../knowledge/practices/implementation-loop.md).
**Current position:** [`CHECKPOINT.md`](CHECKPOINT.md).

---

## How this is ordered, and why

Three principles decide the sequence, and they sometimes fight:

**1. The gate exists before the code it gates (Phase 0).** A quality bar retrofitted onto
existing code grandfathers in whatever is already broken. Building `gate.sh` first costs a
phase and means no code ever enters the repo ungated.

**2. Risk is retired as early as it can be.** [R3](../knowledge/risks/risk-register.md) — the
[D1](../knowledge/decisions/d1-claim-commit-with-padding.md) padding arithmetic — is the one
part of the design with no reference implementation to check against.

So **Phase 2 extracts that arithmetic into pure functions with no ring, no memory and no
threads**, and tests them exhaustively. `PaddingPlan.For(claimed, actual)` is a total function
over two ints; every input up to a bound can be checked in under a second. R3 is retired
before a single byte is allocated.

This is the highest-leverage decision in the plan. The alternative — meeting the boundary
cases inside a concurrent buffer, where a failure appears far from its cause — is how this
project would lose a week.

**3. Acceptance scaffolding precedes the components it exercises (Phase 3, ATDD).**

That creates a bootstrapping problem: `expected.sbe` fixtures are produced *by* the harness,
which needs a ring, which is what we are trying to test. The resolution is to build the
**trivially-correct `List<byte[]>` reference queue first** (3.3). It is obviously right by
inspection, so it is a legitimate oracle for *payload preservation*, and it lets the corpus
exist before either real ring does.

**Its limits, stated plainly:** the reference queue has no wrap, no padding and no capacity
bound, so it cannot be an oracle for ring *semantics*. Those are pinned by Phase 2's exhaustive
algebra tests and Phase 4's unit tests. The queue also becomes the third leg of
[triangulation](../knowledge/practices/triangulation.md) for free.

**Sizes:** S ≈ under an hour, M ≈ half a day, L ≈ a day or more.

---

## Phase 0 — Toolchain and gate

Nothing here is product code. The point is that everything after it lands gated.

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| ✅ 0.1 | `scripts/bootstrap.sh` — apt SDK, JDK check, jar fetch | Runs clean in a fresh container; **does not** call `dotnet-install.sh` ([F1](../knowledge/findings/f1-dotnet-install-egress-blocked.md)) | — | M |
| 🟡 0.2 | `SessionStart` hook invoking bootstrap | A fresh web agent session can `dotnet build` with no manual steps | 0.1 | S |
| ✅ 0.3 | Solution + project skeleton, `Directory.Build.props` | `AllowUnsafeBlocks`, `Nullable`, `TreatWarningsAsErrors`, `LangVersion` set once centrally | 0.1 | M |
| ✅ 0.4 | Vendor SBE runtime at the pinned tag into `src/RingBuffer.Sbe/` | 7 files, 1,851 lines, compiles on net8.0 ([F3](../knowledge/findings/f3-sbe-dll-nuget-stale.md)) | 0.3 | M |
| ✅ 0.5 | `check_vendored_sbe.sh` — re-fetch at tag, assert byte-identical | Editing a vendored file fails the check | 0.4 | M |
| ✅ 0.6 | `scripts/ci/gate.sh` — lanes, `--list`, colour, timing | `--list` prints steps without running them | 0.3 | L |
| ✅ 0.7 | **A lane step with no dispatch case fails the gate** | A deliberately undispatched step goes red, not green ([TRAP-3](../knowledge/practices/trap-log.md)) | 0.6 | S |
| ✅ 0.8 | Wire `okf_validate.py` as the `okf-validate` step | Breaking the bundle fails `gate.sh fast` | 0.6 | S |
| ✅ 0.9 | `deferred_work.py` + seed `docs/TODO.md` | Bidirectional: orphan `DEFERRED:` marker fails; entry missing **Why**/**Done when** fails | 0.6 | M |
| ✅ 0.10 | Seed `docs/TRAPS.md` with TRAP-1..TRAP-4 | Inherited traps recorded with their guards | — | S |
| ✅ 0.11 | `.githooks/pre-push` → `gate.sh fast` | Pushing with a red gate is blocked locally | 0.6 | S |
| ✅ 0.12 | GitHub Actions: **x64 + ARM64 matrix** | Both legs green on an empty tree ([D6](../knowledge/decisions/d6-memory-model-and-arm64.md)) | 0.6 | M |
| ✅ 0.13 | `no-lock` source scan step | Adding a `lock` anywhere in `src/` fails the gate ([D3](../knowledge/decisions/d3-non-blocking-backpressure.md)) | 0.6 | S |

**Phase 0 exit — met locally on 2026-08-19.** `fast`, `full` and `nightly` are all green on a
tree containing no product code. Steps whose subject does not exist yet report `N/A`, counted
separately from passes, never as green (TRAP-5).

Still unproven: the **ARM64 leg**, which cannot run in this container — it needs a real
`ubuntu-24.04-arm` runner. The workflow is committed and its YAML validates; whether the leg
is genuinely green is the first thing to confirm on GitHub. 🟡 0.2 is configured and its
script verified idempotent, but "a fresh session builds with no manual steps" can only be
observed from a fresh session.

---

## Phase 1 — Schemas and codecs

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| ✅ 1.1 | `tools/sbe-csharp-gen/` — productionise the shim | Builds from source; **not** `-Dsbe.target.language` ([F2](../knowledge/findings/f2-csharp-codegen-requires-shim.md)) | 0.4 | M |
| ✅ 1.2 | `scripts/generate-codecs.sh` | Generates into `src/RingBuffer.Codecs/`; stderr **never** discarded ([TRAP-1](../knowledge/practices/trap-log.md)) | ✅ 1.1 | S |
| ✅ 1.3 | `schemas/fix-sbe.xml` v1 | `NewOrderSingle` + `NoPartyIDs`; `MarketDataIncrementalRefresh` + **nested** group + var-data ([D8](../knowledge/decisions/d8-schema-selection.md)) | ✅ 1.2 | M |
| ✅ 1.4 | Codegen wired into the build | `dotnet build` regenerates before compile | ✅ 1.3 | M |
| ✅ 1.5 | `codegen-clean` gate step | Uses `git status --porcelain` **including untracked** ([TRAP-2](../knowledge/practices/trap-log.md)) | ✅ 1.4 | S |
| ✅ 1.6 | Generator test: generated code **compiles and round-trips** | Not "files appeared" — a generator that writes nothing exits 0 | ✅ 1.4 | M |
| ✅ 1.7 | `schemas/fix-sbe-v2.xml` — appends a field and a group | v2 differs from v1 by addition only | ✅ 1.3 | S |
| ✅ 1.8 | Schema-evolution test: v2 codec decodes v1 bytes | Absent fields return their null value via `actingVersion` | ✅ 1.7 | M |
| ✅ 1.9 | Zero-allocation test harness | `GC.GetAllocatedBytesForCurrentThread()` delta asserted **exactly 0** | 0.3 | M |
| ✅ 1.10 | Zero-alloc assertion over codec encode/decode | Span overloads allocate 0; documents that `string` overloads do not | ✅ 1.9 | M |
| ✅ 1.11 | `banned-members` lint (`GetText()`, `GetSymbol()`, …) | Using a `string` overload in `src/` fails ([R5](../knowledge/risks/risk-register.md)) | ✅ 1.10 | M |

**Phase 1 exit — met on 2026-08-19.** Both schemas generate; 9 tests pass covering
round-trip, nested groups with uneven inner counts, byte-determinism, empty groups, schema
evolution, and zero allocation. `gate.sh full` is green with 12 steps passing.

Two gate steps were added that the plan did not anticipate, both because a check turned out to
be unable to fail as specified: `xml-wellformed` (TRAP-10) and `banned-members` moved earlier
than planned to guard the allocation result while it is fresh. The `codegen-clean` step is
implemented as a content hash rather than `git status`, for the reason in TRAP-7.

---

## Phase 2 — Ring algebra (pure functions) — retires R3

No memory, no threads, no ring. Total functions over integers, tested exhaustively.
**This is where the project's main design risk is eliminated.**

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| ✅ 2.1 | `RecordHeader` encode/decode | Length and type round-trip; negative length reads as uncommitted | 0.3 | S |
| ✅ 2.2 | `Align.To8(int)` | Exhaustive over `0..64K`; `Align(x) >= x`, `Align(x) % 8 == 0`, idempotent | 0.3 | S |
| ✅ 2.3 | **`PaddingPlan.For(claimedLength, actualLength)`** | Returns the committed length and the optional padding-record offset/length | 2.1, 2.2 | M |
| ✅ 2.4 | **Exhaustive test of `PaddingPlan`** | **All** `(claimed, actual)` pairs where `0 <= actual <= claimed <= 4096`. Invariant: committed + padding **exactly** covers claimed, and every emitted padding record is `>= 8` bytes | ✅ 2.3 | L |
| ✅ 2.5 | Named tests for the three leftover cases | `== 0`, `>= 8`, and **`< 8` folded into the committed length** ([padding records](../knowledge/concepts/padding-records.md)) | ✅ 2.4 | M |
| ✅ 2.6 | `WrapPlan.For(tailIndex, required, capacity)` | Decides pad-and-restart vs write-in-place | ✅ 2.2 | M |
| ✅ 2.7 | Exhaustive test of `WrapPlan` | Every `(tailIndex, required)` for a small capacity; a message never straddles the end | ✅ 2.6 | M |
| ✅ 2.8 | `Position` arithmetic (mask, occupancy, available) | Property: `available == capacity - (tail - head)` for all valid pairs | ✅ 2.2 | S |
| ✅ 2.9 | **Position arithmetic at `long.MaxValue`** | Seeded directly; behaviour holds across the 64-bit boundary ([L1](../knowledge/testing/l1-unit.md)) | ✅ 2.8 | M |

**Phase 2 exit — met on 2026-08-19. [R3 is retired.](../knowledge/risks/risk-register.md)**
31 tests, of which the exhaustive `PaddingPlan` pass alone checks over 8 million
`(claimed, actual)` pairs against four invariants, in about 7 seconds.

It also **disproved part of the design**. D1 described three leftover cases and singled out
the sub-header one as "the one that will be got wrong". It is unreachable while
`Alignment >= HeaderLength`, so there are two. The knowledge bundle is corrected; the
constant relation is now asserted at runtime rather than assumed.

That is the phase working exactly as intended: the cheapest possible place to find out that a
design was more complicated than it needed to be.

---

## Phase 3 — Journal, reference queue, replay harness (ATDD scaffolding)

Written **before** the rings, per [ATDD](../knowledge/testing/test-pyramid.md).

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| 3.1 | Journal reader/writer — length-prefixed SBE frames | Round-trips; deliberately **not** the ring's record format ([replay harness](../knowledge/architecture/replay-harness.md)) | ✅ 1.3 | M |
| 3.2 | `IRingBuffer` + `Claim` ref struct — signatures only | Compiles; `Claim` **cannot escape to the heap** (verified: a `ref struct` is required, not stylistic) | 0.3 | M |
| 3.3 | **`ReferenceQueue` — the `List<byte[]>` oracle** | Implements `IRingBuffer`; obviously correct by inspection | 3.2 | M |
| 3.4 | `IClock` + seeded id source | No `DateTime.Now`, no `Guid.NewGuid()` anywhere on the replay path | 0.3 | S |
| 3.5 | `replay` CLI — `--input`, `--output`, `--seed` | Pure function; no network, no clock, no other filesystem access ([deterministic replay](../knowledge/concepts/deterministic-replay.md)) | 3.1, 3.3, 3.4 | L |
| 3.6 | Byte-exact differ | Reports first differing offset, expected vs actual hex, with context | 3.1 | M |
| 3.7 | **Schema-resolved field naming in the differ** | Names the *field* at the offset, not just the offset. A raw offset is useless at 3am | 3.6 | L |
| 3.8 | Reqnroll wiring + first scenario | Scenario runs and **fails** for the right reason | 3.5 | M |
| 3.9 | Corpus fixture builder | Produces `input.sbe` from a declarative case description | 3.1 | M |
| 3.10 | First 3 corpus cases via the reference queue | `single`, `multiple`, `nested-groups` green against `ReferenceQueue` | 3.9, 3.5 | M |
| 3.11 | `conformance` gate step (`full` lane) | Runs every case against every available implementation | 3.10 | M |

**Phase 3 exit:** a failing acceptance suite exists, with fixtures, before either real ring is
written. That is what makes it a guardrail rather than a regression net.

---

## Phase 4 — SPSC ring

Strict TDD. Every task is red → green → refactor, one behaviour per commit.

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| 4.1 | Slab allocation — `NativeMemory.AlignedAlloc`, 4096 | Verified 4096-aligned; capacity rejected unless a power of two | 0.3 | M |
| 4.2 | `IDisposable` + finalizer; double-dispose safe | Dispose twice does not crash or double-free | 4.1 | M |
| 4.3 | Trailer layout, cache-line isolated | Tail, head-cache, head **64 bytes apart**, asserted ([D5](../knowledge/decisions/d5-cache-line-padding.md)) | 4.1 | M |
| 4.4 | `TryClaim`/`Commit` — happy path, exact length | Round-trips one message | 4.3, 2.1 | M |
| 4.5 | **Zero-copy assertion (S1)** | `Unsafe.AsPointer(ref MemoryMarshal.GetReference(claim.Span)) == slab + offset + 8` — address identity, verified as the working mechanism | 4.4 | M |
| 4.6 | `Commit` shorter than claimed → padding | Wires in `PaddingPlan`; all three leftover cases green | 4.4, 2.3 | M |
| 4.7 | Wrap → padding-on-wrap | Wires in `WrapPlan`; a message never straddles the end | 4.4, 2.6 | M |
| 4.8 | `Read(handler, messageCountLimit)` | Honours the limit; stops at a non-positive length | 4.4 | M |
| 4.9 | **Zero-on-consume** | Consumed bytes are zeroed before the head advances; a lapped buffer cannot be misread ([publication protocol](../knowledge/concepts/claim-commit-protocol.md)) | 4.8 | M |
| 4.10 | Insufficient capacity → `TryClaim` returns false | Never blocks, never throws, never corrupts ([D3](../knowledge/decisions/d3-non-blocking-backpressure.md)) | 4.4 | M |
| 4.11 | `Abort` → whole claim becomes padding | Consumer skips it entirely | 4.6 | S |
| 4.12 | FsCheck properties ([L2](../knowledge/testing/l2-property.md)) | All six invariants under randomised operation sequences | 4.10 | L |
| 4.13 | Promote every shrunk counterexample into L1 | Each is a named regression test | 4.12 | M |
| 4.14 | Zero-alloc assertion over publish/consume (S2) | Steady-state loop allocates **exactly 0** | 4.8, 1.9 | M |
| 4.15 | Corpus green against SPSC | Same fixtures that Phase 3 built against the reference queue | 4.9, 3.10 | M |

**Phase 4 exit:** SPSC passes the same acceptance corpus as the reference queue, allocates
nothing, and copies nothing.

---

## Phase 5 — MPSC ring, concurrency, ARM64

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| 5.1 | Head caching | Real head read **only** when the cache says space is short | 4.15 | M |
| 5.2 | **CAS claim loop** | `Interlocked.CompareExchange(ref Unsafe.AsRef<long>(tail), …)` — verified working on unmanaged memory | 5.1 | L |
| 5.3 | CAS retry restarts from the re-read | A failed CAS recomputes; **stale padding intent from the failed attempt is discarded** | 5.2 | M |
| 5.4 | Padding-on-wrap under contention | Two producers racing a wrap produce exactly one padding record | 5.3, 4.7 | L |
| 5.5 | Corpus green against MPSC | Same fixtures, unchanged | 5.4, 4.15 | M |
| 5.6 | Coyote: CAS loop under contention | Systematic exploration; failures replay deterministically | 5.2 | L |
| 5.7 | Coyote: claim/commit interleaved across producers | No interleaving publishes a partial message | 5.6 | L |
| 5.8 | Coyote: wrap racing a consume | No interleaving loses or duplicates a message | 5.6, 5.4 | L |
| 5.9 | Stress: N producers × M messages (nightly) | Every message arrives **exactly once**, payload intact | 5.5 | M |
| 5.10 | **ARM64 leg green on the full suite** | [D6](../knowledge/decisions/d6-memory-model-and-arm64.md) proven, not merely planned. [R4 retired](../knowledge/risks/risk-register.md) | 5.5, 0.12 | M |
| 5.11 | Deliberately break a barrier; confirm ARM64 catches it | Removing a `Volatile.Write` goes **green on x64 and red on ARM64** | 5.10 | M |

**Phase 5 exit:** both rings pass the same corpus on both architectures, and 5.11 has *shown*
the ARM64 leg earning its place rather than asserting it.

---

## Phase 6 — Triangulation and corpus completion

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| 6.1 | `triangulate.py` — four verdicts | `UNANIMOUS`, `2-1 SPLIT`, `STALE EXPECTATION`, `NO AGREEMENT` ([triangulation](../knowledge/practices/triangulation.md)) | 5.5 | L |
| 6.2 | Unit tests for the differ and the triangulator | Including: **the reference queue itself can be the named minority** | 6.1 | M |
| 6.3 | Watch both blocking verdicts fail for real | A seeded stale expectation and a seeded 2-1 split are observed going red | 6.2 | M |
| 6.4 | Complete the corpus | All ten case categories ([conformance corpus](../knowledge/architecture/conformance-corpus.md)) | 6.1 | L |
| 6.5 | Corpus coverage check | A case category with no case **fails the gate** | 6.4 | M |
| 6.6 | v1-fixture-decoded-by-v2-codec case | Schema evolution pinned at the byte level | 6.4, 1.8 | M |

**Phase 6 exit:** the corpus is complete and judged symmetrically, so a stale expectation
cannot pass unnoticed.

---

## Phase 7 — Performance

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| 7.1 | BenchmarkDotNet harness + `MemoryDiagnoser` | Throughput and allocation reported per ring | 5.5 | M |
| 7.2 | Latency percentiles including **p99.9** | The tail is the point in this domain | 7.1 | M |
| 7.3 | **Zero-alloc hard gate, `fast` lane** | Gen0 = 0. Deterministic, so it blocks a PR without flaking ([L5](../knowledge/testing/l5-performance.md)) | 7.1 | M |
| 7.4 | Deliberately-unpadded benchmark variant | The value of [D5](../knowledge/decisions/d5-cache-line-padding.md) is a visible number | 7.1 | M |
| 7.5 | Committed baselines + tolerance check (nightly) | A benchmark nobody can fail is a benchmark nobody reads | 7.2 | M |
| 7.6 | Contended vs uncontended MPSC comparison | Quantifies what the CAS loop costs | 7.1 | S |

**Phase 7 exit:** allocation is gated hard, latency is gated loosely, and both cache-line
padding and CAS contention are numbers someone can watch.

---

## Phase 8 — Teaching artifacts

Seven runnable, tested stages. Each stage is a real project — snippets in prose rot.

| ID | Task | Done when | Needs | Size |
|---|---|---|---|---|
| 8.1 | Stage 1 — fixed-size slots, one producer | Builds and its tests pass | 4.15 | M |
| 8.2 | Stage 2 — variable-length records, length in the header | Builds and its tests pass | 8.1 | M |
| 8.3 | Stage 3 — wrap, and why a message cannot straddle | Builds and its tests pass | 8.2 | M |
| 8.4 | Stage 4 — the negative→positive commit protocol | Builds and its tests pass | 8.3 | M |
| 8.5 | Stage 5 — multiple producers, CAS and the head cache | Builds and its tests pass | 8.4, 5.2 | M |
| 8.6 | Stage 6 — SBE, and why var-length breaks claim-before-encode | Arrives at [D1](../knowledge/decisions/d1-claim-commit-with-padding.md) from the problem | 8.5 | L |
| 8.7 | **Stage 7 — memory ordering: stage 4 was already wrong** | Revisits code the reader believes is finished ([teaching artifacts](../knowledge/practices/teaching-artifacts.md)) | 8.6, 5.11 | L |
| 8.8 | `STUDY.md` tying the stages together | Reads as one narrative, not eight READMEs | 8.7 | M |
| 8.9 | `study-stages` gate step (`full` lane) | A stage that stops compiling **fails the build** | 8.8 | M |
| 8.10 | Comment the hot paths, linking into the bundle | Comments say *why*, not what | 5.5 | M |

**Phase 8 exit:** a reader can build the buffer up from scratch, and every stage is code CI
compiles.

---

## Phase 9 — Deferred

| ID | Task | Status |
|---|---|---|
| 9.1 | Cross-process shared-memory ring | **Deferred** — [D10](../knowledge/decisions/d10-cross-process-deferred.md). Registered in `docs/TODO.md`. Constraint held meanwhile: the trailer stays position-independent and no absolute pointer is stored in the buffer. |

---

## Critical path

```
0.6 gate  ->  1.4 codegen  ->  2.3 PaddingPlan  ->  3.5 replay  ->  4.6 SPSC padding
          ->  5.2 CAS  ->  5.10 ARM64  ->  6.1 triangulation
```

**2.3 → 2.5 is the risk-bearing stretch.** If the padding arithmetic is going to be wrong, it
is wrong there, and Phase 2 is designed so it is found there — with no memory, no threads, and
an exhaustive test — rather than inside a concurrent buffer four phases later.

## What would make me revise this plan

- **2.4 cannot be made exhaustive fast enough.** Then R3 is not retired, and Phase 4 becomes
  materially riskier. Mitigation: shrink the bound and add FsCheck above it.
- **5.11 fails to show a difference** — a removed barrier stays green on ARM64 too. That would
  mean the ARM64 leg is not testing what we think, which is worse than not having it, and
  would need diagnosis before Phase 5 could close.
- **3.7 costs more than a day.** Field-resolved diffing is the difference between a corpus
  people use and one they mute, so it is worth the day — but if it runs long, ship offset-only
  diffing and register the rest.
