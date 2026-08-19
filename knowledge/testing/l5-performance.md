---
type: Test Layer
title: "L5: Performance"
description: Allocation gated hard, latency gated with tolerance, false sharing kept visible.
tags: [testing, performance, benchmarks]
resource: benchmarks/RingBuffer.Benchmarks
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What is measured

- throughput (messages/second)
- latency percentiles, **including p99.9** — the tail is the point in this domain
- allocations per operation, which **must be 0**
- the [false sharing](/concepts/false-sharing.md) comparison: padded versus deliberately
  unpadded

# Two gates, deliberately different

**Allocation: hard gate, fast lane.** Allocation count is **deterministic**, so it can block
a PR without ever being flaky. Zero is zero. This is the cheapest high-value gate in the
project and it is what actually protects
[zero-copy](/concepts/zero-copy-in-dotnet.md) from erosion.

**Latency: tolerance gate, nightly lane.** Timing on shared CI runners is noisy. A tight
latency gate would be flaky, and a flaky gate gets muted — after which it protects nothing.
So it runs nightly, against committed baselines, with a tolerance.

# A benchmark nobody fails is a benchmark nobody reads

Committed baselines with a tolerance, checked in the nightly lane. Without a gate, benchmark
numbers are published, admired and ignored while they drift.

# The unpadded variant is documentation

The deliberately-unpadded benchmark exists so the value of
[D5](/decisions/d5-cache-line-padding.md) stays a **number someone can watch**. If the gap
between padded and unpadded ever closes, the padding has silently stopped working — which is
otherwise completely invisible, because nothing fails.
