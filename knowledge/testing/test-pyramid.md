---
type: Practice
title: The five test layers
description: What each layer catches that the others cannot, and why none is redundant.
tags: [testing, strategy, tdd]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The layers

| Layer | Tool | Catches |
|---|---|---|
| [L1 Unit](l1-unit.md) | xUnit v3 | the algebra: alignment, wrap, padding, capacity, position arithmetic |
| [L2 Property](l2-property.md) | FsCheck | invariant violations under **operation sequences nobody would write by hand** |
| [L3 Concurrency](l3-concurrency.md) | Coyote + stress | bad **interleavings** |
| [L4 Acceptance](l4-acceptance-replay.md) | Reqnroll + binary replay | **wire format** drift and end-to-end behaviour change |
| [L5 Performance](l5-performance.md) | BenchmarkDotNet | allocation regressions (hard gate) and throughput regressions |

# None of these is redundant

This is the part worth internalising, because each layer looks skippable from inside another
one:

- L1 cannot generate the awkward input sizes that expose the
  [padding boundary cases](/concepts/padding-records.md). L2 does, by construction.
- L2 runs single-threaded, so it cannot see a lost CAS update. L3 does.
- L3 schedules threads but **does not model weak memory**, so it cannot see a missing
  `Volatile.Write`. The ARM64 CI leg does — see
  [the memory model](/concepts/dotnet-memory-model.md).
- Nothing above L4 notices that the **bytes on the wire changed** while every test still
  passes, because everything above L4 goes through the same codec that changed.
- Nothing above L5 notices that a `string` overload crept onto the hot path.

# TDD and ATDD, concretely

**TDD (L1, L2):** red → green → refactor, one behaviour per commit. The git history is the
evidence, and it is checkable — a commit adding behaviour with no test in the same or prior
commit is a review finding.

**ATDD (L4):** acceptance tests are written **before** the components they exercise. The
Gherkin scenario and the fixture come first; the ring that satisfies them comes second.

This ordering is what makes L4 a guardrail rather than a regression net. A test written after
the implementation encodes what the code does. A test written before it encodes what the code
**should** do — and those differ exactly where the bugs are.
