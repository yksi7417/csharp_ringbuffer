# Checkpoint

Where the implementation stands. Updated in the same commit as the task it records.

## Position

**Phase 0 complete. Phase 1 — Schemas and codecs. Task 1.1 is next.**

`scripts/ci/gate.sh` is green on all three lanes over a tree containing no product code, which
was Phase 0's exit criterion. Everything from here lands gated.

## Next unblocked task

**1.1 — `tools/sbe-csharp-gen/`, productionise the shim.** Needs 0.4 (done). The working
prototype is at
[`knowledge/references/evidence/SbeCsharpGen.java`](../knowledge/references/evidence/SbeCsharpGen.java).

## Progress

| Phase | Done | Total |
|---|---|---|
| 0 — Toolchain and gate | **13** | 13 |
| 1 — Schemas and codecs | 0 | 11 |
| 2 — Ring algebra | 0 | 9 |
| 3 — ATDD scaffolding | 0 | 11 |
| 4 — SPSC ring | 0 | 15 |
| 5 — MPSC, concurrency, ARM64 | 0 | 11 |
| 6 — Triangulation and corpus | 0 | 6 |
| 7 — Performance | 0 | 6 |
| 8 — Teaching artifacts | 0 | 10 |
| **Total** | **13** | **92** |

## Open from Phase 0

Two things are done but **not yet observed working**, and are marked as such rather than
claimed:

| | |
|---|---|
| **ARM64 leg (0.12)** | The workflow is committed and its YAML validates, but no ARM64 runner exists in this container. Whether the leg is genuinely green is the first thing to check on GitHub. Until then [R4](../knowledge/risks/risk-register.md) is *not* retired. |
| **SessionStart hook (0.2)** | `.claude/settings.json` is wired and `bootstrap.sh` is verified idempotent, but "a fresh session builds with no manual steps" can only be observed from a fresh session. |

## Guards proven failing, not just passing

Per [green gate is evidence, not proof](../knowledge/practices/green-gate-is-evidence.md), a
check nobody has watched fail is not yet a check. These were each broken deliberately and
observed going red:

| Guard | Broken by | Result |
|---|---|---|
| `okf-validate` | missing `type`, broken link, orphaned concept, frontmatter in a non-root index, non-ISO log heading, bad `status` | 6/6 caught |
| `vendored-sbe` | editing a vendored file; adding an unlisted file | both caught, exit 1 |
| `no-lock` | a `lock` statement in `src/` | caught, exit 1 |
| `deferred-work` | orphan `DEFERRED:` marker; entry missing **Why** | both caught |
| `dispatch` (TRAP-3) | a lane listing a step with no `step_` function | caught, exit 1 |

## Found while building Phase 0

- **`deferred_work.py` failed on its first run** — correctly. Trap ids and deferred-work ids
  were both `T-n`, so a documentation reference to trap `T-4` was indistinguishable from a
  live marker. Traps are now `TRAP-n`. Recorded as TRAP-6 in [`docs/TRAPS.md`](../docs/TRAPS.md).
- **The pre-push hook earned itself on its first use.** It blocked the Phase 0 push: writing
  TRAP-6 re-tripped the very check it describes, because documenting a marker means writing
  one. The scanner cannot tell prose about a marker from a marker, so the fix is an authoring
  rule rather than a loosened check — loosening it would let a real orphaned marker in a
  `.md` file through.
- **`shellcheck` found four real defects** on its first run (unchecked `cd`, unquoted `case`
  patterns). Fixed; the step is clean.
- **Vendored runtime is 1,851 lines across 7 files** and builds on net8.0 with **0 warnings**,
  exactly as [F3](../knowledge/findings/f3-sbe-dll-nuget-stale.md) predicted.
- **`N/A` had to be distinct from `PASS`.** A gate whose steps mostly have no subject yet
  would otherwise report a full green over an empty tree. Recorded as TRAP-5.

## Verified before planning

Executed in a container, not assumed:

| Claim | Where |
|---|---|
| SBE 1.39.0 generates C# with nested groups, via the shim | [F2](../knowledge/findings/f2-csharp-codegen-requires-shim.md), [F5](../knowledge/findings/f5-end-to-end-roundtrip-proof.md) |
| `NativeMemory.AlignedAlloc` gives a 4096-aligned off-heap slab | probe, feeds 4.1 |
| `Interlocked.CompareExchange(ref Unsafe.AsRef<long>(ptr), …)` works on unmanaged memory | probe, feeds 5.2 |
| A `Claim` ref struct exposes a `Span` pointing into the slab | probe, feeds 3.2 |
| Zero copy is assertable as address identity, via `Unsafe.AsPointer(ref MemoryMarshal.GetReference(span))` | probe, feeds 4.5 |

Note for 3.2/4.5: `&claim.Span[0]` **does not compile** — you cannot take the address of a
`ref struct` property. Use `MemoryMarshal.GetReference`. Found the hard way; recorded so the
next person does not.
