---
type: Concept
title: Padding records
description: Skip records that bridge the end of the buffer and absorb over-claimed space.
tags: [ring-buffer, layout, d1]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What they are

A padding record is an ordinary record header whose `type` is `PADDING_MSG_TYPE_ID`. The
consumer advances past it without invoking the handler. Its payload is meaningless.

# Two reasons they exist

**1. Wrap (from Agrona).** A message must be contiguous — a decoder cannot straddle the end
of the data region. When the remaining space at the end is too small, the producer fills it
with a padding record and starts the message at index 0.

**2. Over-claim (ours, from [D1](/decisions/d1-claim-commit-with-padding.md)).** A producer
claims `maxLength` because SBE variable-length data means the true length is unknown until
encoding finishes. When it commits a shorter actual length, the leftover becomes a padding
record.

Reason 2 does not exist in Agrona. It is the project's one genuine extension, and the
consumer needs no change to support it because it already skips padding for reason 1.

# The boundary cases

The leftover after a short commit has **exactly two cases**:

| Leftover | Action |
|---|---|
| `== 0` | Nothing to do. |
| `>= 8` | Write a padding header. |

**A leftover of 1–7 bytes cannot occur.** Both the claimed and the committed record lengths
are multiples of `Alignment`, so their difference is too; and `Alignment >= HeaderLength`, so
any non-zero difference is large enough to hold a padding header.

This concept previously described a third case — a sub-header leftover folded into the
committed length — and named it the one most likely to be got wrong. Task 2.4 checked every
`(claimed, actual)` pair to 4 KiB and found it unreachable. Corrected 2026-08-19.

**What still matters** is the invariant underneath, and that is what the tests assert:
committed length plus padding length must cover the claim **exactly**. Under-covering leaves
an unreadable gap that desynchronises the consumer for the rest of the buffer's life — a
failure that appears far from its cause. Over-covering corrupts the next record.

The guarantee is structural, not defensive, so it is easy to break by accident:
`RecordDescriptor.Alignment` dropping below `HeaderLength` would reintroduce the case.
`PaddingPlan.For` throws rather than silently emitting an unusable padding record.
