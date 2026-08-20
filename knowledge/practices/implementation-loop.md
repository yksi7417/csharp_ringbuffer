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
6. **Produce regression evidence before recording the checkpoint:**

   ```bash
   scripts/ci/evidence.sh --markdown
   ```

   It builds, runs **every existing test suite**, runs `gate.sh full`, and exits
   non-zero if anything is red — so it cannot be used to record a green checkpoint
   over a broken tree. Paste its output into
   [`CHECKPOINT.md`](/../IMPL/CHECKPOINT.md).
7. Commit the code, the plan tick and the checkpoint **together**.
8. **Push to `main` at a phase boundary**, not mid-phase. Commits accumulate locally
   through a phase; the push happens when the phase's exit criterion is met and the
   evidence block is green.
7. If the task changed something this bundle asserts, update the bundle in the same commit —
   see [bundle maintenance](knowledge-bundle-maintenance.md).

# One check-in per phase

A phase is the unit of review. Its exit criterion is a statement someone can check
("both rings pass the same corpus", "R3 is retired"), and a push carrying half a
phase asks a reviewer to evaluate an argument that is not finished yet.

Commit as often as the work suggests — one behaviour per commit is still the rule,
and it is what makes the TDD history checkable (S6). But **push when the phase
lands**, with the evidence block covering the whole phase rather than a slice of it.

The exception is a fix to something already on `main` — a red gate, a broken CI lane.
That goes immediately, because leaving `main` red to preserve a cadence is the wrong
trade.

# Why evidence, and not just "the gate was green"

A checkpoint is a claim about the state of the whole tree, not about the task just
finished. Claiming it without re-running the suites is how a green checkpoint comes
to sit on top of a regression introduced two tasks ago — every individual change
looked fine in isolation.

The evidence block records **which suites ran and how many assertions passed**, so a
later reader can tell the difference between "40 tests passed" and "the 3 tests I
happened to run passed". A count that silently drops is itself a regression.

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
