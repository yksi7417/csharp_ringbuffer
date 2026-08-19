---
type: Architecture
title: Repository layout
description: Where everything lives and why, including what is generated and what is committed.
tags: [layout, build]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Layout

```
schemas/fix-sbe.xml               v1 schema -- the source of truth (D8)
schemas/fix-sbe-v2.xml            v2, for the evolution test
tools/sbe-csharp-gen/             the Java shim (F2) -- pinned and tested
src/RingBuffer.Sbe/               vendored SBE runtime (F3) -- pinned tag, checksum-verified
src/RingBuffer.Core/              the ring buffers (D2: SPSC and MPSC)
src/RingBuffer.Codecs/            generated codecs -- GENERATED, NOT COMMITTED
tests/RingBuffer.Core.Tests/      L1 unit, L2 property
tests/RingBuffer.Concurrency/     L3 Coyote + stress
tests/RingBuffer.Acceptance/      L4 Reqnroll features + replay harness
tests/conformance/corpus/         L4 golden binary fixtures
benchmarks/RingBuffer.Benchmarks/ L5 BenchmarkDotNet
scripts/ci/gate.sh                the one command (see practices)
scripts/ci/checks/                individual gate checks, each independently runnable
knowledge/                        this OKF bundle (D9)
docs/decisions/                   ADR stubs pointing at /decisions in this bundle
```

# Generated code is not committed

`src/RingBuffer.Codecs/` is regenerated from `schemas/` on every build.

A CI step regenerates and asserts the tree is clean. **It must use
`git status --porcelain` including untracked files, not `git diff`** — this is
[trap TRAP-2](/practices/trap-log.md), lifted directly from the reference project, where a
`git diff` check could not see an untracked generated file and passed green while the
generator was producing nothing.

# The dependency direction

```
schemas/  ->  tools/sbe-csharp-gen/  ->  src/RingBuffer.Codecs/
                                              |
src/RingBuffer.Sbe/  ------------------------->+--> tests, benchmarks
                                              |
src/RingBuffer.Core/  ------------------------>+
```

`RingBuffer.Core` **does not depend on the codecs**. The ring carries opaque bytes; SBE is
a payload concern. This is [D7](/decisions/d7-dual-header-framing.md) expressed as a project
reference graph, and it is what keeps the ring reusable and independently testable.

A test that the Core project has no reference to Codecs is worth having, because this is the
kind of layering that erodes one convenient `using` at a time.
