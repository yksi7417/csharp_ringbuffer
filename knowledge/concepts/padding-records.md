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

The leftover after a short commit has **three cases and they are not symmetric**:

| Leftover | Action |
|---|---|
| `== 0` | Nothing to do. |
| `>= 8` | Write a padding header. |
| `< 8` | **Cannot hold a header.** Must be folded into the committed length. |

The third case is the one that will be got wrong, because it is the only one where the
committed length is **not** the encoder's actual length. Getting it wrong leaves an
unreadable gap that desynchronises the consumer for the rest of the buffer's life — a
failure that appears far from its cause.

It has dedicated tests in [L1](/testing/l1-unit.md), and randomised message sizes in
[L2](/testing/l2-property.md) exist largely to hit it.
