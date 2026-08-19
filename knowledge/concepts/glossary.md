---
type: Glossary
title: Glossary
description: Terms used throughout this bundle, with the meaning they carry here.
tags: [reference]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Terms

| Term | Meaning here |
|---|---|
| **ATDD** | Acceptance Test Driven Development. Acceptance tests are written **before** the components they exercise. |
| **Attested / conformance case** | A committed fixture pair (`input.sbe`, `expected.sbe`) reviewed like source code. |
| **Claim** | A reserved span of the ring that a producer may write into but has not yet committed. |
| **Commit point** | The store-release of a positive record length. Before it the record is invisible; after it, visible. |
| **DirectBuffer** | The SBE C# runtime type that wraps memory for codecs. See [F4](/findings/f4-directbuffer-native-pointer.md). |
| **Flyweight** | A codec that holds no data and reads/writes through to a wrapped buffer. |
| **Head / tail** | Monotonic 64-bit **positions**, not indices. Index is `position & (capacity - 1)`. |
| **Lapping** | The producer overtaking the consumer. Prevented by the capacity check; its symptoms are prevented by zeroing on consume. |
| **MPSC / SPSC** | Multi-producer single-consumer / single-producer single-consumer. Both are built — [D2](/decisions/d2-spsc-and-mpsc.md). |
| **Over-claim** | Claiming `maxLength` because the true SBE length is unknown until encoding completes. |
| **Padding record** | A skip record the consumer advances past. Two causes — see [padding records](padding-records.md). |
| **SBE** | Simple Binary Encoding. Fixed-layout binary codec with a schema and generated flyweights. |
| **Trailer** | The metadata region past the data region, holding the cache-line-isolated counters. |
| **Triangulation** | Judging implementations against **each other**, not against one reference. See [the practice](/practices/triangulation.md). |
| **Zero copy** | One write at the final address, one read from it. See [the concept](zero-copy-in-dotnet.md). |
