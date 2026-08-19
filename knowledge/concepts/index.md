# Concepts

The domain knowledge an agent needs before touching the code. These describe **how things
work**, not what we decided — decisions live in [decisions](/decisions/index.md).

# Ring buffer mechanics

* [Ring buffer record and trailer layout](ring-buffer-record-layout.md) - The on-wire layout, the trailer counters, and why positions are monotonic.
* [The publication protocol](claim-commit-protocol.md) - How a record becomes visible without a lock, via the negative-to-positive length transition.
* [Padding records](padding-records.md) - Skip records that bridge the buffer end and absorb over-claimed space. Includes the three asymmetric boundary cases.

# Memory and concurrency

* [What "zero copy" actually means here](zero-copy-in-dotnet.md) - The specific claim, how it is verified, and three ways to lose it by accident.
* [The .NET memory model, as it applies to unmanaged counters](dotnet-memory-model.md) - Why `volatile` fields do not help, and why x64 CI is not enough.
* [False sharing](false-sharing.md) - Why the hot counters are 64 bytes apart, and how that invariant is kept measurable.

# Encoding

* [SBE encoding and FIX repeating groups](sbe-and-fix-repeating-groups.md) - The flyweight model, the three message regions, and the shared-limit trap that nested groups expose.

# Testing

* [Deterministic replay](deterministic-replay.md) - Why the acceptance layer is a pure function from input journal to output journal, and why the journals are binary.

# Reference

* [Glossary](glossary.md) - Terms used throughout this bundle.
