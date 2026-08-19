# Decisions

Every design choice, why it was made, and what it rules out. **Read the relevant decision
before changing behaviour** — the reasoning is here, not in the code.

All ten were accepted at review on 2026-08-19. Those carrying a `verified` frontmatter entry
were signed off explicitly by name; the rest were accepted as proposed.

# Core design

* [D1: Claim a bound, commit the actual, pad the remainder](d1-claim-commit-with-padding.md) - Resolves the conflict between SBE variable-length encoding and zero-copy claim-before-encode. **The central design of the project.**
* [D2: Build both SPSC and MPSC](d2-spsc-and-mpsc.md) - Two implementations behind one interface, sharing a conformance suite.
* [D3: The buffer never blocks](d3-non-blocking-backpressure.md) - TryClaim returns false when full; backpressure is the caller's policy.
* [D7: Keep the ring record header and the SBE MessageHeader separate](d7-dual-header-framing.md) - 16 bytes per message, buying a layering boundary.

# Memory and concurrency

* [D4: Back the ring with off-heap NativeMemory](d4-native-memory-slab.md) - AlignedAlloc rather than a pinned byte[] or a mapped file.
* [D5: Hot counters sit on separate cache lines](d5-cache-line-padding.md) - False-sharing avoidance, measured by a deliberately-unpadded benchmark.
* [D6: Volatile/Interlocked on unmanaged counters, with an ARM64 CI leg](d6-memory-model-and-arm64.md) - x86-TSO hides missing barriers; ARM64 does not.

# Scope and process

* [D8: Two FIX messages plus a v1/v2 evolution pair](d8-schema-selection.md) - Nested repeating groups, because that is where group handling breaks.
* [D9: Project context lives in an OKF bundle](d9-okf-knowledge-bundle.md) - This bundle, and why it is validated by CI.
* [D10: Cross-process shared memory is an explicit later phase](d10-cross-process-deferred.md) - Deferred, but the layout stays position-independent.
