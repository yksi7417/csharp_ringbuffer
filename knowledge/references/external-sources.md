---
type: Reference
title: External sources
description: The upstream material this project is built on, and what each one contributed.
tags: [reference, sources]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Simple Binary Encoding

| Source | Contributed |
|---|---|
| [SBE C# README](https://github.com/aeron-io/simple-binary-encoding/blob/master/csharp/README.md) | The documented codegen path — which [does not work](/findings/f2-csharp-codegen-requires-shim.md). |
| [`csharp/sbe-dll` at tag 1.39.0](https://github.com/aeron-io/simple-binary-encoding/tree/1.39.0/csharp/sbe-dll) | The vendored runtime. `DirectBuffer`, `ThrowHelper`, `PrimitiveValue`. |
| [`uk.co.real-logic:sbe-all` on Maven Central](https://repo1.maven.org/maven2/uk/co/real-logic/sbe-all/) | The generator jar. Current where NuGet is stale. |

# Agrona ring buffers

| Source | Contributed |
|---|---|
| [`ManyToOneRingBuffer`](https://github.com/real-logic/agrona/blob/master/agrona/src/main/java/org/agrona/concurrent/ringbuffer/ManyToOneRingBuffer.java) | `claimCapacity`, head caching, padding-on-wrap, bounded read, zero-on-consume. |
| [`RecordDescriptor`](https://github.com/real-logic/agrona/blob/master/agrona/src/main/java/org/agrona/concurrent/ringbuffer/RecordDescriptor.java) | The 8-byte record header, `ALIGNMENT`, `PADDING_MSG_TYPE_ID`, `checkTypeId`. |

**What Agrona does not give us:** a way to commit a shorter length than was claimed. That gap
is [D1](/decisions/d1-claim-commit-with-padding.md), the project's one genuine extension.

# LMAX Disruptor

[Disruptor](https://lmax-exchange.github.io/disruptor/) — contributed cache-line padding of
hot counters, pluggable wait strategies, and "pre-allocate everything".

**Deliberately not adopted:** its fixed-size object-slot structure. A fixed slot cannot hold
a variable-length SBE message with an arbitrary group count without either wasting space or
capping the group. Borrow the ideas, not the structure.

# cross_asset_ems

[yksi7417/cross_asset_ems](https://github.com/yksi7417/cross_asset_ems) — the quality model.
Its **testing architecture** proved more valuable to this project than its domain code.

| From | Adopted as |
|---|---|
| `conformance/README.md` — the pure input-journal to output-journal contract | [deterministic replay](/concepts/deterministic-replay.md), [the corpus](/architecture/conformance-corpus.md) |
| `conformance/harness/triangulate.py` | [triangulation](/practices/triangulation.md) |
| `scripts/ci/gate.sh` | [one gate command](/practices/one-gate-command.md) |
| `docs/polyglot/traps.md` | [green gate is evidence](/practices/green-gate-is-evidence.md), [trap log](/practices/trap-log.md) |
| `CONTRIBUTING.md` — the deferred-work register | [deferred-work register](/practices/deferred-work-register.md) |
| `docs/decisions/` | [ADR discipline](/practices/adr-discipline.md) |
| `tests/README.md` — the pyramid and the unit/cross-module split | [the five test layers](/testing/test-pyramid.md) |

Adopted with the fixture format changed from `.jsonl` to **binary SBE frames**, which is
strictly stronger: the fixture then pins the wire format itself.

# Format and tooling

- [Open Knowledge Format v0.2 spec](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md) — the format of this bundle ([D9](/decisions/d9-okf-knowledge-bundle.md)).
- [Microsoft Coyote](https://microsoft.github.io/coyote/) — [L3](/testing/l3-concurrency.md).
- [Reqnroll](https://reqnroll.net/) — [L4](/testing/l4-acceptance-replay.md).
- [BenchmarkDotNet](https://benchmarkdotnet.org/) — [L5](/testing/l5-performance.md).
