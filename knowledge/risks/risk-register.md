---
type: Risk Register
title: Risk register
description: What could still go wrong, what is watching for it, and what has been retired.
tags: [risks, process]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Open

| ID | Risk | Severity | Watched by |
|---|---|---|---|
| **R4** | Weak-memory bugs are **invisible on x64** and pass every test. | high | The ARM64 CI leg ([D6](/decisions/d6-memory-model-and-arm64.md)). Confirmed in scope; not yet proven working. |
| **R5** | Allocation creeps onto the hot path via an ergonomic `string` overload. | medium | Hard zero-alloc gate in the fast lane, plus a lint rule on banned members ([L5](/testing/l5-performance.md)). |
| **R7** | Neither Coyote nor the ARM64 leg covers the other's failure mode, and one may later be dropped as "redundant". | medium | Recorded explicitly in [L3](/testing/l3-concurrency.md) and [the memory model concept](/concepts/dotnet-memory-model.md). **Accepted, mitigated by documentation only.** |
| **R8** | Teaching clarity and peak performance pull apart. | medium | Favour clarity, comment the trade-off, benchmark both ([teaching artifacts](/practices/teaching-artifacts.md)). **Accepted.** |
| **R9** | A conformance fixture gets regenerated to make a red test go green, converting the corpus into a rubber stamp. | medium | Review discipline plus [triangulation](/practices/triangulation.md)'s `STALE EXPECTATION` verdict. |

# Retired

| ID | Risk | Retired by |
|---|---|---|
| **R3** | [D1](/decisions/d1-claim-commit-with-padding.md)'s claim/commit padding was the project's main design risk: a genuine extension with no reference implementation to check against. | **Task 2.4**, which checks every `(claimed, actual)` pair to 4 KiB — over 8 million — against four invariants. It also **disproved** the design's scariest case: a sub-header leftover is unreachable while `Alignment >= HeaderLength`. Retired 2026-08-19, before any memory or concurrency existed to obscure a failure. |
| **R1** | SBE C# codegen unreachable via the documented CLI. *Was critical — would have blocked the project entirely.* | [F2](/findings/f2-csharp-codegen-requires-shim.md) — Java shim, verified working. |
| **R2** | Generator/runtime version skew across the codegen boundary. | [F3](/findings/f3-sbe-dll-nuget-stale.md) — vendor at a pinned tag with a checksum gate. |
| **R6** | `dotnet-install.sh` blocked, so web agent sessions cannot build. | [F1](/findings/f1-dotnet-install-egress-blocked.md) — apt in bootstrap, verified. |

# On R7

R7 is the risk of a **future correct-looking simplification**. Someone will eventually look at
two concurrency-testing mechanisms and remove one. The only defence available is writing down
why both exist, in the place they would look — which is done, and is why the risk is
recorded as accepted rather than mitigated.
