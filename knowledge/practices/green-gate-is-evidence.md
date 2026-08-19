---
type: Practice
title: A green gate is evidence, not proof
description: Most things that go wrong look green while they are wrong. What to do when something fools you.
tags: [guardrails, process, quality]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# The observation

From the reference project, which had earned it 34 times over:

> [Their trap log] records 34 things that have actually gone wrong here, and most of them
> **looked green while they were wrong**: a generator whose stderr was redirected to
> `/dev/null` and wrote nothing, a `git diff` that could not see an untracked generated
> file, a gate step listed in a lane with no dispatch case, sanitizers that compiled the tree
> and never ran a test.

Every one of those is a check that **passed without checking anything**. That is a different
failure class from a bug, and it is more dangerous, because the signal that would normally
alert you is precisely what is broken.

# The rule

**When something fools you, the fix is not finished until you have asked whether a check
could have caught it.**

If one could, add it in the same change. If one could not, record it in
[the trap log](trap-log.md) so the next person knows the gap exists.

# The shape to look for

A check is suspect when it can pass **without doing its work**:

- Does it assert on **output**, or merely on **exit code**?
- Would it notice if the tool it invokes produced **nothing at all**?
- Is its output redirected somewhere nobody reads?
- If the thing it checks were deleted entirely, would it go red?

That last question is the sharpest one, and it is worth actually running rather than
reasoning about. Delete the thing; watch the check fail; put it back.

# Applied here

This is why:

- codegen freshness uses `git status --porcelain` including untracked files, not
  `git diff` ([TRAP-2](trap-log.md))
- the codec generator's test asserts the output **compiles and round-trips**, not that files
  appeared ([TRAP-1](trap-log.md))
- [false sharing](/concepts/false-sharing.md) has a deliberately-unpadded benchmark, so the
  padding's value stays a visible number
- a lane with an undispatched step **fails** ([TRAP-3](trap-log.md))
