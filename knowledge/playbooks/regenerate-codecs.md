---
type: Playbook
title: Regenerate the SBE codecs
description: After changing a schema, or when the codegen-freshness gate step fails.
tags: [sbe, codegen, build]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Trigger

You changed `schemas/*.xml`, or `scripts/ci/gate.sh fast` failed on codegen freshness.

# Steps

```bash
scripts/generate-codecs.sh          # wraps the Java shim
dotnet build
scripts/ci/gate.sh fast
```

# What the script does

```bash
java -cp sbe-all-<PINNED>.jar:tools/sbe-csharp-gen/out \
     SbeCsharpGen schemas/fix-sbe.xml src/RingBuffer.Codecs
```

**Not** `-Dsbe.target.language=CSharp` — that does not work. See
[F2](/findings/f2-csharp-codegen-requires-shim.md) for why, before "fixing" the script to use
the documented flag.

# If you are bumping the SBE version

**Both pins move together**, always:

1. The generator jar version.
2. The vendored runtime tag in `src/RingBuffer.Sbe/`.

Bumping either alone reintroduces the version skew in
[F3](/findings/f3-sbe-dll-nuget-stale.md). The vendored-runtime integrity gate step will
catch it, but knowing why saves the debugging.

# Gotchas

- Generated code is **not committed**. If `git status` shows new untracked files under
  `src/RingBuffer.Codecs/`, that is expected — they are gitignored.
- **Never edit generated code.** Change the schema.
- If the generator appears to succeed but writes nothing, check that its stderr is not being
  discarded — that is [T-1](/practices/trap-log.md), and it exits 0.
