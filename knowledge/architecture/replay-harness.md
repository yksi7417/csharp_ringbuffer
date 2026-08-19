---
type: Component
title: Replay harness
description: The pure input-journal to output-journal function that the acceptance layer diffs.
tags: [testing, atdd, determinism, component]
resource: tests/RingBuffer.Acceptance
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: draft
---

# Status

**Not yet implemented.** Design agreed at review.

# The contract

```
replay --input <journal> --output <journal> [--seed <n>]
```

No network, no clock, no filesystem beyond those two paths. See
[deterministic replay](/concepts/deterministic-replay.md) for why this shape is required
rather than merely tidy.

# Journal format

A journal is a flat sequence of **length-prefixed SBE frames**:

```
uint32 frameLength (little-endian)
byte[] frame        -- SBE MessageHeader followed by the message body
```

Deliberately **not** the ring's record format. The journal is a transport-independent
serialisation, so a fixture stays valid if the ring's internal framing ever changes. Coupling
them would mean a ring-layout change invalidated every fixture, which would make the corpus
expensive to keep and therefore the first thing to be abandoned.

# What it does

1. Read frames from `--input`.
2. Publish each into the ring under test via `TryClaim`/`Commit`.
3. **Drain single-threaded** — the acceptance layer tests format and logic, not concurrency.
4. Write consumed frames to `--output`.
5. The harness diffs `--output` against `expected.sbe` **byte-for-byte**.

# The differ

A raw byte offset is useless at 3am. The differ reports:

- the offset of the first difference
- **the decoded field at that offset**, resolved through the schema
- expected versus actual, as hex with the surrounding context

Building that field resolution is real work and it is the difference between a corpus people
use and a corpus people mute.

# Triangulation

The same input journal is run through **SPSC, MPSC, and a trivially-correct
`List<byte[]>` reference queue**. See [the practice](/practices/triangulation.md).
