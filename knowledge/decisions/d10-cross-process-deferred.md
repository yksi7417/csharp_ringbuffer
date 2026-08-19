---
type: Decision
title: "D10: Cross-process shared memory is an explicit later phase"
description: Not built now, but the trailer layout stays position-independent so it can be.
tags: [roadmap, memory, ipc]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

A ring buffer in shared memory across processes is the natural sequel to this project — it is
what Aeron's own IPC path is. It is also a large amount of extra work (lifecycle, crash
recovery, a consumer heartbeat that means something).

# Decision

**Confirmed as an explicit later phase.** Not built now.

But the design must not foreclose it: **the trailer layout stays position-independent from
the start**, and no absolute pointer is stored inside the buffer.

# Consequences

- Costs nothing today. Retrofitting position-independence later would mean changing the
  on-wire layout, which would invalidate every committed
  [conformance fixture](/architecture/conformance-corpus.md).
- Concretely: all offsets are relative to the slab base; the trailer holds no managed
  references; `CONSUMER_HEARTBEAT` is reserved in the layout now even though nothing writes
  it yet.
- The backing swap is then `NativeMemory` → `MemoryMappedFile`
  ([D4](d4-native-memory-slab.md)) with no format change.
- Registered in [the deferred-work register](/practices/deferred-work-register.md) so it
  cannot quietly evaporate.
