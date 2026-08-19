---
type: Practice
title: Deferred-work register
description: Every conscious deferral is registered in the same change that defers it. Enforced bidirectionally.
tags: [guardrails, process, quality]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# The rule

**When you consciously decide not to do something now, register it in the same commit that
defers it.** Not in the commit message, not in a PR comment, not in your head.

The register is `docs/TODO.md`. An entry needs three things:

```markdown
## T-7 — Short imperative title

**Why:** what forced the deferral, and what breaks if it is never done.

**Done when:** a condition someone else could check without asking you.
```

Both sections are enforced by `scripts/ci/checks/deferred_work.py` in every gate lane.

An entry without a **Why** cannot be prioritised. One without a **Done when** never closes,
because nobody can tell whether it is finished.

# Mark the code that points at it

```csharp
// DEFERRED: T-1 -- memory-mapped backing lands with the cross-process phase
```

**The check is bidirectional:** a `DEFERRED:` marker with no register entry fails, and so
does an entry that has lost its sections.

So a comment cannot outlive the work it points at. When T-1 lands, the marker goes with it
or the build breaks — which is the only reliable way stale `TODO` comments ever get
removed.

# What belongs, and what does not

| Belongs | Does not |
|---|---|
| "We decided to do X, but after Y" | "It'd be nice if X" |

It is a register of **decided-and-deferred work**, not a wish list. A wish list nobody
prunes is indistinguishable from noise within a month.

# Numbering

`T-n` belongs to **this** register. Trap-log entries use `TRAP-n` and are a separate
namespace — see [the trap log](trap-log.md). They were briefly the same namespace, and the
collision was confusing enough to be worth the rename.

# Current entries

The live register is [`docs/TODO.md`](../../docs/TODO.md). It carries the cross-process ring
([D10](/decisions/d10-cross-process-deferred.md)), the triangulation harness, and the latency
baselines — each deferred deliberately, each with a condition that closes it.
