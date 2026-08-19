---
type: Practice
title: Teaching artifacts
description: The repo is meant to be learned from. What that obliges, beyond good tests.
tags: [documentation, teaching, process]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# The commitment

This project is **production-shaped and teaching-grade at once**. Confirmed at review: the
teaching material is in scope, not a nice-to-have.

# What ships

**`STUDY.md` — a staged narrative walkthrough.** Builds the buffer up in the order that
makes it comprehensible, not the order the files sit in:

1. A ring of fixed-size slots, single producer. No SBE, no claim/commit.
2. Variable-length records, and why the header carries the length.
3. Wrap, and why a message cannot straddle the end → padding records.
4. The negative-to-positive commit protocol, and what a consumer sees mid-write.
5. Multiple producers → the CAS loop and the head cache.
6. SBE, and why variable length breaks claim-before-encode →
   [D1](/decisions/d1-claim-commit-with-padding.md).
7. Memory ordering, and why stage 4 was already wrong on ARM64.

Stage 7 is deliberate: it revisits code the reader already believes is finished, which is
the honest way to teach memory ordering. Being told about barriers up front teaches nothing;
discovering that working code was broken all along does.

**Runnable stages.** Each stage is a real, tested project — not a snippet in prose. Stage
code that does not compile is the fastest way to lose a reader's trust, and it rots the
moment the real code moves.

**Commented hot paths.** The tricky functions carry comments explaining *why*, with links
into this bundle for the full reasoning. Not restating what the code does.

# The trade-off, stated

Where clarity and peak performance conflict, **favour clarity** — then comment the trade-off
and benchmark both. The benchmark is what keeps this honest: it turns "we chose the clear
version" into a number, so the cost of the choice is known rather than assumed.

# What this obliges

- `STUDY.md` stages are built and tested by the `full` gate lane. They are code, and
  code rots.
- A change to the real implementation that invalidates a stage must update the stage. See
  [bundle maintenance](knowledge-bundle-maintenance.md).
