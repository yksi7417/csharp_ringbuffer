---
type: Component
title: Conformance corpus
description: Committed binary fixtures that pin the wire format, reviewed like source.
tags: [testing, atdd, fixtures, determinism]
resource: tests/conformance/corpus
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: draft
---

# Status

**Not yet populated.** Layout agreed at review, adapted from the
[reference project](/references/external-sources.md).

# Layout

```
tests/conformance/corpus/<case-name>/
    input.sbe        length-prefixed SBE frames, committed binary
    expected.sbe     the output journal, committed binary
    case.md          one paragraph: what this covers and why it exists
    seed             single integer; absent means 0
```

`case.md` is not optional decoration. A fixture whose purpose nobody recorded cannot be
reviewed, and cannot be correctly updated when it legitimately changes — the reviewer has no
way to tell a fix from a regression.

# Cases to cover

| Case | Why |
|---|---|
| single message | the floor |
| multiple messages | sequencing |
| message that wraps the buffer | padding-on-wrap |
| message that forces an over-claim padding record | [D1](/decisions/d1-claim-commit-with-padding.md) |
| empty repeating group (count 0) | the group boundary nobody writes by hand |
| maximum group count | the other end |
| nested groups | the [shared-limit trap](/concepts/sbe-and-fix-repeating-groups.md) |
| var-length data at size boundaries | 0 bytes, 1 byte, exactly-aligned, one-past-aligned |
| buffer-full rejection | [D3](/decisions/d3-non-blocking-backpressure.md) |
| **v1 fixture decoded by v2 codec** | schema evolution ([D8](/decisions/d8-schema-selection.md)) |

# Fixtures are reviewed like source

A regenerated fixture in a diff is a **behaviour change**, and must be justified in the commit
message like any other. The failure mode this guards against is regenerating the expectation
to make a red test go green — which converts the corpus from a guardrail into a rubber stamp,
silently and permanently.

[Triangulation](/practices/triangulation.md) is the structural backstop: it detects a stale
expectation that all implementations now disagree with, which a reference-diff **cannot**
detect because everything fails it in unison.
