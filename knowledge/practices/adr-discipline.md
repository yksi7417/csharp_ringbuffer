---
type: Practice
title: ADR discipline
description: One decision per file, numbered, never deleted. They live in this bundle.
tags: [process, documentation, decisions]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# Where ADRs live

**In this bundle, at [/decisions](/decisions/index.md)** — as OKF concepts with
`type: Decision`, rather than in a separate `docs/decisions/` tree.

One decision per file. Numbered `D<n>`. `docs/decisions/` holds thin stubs pointing here,
so the conventional location still leads somewhere for a reader who looks there first.

Keeping them in the bundle means a decision is retrievable by the same mechanism as
everything else an agent loads, and is covered by
[the validator](/playbooks/validate-the-bundle.md).

# Structure

```markdown
# Context      -- the forces, including what makes this hard
# Decision     -- what we chose, unambiguously
# Consequences -- what follows, including what gets worse
# Alternatives rejected -- what else was considered, and why not
```

**"Alternatives rejected" is the section that earns the format.** Without it, a future
reader cannot tell a considered choice from an accident, and will re-litigate it — or worse,
"fix" it.

# Superseding, not deleting

A decision that no longer holds gets `status: deprecated` and a link to the one that
replaced it. **It is never deleted.**

The reasoning is the artefact. Deleting a decision destroys the record of why an obvious-looking
alternative was rejected, which guarantees someone proposes it again.

# When a change contradicts a decision

Stop and say so. Contradicting a recorded decision is a decision in itself, and it belongs to
the maintainer — see [agent onboarding](agent-onboarding.md).
