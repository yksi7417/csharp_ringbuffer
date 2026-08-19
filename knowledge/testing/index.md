# Testing

Five layers, each catching something the others structurally cannot. **Start with the
pyramid** — it explains why none of them is redundant, which is the part that gets
forgotten first.

# Strategy

* [The five test layers](test-pyramid.md) - What each layer catches, why none is redundant, and how TDD and ATDD apply concretely.

# Layers

* [L1: Unit tests](l1-unit.md) - Strict TDD over the ring's algebra, including the position arithmetic at `long.MaxValue`.
* [L2: Property-based tests](l2-property.md) - Invariants under randomised operation sequences. Where the padding boundary cases actually get hit.
* [L3: Concurrency tests](l3-concurrency.md) - Coyote for interleavings, stress for the real scheduler. Explicitly does **not** cover weak memory.
* [L4: Acceptance tests and binary replay](l4-acceptance-replay.md) - **The load-bearing guardrail.** The only layer that notices the wire format changed.
* [L5: Performance](l5-performance.md) - Allocation gated hard, latency gated with tolerance, false sharing kept visible.
