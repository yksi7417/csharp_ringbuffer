---
type: Component
title: Codec generation
description: The Java shim and the vendored runtime that turn schemas into C# codecs.
tags: [sbe, codegen, build, component]
resource: tools/sbe-csharp-gen
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: draft
---

# Status

**Prototype proven, not yet productionised.** The working shim is at
[`/references/evidence/SbeCsharpGen.java`](/references/evidence/SbeCsharpGen.java).

# Why this component exists at all

Because the documented path does not work. See
[F2](/findings/f2-csharp-codegen-requires-shim.md) — this is not a convenience wrapper, it is
the only way to reach the C# generator.

# The pipeline

```
schemas/fix-sbe.xml
  -> tools/sbe-csharp-gen (Java shim, sbe-all-<PINNED>.jar)
  -> src/RingBuffer.Codecs/**/*.g.cs
  -> compiled against src/RingBuffer.Sbe (vendored runtime, SAME pinned tag)
```

# The two pins move together

The generator jar version and the vendored runtime tag are **one decision, not two**.
Bumping either alone reintroduces exactly the version skew that
[F3](/findings/f3-sbe-dll-nuget-stale.md) documents.

Enforced by two gate steps:

1. **Vendored-runtime integrity** — re-fetch `csharp/sbe-dll/*.cs` at the pinned tag and
   assert the committed copy is byte-identical. The pin cannot silently drift.
2. **Codegen freshness** — regenerate and assert the tree is clean, via
   `git status --porcelain` (see [T-2](/practices/trap-log.md)).

# This component needs its own tests

It is the load-bearing step nobody else maintains for us. Its test generates a known schema
and asserts the output **compiles and round-trips** — not merely that files appeared.

A generator that writes nothing exits 0. That is
[T-1](/practices/trap-log.md), and it is why "files appeared" is not the assertion.

# Related

- [Bootstrap playbook](/playbooks/bootstrap-environment.md)
- [Regenerate codecs playbook](/playbooks/regenerate-codecs.md)
