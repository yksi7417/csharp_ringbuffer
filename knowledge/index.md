---
okf_version: "0.2"
---

# Start here

This is the knowledge bundle for **csharp_ringbuffer**: a lock-free, zero-copy ring buffer
carrying SBE-encoded FIX messages with repeating groups, built test-first.

It is written in [Open Knowledge Format](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md)
v0.2 so that an agent picking the project up cold can find context without reading the whole
repository. One concept per file, `type` in the frontmatter, links between concepts.

**If you are an agent starting a task here, read [`/practices/agent-onboarding.md`](practices/agent-onboarding.md) first.**
It says which concepts to load for which kind of task, and which to leave alone.

# Sections

* [decisions](decisions/index.md) - Every design choice, why it was made, and what it rules out. Read before changing behaviour.
* [findings](findings/index.md) - Verified facts about the toolchain, established by running it. Read before fighting the build.
* [concepts](concepts/index.md) - The domain: ring buffer mechanics, SBE, FIX groups, the .NET memory model.
* [architecture](architecture/index.md) - What the components are and how they fit together.
* [testing](testing/index.md) - The five test layers and what each one is for.
* [practices](practices/index.md) - The guardrails that keep quality from eroding as the project grows.
* [risks](risks/index.md) - What could still go wrong, and what is watching for it.
* [playbooks](playbooks/index.md) - Step-by-step runbooks for recurring tasks.
* [references](references/index.md) - External material and reproducible evidence.

# Project state

**Phase: research complete, design decided, plan written, implementation not started.**

The research is in [findings](findings/index.md) and the decisions it fed are in
[decisions](decisions/index.md) — all ten are accepted. No production code exists yet.

The sequenced work is at [`IMPL/PLAN.md`](../IMPL/PLAN.md) — 92 tasks across nine phases,
with [`IMPL/CHECKPOINT.md`](../IMPL/CHECKPOINT.md) tracking position. To pick up a task, read
[the implementation loop](practices/implementation-loop.md).

The narrative research report that preceded this bundle is preserved in git history at
`docs/RESEARCH.md`.

# The one-paragraph version

A producer claims a bounded span of an off-heap ring slab, encodes an SBE message **directly
into it** (no scratch buffer, no copy), and commits the true length — padding the leftover,
because SBE variable-length data means the length is not known until encoding is done
([D1](decisions/d1-claim-commit-with-padding.md)). A consumer decodes in place from the same
address. Correctness is held down by five test layers, the load-bearing one being a
byte-exact binary replay corpus ([L4](testing/l4-acceptance-replay.md)) that pins the wire
format itself.
