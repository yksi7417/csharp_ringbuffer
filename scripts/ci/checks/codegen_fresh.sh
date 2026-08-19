#!/usr/bin/env bash
# Assert the generated codecs on disk match what the schemas produce right now.
#
# NOT a `git status` check. Generated sources are gitignored, and git does not
# report ignored files -- so a git-based check would pass unconditionally, which
# is a worse version of TRAP-2: not a check that misses a case, a check that can
# never fail.
#
# Instead: hash what is there, regenerate, hash again, compare.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT" || exit 1

OUT_DIR="src/RingBuffer.Codecs"

die() { printf '\033[31mcodegen-fresh: %s\033[0m\n' "$*" >&2; exit 1; }

hash_codecs() {
    # Sorted, so the digest does not depend on filesystem ordering.
    find "$OUT_DIR" -name '*.g.cs' -print0 2>/dev/null \
        | sort -z \
        | xargs -0 --no-run-if-empty sha256sum \
        | sha256sum \
        | cut -d' ' -f1
}

before=$(hash_codecs)
before_count=$(find "$OUT_DIR" -name '*.g.cs' 2>/dev/null | wc -l)

scripts/generate-codecs.sh >/dev/null 2>&1 || die "the generator failed -- run scripts/generate-codecs.sh to see why"

after=$(hash_codecs)
after_count=$(find "$OUT_DIR" -name '*.g.cs' 2>/dev/null | wc -l)

# A generator that writes nothing exits 0 (TRAP-1). Assert output exists before
# comparing, or an empty-vs-empty match would read as "fresh".
[ "$after_count" -gt 0 ] || die "generation produced no files"

if [ "$before" != "$after" ]; then
    die "generated codecs were stale (${before_count} file(s) before, ${after_count} after).
The working tree did not match the schemas. They have now been regenerated;
re-run the gate. If you hand-edited a .g.cs file, do not -- change the schema."
fi

echo "codegen fresh: ${after_count} file(s) match schemas/"
