# csharp_ringbuffer

A lock-free, zero-copy ring buffer for SBE-encoded FIX messages with repeating groups, in C#.

Production-shaped and teaching-grade at once: a producer claims a bounded span of an off-heap
slab, encodes an SBE message **directly into it** — no scratch buffer, no copy — and commits
the true length. A consumer decodes in place from the same address.

**Status: research complete, design decided, implementation not started.**

## Documentation

Project knowledge lives in **[`knowledge/`](knowledge/index.md)**, written in
[Open Knowledge Format](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md)
v0.2 so both people and agents can find context without reading the whole repository.

| Looking for | Go to |
|---|---|
| Why it is designed this way | [decisions](knowledge/decisions/index.md) |
| How the ring actually works | [concepts](knowledge/concepts/index.md) |
| Why the build is set up strangely | [findings](knowledge/findings/index.md) |
| How it is tested | [testing](knowledge/testing/index.md) |
| How to do a specific task | [playbooks](knowledge/playbooks/index.md) |

## The interesting problem

SBE messages with repeating groups have a length that **is not known until encoding
finishes**. But zero copy means encoding *into* the ring, which means claiming space
*before* encoding. Agrona does not solve this — its `tryClaim` takes an exact length.

The answer here is claim a bound, commit the actual, pad the remainder:
[D1](knowledge/decisions/d1-claim-commit-with-padding.md).

## Building

Requires .NET 8 and a JDK (the SBE codec generator is Java). See the
[bootstrap playbook](knowledge/playbooks/bootstrap-environment.md) — `dotnet-install.sh` will
not work in sandboxed environments.

```bash
scripts/ci/gate.sh fast
```

## License

Apache-2.0. The vendored SBE runtime under `src/RingBuffer.Sbe/` is Apache-2.0, copyright its
original authors.
