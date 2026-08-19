# Trap log

Things that **looked green while they were wrong**. A different failure class from a bug, and
more dangerous — the signal that would normally alert you is precisely what is broken.

**The rule:** when something fools you, the fix is not finished until you have asked whether a
check could have caught it. See
[the practice](../knowledge/practices/green-gate-is-evidence.md).

Numbering is `TRAP-n`. `T-n` belongs to [the deferred-work register](TODO.md) — a separate
namespace.

---

## TRAP-1 — A generator whose stderr was discarded wrote nothing, and the build passed

**What looked green:** the codegen step. It was supposed to catch a generator that failed.

**Why it did not catch it:** stderr went to `/dev/null` and the generator exited 0 having
written no files. Nothing downstream asserted that output existed, let alone that it was
correct.

**Guard:** `scripts/generate-codecs.sh` never discards generator stderr, and the generator's
test asserts the output **compiles and round-trips** — not that files appeared. *(Inherited
from cross_asset_ems. Guard lands with task 1.6.)*

## TRAP-2 — A `git diff` check could not see an untracked generated file

**What looked green:** the codegen-freshness step, which should catch stale generated code.

**Why it did not catch it:** generated code is gitignored and therefore untracked. `git diff`
reports changes to **tracked** files, so a newly generated file was invisible to it and the
check passed against a tree it had never actually compared.

**Guard:** `step_codegen_clean` in `scripts/ci/gate.sh` uses
`git status --porcelain --untracked-files=all`.

## TRAP-3 — A lane listed a step with no dispatch case, and skipped it silently

**What looked green:** the whole lane.

**Why it did not catch it:** the step name was in the lane array but the dispatch `case` had
no matching branch, so it fell through and did nothing. The lane reported success over a step
that never ran.

**Guard:** `dispatch()` in `scripts/ci/gate.sh` fails when no `step_<name>` function exists.
Verified failing — see below.

## TRAP-4 — Sanitizers compiled the tree and never ran a test

**What looked green:** the sanitizer step.

**Why it did not catch it:** it built the instrumented binaries and stopped. No test body ever
executed under instrumentation, so no finding was possible.

**Guard:** none yet — **known gap**. This project has no sanitizer step. When one is added
(Coyote and stress in Phase 5 are the analogous steps), it must assert that tests *executed*,
not that they built. *(Inherited from cross_asset_ems.)*

---

## Our own

## TRAP-5 — A step whose subject does not exist yet could read as a pass

**What looked green:** early gate runs, where most steps have nothing to check — no tests
before Phase 4, no codecs before Phase 1.

**Why it would not catch anything:** if "nothing to check" returned 0, a lane on an empty tree
would report a full green, and the day a subject silently stopped being detected it would keep
doing so.

**Guard:** `scripts/ci/gate.sh` returns a distinct `NOT_APPLICABLE` code, reported as `N/A`
and counted separately from passes in the summary. A green run states how many steps were not
yet applicable.

## TRAP-6 — Traps and deferred work shared one `T-n` namespace

**What looked green:** `deferred_work.py`, briefly — it failed for what looked like a bug in
the check, on `DEFERRED: T-4` in the register's own documentation.

**Why it mattered:** it was not a false positive. Trap ids and deferred-work ids were both
`T-n`, so a reference to trap `T-4` was indistinguishable from a marker pointing at
deferred-work entry `T-4`. Confusing for a person, unresolvable for a script.

**Guard:** traps are `TRAP-n`, deferred work is `T-n`. Both registers state the split.
