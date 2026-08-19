# Practices

The guardrails that keep quality from eroding as the project grows. Most are adapted from
[the reference project](/references/external-sources.md), which earned them.

**If you are an agent, start with [agent onboarding](agent-onboarding.md).**

# For agents

* [Agent onboarding](agent-onboarding.md) - Which concepts to load for which task, the rules that apply to every task, and what not to do.

# Gate and quality

* [One gate command](one-gate-command.md) - `scripts/ci/gate.sh` is what CI runs and what the pre-push hook runs. There is no second list.
* [A green gate is evidence, not proof](green-gate-is-evidence.md) - Most things that go wrong look green while wrong. What to do when something fools you.
* [Trap log](trap-log.md) - Checks that passed without checking anything. Inherited entries and our own.
* [Triangulation](triangulation.md) - Judge implementations against each other. Catches the stale expectation a reference-diff structurally cannot.

# Process

* [ADR discipline](adr-discipline.md) - One decision per file, numbered, superseded rather than deleted.
* [The implementation loop](implementation-loop.md) - How to pick up a task from `IMPL/PLAN.md`, land it, and record it. Includes why the plan lives outside this bundle.
* [Deferred-work register](deferred-work-register.md) - Every conscious deferral registered in the same change, enforced bidirectionally.
* [Keeping this bundle current](knowledge-bundle-maintenance.md) - When a change obliges a documentation update, and why derived beats asserted.
* [Teaching artifacts](teaching-artifacts.md) - What "teaching-grade" obliges beyond good tests.
