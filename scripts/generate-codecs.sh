#!/usr/bin/env bash
# Generate the C# SBE codecs from schemas/.
#
# Uses the Java shim in tools/sbe-csharp-gen, NOT -Dsbe.target.language=CSharp --
# that reaches no generator on any release. Before "fixing" this to use the
# documented flag, read knowledge/findings/f2-csharp-codegen-requires-shim.md.
#
# Generator stderr is never discarded: a generator that writes nothing exits 0,
# which is TRAP-1 in docs/TRAPS.md.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT" || exit 1

SBE_VERSION="$(tr -d '[:space:]' < sbe-version.txt)"
SHIM_DIR="tools/sbe-csharp-gen"
SBE_JAR="$SHIM_DIR/sbe-all-${SBE_VERSION}.jar"
OUT_DIR="src/RingBuffer.Codecs"

[ -d /usr/lib/dotnet ] && export PATH="$PATH:/usr/lib/dotnet"

die() { printf '\033[31mgenerate-codecs: %s\033[0m\n' "$*" >&2; exit 1; }
log() { printf 'generate-codecs: %s\n' "$*"; }

[ -f "$SBE_JAR" ] || die "missing $SBE_JAR -- run scripts/bootstrap.sh"
command -v java >/dev/null 2>&1 || die "no JDK. The SBE generator is Java (F2)."

# ── compile the shim if stale ───────────────────────────────────────────────
if [ ! -f "$SHIM_DIR/out/SbeCsharpGen.class" ] \
   || [ "$SHIM_DIR/SbeCsharpGen.java" -nt "$SHIM_DIR/out/SbeCsharpGen.class" ]; then
    log "compiling the shim"
    mkdir -p "$SHIM_DIR/out"
    javac -Xlint:all -cp "$SBE_JAR" -d "$SHIM_DIR/out" "$SHIM_DIR/SbeCsharpGen.java" \
        || die "shim failed to compile"
fi

# ── generate ────────────────────────────────────────────────────────────────
# Remove only generated sources. The .csproj is hand-written and tracked.
mkdir -p "$OUT_DIR"
find "$OUT_DIR" -name '*.g.cs' -delete

for schema in schemas/*.xml; do
    log "generating from $schema"
    java -cp "$SBE_JAR:$SHIM_DIR/out" SbeCsharpGen "$schema" "$OUT_DIR" \
        || die "generation failed for $schema"
done

# A generator that writes nothing exits 0. Assert output exists rather than
# trusting the exit code -- TRAP-1.
count=$(find "$OUT_DIR" -name '*.cs' | wc -l)
[ "$count" -gt 0 ] || die "generator produced no .cs files (it exited 0 -- see TRAP-1)"


log "$count file(s) generated into $OUT_DIR"
