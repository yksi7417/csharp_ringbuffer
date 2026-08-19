---
type: Playbook
title: Validate this knowledge bundle
description: Run the OKF conformance check, and what its house rules are.
tags: [okf, documentation, ci]
resource: scripts/ci/checks/okf_validate.py
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Run it

```bash
python3 scripts/ci/checks/okf_validate.py knowledge
```

Also runs as the `okf-validate` step in the `fast` gate lane.

# What it enforces

**OKF v0.2 conformance (§11):**

1. Every non-reserved `.md` file has a parseable YAML frontmatter block.
2. Every frontmatter block has a non-empty `type`.
3. `index.md` carries no frontmatter — except a bundle-root `index.md`, which may carry
   `okf_version` and nothing else.
4. `log.md` date headings are ISO `YYYY-MM-DD`.

**Our house rules, beyond the spec:**

5. No broken bundle-relative links.
6. Every concept is reachable from some `index.md`.
7. `generated.at` / `verified[].at` parse as ISO 8601; `stale_after` as a date.
8. Warn when `stale_after` has passed.

# Why stricter than the spec

OKF says **consumers** MUST NOT reject a bundle for broken cross-links or a missing
`index.md`. That is correct for a consumer reading someone else's bundle.

We are the **producer**, and producer-side CI is exactly where those should be caught. A
broken link in our own bundle is a defect, not something to tolerate.

Rule 6 matters most as the bundle grows: an unreachable concept is one an agent will never
load, which makes it worse than absent — it looks like documentation while doing nothing.

# Gotchas

- `index.md` and `log.md` are **reserved**; they are never concepts and must not have
  `type`.
- Bundle-relative links start with `/` and resolve from the bundle root — **not** the
  repository root.
