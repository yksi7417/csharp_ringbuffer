---
type: Test Layer
title: "L4: Acceptance tests and binary replay"
description: The load-bearing guardrail. Gherkin scenarios over a byte-exact binary corpus.
tags: [testing, atdd, determinism, guardrail, core]
resource: tests/RingBuffer.Acceptance
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Why this is the important one

**This is the guardrail that makes everything else safe to refactor**, and it is written
before the components it exercises ([ATDD](test-pyramid.md)).

It is also the only layer that can notice **the bytes on the wire changed**. Every other
layer reads through the same codec that changed, so a codegen regression passes them all.

# Gherkin

Scenarios in domain language, so the intent survives the implementation:

```gherkin
Scenario: A market data refresh with nested party groups survives the ring
  Given a ring buffer of 64 KiB
  And a MarketDataIncrementalRefresh with 3 MD entries
  And entry 2 carries 2 party IDs
  When the message is published and consumed
  Then the consumed message is byte-identical to the golden fixture
  And no heap allocation occurred during publish or consume
```

# The machinery

- [Replay harness](/architecture/replay-harness.md) — the pure
  `--input`/`--output` function.
- [Conformance corpus](/architecture/conformance-corpus.md) — committed binary fixtures.
- [Deterministic replay](/concepts/deterministic-replay.md) — why it is a pure function, and
  why the journals are binary rather than JSON.
- [Triangulation](/practices/triangulation.md) — SPSC vs MPSC vs reference queue, judged
  symmetrically.

# The failure mode to guard against

Regenerating `expected.sbe` to make a red test go green. That converts the corpus from a
guardrail into a rubber stamp, silently and permanently, and nothing in CI can distinguish it
from a legitimate update.

Only two things stop it: **review** (a regenerated fixture in a diff is a behaviour change
and must be justified as one) and **triangulation** (which catches an expectation that all
implementations now disagree with).
