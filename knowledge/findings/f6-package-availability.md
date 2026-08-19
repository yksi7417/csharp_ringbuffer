---
type: Finding
title: "F6: All required NuGet packages are available"
description: Test, concurrency, property and benchmark tooling verified present with versions.
tags: [dependencies, testing, toolchain]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
stale_after: 2027-08-19
---

# What was observed

Queried against `api.nuget.org` on 2026-08-19:

| Package | Latest | Role |
|---|---|---|
| `xunit.v3` | 4.0.0 | unit + integration tests ([L1](/testing/l1-unit.md)) |
| `Reqnroll.xUnit` | 3.3.4 | Gherkin ATDD, the SpecFlow successor ([L4](/testing/l4-acceptance-replay.md)) |
| `Microsoft.Coyote` / `.Test` | 1.7.11 | systematic concurrency exploration ([L3](/testing/l3-concurrency.md)) |
| `FsCheck` | 3.3.4 | property-based tests ([L2](/testing/l2-property.md)) |
| `BenchmarkDotNet` | 0.16.0-preview.1 | latency histograms, allocation diagnostics ([L5](/testing/l5-performance.md)) |
| `Verify.XUnit` | 31.12.5 | snapshot tests for hex dumps |

# Consequence

No tooling gaps. The one to watch is BenchmarkDotNet, which is on a **preview** version —
acceptable for a benchmark project that does not ship, but it should not be load-bearing for
a gate that blocks merges until it goes stable.

This finding carries `stale_after: 2027-08-19` because version availability decays.
