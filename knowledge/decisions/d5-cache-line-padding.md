---
type: Decision
title: "D5: Hot counters sit on separate cache lines"
description: Tail, head-cache and head are 64-byte isolated in the trailer. Non-negotiable.
tags: [performance, concurrency, memory]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

The tail counter is written by every producer. The head counter is written by the consumer.
If they share a 64-byte cache line, every producer write invalidates the consumer's line and
vice versa — [false sharing](/concepts/false-sharing.md), which can cost an order of magnitude.

# Decision

**Tail, head-cache and head each sit on their own 64-byte cache line**, in the trailer past
the data region.

# Consequences

- Non-negotiable and directly measurable. A benchmark **with the padding removed** should
  show a large throughput drop.
- That negative benchmark is worth committing as documentation: it turns an invisible
  invariant into a number someone can watch move. See [L5](/testing/l5-performance.md).
- The trailer grows to several cache lines. Irrelevant next to the data region.
