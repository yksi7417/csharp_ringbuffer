# csharp_ringbuffer

A lock-free, zero-copy ring buffer carrying SBE-encoded FIX messages with repeating groups,
built test-first in C#.

## Read the knowledge bundle first

**Project context lives in [`knowledge/`](knowledge/index.md)**, an
[OKF v0.2](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md)
bundle: one concept per file, typed frontmatter, cross-linked.

**Start at [`knowledge/practices/agent-onboarding.md`](knowledge/practices/agent-onboarding.md).**
It maps task types to the three or four concepts each one needs, so you do not have to read
the repository to work in it.

Do not re-derive things that are written down. In particular the ring buffer algorithm, the
padding boundary cases, and the reasons the SBE toolchain is set up unusually are all
recorded.

## Non-obvious things that will bite you

| | |
|---|---|
| `dotnet-install.sh` does not work here | Egress-blocked. Use apt. [F1](knowledge/findings/f1-dotnet-install-egress-blocked.md) |
| Java is required, in a C# project | The SBE codec generator is Java. [F2](knowledge/findings/f2-csharp-codegen-requires-shim.md) |
| `-Dsbe.target.language=CSharp` does not work | It reaches no generator on any release. A shim is required. [F2](knowledge/findings/f2-csharp-codegen-requires-shim.md) |
| The SBE runtime is vendored, not a NuGet package | The package is abandoned. [F3](knowledge/findings/f3-sbe-dll-nuget-stale.md) |
| Generated codecs are not committed | Regenerated per build; CI asserts freshness. |

## The work queue

Sequenced tasks are in [`IMPL/PLAN.md`](IMPL/PLAN.md); position in
[`IMPL/CHECKPOINT.md`](IMPL/CHECKPOINT.md). To pick one up, read
[the implementation loop](knowledge/practices/implementation-loop.md).

## Before you push

```bash
scripts/ci/gate.sh fast
```

The same script CI runs. There is no second list of things CI checks.
See [one gate command](knowledge/practices/one-gate-command.md).

## Rules

1. **Check [decisions](knowledge/decisions/index.md) before changing behaviour.** If your
   change contradicts one, say so — that is the maintainer's call, not a thing to quietly
   override.
2. **Never regenerate an `expected.sbe` fixture to make a test pass.** That converts the
   conformance corpus from a guardrail into a rubber stamp.
   See [L4](knowledge/testing/l4-acceptance-replay.md).
3. **No `lock`, no `Monitor`** on the publish or consume path.
   See [D3](knowledge/decisions/d3-non-blocking-backpressure.md).
4. **Nothing allocates on the hot path.** The ergonomic SBE overload is usually the
   allocating one. See [zero copy](knowledge/concepts/zero-copy-in-dotnet.md).
5. **Register anything you consciously defer**, in the same change that defers it.
   See [the register](knowledge/practices/deferred-work-register.md).
6. **Update the bundle when behaviour changes.**
   See [bundle maintenance](knowledge/practices/knowledge-bundle-maintenance.md).

## Project state

Research complete, all ten design decisions accepted, **implementation not started**.
Architecture concepts are `status: draft` — designed and agreed, not built.
