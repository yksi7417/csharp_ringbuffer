---
type: Decision
title: "D3: The buffer never blocks; backpressure is the caller's policy"
description: TryClaim returns false when full. Wait strategies live outside the data structure.
tags: [ring-buffer, api, lock-free]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

A ring buffer that is full must do something. Blocking, spinning and failing are all
defensible, but only one of them keeps the "lock-free" claim checkable.

# Decision

**`TryClaim` returns `false` and never blocks.** Backpressure is the caller's policy.

A pluggable `IIdleStrategy` (`Busy`, `Yielding`, `Sleeping`, `BackoffV`) is provided
**outside** the buffer for callers that want to retry.

# Consequences

- Keeping blocking out of the data structure is what makes success criterion S3 mechanically
  checkable: the source scan for `lock`/`Monitor` has no legitimate exceptions to whitelist.
  See [the no-lock gate step](/practices/one-gate-command.md).
- Callers must handle `false`. That is the correct place for the decision — a market data
  handler drops, an order path must not.
