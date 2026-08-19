---
type: Finding
title: "F3: The sbe-dll NuGet package is abandoned; vendor the runtime instead"
description: Latest published sbe-dll is 1.13.0 from ~2019, against a 1.39.0 generator.
tags: [sbe, dependencies, toolchain]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What was observed

Generated C# opens with `using Org.SbeTool.Sbe.Dll;` and needs `DirectBuffer`,
`ThrowHelper` and `PrimitiveValue` at runtime. NuGet state, queried directly:

| Package | Latest published | Verdict |
|---|---|---|
| `sbe-dll` | **1.13.0** (~2019) | abandoned |
| `sbe-tool` | **1.23.1.1** | the real runtime package — but 16 releases behind |
| `uk.co.real-logic:sbe-all` (Maven) | **1.39.0** | current |

Confusingly, the `sbe-dll` **project** publishes under the `sbe-tool` **package id** — so
the package that sounds like the generator is in fact the runtime DLL.

Pairing a 1.39.0 generator with a 1.23.1.1 runtime is version skew **across a codegen
boundary**: generated code calls runtime methods that may not exist in the older DLL. Observed
concretely — 1.39.0 output calls `ThrowHelper.ThrowCountOutOfRangeException` and
`DirectBuffer.SetBytes(int, ReadOnlySpan<byte>)`.

# The resolution

**Vendor `csharp/sbe-dll/*.cs` from the SBE repository at the same pinned tag as the
generator jar.** 1,851 lines of Apache-2.0 source across 7 files:

| File | Lines |
|---|---|
| `DirectBuffer.cs` | 846 |
| `PrimitiveValue.cs` | 534 |
| `PrimitiveType.cs` | 216 |
| `EndianessConverter.cs` | 129 |
| `SbePrimitiveType.cs` | 63 |
| `ThrowHelper.cs` | 46 |
| `ByteOrder.cs` | 17 |

Small enough to read, review and step through in a debugger — which is a **feature** for a
teaching project, not just a workaround.

# Consequence

A CI step re-fetches at the pinned tag and asserts the vendored copy is byte-identical, so the
pin cannot silently drift. Generator version and runtime version are bumped together, never
separately. See [the codec generation component](/architecture/codec-generation.md).
