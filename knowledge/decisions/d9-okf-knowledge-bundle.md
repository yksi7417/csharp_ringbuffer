---
type: Decision
title: "D9: Project context lives in an OKF bundle, validated by CI"
description: One concept per file with typed frontmatter, so agents can find context without reading the repo.
tags: [documentation, agents, guardrails, okf]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
verified:
  - { by: human:yksi7417, at: 2026-08-19T00:00:00Z }
status: stable
---

# Context

A single growing `RESEARCH.md` works at 500 lines and fails at 5,000. An agent picking up a
task has to read all of it or none of it, and there is no way to tell which parts are still
true. The reference project's own warning applies directly: prose goes stale silently, and
nothing checks prose against reality.

# Decision

Project context lives in `knowledge/`, an **[OKF](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md)
v0.2 bundle**: one concept per file, YAML frontmatter with a required `type`, cross-linked
with bundle-relative paths, sectioned by `index.md` files for progressive disclosure.

Conformance is enforced by `scripts/ci/checks/okf_validate.py` in the `fast` gate lane.

# Consequences

- An agent loads **the three or four concepts its task needs**, not the whole history. That
  is the entire reason for the format, and it is what keeps this working at scale.
- `type` and `tags` make the bundle filterable without a database.
- `status` and `stale_after` give claims a **lifecycle**, so a superseded decision is
  marked rather than deleted — the reasoning survives.
- `verified: [{ by: human:... }]` distinguishes what a human signed off from what an agent
  asserted. The decisions reviewed on 2026-08-19 carry it; the rest do not, and that
  difference is legible to a consumer.
- **The validator is stricter than the spec.** OKF says consumers MUST NOT reject a bundle
  for broken cross-links; we reject our own, because a producer-side house rule is exactly
  where that should be caught. See [the validator playbook](/playbooks/validate-the-bundle.md).
- Cost: every behavioural change now has a documentation obligation. That is the point, and
  [the maintenance practice](/practices/knowledge-bundle-maintenance.md) says when it applies.
