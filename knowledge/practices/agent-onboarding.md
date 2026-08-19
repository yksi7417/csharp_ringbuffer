---
type: Playbook
title: Agent onboarding
description: Which concepts to load for which kind of task. Read this first.
tags: [agents, onboarding, okf]
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Read this first

This bundle exists so you **do not have to read the whole repository**. Load the concepts
your task needs and skip the rest.

# By task

| Your task | Load |
|---|---|
| Implementing or changing the ring buffer | [record layout](/concepts/ring-buffer-record-layout.md), [publication protocol](/concepts/claim-commit-protocol.md), [padding records](/concepts/padding-records.md), [D1](/decisions/d1-claim-commit-with-padding.md), [RingBuffer.Core](/architecture/ring-buffer-core.md) |
| Anything touching concurrency or ordering | [.NET memory model](/concepts/dotnet-memory-model.md), [D6](/decisions/d6-memory-model-and-arm64.md), [L3](/testing/l3-concurrency.md) |
| Touching SBE, schemas or codecs | [SBE and FIX repeating groups](/concepts/sbe-and-fix-repeating-groups.md), [D8](/decisions/d8-schema-selection.md), [codec generation](/architecture/codec-generation.md), [F2](/findings/f2-csharp-codegen-requires-shim.md), [F3](/findings/f3-sbe-dll-nuget-stale.md) |
| The build will not work | [F1](/findings/f1-dotnet-install-egress-blocked.md), [bootstrap playbook](/playbooks/bootstrap-environment.md) |
| Adding or changing a test | [the five test layers](/testing/test-pyramid.md), then the specific layer |
| Adding a conformance fixture | [deterministic replay](/concepts/deterministic-replay.md), [conformance corpus](/architecture/conformance-corpus.md), [the playbook](/playbooks/add-conformance-case.md) |
| Performance work | [zero copy](/concepts/zero-copy-in-dotnet.md), [false sharing](/concepts/false-sharing.md), [L5](/testing/l5-performance.md) |
| Writing docs or updating this bundle | [D9](/decisions/d9-okf-knowledge-bundle.md), [bundle maintenance](knowledge-bundle-maintenance.md) |

# Rules that apply to every task

1. **Check [decisions](/decisions/index.md) before changing behaviour.** If your change
   contradicts one, that is a decision to revisit with the maintainer — not to quietly
   override. Say so explicitly rather than proceeding.
2. **Run `scripts/ci/gate.sh fast` before pushing.** It is the same script CI runs. See
   [one gate command](one-gate-command.md).
3. **A green gate is evidence, not proof.** If something surprised you, ask whether a check
   could have caught it, and add one. See [green gate is evidence](green-gate-is-evidence.md).
4. **Register anything you consciously defer** in the same change that defers it. See
   [the deferred-work register](deferred-work-register.md).
5. **Update this bundle when behaviour changes.** See
   [bundle maintenance](knowledge-bundle-maintenance.md).

# What NOT to do

- Do not re-derive the ring buffer algorithm from first principles. It is written down.
- Do not regenerate an `expected.sbe` fixture to make a test pass. See
  [L4](/testing/l4-acceptance-replay.md).
- Do not add a `lock`. See [D3](/decisions/d3-non-blocking-backpressure.md); the gate will
  reject it anyway.
- Do not bump the SBE generator version without the runtime tag. See
  [codec generation](/architecture/codec-generation.md).
