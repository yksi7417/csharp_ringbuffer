---
type: Practice
title: One gate command
description: scripts/ci/gate.sh is what CI runs and what the pre-push hook runs. There is no second list.
tags: [ci, guardrails, process]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# The rule

```bash
scripts/ci/gate.sh fast     # before every push -- also wired to .githooks/pre-push
scripts/ci/gate.sh full     # what a PR runs
scripts/ci/gate.sh nightly  # scheduled
```

Taken from the reference project, whose framing is exactly right:

> That is the same script CI runs [...] There is no separate list of "things CI checks" to
> keep in your head — if the gate is green, the fast lane in CI is green.

# Lanes

| Lane | Contains | When |
|---|---|---|
| `fast` | format, lint, OKF validate, codegen-clean, no-lock scan, L1, L2, L4, zero-alloc | every push |
| `full` | + coverage, conformance corpus, Coyote, **ARM64 leg** | every PR |
| `nightly` | + long stress, long Coyote, benchmark regression | scheduled |

# Why one script and not a CI config

A CI-only check cannot be run locally, so it is discovered at push time; a local-only check
does not block anyone. Both drift. A single script that both call is the only arrangement
where "it passed locally" and "it passed in CI" mean the same thing.

# Evidence at a checkpoint

`gate.sh` answers "is this push safe". `scripts/ci/evidence.sh` answers the different
question "is the tree still whole", by running every suite and reporting the counts.
Recording a checkpoint requires the second. See
[the implementation loop](implementation-loop.md).

# A lane listing a step with no dispatch case must FAIL

Not warn, not skip. This is [TRAP-3](trap-log.md) from the reference project — a gate step
listed in a lane with no matching dispatch case silently did nothing, and the lane went
green.

`--list` prints what a lane would run without running it, so the lane's contents can be
inspected without a full execution.
