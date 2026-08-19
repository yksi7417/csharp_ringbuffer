---
type: Decision
title: "D8: Two FIX messages, both with repeating groups, plus a v1/v2 evolution pair"
description: NewOrderSingle with NoPartyIDs, and MarketDataIncrementalRefresh with a nested group.
tags: [sbe, fix, schema, testing]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

The project needs FIX messages that genuinely exercise repeating groups, not a toy schema
that would let group-handling bugs hide.

# Decision

`schemas/fix-sbe.xml` defines two messages:

1. **`NewOrderSingle`** with a `NoPartyIDs` (453) repeating group — the canonical FIX group.
2. **`MarketDataIncrementalRefresh`** with a `NoMDEntries` (268) group containing a
   **nested** `NoPartyIDs` group and a variable-length `text` field.

Plus `schemas/fix-sbe-v2.xml`: v2 appends a field and a group to v1.

# Consequences

- **Nesting is the point.** It is where flyweight limit-management goes wrong — the inner
  group advances the shared `Limit` on the parent message, so a decoder that reads the outer
  group's fields after walking the inner group reads garbage. A single-level group would not
  catch this.
- Already proven to generate and round-trip end to end — see
  [F5](/findings/f5-end-to-end-roundtrip-proof.md). The schema in
  [the probe evidence](/references/evidence/index.md) is the prototype for the real one.
- The v1/v2 pair gives us **schema evolution as an acceptance test**: a v2 codec must decode
  a v1 golden fixture correctly via `actingVersion`. One of the strongest tests available
  to us, and it costs almost nothing.
