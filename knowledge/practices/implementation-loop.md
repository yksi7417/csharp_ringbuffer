---
type: Practice
title: The implementation loop
description: How to pick up a task from the plan, land it, and record it.
tags: [process, agents, planning]
resource: ../../IMPL/PLAN.md
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Where the plan lives, and why not here

The plan is at [`IMPL/PLAN.md`](../../IMPL/PLAN.md); position is at
[`IMPL/CHECKPOINT.md`](../../IMPL/CHECKPOINT.md). **Deliberately outside this bundle.**

This bundle holds knowledge that is **stable** — decisions, mechanics, verified findings. The
plan is **churn**: every landed task edits it. Keeping churn out means a diff touching
`knowledge/` signals that something we believed has actually changed, rather than that a
checkbox moved.

# The loop

1. Open [`CHECKPOINT.md`](../../IMPL/CHECKPOINT.md) — it names the next unblocked task.
   Otherwise: the first unstarted task in [`PLAN.md`](../../IMPL/PLAN.md) whose **Needs** are all
   done.
2. Load the concepts that task needs — [agent onboarding](agent-onboarding.md) maps task
   types to concepts.
3. **Write the failing test first.** Every task's *Done when* is phrased so it can be a test.
4. Implement until it passes.
5. `scripts/ci/gate.sh fast`.
6. Commit the code, the plan tick and the checkpoint **together**.
7. If the task changed something this bundle asserts, update the bundle in the same commit —
   see [bundle maintenance](knowledge-bundle-maintenance.md).

# Commit convention

```
feat(4.6): commit shorter than claimed emits a padding record
test(2.4): exhaustive PaddingPlan over all claimed/actual pairs
chore(0.6): gate.sh lanes and --list
```

The task ID in the subject makes `git log --oneline` a record of plan progress, so the plan
and the history cannot silently disagree.

# When a task turns out to be wrong

The plan is a sequencing hypothesis, not a contract. If a task is mis-specified, unnecessary,
or in the wrong order:

- **Say so and amend the plan** in the same commit. A plan quietly worked around is worse
  than one openly revised — the next reader trusts it either way.
- If the change contradicts a [decision](/decisions/index.md), stop. That is the maintainer's
  call, not something to route around ([ADR discipline](adr-discipline.md)).
- If you defer part of a task, register it ([deferred-work register](deferred-work-register.md)).

[`PLAN.md`](../../IMPL/PLAN.md) ends with a *What would make me revise this plan* section, naming
the three assumptions most likely to break. Those are the expected revisions, not failures.

# One behaviour per commit

Success criterion S6 is that every behaviour arrived test-first, **evidenced by git history**.
A commit adding behaviour with no test in the same or a prior commit is a review finding.

This is only checkable if commits stay small. A commit that lands three tasks destroys the
evidence even when the work was done correctly.
