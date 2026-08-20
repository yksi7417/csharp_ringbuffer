#!/usr/bin/env bash
# Run every conformance case against every available ring, diffing byte for byte.
#
# The contract, per case:
#   replay --input input.sbe --output <tmp> [--seed n] --ring <name>
#   replay --diff expected.sbe <tmp> --schema schemas/fix-sbe.xml
#
# Any difference fails. See knowledge/architecture/conformance-corpus.md.
#
#   tests/conformance/harness/run.sh            every case, every ring
#   tests/conformance/harness/run.sh --list     what would run, and what is not built
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT" || exit 1

[ -d /usr/lib/dotnet ] && export PATH="$PATH:/usr/lib/dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

CORPUS="tests/conformance/corpus"
SCHEMA="schemas/fix-sbe.xml"

# Rings the replay CLI can drive. SPSC and MPSC join in Phases 4 and 5; when they
# do, every case below runs against all three and triangulation becomes possible.
RINGS=(reference)

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
    C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_DIM=$'\033[2m'; C_RESET=$'\033[0m'
else
    C_RED=''; C_GREEN=''; C_DIM=''; C_RESET=''
fi

mapfile -t CASES < <(find "$CORPUS" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' 2>/dev/null | sort)

if [ "${1:-}" = "--list" ]; then
    printf 'rings:  %s\n' "${RINGS[*]}"
    printf 'cases:  %d\n' "${#CASES[@]}"
    printf '  %s\n' "${CASES[@]}"
    exit 0
fi

if [ ${#CASES[@]} -eq 0 ]; then
    echo "conformance: no cases in $CORPUS" >&2
    exit 1
fi

REPLAY_DLL="tests/RingBuffer.Replay/bin/Debug/net8.0/RingBuffer.Replay.dll"
if [ ! -f "$REPLAY_DLL" ]; then
    dotnet build tests/RingBuffer.Replay -v q --nologo >/dev/null 2>&1 \
        || { echo "conformance: could not build the replay tool" >&2; exit 1; }
fi
replay() { dotnet "$REPLAY_DLL" "$@"; }

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

failures=0
runs=0

for ring in "${RINGS[@]}"; do
    for case_name in "${CASES[@]}"; do
        dir="$CORPUS/$case_name"
        seed=0
        [ -f "$dir/seed" ] && seed=$(tr -d '[:space:]' < "$dir/seed")

        # A case without a recorded reason cannot be reviewed, and cannot be
        # correctly updated later -- a reviewer cannot tell a fix from a regression.
        if [ ! -f "$dir/case.md" ]; then
            printf '%s  %s/%s: no case.md%s\n' "$C_RED" "$ring" "$case_name" "$C_RESET" >&2
            failures=$((failures + 1))
            continue
        fi

        actual="$tmp/$ring-$case_name.sbe"
        runs=$((runs + 1))

        if ! replay --input "$dir/input.sbe" --output "$actual" --seed "$seed" --ring "$ring" >/dev/null; then
            printf '%s  FAIL %s/%s: replay failed%s\n' "$C_RED" "$ring" "$case_name" "$C_RESET" >&2
            failures=$((failures + 1))
            continue
        fi

        if diff_output=$(replay --diff "$dir/expected.sbe" "$actual" --schema "$SCHEMA" 2>&1); then
            printf '%s  PASS%s %s/%s %s(%s)%s\n' \
                "$C_GREEN" "$C_RESET" "$ring" "$case_name" "$C_DIM" "$(echo "$diff_output" | head -1)" "$C_RESET"
        else
            printf '%s  FAIL %s/%s%s\n' "$C_RED" "$ring" "$case_name" "$C_RESET" >&2
            printf '%s\n' "$diff_output" | sed 's/^/      /' >&2
            failures=$((failures + 1))
        fi
    done
done

echo ""
if [ $failures -eq 0 ]; then
    printf '%sconformance: %d run(s) across %d case(s) and %d ring(s), all byte-identical%s\n' \
        "$C_GREEN" "$runs" "${#CASES[@]}" "${#RINGS[@]}" "$C_RESET"
    exit 0
fi

printf '%sconformance: %d of %d run(s) failed%s\n' "$C_RED" "$failures" "$runs" "$C_RESET" >&2
exit 1
