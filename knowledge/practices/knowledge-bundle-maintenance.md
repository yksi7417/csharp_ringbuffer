---
type: Practice
title: Keeping this bundle current
description: When a change obliges a bundle update, and how the obligation is enforced.
tags: [documentation, okf, guardrails, agents]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# The failure this prevents

Prose goes stale **silently**. The reference project put it plainly, about its own docs:

> This paragraph has been stale before — it claimed "journal codec and identifiers only"
> while six further components were live. Nothing checks prose against the corpus directory;
> when they disagree, trust `run.sh --list`.

A knowledge bundle that drifts is worse than no bundle, because an agent will **trust** it
and act on stale context with full confidence.

# When an update is obliged

| You changed | Update |
|---|---|
| behaviour that a decision describes | that [decision](/decisions/index.md), or supersede it |
| the ring's layout or protocol | the relevant [concept](/concepts/index.md) |
| a component's shape | the [architecture](/architecture/index.md) concept, and its `status` |
| what a test layer covers | the [testing](/testing/index.md) concept |
| a toolchain fact | the [finding](/findings/index.md) — or add one |
| anything structural | `log.md` |

# Prefer structure that CANNOT go stale

The strongest lesson from the reference project. Where a fact can be **derived** rather than
asserted, derive it:

- A section index that lists files is better generated than hand-maintained.
- A count ("eight corpus cases") in prose **will** go stale. Link to the directory instead.
- A claim about what CI runs should point at
  [the gate script](one-gate-command.md), not restate its contents.

The general rule: **do not write down anything a script could tell you**, because the
written copy has no way to notice when it becomes false.

# Enforcement

- `scripts/ci/checks/okf_validate.py` enforces OKF conformance plus our house rules —
  frontmatter, `type`, no broken bundle-relative links, every concept reachable from an
  index. Runs in the `fast` lane.
- `status: draft` on architecture concepts marks *designed but not built*. When the code
  lands, the status moves to `stable` in the same change.
- `stale_after` on time-sensitive findings (such as
  [F6](/findings/f6-package-availability.md)) makes decay visible instead of invisible.

# What the validator cannot check

It cannot tell whether the prose is **true**. Nothing can. That is what review is for, and
why [green gate is evidence, not proof](green-gate-is-evidence.md) applies here as much as to
the code.
