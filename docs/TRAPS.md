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
the check, on a `T-4` reference inside the register's own documentation.

**Why it mattered:** it was not a false positive. Trap ids and deferred-work ids were both
`T-n`, so a reference to trap `T-4` was indistinguishable from a marker pointing at
deferred-work entry `T-4`. Confusing for a person, unresolvable for a script.

**Guard:** traps are `TRAP-n`, deferred work is `T-n`. Both registers state the split.

**And a second time.** Writing this very entry re-tripped the check, because describing a
marker means writing one. The scanner cannot distinguish prose about a marker from a marker.
Authoring rule, now stated in
[the practice](../knowledge/practices/deferred-work-register.md): when writing *about*
markers, never put the keyword and the id adjacent — say "a `T-4` reference" rather than
spelling out the live form. Caught by the pre-push hook, before it reached the remote.

## TRAP-7 — A build succeeded having compiled none of its generated sources

**What looked green:** `dotnet build`. It reported success, 0 warnings, 0 errors, over a
codec project whose generated sources had just been deleted.

**Why it did not catch it:** two independent faults, either of which alone produces a
convincing green:

1. **MSBuild evaluates the default `Compile` glob before any `BeforeCompile` target runs.**
   Sources generated during the build are therefore invisible to the compiler. The project
   compiled zero files and succeeded, because compiling nothing is not an error.
2. **The generator writes into a namespace subdirectory.**
   `CSharpNamespaceOutputManager` creates `RingBuffer_Codecs/` beneath the output directory,
   so a non-recursive `*.g.cs` glob matched nothing while the generator's own log line
   correctly reported 12 files written.

The generator was working the whole time. Every signal said fine.

**Guard:** `EnableDefaultCompileItems` is off; `CollectSbeCodecs` adds `**/*.g.cs` to
`@(Compile)` inside the target and **errors when the item list is empty**. That target runs on
every build, not only when generation ran, so a wiped codec directory cannot pass as an
empty-but-successful compile. Verified by deleting the generated sources and watching the
build go red.

**Also:** the freshness check cannot be a `git status` check here. Generated sources are
gitignored, and git does not report ignored files — so a git-based check would pass
*unconditionally*. That is worse than TRAP-2: not a check that misses a case, a check that can
never fail. `scripts/ci/checks/codegen_fresh.sh` hashes the tree, regenerates, and compares.

## TRAP-8 — "It passed locally" meant nothing, because local and CI ran different SDKs

**What looked green:** `scripts/ci/gate.sh fast`, locally, immediately before a push that went
red in CI. The pre-push hook ran the same script CI runs and passed.

**Why it did not catch it:** the whole premise of
[one gate command](../knowledge/practices/one-gate-command.md) — "if the gate is green, the
fast lane in CI is green" — silently assumes **the same toolchain on both sides**. It was not.
This container had .NET SDK 8.0.130; GitHub runners ship 10.0.302, where VSTest has been
removed, so `dotnet test` fails there and passes here. Same script, same repo, opposite result.

The gate was not wrong. Its guarantee was just narrower than its wording, and nothing said so.

**Guard:** `global.json` pins the SDK, CI installs exactly that with `actions/setup-dotnet`
using `global-json-file`, and `bootstrap.sh` now checks the SDK **major version** rather than
mere presence — so a developer machine carrying only .NET 10 gets 8 installed instead of
appearing ready.

**The general shape:** any check whose result depends on ambient environment is only as
trustworthy as the pinning behind it. A version that is not pinned is a variable, and a
variable in a guardrail is a hole.

## TRAP-9 — A third-party outage failed the build with no retry

**What looked green:** nothing. This one failed honestly, which is why it is here as a
near-miss rather than a defect.

**What happened:** Maven Central answered HTTP 429 when both matrix legs fetched the SBE jar
at the same second. The build failed for a reason that had nothing to do with the code.

**Why it matters:** a red build that is not the code's fault is how a team learns to ignore
red builds. Two or three of those and every failure becomes "probably just CI".

**Guard:** the fetch retries five times with quadratic backoff and validates the archive
before accepting it (a truncated download fails later, somewhere else, and much more
confusingly). CI additionally caches the jar keyed on `sbe-version.txt`, so a green run does
not depend on someone else's quota.
