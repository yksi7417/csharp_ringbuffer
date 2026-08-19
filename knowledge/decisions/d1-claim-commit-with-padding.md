---
type: Decision
title: "D1: Claim a bound, commit the actual, pad the remainder"
description: Resolves the conflict between SBE variable-length encoding and zero-copy claim-before-encode.
tags: [ring-buffer, sbe, zero-copy, core-design]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

This is the central design problem of the project, and the one with no reference
implementation to copy.

Zero copy means the producer encodes **into** the ring's memory, which means space must be
claimed **before** encoding starts. But SBE messages with repeating groups and variable-length
data have a length that is **not known until encoding finishes** — the group count and the
var-data lengths are only settled as the encoder walks the message.

Claim-before-encode and length-known-after-encode are in direct conflict.

Agrona does not solve this. Its `tryClaim(length)` takes an exact length and `commit()`
publishes exactly that. See [the record layout concept](/concepts/ring-buffer-record-layout.md).

# Decision

**Option C: claim a bound, commit the actual, pad the remainder.**

1. `TryClaim(maxLength, out Claim)` advances the tail by `maxLength`.
2. The producer encodes into `claim.Span`, which points into the ring slab.
3. `claim.Commit()` writes the true aligned length into the record header.
4. If claimed space is left over, Commit writes a **second record header** at the leftover
   offset carrying `PADDING_MSG_TYPE_ID`.

The consumer already skips padding records, so this needs no consumer change. See
[padding records](/concepts/padding-records.md).

# Consequences

- Zero copy is preserved. This is the whole point.
- Cost is one 8-byte header when the estimate overshoots.
- **The leftover has only two cases, not three.** This was established by task 2.4, which
  checks every `(claimed, actual)` pair to 4 KiB:
  - leftover `== 0` — nothing to do.
  - leftover `>= 8` — write a padding header.

  **Corrected 2026-08-19.** This decision originally described a third case — a leftover of
  fewer than 8 bytes, too small to hold a header, needing to be folded into the committed
  length — and called it "the one that will be got wrong". It is **unreachable by
  construction**: both the claimed and committed record lengths are multiples of `Alignment`,
  so their difference is too, and `Alignment >= HeaderLength` means any non-zero difference
  has room for a header.

  The design is therefore simpler than it was specified to be, and the reasoning now rests on
  a relation between two constants rather than on a branch. `PaddingPlan.For` asserts that
  relation at runtime instead of assuming it, because it stops holding the moment someone
  sets `Alignment` below `HeaderLength`.
- This is a deliberate extension beyond Agrona, so it carries
  [R3](/risks/index.md), the project's main design risk. It is covered by dedicated boundary
  tests ([L1](/testing/l1-unit.md)), randomised sizes ([L2](/testing/l2-property.md)), and
  triangulation against the reference queue ([L4](/testing/l4-acceptance-replay.md)).

# Alternatives rejected

| Option | Zero-copy | Why rejected |
|---|---|---|
| A. Encode to a scratch buffer, then copy in | no | Defeats the entire premise of the project. |
| B. Compute the exact length up front | yes | Requires the caller to know group counts and var-data lengths before encoding, which leaks the encoding into every call site. More conservative, much worse ergonomics. |
