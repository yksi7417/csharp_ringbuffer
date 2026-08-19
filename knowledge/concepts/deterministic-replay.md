---
type: Concept
title: Deterministic replay
description: Why the acceptance layer is a pure function from input journal to output journal.
tags: [testing, determinism, atdd, core]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# The contract

Adapted from the [reference project](/references/external-sources.md), whose phrasing is
worth keeping verbatim:

> `replay --input <journal> --output <journal> [--seed <n>]`
>
> No network, no clock, no filesystem beyond those two paths. It is a pure function from
> input journal to output journal.

# Why this shape

**Byte-for-byte diffing is only meaningful if the output is a function of the input alone.**
Every source of ambient variation has to be closed off, or the diff produces false failures
and gets disabled — which is how byte-exact suites usually die.

The three that matter here:

| Source | Closed by |
|---|---|
| wall-clock time | an `IClock` seeded per case |
| generated ids | a seeded counter |
| thread scheduling | the replay drains the ring **single-threaded** |

That last one deserves emphasis: the acceptance layer deliberately does **not** test
concurrency. It tests that the *format and the logic* are stable. Concurrency is
[L3](/testing/l3-concurrency.md)'s job, and mixing the two would make the corpus flaky and
the concurrency results unreadable.

Determinism here is a **design constraint**, not a property we hope holds.

# Why the journals are binary, not JSON

The fixture then pins **the wire format itself**. Bump the SBE version, change a generator
flag, reorder a group, break endianness on a new platform — every one of those shows up
immediately as a hex diff.

A JSON fixture would hide all of them, because it re-serialises through a decoder that
changed in lockstep. The binary fixture is the only artefact in the project that a
codegen change cannot silently update.

See [the conformance corpus](/architecture/conformance-corpus.md) for the layout.
