# Deferred-work register

**Decided-and-deferred work, not a wish list.** Every entry needs a **Why** and a
**Done when**. Both are enforced by `scripts/ci/checks/deferred_work.py` in every gate lane.

If a comment in code or docs points at deferred work, mark it `DEFERRED: T-n`. The check is
bidirectional — a marker with no entry fails, and so does an entry that has lost its sections.

See [the practice](../knowledge/practices/deferred-work-register.md).

---

## T-1 — Cross-process shared-memory ring

**Why:** Accepted as an explicit later phase at review
([D10](../knowledge/decisions/d10-cross-process-deferred.md)). It needs lifecycle, crash
recovery and a consumer heartbeat that means something — a body of work comparable to the
in-process ring itself. Deferring it costs nothing **provided** the trailer layout stays
position-independent; retrofitting that later would change the on-wire layout and invalidate
every committed conformance fixture.

**Done when:** `MemoryMappedFile` can back the slab in place of `NativeMemory` with no change
to the record or trailer format, and two OS processes pass the conformance corpus across one
shared ring.

## T-2 — Triangulation harness

**Why:** [Triangulation](../knowledge/practices/triangulation.md) needs three implementations
to judge against each other, and until Phase 5 lands there is only the reference queue. Until
then the corpus diffs against a single expectation, which structurally cannot detect a stale
expectation.

**Done when:** `conformance/harness/triangulate.py` produces all four verdicts, both blocking
verdicts have been watched failing against seeded corruption (task 6.3), and it runs in the
`full` gate lane.

## T-3 — Latency regression baselines

**Why:** Committed latency baselines are meaningless before there is a ring to measure, and a
tolerance chosen against noisy shared runners before we know the real variance would either
flake or be so loose it catches nothing.

**Done when:** `benchmarks/baselines.json` holds p50/p99/p99.9 for both rings, and
`benchmark-regression` runs in the nightly lane with a tolerance justified by observed
run-to-run variance.
