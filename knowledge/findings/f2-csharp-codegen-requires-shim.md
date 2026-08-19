---
type: Finding
title: "F2: SBE's documented C# codegen path does not work; a Java shim is required"
description: TargetCodeGeneratorLoader registers no CSharp target on any current release, so -Dsbe.target.language cannot reach the generator.
tags: [sbe, codegen, toolchain, critical]
resource: /references/evidence/SbeCsharpGen.java
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What was observed

The SBE C# README points at `./gradlew generateCSharpCodecs`. Underneath, the documented
CLI path is `-Dsbe.target.language=<X>`. **That path cannot reach the C# generator.**

`TargetCodeGeneratorLoader` — the enum `SbeTool` resolves `sbe.target.language`
against — registers only five targets. Verified by decompiling the shipped jars:

```
1.27.0: JAVA C CPP GOLANG RUST
1.30.0: JAVA C CPP GOLANG RUST
1.35.6: JAVA C CPP GOLANG RUST
1.39.0: JAVA C CPP GOLANG RUST
```

`CSharpGenerator` **is** in the jar and is actively maintained — a 73 KB class, plus a
`CSharpDtoGenerator`. It simply has no no-arg constructor, so the loader cannot instantiate
it:

```
java.lang.IllegalArgumentException: No code generator for name: ...csharp.CSharpGenerator
Caused by: java.lang.NoSuchMethodException: ...CSharpGenerator.<init>()
```

# The resolution, verified working

Drive the generator through its Java API with an ~18-line shim we own:

```java
final MessageSchema schema = SbeTool.parseSchema(schemaFile);
final Ir ir = new IrGenerator().generate(schema, schema.packageName());
final OutputManager om = new CSharpNamespaceOutputManager(outputDir, ir.applicableNamespace());
new CSharpGenerator(ir, om).generate();
```

Compiled against `sbe-all-1.39.0.jar` and run against a FIX-style schema with a **nested**
repeating group and var-length data. Full source at
[`/references/evidence/SbeCsharpGen.java`](/references/evidence/SbeCsharpGen.java).

# Consequence

`tools/sbe-csharp-gen/` is a **first-class, version-pinned part of this repository**, not a
build detail. It needs its own test — generate a known schema, assert the output compiles and
round-trips — because it is the load-bearing step that nobody else maintains for us.

This finding is why the research phase existed. Discovering it mid-implementation would have
stalled the project.
