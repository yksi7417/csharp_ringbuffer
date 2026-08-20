---
type: Finding
title: "F7: Reqnroll pins xUnit v2, so the acceptance project cannot use xunit.v3"
description: Reqnroll.xUnit 3.3.4 depends on xunit.core 2.8.1; the two versions coexist in a solution but not in a project.
tags: [testing, dependencies, atdd]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# What was observed

[F6](f6-package-availability.md) listed `xunit.v3` 4.0.0 and `Reqnroll.xUnit` 3.3.4 as both
available, which is true — but tacitly assumed they could be used together. They cannot, in
one project.

`Reqnroll.xUnit` 3.3.4 resolves to **xUnit v2**:

```
xunit.core/2.8.1
xunit.extensibility.core/2.8.1
xunit.extensibility.execution/2.8.1
xunit.abstractions/2.0.3
```

Pairing it with `xunit.runner.visualstudio` 3.1.5 (a v3 runner) builds, then fails with
`CS0103: The name 'Assert' does not exist` — because no assertion library is present at all:
the v3 package was not going to supply one to a v2 test, and Reqnroll does not bring one.

The message points at the symbol, not at the version mismatch underneath it.

# The resolution

The acceptance project runs on xUnit **v2**: `xunit.assert` 2.8.1 and
`xunit.runner.visualstudio` 2.8.1. The unit and algebra suites stay on `xunit.v3`.

Both versions coexist in the solution without trouble — `dotnet test` runs each project with
its own runner. The constraint is per project, not per repository.

# Consequence

- `tests/RingBuffer.Acceptance` must not take a dependency on anything expecting xunit.v3.
- The version split is deliberate and recorded, so a future tidy-up does not "unify" the
  versions and break the Gherkin layer.
- Watch for a Reqnroll release supporting xUnit v3; unifying then is a real simplification
  rather than a cosmetic one.
