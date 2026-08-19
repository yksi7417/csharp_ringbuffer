---
type: Concept
title: False sharing
description: Why the hot counters are 64 bytes apart, and how that invariant is kept measurable.
tags: [performance, concurrency, memory]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The mechanism

Cache coherence works at **cache-line granularity**, typically 64 bytes — not at variable
granularity. Two unrelated variables on the same line behave, to the coherence protocol, as
one variable.

The tail counter is written by every producer. The head counter is written by the consumer.
If they share a line, every producer write invalidates the consumer's copy and every consumer
write invalidates all the producers' copies. The variables never actually conflict; the
**line** does. Hence "false".

The cost is an order of magnitude, and it is completely invisible in the source — the code
looks correct because it *is* correct. Only the layout is wrong.

# What we do

[D5](/decisions/d5-cache-line-padding.md): tail, head-cache and head each get their own
64-byte line in the trailer.

# Keeping it honest

A padded layout that stops being padded — because someone added a field, or reordered the
trailer — degrades **silently**. Nothing fails.

So the project commits a **deliberately unpadded benchmark variant** alongside the real one.
The gap between them is the value of the padding, expressed as a number someone can watch.
If the gap ever closes, the padding has stopped working. See
[L5](/testing/l5-performance.md).

This is the general pattern from [green gate is evidence, not proof](/practices/green-gate-is-evidence.md):
an invariant nothing can fail is an invariant nobody is maintaining.
