---
type: Decision
title: "D2: Build both SPSC and MPSC"
description: Ship a single-producer and a multi-producer ring behind one interface, sharing a conformance suite.
tags: [ring-buffer, concurrency, api]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

The multi-producer (MPSC) buffer is the interesting one, and could stand alone. Building the
single-producer (SPSC) buffer as well costs roughly 20% more work.

# Decision

**Build both**, behind one `IRingBuffer` interface, sharing one conformance suite.

# Consequences

- SPSC is materially simpler: a plain store-release on tail, **no CAS loop at all**. It is
  both the faster path and the better teaching on-ramp — see
  [teaching artifacts](/practices/teaching-artifacts.md).
- Sharing a conformance suite between two implementations turns `IRingBuffer` into a
  **contract** rather than a shape.
- It supplies the third leg of triangulation (SPSC / MPSC / reference queue), which catches
  stale expectations that reference-diffing structurally cannot. See
  [triangulation](/practices/triangulation.md).
- The interface must not leak MPSC-only concepts (correlation counters) or SPSC-only
  assumptions (single-threaded tail).
