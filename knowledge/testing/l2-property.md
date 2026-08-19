---
type: Test Layer
title: "L2: Property-based tests"
description: Invariants that must hold for any operation sequence. FsCheck.
tags: [testing, property, invariants]
resource: tests/RingBuffer.Core.Tests
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The invariants

For **any** sequence of publish and consume operations, with **any** message sizes and group
counts:

- everything published is eventually consumed, **exactly once, in order** (per producer)
- consumed bytes always equal published bytes
- `head <= tail`
- `tail - head <= capacity`
- decoding any committed record yields the message that was encoded
- a full buffer always **rejects** rather than corrupting

# Why this layer earns its place

Randomised message sizes are what actually exercise
[D1's padding logic](/decisions/d1-claim-commit-with-padding.md).

Hand-written tests use round numbers. The awkward leftovers — 3 bytes remaining, 7 bytes
remaining, exactly 8 — are the cases a person does not think to write and a generator hits
within a few hundred runs.

# Shrinking is the real payoff

When FsCheck finds a failure it shrinks the operation sequence to a minimal reproduction. A
1,000-operation failure becomes "publish 13 bytes, publish 9 bytes, consume" — which is
debuggable.

Every shrunk counterexample should be **promoted into [L1](l1-unit.md)** as a named
regression test. The property finds it once; the unit test keeps it found.
