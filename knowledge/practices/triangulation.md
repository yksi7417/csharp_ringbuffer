---
type: Practice
title: Triangulation
description: Judge implementations against each other, not against one reference. Catches stale expectations.
tags: [testing, guardrails, conformance]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# The problem with a reference implementation

Diffing every implementation against one designated reference **cannot detect a stale
expectation** — because when the expectation is wrong, everything fails it in unison, which
looks exactly like everything being broken, or gets "fixed" by regenerating the expectation.

The reference project hit this and built `triangulate.py` for it.

# Our triangle

[D2](/decisions/d2-spsc-and-mpsc.md) gives us three independent implementations for free:

1. **SPSC** ring
2. **MPSC** ring
3. A **trivially-correct `List<byte[]>` reference queue** — obviously right, absurdly slow

All three run the same input journal through
[the replay harness](/architecture/replay-harness.md) and must produce the same output.

The reference queue is doing real work here: it is the only one of the three with no clever
memory management at all, so a bug shared between SPSC and MPSC — the likely correlated
failure, since they share the record layout — still shows up as a split.

# The four verdicts

| Verdict | Meaning | Blocks |
|---|---|---|
| `UNANIMOUS` | all agree, and match the committed expectation | no |
| `2-1 SPLIT` | one stands alone — **named, not convicted** | yes |
| `STALE EXPECTATION` | all agree with each other, none with `expected.sbe` | yes |
| `NO AGREEMENT` | every output differs from every other | yes |

`STALE EXPECTATION` is the verdict a reference-diff **structurally cannot produce**. It is
the entire reason for this practice.

"Named, not convicted" matters: two implementations rarely make the same mistake, but the
majority **can** share a bug — especially when two of them share a layout. A split points at
the minority; it does not prove it wrong.

# Scheduling

Adopt once both rings and the corpus exist. Until then the corpus diffs against a single
expectation, and that limitation is [a known gap](trap-log.md), not an oversight.
