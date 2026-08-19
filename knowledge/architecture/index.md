# Architecture

What the components are, how they fit together, and what is generated versus committed.

Status is on each concept: everything here except the codec-generation prototype is
`status: draft`, meaning **designed and agreed, not yet built**.

# Overview

* [Repository layout](repository-layout.md) - Where everything lives, the dependency direction, and why generated code is not committed.

# Components

* [RingBuffer.Core](ring-buffer-core.md) - The SPSC and MPSC buffers behind a shared `IRingBuffer` contract, and the invariants they hold.
* [Codec generation](codec-generation.md) - The Java shim and vendored runtime that turn schemas into C# codecs, and the two pins that must move together.
* [Replay harness](replay-harness.md) - The pure input-journal to output-journal function, the journal format, and the differ.
* [Conformance corpus](conformance-corpus.md) - Committed binary fixtures that pin the wire format, and the cases they must cover.
