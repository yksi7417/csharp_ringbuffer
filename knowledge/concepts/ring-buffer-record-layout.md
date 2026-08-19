---
type: Concept
title: Ring buffer record and trailer layout
description: The on-wire layout of a ring record and the counters in the trailer, following Agrona.
tags: [ring-buffer, layout, agrona]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Record layout

Read from the Agrona source rather than from blog posts. `HEADER_LENGTH = 8`,
`ALIGNMENT = 8`.

```
offset +0 : int32  length   (negative = claimed/in-flight, positive = committed)
offset +4 : int32  type     (msgTypeId; PADDING_MSG_TYPE_ID marks a skip record)
offset +8 : payload         (for us, starts with the SBE MessageHeader -- see D7)
```

`checkTypeId` requires `msgTypeId > 0`, which is what frees negative values to be used as
sentinels.

# Trailer layout

Past the power-of-two data region, **each field on its own cache line**
([D5](/decisions/d5-cache-line-padding.md)):

| Field | Written by | Purpose |
|---|---|---|
| `TAIL_POSITION` | producers (CAS) | next claim position |
| `HEAD_CACHE_POSITION` | producers | cached consumer position, to avoid reading the real head |
| `HEAD_POSITION` | consumer | actual consumer position |
| `CORRELATION_COUNTER` | producers | monotonic id source |
| `CONSUMER_HEARTBEAT` | consumer | liveness; reserved now, used by [D10](/decisions/d10-cross-process-deferred.md) |

# Why positions are monotonic

Head and tail are **monotonically increasing 64-bit counters**, not indices. The index into
the data region is `position & (capacity - 1)`, which is why capacity must be a power of two.

Keeping them monotonic is what makes `tail - head` an unambiguous measure of occupancy. If
they wrapped, a full buffer and an empty buffer would look identical.

The arithmetic at `long.MaxValue` is unreachable in practice and trivially testable by
seeding the trailer. It is tested — see [L1](/testing/l1-unit.md). Untested arithmetic that
"can't happen" is where these buffers actually break.

# Head caching

A producer needs to know the consumer's position to check for space. Reading the real
`HEAD_POSITION` on every claim would pull the consumer's cache line to every producer core
on every attempt.

Instead producers read `HEAD_CACHE_POSITION`, and only re-read the real head **when the
cached value says there is not enough space**. When the buffer is not close to full — the
common case — the consumer's line is never touched.
