#!/usr/bin/env bash
# Produce regression evidence for a checkpoint.
#
# Runs the existing test suites and the full gate, and prints a block suitable for
# pasting into IMPL/CHECKPOINT.md. Exits non-zero if anything is red, so it cannot
# be used to record a green checkpoint over a broken tree.
#
#   scripts/ci/evidence.sh            # human-readable
#   scripts/ci/evidence.sh --markdown # table for CHECKPOINT.md
#
# See knowledge/practices/implementation-loop.md.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT" || exit 1

[ -d /usr/lib/dotnet ] && export PATH="$PATH:/usr/lib/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

MARKDOWN=0
[ "${1:-}" = "--markdown" ] && MARKDOWN=1

SHA="$(git rev-parse --short HEAD)"
WHEN="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
DIRTY=""
[ -n "$(git status --porcelain)" ] && DIRTY=" (working tree dirty)"

build_out=$(dotnet build RingBuffer.sln -v q --nologo 2>&1)
build_rc=$?

test_out=$(dotnet test RingBuffer.sln --nologo --no-build 2>&1)
test_rc=$?

gate_out=$(./scripts/ci/gate.sh full 2>&1)
gate_rc=$?

# One "Passed!" line per test assembly.
results=$(printf '%s\n' "$test_out" | grep -E "^(Passed|Failed)!" || true)
total_passed=$(printf '%s\n' "$results" | grep -oE "Passed:\s+[0-9]+" | grep -oE "[0-9]+" | paste -sd+ - | bc 2>/dev/null || echo 0)
total_failed=$(printf '%s\n' "$results" | grep -oE "Failed:\s+[0-9]+" | grep -oE "[0-9]+" | paste -sd+ - | bc 2>/dev/null || echo 0)
gate_line=$(printf '%s\n' "$gate_out" | grep -E "^gate: " | tail -1)

rc=0
[ $build_rc -ne 0 ] && rc=1
[ $test_rc -ne 0 ] && rc=1
[ $gate_rc -ne 0 ] && rc=1

if [ $MARKDOWN -eq 1 ]; then
    printf '### Regression evidence — `%s`, %s%s\n\n' "$SHA" "$WHEN" "$DIRTY"
    printf '| Suite | Result |\n|---|---|\n'
    printf '%s\n' "$results" | while IFS= read -r line; do
        [ -z "$line" ] && continue
        name=$(printf '%s' "$line" | grep -oE "[A-Za-z0-9_.]+\.dll" | head -1)
        passed=$(printf '%s' "$line" | grep -oE "Passed:\s+[0-9]+" | grep -oE "[0-9]+")
        failed=$(printf '%s' "$line" | grep -oE "Failed:\s+[0-9]+" | grep -oE "[0-9]+")
        printf '| `%s` | %s passed, %s failed |\n' "${name:-unknown}" "${passed:-?}" "${failed:-?}"
    done
    printf '| **Total** | **%s passed, %s failed** |\n' "${total_passed:-0}" "${total_failed:-0}"
    printf '| `gate.sh full` | %s |\n' "$(printf '%s' "$gate_line" | sed 's/^gate: //')"
else
    echo "=============================================================="
    echo " REGRESSION EVIDENCE  $WHEN"
    echo " commit $SHA on $(git branch --show-current)$DIRTY"
    echo "=============================================================="
    printf '%s\n' "$build_out" | grep -E "Build succeeded|Build FAILED|error" | head -5
    echo ""
    printf '%s\n' "$results"
    printf '  TOTAL: %s passed, %s failed\n' "${total_passed:-0}" "${total_failed:-0}"
    echo ""
    printf '%s\n' "$gate_line"
fi

if [ $rc -ne 0 ]; then
    echo "" >&2
    echo "EVIDENCE: REGRESSION -- build=$build_rc test=$test_rc gate=$gate_rc" >&2
    printf '%s\n' "$test_out" | grep -iE "^\s+(Failed|error)" | head -20 >&2
fi

exit $rc
