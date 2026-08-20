#!/usr/bin/env bash
# The gate. This is what CI runs and what .githooks/pre-push runs.
# There is no separate list of "things CI checks" to keep in your head.
#
#   scripts/ci/gate.sh fast            before every push
#   scripts/ci/gate.sh full            what a PR runs
#   scripts/ci/gate.sh nightly         scheduled
#   scripts/ci/gate.sh <lane> --list   print the steps, run nothing
#
# See knowledge/practices/one-gate-command.md.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT" || exit 1

# The apt-installed SDK does not put itself on PATH.
[ -d /usr/lib/dotnet ] && export PATH="$PATH:/usr/lib/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
    C_BOLD=$'\033[1m'; C_RED=$'\033[31m'; C_GREEN=$'\033[32m'
    C_YELLOW=$'\033[33m'; C_DIM=$'\033[2m'; C_RESET=$'\033[0m'
else
    C_BOLD=''; C_RED=''; C_GREEN=''; C_YELLOW=''; C_DIM=''; C_RESET=''
fi

# ── lanes ───────────────────────────────────────────────────────────────────

FAST_STEPS=(
    exec-bits
    shellcheck
    okf-validate
    xml-wellformed
    deferred-work
    vendored-sbe
    no-lock
    banned-members
    build
    unit-tests
)

FULL_EXTRA_STEPS=(
    vendored-sbe-upstream
    codegen-clean
    corpus-fresh
    conformance
)

NIGHTLY_EXTRA_STEPS=(
    stress
    benchmark-regression
)

lane_steps() {
    case "$1" in
        fast)    printf '%s\n' "${FAST_STEPS[@]}" ;;
        full)    printf '%s\n' "${FAST_STEPS[@]}" "${FULL_EXTRA_STEPS[@]}" ;;
        nightly) printf '%s\n' "${FAST_STEPS[@]}" "${FULL_EXTRA_STEPS[@]}" "${NIGHTLY_EXTRA_STEPS[@]}" ;;
        *)       return 1 ;;
    esac
}

# ── steps ───────────────────────────────────────────────────────────────────
# Each returns 0 pass, 1 fail, 78 not-applicable-yet (see run_step).
#
# NOT-APPLICABLE is for a step whose subject does not exist yet -- there are no
# tests before Phase 4, no codecs before Phase 1. It is reported distinctly and
# never silently as a pass, because "nothing to check" reading as green is
# exactly the failure class in knowledge/practices/green-gate-is-evidence.md.

NOT_APPLICABLE=78

step_exec_bits() {
    local bad
    bad=$(find scripts .githooks -name '*.sh' -type f ! -perm -u+x 2>/dev/null || true)
    [ -z "$bad" ] || { echo "not executable:"; echo "$bad"; return 1; }
    echo "all shell scripts are executable"
}

step_shellcheck() {
    command -v shellcheck >/dev/null 2>&1 || { echo "shellcheck not installed"; return $NOT_APPLICABLE; }
    # shellcheck disable=SC2046
    shellcheck -S warning $(find scripts .githooks -name '*.sh' -type f 2>/dev/null)
}

step_xml_wellformed() {
    python3 scripts/ci/checks/xml_wellformed.py
}

step_okf_validate() {
    python3 scripts/ci/checks/okf_validate.py knowledge
}

step_deferred_work() {
    python3 scripts/ci/checks/deferred_work.py
}

step_vendored_sbe() {
    SKIP_UPSTREAM_CHECK=1 scripts/ci/checks/check_vendored_sbe.sh
}

step_vendored_sbe_upstream() {
    scripts/ci/checks/check_vendored_sbe.sh
}

step_no_lock() {
    scripts/ci/checks/no_lock.sh
}

step_banned_members() {
    python3 scripts/ci/checks/banned_members.py
}

step_build() {
    ls src/*/*.csproj >/dev/null 2>&1 || { echo "no projects yet"; return $NOT_APPLICABLE; }
    dotnet build RingBuffer.sln -v q --nologo
}

step_unit_tests() {
    ls tests/*/*.csproj >/dev/null 2>&1 || { echo "no test projects yet"; return $NOT_APPLICABLE; }
    dotnet test RingBuffer.sln -v q --nologo --no-build
}

step_codegen_clean() {
    [ -f scripts/generate-codecs.sh ] || { echo "no codegen yet"; return $NOT_APPLICABLE; }
    scripts/ci/checks/codegen_fresh.sh
}

step_conformance() {
    [ -d tests/conformance/corpus ] && [ -n "$(ls -A tests/conformance/corpus 2>/dev/null)" ] \
        || { echo "no corpus yet"; return $NOT_APPLICABLE; }
    tests/conformance/harness/run.sh
}

step_corpus_fresh() {
    [ -d tests/conformance/corpus ] || { echo "no corpus yet"; return $NOT_APPLICABLE; }
    # Asserts the committed fixtures still match their declarations in
    # tools/CorpusBuilder/Cases.cs. Catches a fixture edited by hand, and a case
    # declaration changed without rebuilding.
    dotnet run --project tools/CorpusBuilder -v q -- \
        --out tests/conformance/corpus --check
}

step_stress() {
    ls tests/RingBuffer.Concurrency/*.csproj >/dev/null 2>&1 || { echo "no concurrency tests yet"; return $NOT_APPLICABLE; }
    dotnet test tests/RingBuffer.Concurrency -v q --nologo -- --filter Category=Stress
}

step_benchmark_regression() {
    [ -f benchmarks/baselines.json ] || { echo "no baselines yet"; return $NOT_APPLICABLE; }
    scripts/ci/checks/benchmark_regression.sh
}

# ── dispatch ────────────────────────────────────────────────────────────────
# A lane listing a step with no dispatch case must FAIL, not silently skip.
# That is trap TRAP-3: a gate step in a lane with no dispatch case went green
# while doing nothing. See docs/TRAPS.md.

dispatch() {
    local fn="step_${1//-/_}"
    if ! declare -F "$fn" >/dev/null; then
        printf '%sno dispatch case for step "%s" -- a lane lists it but nothing runs it%s\n' \
            "$C_RED" "$1" "$C_RESET" >&2
        return 1
    fi
    "$fn"
}

run_step() {
    local step=$1 start elapsed rc
    start=$SECONDS
    printf '%s>>> %s%s\n' "$C_BOLD" "$step" "$C_RESET"
    local out
    out=$(dispatch "$step" 2>&1)
    rc=$?
    elapsed=$((SECONDS - start))
    [ -n "$out" ] && printf '%s%s%s\n' "$C_DIM" "$(echo "$out" | sed 's/^/    /')" "$C_RESET"
    case $rc in
        0)  printf '%s    PASS%s %s(%ds)%s\n\n' "$C_GREEN" "$C_RESET" "$C_DIM" "$elapsed" "$C_RESET" ;;
        "$NOT_APPLICABLE")
            printf '%s    N/A%s  %s(%ds) -- subject does not exist yet%s\n\n' \
                "$C_YELLOW" "$C_RESET" "$C_DIM" "$elapsed" "$C_RESET" ;;
        *)  printf '%s    FAIL%s %s(%ds)%s\n\n' "$C_RED" "$C_RESET" "$C_DIM" "$elapsed" "$C_RESET" ;;
    esac
    return $rc
}

usage() {
    cat <<USAGE
usage: scripts/ci/gate.sh <lane> [--list]

lanes:
  fast     $( printf '%s ' "${FAST_STEPS[@]}" )
  full     fast + $( printf '%s ' "${FULL_EXTRA_STEPS[@]}" )
  nightly  full + $( printf '%s ' "${NIGHTLY_EXTRA_STEPS[@]}" )

  --list   print the steps the lane would run, then exit
USAGE
}

main() {
    local lane=${1:-}
    case "$lane" in
        fast|full|nightly) ;;
        -h|--help|"")      usage; exit 0 ;;
        *)                 printf '%sunknown lane: %s%s\n\n' "$C_RED" "$lane" "$C_RESET" >&2; usage; exit 2 ;;
    esac

    local steps
    mapfile -t steps < <(lane_steps "$lane")

    if [ "${2:-}" = "--list" ]; then
        printf '%s\n' "${steps[@]}"
        exit 0
    fi

    printf '%sgate: %s lane -- %d steps%s\n\n' "$C_BOLD" "$lane" "${#steps[@]}" "$C_RESET"

    local failed=() skipped=() passed=0 start=$SECONDS
    for step in "${steps[@]}"; do
        run_step "$step"
        case $? in
            0)               passed=$((passed + 1)) ;;
            "$NOT_APPLICABLE") skipped+=("$step") ;;
            *)               failed+=("$step") ;;
        esac
    done

    local total=$((SECONDS - start))
    if [ ${#failed[@]} -eq 0 ]; then
        printf '%sgate: %s GREEN%s -- %d passed, %d not yet applicable (%ds)\n' \
            "$C_GREEN$C_BOLD" "$lane" "$C_RESET" "$passed" "${#skipped[@]}" "$total"
        [ ${#skipped[@]} -gt 0 ] && printf '%s  not yet applicable: %s%s\n' \
            "$C_DIM" "${skipped[*]}" "$C_RESET"
        exit 0
    fi

    printf '%sgate: %s RED%s -- %d failed: %s (%ds)\n' \
        "$C_RED$C_BOLD" "$lane" "$C_RESET" "${#failed[@]}" "${failed[*]}" "$total"
    exit 1
}

main "$@"
