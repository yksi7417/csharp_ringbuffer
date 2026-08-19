---
type: Practice
title: Trap log
description: Things that looked green while they were wrong. Inherited entries and our own.
tags: [guardrails, quality, process]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# What this is

A register of failure modes where **a check passed without checking anything**. See
[green gate is evidence, not proof](green-gate-is-evidence.md) for why this class deserves
its own log.

The live log is `docs/TRAPS.md`. This concept explains the practice and seeds it with the
traps inherited from the reference project that apply directly to us.

# Inherited traps that apply here

| ID | Trap | Our guard |
|---|---|---|
| T-1 | A generator whose stderr went to `/dev/null` wrote nothing, and the build passed. | The codec generator's test asserts output **compiles and round-trips**. Generator stderr is never discarded. |
| T-2 | A `git diff` check could not see an **untracked** generated file, so codegen drift passed green. | Codegen freshness uses `git status --porcelain` **including untracked files**. |
| T-3 | A gate step listed in a lane had no dispatch case, so the lane silently skipped it and went green. | A lane listing an undispatched step **fails**. |
| T-4 | Sanitizers compiled the tree and never ran a test. | Any analysis step must assert it **executed** tests, not that it built them. |

# Adding an entry

Format, matching the deferred-work register's discipline:

```markdown
## T-n — Short imperative title

**What looked green:** the check, and what it was supposed to catch.

**Why it did not catch it:** the mechanism.

**Guard:** what now catches it, or "none — known gap".
```

An entry with **"none — known gap"** is legitimate and useful. Not every trap has a cheap
guard, and a recorded gap is worth far more than a pretended cover.
