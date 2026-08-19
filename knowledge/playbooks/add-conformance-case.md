---
type: Playbook
title: Add a conformance case
description: Add a golden binary fixture to the replay corpus.
tags: [testing, atdd, fixtures]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Trigger

New behaviour that should be pinned at the wire-format level, or a bug that a byte-level
fixture would have caught.

# Steps

```bash
mkdir -p tests/conformance/corpus/<case-name>
```

1. **Write `case.md` first** — one paragraph: what this covers and why it exists. Not
   decoration. A fixture whose purpose nobody recorded cannot be reviewed, and cannot be
   correctly updated later, because a reviewer has no way to tell a fix from a regression.
2. Build `input.sbe` with the fixture builder (length-prefixed SBE frames).
3. Add `seed` if the case needs a non-zero seed. Absent means 0.
4. Generate `expected.sbe` by running the harness — **then read it** and confirm it is what
   you meant. Generating an expectation and committing it unexamined is how a corpus becomes
   a rubber stamp.
5. `scripts/ci/gate.sh full` to confirm it passes against all implementations.

# Review

Fixtures are **reviewed like source**. A regenerated `expected.sbe` in a diff is a
behaviour change and must be justified in the commit message as one.

# Gotchas

- Do **not** regenerate an existing `expected.sbe` to make a red test pass. That is the
  failure mode the corpus exists to prevent — see [L4](/testing/l4-acceptance-replay.md).
- The journal format is **not** the ring's record format. It is deliberately transport-independent
  so fixtures survive a ring-layout change. See
  [the replay harness](/architecture/replay-harness.md).
- Anything non-deterministic (clock, ids) must go through the seeded seams, or the fixture
  will fail on the second run. See [deterministic replay](/concepts/deterministic-replay.md).
