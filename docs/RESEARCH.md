# Research: a lock-free, zero-copy ring buffer for SBE FIX messages in C#

**Status:** research complete, awaiting review. No implementation plan is committed yet —
that lands after this document is signed off.

**Question this answers:** what should we build, on what evidence, and what will keep it
honest once it is built?

Everything in [§2](#2-toolchain-feasibility--verified-not-assumed) was executed in this
container, not recalled. Where a claim is unverified it is marked **(unverified)**.

---

## 1. Objective and success criteria

Build a teaching-grade *and* production-shaped library: a lock-free ring buffer that carries
SBE-encoded FIX messages with repeating groups, where the message is **encoded directly into
the ring's memory and decoded in place** — no intermediate `byte[]`, no serialize-then-copy.

The project succeeds when all six hold:

| # | Criterion | How it is proven |
|---|---|---|
| S1 | Zero copy on the hot path | Producer writes SBE fields straight into ring memory; consumer decodes from the same address. Asserted by a test that compares the address the encoder wrote to against the ring slab base + claimed offset. |
| S2 | Zero allocation on the hot path | A steady-state publish/consume loop allocates 0 bytes. Asserted with `GC.GetAllocatedBytesForCurrentThread()` deltas, plus BenchmarkDotNet `MemoryDiagnoser` showing Gen0 = 0. |
| S3 | Lock-free | No `lock`, no `Monitor`, no kernel mutex on publish or consume. Producers make progress via `Interlocked.CompareExchange` only. Enforced by a source-scan gate step, not by good intentions. |
| S4 | Correct under concurrency | Systematic interleaving exploration (Microsoft Coyote), not just "ran 10M messages and it seemed fine". |
| S5 | Byte-deterministic wire format | A golden binary corpus replays byte-for-byte identically. Any codec, schema or layout change surfaces as a hex diff in CI. |
| S6 | Every behaviour arrived test-first | Git history shows failing test → implementation, and the acceptance suite was written before the components it exercises. |

**Explicit non-goal:** competing with Aeron. This is a single-process, shared-memory ring
buffer. No media driver, no network, no multicast.

---

## 2. Toolchain feasibility — verified, not assumed

This section is the reason the research phase existed. Three of the five findings below
contradict what the public documentation implies, and each one would have derailed the
project if discovered mid-implementation.

### 2.1 .NET SDK — available, with a caveat

`dotnet-install.sh` **fails in this environment**: `builds.dotnet.microsoft.com` is denied by
the egress proxy (403 on CONNECT). The Ubuntu archive works:

```bash
apt-get update && apt-get install -y dotnet-sdk-8.0   # -> 8.0.130 at /usr/lib/dotnet
```

`api.nuget.org` and `repo1.maven.org` are both reachable (HTTP 200). **Consequence:** the
CI/dev bootstrap script must use apt, not the Microsoft install script, or web sessions will
fail to build. This goes in `scripts/bootstrap.sh` and a `SessionStart` hook.

### 2.2 SBE C# codegen is *not* reachable through `SbeTool` — this is the big one

The C# README says `./gradlew generateCSharpCodecs`. Underneath, the documented CLI path is
`-Dsbe.target.language=<X>`. That path **does not work for C#** on any current SBE release.

`TargetCodeGeneratorLoader` — the enum `SbeTool` resolves `sbe.target.language` against —
registers only five targets. Verified by decompiling the shipped jars:

```
1.27.0: JAVA C CPP GOLANG RUST
1.30.0: JAVA C CPP GOLANG RUST
1.35.6: JAVA C CPP GOLANG RUST
1.39.0: JAVA C CPP GOLANG RUST
```

`CSharpGenerator` **is** in the jar and is fully maintained (73 KB class, plus
`CSharpDtoGenerator`), but it has no no-arg constructor, so the loader cannot instantiate it:

```
java.lang.IllegalArgumentException: No code generator for name: ...csharp.CSharpGenerator
Caused by: java.lang.NoSuchMethodException: ...CSharpGenerator.<init>()
```

**Resolution — verified working.** Drive the generator through its Java API with an
~18-line shim we own:

```java
final MessageSchema schema = SbeTool.parseSchema(schemaFile);
final Ir ir = new IrGenerator().generate(schema, schema.packageName());
final OutputManager om = new CSharpNamespaceOutputManager(outputDir, ir.applicableNamespace());
new CSharpGenerator(ir, om).generate();
```

Compiled against `sbe-all-1.39.0.jar` and run against a FIX-style schema with a **nested**
repeating group and var-length data. Output:

```
Probe_Sbe/MessageHeader.g.cs   Probe_Sbe/MarketDataIncrementalRefresh.g.cs
Probe_Sbe/GroupSizeEncoding.g.cs   Probe_Sbe/VarStringEncoding.g.cs
Probe_Sbe/MdEntryType.g.cs   Probe_Sbe/Decimal64.g.cs   Probe_Sbe/MetaAttribute.g.cs
```

**Consequence:** `tools/sbe-csharp-gen/` is a first-class, version-pinned part of this repo,
not a build detail. It needs its own test (generate a known schema, assert the output
compiles and round-trips) because it is the load-bearing step nobody else maintains for us.

### 2.3 The `sbe-dll` NuGet package is stale — vendor the runtime instead

Generated C# has `using Org.SbeTool.Sbe.Dll;` and needs `DirectBuffer`, `ThrowHelper`,
`PrimitiveValue`. NuGet state:

| Package | Latest published | Verdict |
|---|---|---|
| `sbe-dll` | **1.13.0** (~2019) | abandoned |
| `sbe-tool` | **1.23.1.1** | the real runtime package — but 16 releases behind |
| `uk.co.real-logic:sbe-all` (Maven) | **1.39.0** | current |

Pairing a 1.39.0 generator with a 1.23.1.1 runtime is version skew across a
codegen boundary — the generated code calls runtime methods that may not exist in the older
DLL. Verified failure mode of exactly this shape: 1.39.0 output calls
`ThrowHelper.ThrowCountOutOfRangeException` and `DirectBuffer.SetBytes(int, ReadOnlySpan<byte>)`.

**Resolution.** Vendor `csharp/sbe-dll/*.cs` from the SBE repo at the **same pinned tag** as
the generator jar. It is 1,851 lines of Apache-2.0 source across 7 files
(`DirectBuffer.cs` 846, `PrimitiveValue.cs` 534, `PrimitiveType.cs` 216,
`EndianessConverter.cs` 129, `SbePrimitiveType.cs` 63, `ThrowHelper.cs` 46, `ByteOrder.cs` 17).
Small enough to read, review and step through — which is a feature for a teaching project.
A CI step re-fetches at the pinned tag and asserts the vendored copy is unmodified, so the
version pin cannot silently drift.

### 2.4 `DirectBuffer` supports true zero-copy over native memory

The decisive API:

```csharp
public DirectBuffer(byte* pBuffer, int bufferLength)
public void Wrap(byte* pBuffer, int bufferLength)   // _needToFreeGCHandle = false
```

It wraps a raw pointer with **no GC handle and no pinning**. So the ring's slab can be
`NativeMemory.AllocZeroed` — off-heap, never moved, never scanned by the GC. This is what
makes S1 and S2 achievable rather than aspirational. (`Wrap(byte[])` also exists but pins via
`GCHandle`, which we avoid.)

Generated accessors are span-based where it matters:

```csharp
public ReadOnlySpan<byte> Symbol { get; }     // decode, no allocation
public Span<byte> SymbolAsSpan();             // encode, no allocation
public int SetText(ReadOnlySpan<byte> src);   // var-length, no allocation
public string GetText();                      // convenience — allocates, banned on hot path
```

**Consequence:** the hot path uses the span overloads; the `string` overloads are for tests
and diagnostics only. Worth a lint rule, since the ergonomic call is the allocating one.

### 2.5 End-to-end proof — it actually works

Full round trip executed in this container: SBE 1.39.0 → shim → C# codecs → compiled on
.NET 8 against the vendored runtime → encode into `NativeMemory` → decode from the same
address.

```
encoded bytes = 96
templateId=1 blockLength=8 transactTime=1700000000000
mdEntries count=2
  BID   ESZ6     px=5432e-2 sz=10 role=7 text=bid
  OFFER ESZ6     px=5433e-2 sz=20 role=8 text=ask
head32=08000100010000000068e5cf8b0100001a0002003045535a3620202020381500
```

Nested repeating group (`parties` inside `mdEntries`), var-length `text`, composite
`Decimal64`, char-array `Symbol`, all through a pointer to unmanaged memory. **The hardest
technical risk in the project is retired before implementation starts.**

### 2.6 Supporting packages — all present on NuGet

| Package | Latest | Role |
|---|---|---|
| `xunit.v3` | 4.0.0 | unit + integration tests |
| `Reqnroll.xUnit` | 3.3.4 | Gherkin ATDD (SpecFlow successor) |
| `Microsoft.Coyote` / `.Test` | 1.7.11 | systematic concurrency exploration |
| `FsCheck` | 3.3.4 | property-based tests |
| `BenchmarkDotNet` | 0.16.0-preview.1 | latency histograms, allocation diagnostics |
| `Verify.XUnit` | 31.12.5 | snapshot/approval tests for hex dumps |

---

## 3. Prior art

### 3.1 Agrona `ManyToOneRingBuffer` — the reference design

The design we should follow, read from the Agrona source rather than from blog posts.

**Record layout**, `RecordDescriptor`: `HEADER_LENGTH = 8`, `ALIGNMENT = 8`.

```
offset +0 : int32  length   (negative = claimed/in-flight, positive = committed)
offset +4 : int32  type      (msgTypeId; PADDING_MSG_TYPE_ID marks a skip record)
offset +8 : payload
```

`checkTypeId` requires `msgTypeId > 0`, which frees negative values as sentinels.

**Trailer**, past the power-of-two data region, each field on its own cache line:
`TAIL_POSITION`, `HEAD_CACHE_POSITION`, `HEAD_POSITION`, `CORRELATION_COUNTER`,
`CONSUMER_HEARTBEAT`.

**`claimCapacity`** — CAS loop on tail:
1. read the *cached* head; compute `capacity - (tail - head)`
2. if short, re-read the real head and retest; still short → `INSUFFICIENT_CAPACITY`
3. align the required length; if it would straddle the end of the data region, emit a
   **padding record** filling the tail and restart the message at index 0
4. `CAS(tail, old, new)`; retry on failure

**Publication protocol** — the ordering that makes it work without a lock:
write `length = -recordLength` first, then type and payload, then store-release
`length = +recordLength`. A consumer seeing `length <= 0` stops; a positive length is the
single commit point.

**`read(messageCountLimit)`** — one consumer, bounded work per call, skips padding records,
**zeroes consumed bytes** in a `finally`, then store-releases the new head. The zeroing is
not hygiene: it stops a lapped buffer from being reinterpreted as a valid record.

**Applicable to us as-is:** header layout, negative-length commit protocol, padding-on-wrap,
head caching, bounded read, zero-on-consume. This is a well-tested design and we should not
be inventive where it already answers the question.

### 3.2 LMAX Disruptor / Disruptor-net

Different shape: fixed-size pre-allocated **object** slots, sequence barriers, pluggable wait
strategies. Contributes cache-line padding of hot counters, wait-strategy pluggability, and
"pre-allocate everything". It does **not** fit variable-length SBE payloads with repeating
groups — a fixed slot size cannot hold an arbitrary group count without either wasting space
or capping the group. Borrow the ideas, not the structure.

### 3.3 `cross_asset_ems` — the quality model to copy

The reference repo's *testing* architecture is more valuable to us than its domain code.

**Byte-exact conformance corpus** — `conformance/corpus/<case>/{input.jsonl, expected.jsonl, case.md, seed}`.
Each case is a committed fixture reviewed like source. The contract is deliberately brutal:

> `ems-slice --input <journal> --output <journal> [--seed <n>]` — "No network, no clock, no
> filesystem beyond those two paths. It is a pure function from input journal to output journal."

That is precisely the deterministic-replay shape we want, and it is what makes byte-for-byte
diffing meaningful. **Directly adoptable**, with `.jsonl` swapped for length-prefixed SBE
frames — which is *stronger*, because the fixture then pins the wire format itself.

**Triangulation over reference-diffing.** Diffing every implementation against one reference
cannot detect a stale expectation, because everything fails it in unison. Their
`triangulate.py` judges implementations symmetrically and distinguishes `UNANIMOUS` /
`2-1 SPLIT` / `STALE EXPECTATION` / `NO AGREEMENT`. We have a natural triangle:
**SPSC ring / MPSC ring / a trivially-correct `List<byte[]>` reference queue**. All three must
produce the same output journal for the same input. *(Adopt in a later phase.)*

**One gate command.** `scripts/ci/gate.sh fast|full|nightly` — the same script CI runs, also
wired to `.githooks/pre-push`. "If the gate is green, the fast lane in CI is green." No
mental list of what CI checks. **Adopt on day one.**

**A green gate is evidence, not proof.** Their `docs/polyglot/traps.md` records 34 things
that went wrong, and notes most of them *looked green while they were wrong* — a generator
whose stderr went to `/dev/null` and wrote nothing; a `git diff` blind to an untracked
generated file; a gate step in a lane with no dispatch case. Their rule: when something fools
you, the fix is not finished until you have asked whether a check could have caught it.
**Adopt the discipline and the trap log**, sized to this project.

**Deferred-work register.** `docs/TODO.md`, where every entry needs a **Why** and a
**Done when**, enforced by a CI check that is bidirectional — a `DEFERRED: T-n` comment with
no register entry fails, and so does an entry that lost its sections. Cheap, and it stops a
teaching repo accreting silent shortcuts. **Adopt.**

**Test pyramid.** ~5,000 unit / ~500 component / ~50 BDD, with cross-module tests in
`tests/{integration,smoke,e2e}` separate from per-module unit tests. Adopt the *shape* and
the separation; our absolute numbers will be far smaller.

**ADRs.** `docs/decisions/NNNN-title.md`, numbered, one decision each. **Adopt.**

---

## 4. Design space — the decisions this project has to make

Each becomes an ADR. Recommendations are mine; they are the reviewable part.

### D1. Variable-length payloads vs. claim/commit — the central problem

SBE with repeating groups and var-length data means **the encoded length is not known until
after encoding**. But zero-copy means encoding *into* the ring, which means claiming space
*before* encoding. These are in direct conflict, and Agrona does not solve it: its `tryClaim`
takes an exact length and `commit` publishes exactly that.

Options:

| Option | Zero-copy | Cost |
|---|---|---|
| A. Encode to scratch, then copy in | ✗ | defeats the entire premise |
| B. Compute exact length up front | ✓ | requires knowing group counts and var-data lengths before encoding; leaks encoding into the caller |
| C. **Claim a bound, commit the actual, pad the remainder** | ✓ | one extra padding record when the estimate overshoots |

**Recommend C.** `TryClaim(maxLength, out Claim)` advances the tail by `maxLength`; the
producer encodes into `claim.Span`; `claim.Commit()` writes the true aligned length into the
record header and, if any claimed space is left over, writes a **second header at the
leftover offset with `PADDING_MSG_TYPE_ID`**. The consumer already skips padding records, so
this costs one 8-byte header and no consumer change.

This is a deliberate extension beyond Agrona and is the most interesting design in the
project. It needs its own dedicated test class — including the boundary cases where leftover
is exactly 0, exactly 8, and less than 8 bytes (which cannot hold a header and must be folded
into the committed length).

### D2. SPSC and MPSC, or MPSC only

**Recommend both**, behind one `IRingBuffer`. SPSC is materially simpler — a plain
store-release on tail, no CAS — and is both the faster path and the better teaching
on-ramp. Sharing a conformance suite between them turns the interface into a contract and
gives us the third leg of the triangulation in §3.3.

### D3. Wait strategy on a full buffer

**Recommend: `TryClaim` returns false and never blocks.** Backpressure is the caller's
policy. A pluggable `IIdleStrategy` (`Busy` / `Yielding` / `Sleeping` / `BackoffV`) lives
outside the buffer. Keeping blocking out of the data structure is what keeps S3 checkable.

### D4. Memory: `NativeMemory` vs pinned `byte[]` vs `MemoryMappedFile`

**Recommend `NativeMemory.AlignedAlloc`**, aligned to 4096. Off-heap, immune to GC
compaction, no pin to hold, and alignment lets us guarantee cache-line placement of the
trailer counters. Add a memory-mapped backing later — that unlocks cross-process IPC, which
is the natural sequel and is why the trailer layout should be position-independent from the
start. *(Deferred, but designed for.)*

### D5. False sharing

Tail, head-cache and head **must** sit on separate 64-byte cache lines, in the trailer past
the data region. Non-negotiable and directly measurable: a benchmark with padding removed
should show a large throughput drop. That negative benchmark is worth committing as
documentation.

### D6. Memory model — the C# specifics

.NET's model is not Java's, and the differences bite exactly here:

- `Volatile.Read`/`Volatile.Write` for acquire/release. **Not** `volatile` on a field —
  our counters live in unmanaged memory, reached via `Unsafe.AsRef<long>(ptr)`.
- `Interlocked.CompareExchange(ref Unsafe.AsRef<long>(ptr), next, current)` for the tail CAS.
- x86-TSO hides missing barriers. **ARM64 does not.** A test suite that only ever runs on
  x64 CI will pass while the code is wrong on Apple Silicon and Graviton.
  **Recommend a CI matrix with an ARM64 leg** — this is a real bug class, not a hypothetical.
- Coyote ([§2.6](#26-supporting-packages--all-present-on-nuget)) explores interleavings
  systematically; it does not model weak memory. It and the ARM64 leg cover different
  failure modes and we need both.

### D7. Message framing: ring record header vs SBE `MessageHeader`

Both. The ring record header (8 bytes: length + type) is transport framing and stays opaque
to SBE. The SBE `MessageHeader` (8 bytes: blockLength, templateId, schemaId, version) is the
payload's first 8 bytes and is what makes schema evolution work. Overhead is 16 bytes per
message; a fixed cost worth documenting rather than optimising away, since collapsing them
would couple the transport to the codec.

### D8. Schema — which FIX messages

**Recommend two**, both with repeating groups, in `schemas/fix-sbe.xml`:

1. **`NewOrderSingle`** with a `NoPartyIDs` (453) repeating group — the canonical FIX group.
2. **`MarketDataIncrementalRefresh`** with `NoMDEntries` (268) containing a **nested**
   `NoPartyIDs` group and a var-length `text` field.

Nesting matters: it is where flyweight limit-management goes wrong, and it is already
proven to generate and round-trip ([§2.5](#25-end-to-end-proof--it-actually-works)).

Add a **v1 → v2 schema evolution** pair: v2 appends a field and a group. A v2 codec must
decode a v1 golden file correctly via `actingVersion`. This is one of the strongest
acceptance tests available to us and it costs almost nothing.

---

## 5. Testing strategy

Five layers, each catching something the others cannot.

### L1 — Unit (xUnit), strict TDD

Red → green → refactor, one behaviour per commit, visible in git history. Coverage targets
the algebra: header encode/decode, alignment, wrap detection, padding insertion,
insufficient-capacity, negative-length-not-yet-committed, read limits, head/tail arithmetic
across `long` wraparound.

**Explicitly test the arithmetic at `long.MaxValue`.** Positions are monotonic 64-bit
counters masked into the buffer; the wrap case is unreachable in practice and trivially
gettable in a test by seeding the trailer. Untested arithmetic that "can't happen" is where
these buffers actually break.

### L2 — Property-based (FsCheck)

Invariants that must hold for *any* sequence of operations:

- everything published is eventually consumed, exactly once, in order (per producer)
- consumed bytes always equal published bytes
- `head <= tail` always; `tail - head <= capacity` always
- decoding any committed record yields the message that was encoded
- a full buffer always rejects rather than corrupting

Generate random operation sequences with random message sizes and group counts. This is
where the D1 padding logic gets a real workout, because random sizes hit the awkward
leftovers that hand-written tests miss.

### L3 — Concurrency (Microsoft Coyote)

Coyote takes over scheduling and explores interleavings systematically, replaying any
failure deterministically. Target the MPSC CAS loop, claim/commit under contention, and the
wrap-while-consuming race. This is the difference between S4 and "we ran it for a while".

Complement with a stress test: N producers × M messages, assert every message arrives
exactly once with an intact payload. Stress finds different bugs than Coyote does; keep it
in the nightly lane where a long run is affordable.

### L4 — Acceptance / ATDD (Reqnroll + deterministic binary replay)

**This is the guardrail that makes the rest safe to refactor**, and it is written before the
components it exercises.

Gherkin scenarios in domain language:

```gherkin
Scenario: A market data refresh with nested party groups survives the ring
  Given a ring buffer of 64 KiB
  And a MarketDataIncrementalRefresh with 3 MD entries
  And entry 2 carries 2 party IDs
  When the message is published and consumed
  Then the consumed message is byte-identical to the golden fixture
  And no heap allocation occurred during publish or consume
```

Behind them, the **replay harness**, modelled on `cross_asset_ems`'s conformance contract:

```
replay --input <journal> --output <journal> [--seed <n>]
```

A pure function from input journal to output journal. No clock, no network, no filesystem
beyond those two paths. Timestamps come from a `IClock` seeded per case; IDs from a seeded
counter. Determinism is a design constraint, not something we hope for.

Corpus layout, adapted from the reference:

```
tests/conformance/corpus/<case-name>/
    input.sbe        length-prefixed SBE frames, committed binary
    expected.sbe     the output journal, committed binary
    case.md          one paragraph: what this covers and why it exists
    seed             single integer, absent means 0
```

Diffing is **byte-for-byte**, with a hex-dump differ that reports the offset, the decoded
field at that offset, and expected-vs-actual — a raw byte offset alone is useless at 3am.
Fixtures are reviewed like source.

Why binary rather than JSON: the fixture then pins the *wire format*. Bump the SBE version,
change the generator flags, reorder a group, break endianness on a new platform — the diff
appears immediately. A JSON fixture would hide every one of those.

Cases to cover: single message; multiple messages; a message that wraps the buffer; a
message that forces a padding record; empty repeating group (count 0); maximum group count;
nested groups; var-length data at the size boundaries; buffer-full rejection; **v1 fixture
decoded by v2 codec**.

### L5 — Performance (BenchmarkDotNet), gated not just reported

Measured: throughput (msg/s), latency percentiles including p99.9, allocations per operation
(**must be 0**), and the padding-removed false-sharing comparison.

**Regression gate:** committed baselines with a tolerance, checked in the nightly lane. A
benchmark nobody fails is a benchmark nobody reads. Allocation count is gated *hard* at zero
in the fast lane — it is deterministic, unlike latency, so it can block a PR without
flakiness.

---

## 6. Repository shape and guardrails

```
schemas/fix-sbe.xml               v1 schema — the source of truth
schemas/fix-sbe-v2.xml            v2, for the evolution test
tools/sbe-csharp-gen/             the Java shim from §2.2 (pinned, tested)
src/RingBuffer.Sbe/               vendored SBE runtime (pinned tag, checksum-verified)
src/RingBuffer.Core/              the ring buffers
src/RingBuffer.Codecs/            generated codecs (generated, not committed — see below)
tests/RingBuffer.Core.Tests/      L1, L2
tests/RingBuffer.Concurrency/     L3
tests/RingBuffer.Acceptance/      L4 — Reqnroll features + replay harness
tests/conformance/corpus/         L4 — golden binary fixtures
benchmarks/RingBuffer.Benchmarks/ L5
scripts/ci/gate.sh                the one command
docs/decisions/                   ADRs
docs/TODO.md                      deferred-work register (Why + Done when)
docs/TRAPS.md                     things that looked green while wrong
```

**Generated code is not committed**, but a CI step regenerates and asserts the tree is clean
— that is `cross_asset_ems` trap #2 (a `git diff` that could not see an untracked generated
file), so the check must use `git status --porcelain` including untracked files, not
`git diff`.

Gate lanes:

| Lane | Contains | When |
|---|---|---|
| `fast` | format, lint, codegen-clean, no-lock scan, unit, property, acceptance, zero-alloc | every push, via pre-push hook |
| `full` | + coverage, conformance corpus, Coyote, ARM64 leg | every PR |
| `nightly` | + long stress, long Coyote, benchmark regression | scheduled |

---

## 7. Risks

| Risk | Severity | Mitigation | Status |
|---|---|---|---|
| SBE C# codegen unreachable via documented CLI | **was critical** | Java shim, §2.2 | **retired — verified working** |
| Runtime/generator version skew | high | vendor at pinned tag + checksum gate | resolved by design |
| D1 claim/commit padding is novel — no reference implementation to check against | **high** | dedicated boundary tests + FsCheck; triangulate against the reference queue | **open — the main design risk** |
| Weak memory bugs invisible on x64 | high | ARM64 CI leg (D6) | plan, not yet proven |
| Allocation creeping onto the hot path via a `string` overload | medium | hard zero-alloc gate in fast lane + lint on banned members | plan |
| `dotnet-install.sh` blocked in web sessions | medium | apt in bootstrap + SessionStart hook | **retired — verified** |
| Coyote does not model weak memory; ARM64 leg does not explore interleavings | medium | run both; document that neither alone suffices | accepted |
| Teaching clarity vs. peak performance pulling apart | medium | favour clarity; where they conflict, comment the trade-off and benchmark both | accepted |

---

## 8. Open questions for review

1. **Scope of D1.** Is the claim/commit-with-padding extension the right call, or would you
   rather constrain the schema so exact lengths are computable up front (option B)? C is more
   useful and more interesting; B is more conservative.
2. **SPSC + MPSC, or MPSC only?** Both costs perhaps 20% more work and buys the triangulation
   leg plus a much gentler teaching progression.
3. **ARM64 CI leg** — is a GitHub Actions ARM runner available to this repo? Without it, D6
   is a documented gap rather than a covered one.
4. **Cross-process (memory-mapped) ring** — confirm as an explicit later phase, so the
   trailer layout stays position-independent now. It costs nothing today and is expensive to
   retrofit.
5. **Depth of the `cross_asset_ems` guardrail stack.** Gate script, ADRs and the corpus are
   clearly worth it. Are the deferred-work register and trap log wanted from the start, or
   over-engineering at this size? I lean towards including them — they are what stopped that
   repo drifting.
6. **Teaching artefacts.** Should the repo carry a narrative walkthrough (a `STUDY.md` that
   builds the buffer up in stages), or is a well-tested library with good ADRs enough?

---

## Sources

- [SBE C# README](https://github.com/aeron-io/simple-binary-encoding/blob/master/csharp/README.md)
- [SBE `csharp/sbe-dll`, tag 1.39.0](https://github.com/aeron-io/simple-binary-encoding/tree/1.39.0/csharp/sbe-dll)
- [Agrona `ManyToOneRingBuffer`](https://github.com/real-logic/agrona/blob/master/agrona/src/main/java/org/agrona/concurrent/ringbuffer/ManyToOneRingBuffer.java)
- [Agrona `RecordDescriptor`](https://github.com/real-logic/agrona/blob/master/agrona/src/main/java/org/agrona/concurrent/ringbuffer/RecordDescriptor.java)
- [Agrona concurrent collections](https://aeron.io/docs/agrona/concurrent/)
- [cross_asset_ems](https://github.com/yksi7417/cross_asset_ems) — `CONTRIBUTING.md`, `conformance/README.md`, `tests/README.md`, `scripts/ci/gate.sh`, `docs/decisions/`
- [LMAX Disruptor](https://lmax-exchange.github.io/disruptor/)
- [Microsoft Coyote](https://microsoft.github.io/coyote/)
