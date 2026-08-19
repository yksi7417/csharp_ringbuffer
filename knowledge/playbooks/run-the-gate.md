---
type: Playbook
title: Run the gate
description: The one command to run before pushing, and how to read a failure.
tags: [ci, process]
resource: scripts/ci/gate.sh
generated: { by: process:claude-code-session, at: 2026-08-19T00:00:00Z }
status: stable
---

# Run it

```bash
scripts/ci/gate.sh fast        # before every push (also the pre-push hook)
scripts/ci/gate.sh full        # what a PR runs
scripts/ci/gate.sh nightly     # scheduled
scripts/ci/gate.sh fast --list # print the steps, run nothing
```

If the gate is green, the corresponding CI lane is green. There is no second list of things
CI checks. See [one gate command](/practices/one-gate-command.md).

# Reading a failure

| Step | Usually means |
|---|---|
| `codegen-clean` | You changed a schema and did not regenerate. See [the playbook](regenerate-codecs.md). |
| `vendored-sbe` | Someone edited the vendored runtime, or bumped one pin without the other. See [F3](/findings/f3-sbe-dll-nuget-stale.md). |
| `no-lock` | A `lock`/`Monitor` reached the source. See [D3](/decisions/d3-non-blocking-backpressure.md). |
| `zero-alloc` | Something allocates on the hot path — often a `string` overload or a captured closure. See [zero copy](/concepts/zero-copy-in-dotnet.md). |
| `okf-validate` | This bundle is malformed. See [the playbook](validate-the-bundle.md). |
| `conformance` | The **wire format changed.** Read the hex diff before assuming the fixture is wrong. |
| `deferred-work` | A `DEFERRED:` marker with no register entry, or an entry missing a section. See [the register](/practices/deferred-work-register.md). |

# When a step surprises you

A gate step that fails for a reason you did not expect is worth a
[trap log](/practices/trap-log.md) entry — and so is a step that **should** have caught
something and did not. See
[green gate is evidence, not proof](/practices/green-gate-is-evidence.md).
