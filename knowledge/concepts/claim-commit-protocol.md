---
type: Concept
title: The publication protocol
description: How a record becomes visible to the consumer without a lock, via the negative-to-positive length transition.
tags: [ring-buffer, lock-free, concurrency, core]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The ordering that makes it work

A single field — the record's `length` — is both the size and the commit flag.

1. Producer claims space via CAS on the tail.
2. Producer writes `length = -recordLength`. **Negative: claimed, do not read.**
3. Producer writes the type and the payload.
4. Producer **store-releases** `length = +recordLength`. **Positive: committed.**

The consumer reads `length` with acquire semantics. If it sees `length <= 0` it **stops
scanning entirely** — it does not skip ahead, because a claimed-but-uncommitted record in
front means everything behind it is not yet safely readable in order.

**Step 4 is the single commit point.** Everything before it is invisible; everything after it
is visible. There is no lock, and no window in which a partial message can be observed.

# Why the store-release matters

Without release semantics on step 4, a weakly-ordered CPU may make the positive length
visible **before** the payload writes from step 3. The consumer would then read a record
marked committed whose contents are still garbage.

x86-TSO will not reorder those stores, so **this bug is invisible on x64 CI and fires on
ARM64**. That is the entire reason for the ARM64 leg in
[D6](/decisions/d6-memory-model-and-arm64.md).

# claimCapacity, in order

1. Read the cached head; compute `capacity - (tail - head)`.
2. If short, re-read the **real** head and retest. Still short → `INSUFFICIENT_CAPACITY`.
3. Align the required length. If it would straddle the end of the data region, emit a
   [padding record](padding-records.md) and restart the message at index 0.
4. `CAS(tail, old, new)`. On failure, **restart from step 1** — the world has changed.

# Consumption

`Read(messageCountLimit)` is single-consumer and does bounded work per call. It skips
padding records, and **zeroes consumed bytes** before store-releasing the new head.

The zeroing is not hygiene. It is what stops a **lapped buffer** from being reinterpreted as
a valid record: stale bytes that happen to carry a positive length would otherwise be read as
a message that was never published.
