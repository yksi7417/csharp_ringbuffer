---
type: Test Layer
title: "L1: Unit tests"
description: Strict TDD over the ring's algebra. xUnit v3.
tags: [testing, tdd, unit]
resource: tests/RingBuffer.Core.Tests
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Scope

The algebra of the buffer, tested directly:

- record header encode/decode
- 8-byte alignment
- wrap detection
- [padding record insertion](/concepts/padding-records.md), including all three leftover cases
- insufficient-capacity rejection
- negative-length-not-yet-committed is not read
- read message-count limits
- head/tail arithmetic

# Test the arithmetic at long.MaxValue

Positions are monotonic 64-bit counters masked into the buffer
([layout](/concepts/ring-buffer-record-layout.md)). Overflow is unreachable in practice — at
a billion messages a second it is centuries away.

**Test it anyway.** It is trivially reachable by seeding the trailer directly, and untested
arithmetic that "can't happen" is exactly where these buffers break. The cost is one test;
the alternative is a class of bug that no other layer can reach.

# Discipline

Red → green → refactor. One behaviour per commit, visible in git history (S6).

Where a test needs to reach into the trailer to set up a state, it uses an explicit
test-only seam rather than reflection — reflection-based setup silently rots when a field is
renamed, and rots **into a passing test**.
