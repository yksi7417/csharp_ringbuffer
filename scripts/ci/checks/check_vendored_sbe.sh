#!/usr/bin/env bash
# Assert the vendored SBE runtime is unmodified and matches the pinned upstream tag.
#
# Two layers, because they catch different things:
#   1. Local checksums  -- catches "someone edited a vendored file". Always runs, offline.
#   2. Upstream re-fetch -- catches "the pin drifted from what is committed". Needs network.
#
# See knowledge/findings/f3-sbe-dll-nuget-stale.md.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
VENDOR_DIR="$REPO_ROOT/src/RingBuffer.Sbe"
MANIFEST="$VENDOR_DIR/VENDORED.sha256"
SBE_VERSION="$(tr -d '[:space:]' < "$REPO_ROOT/sbe-version.txt")"

die() { printf '\033[31mvendored-sbe: %s\033[0m\n' "$*" >&2; exit 1; }
log() { printf 'vendored-sbe: %s\n' "$*"; }

if [ "${1:-}" = "--update" ]; then
    (cd "$VENDOR_DIR" && sha256sum ./*.cs > VENDORED.sha256)
    log "manifest regenerated for ${SBE_VERSION}"
    exit 0
fi

[ -f "$MANIFEST" ] || die "no checksum manifest at $MANIFEST"

# ── Layer 1: local integrity (always) ───────────────────────────────────────
(cd "$VENDOR_DIR" && sha256sum --quiet --check VENDORED.sha256) \
    || die "a vendored file has been modified. These are upstream source -- do not edit them."

# Guard against a file being *added* to the vendor directory without entering the
# manifest: a checksum check only validates what it lists.
listed=$(awk '{print $2}' "$MANIFEST" | sed 's|^\./||' | sort)
actual=$(cd "$VENDOR_DIR" && ls -1 ./*.cs | sed 's|^\./||' | sort)
[ "$listed" = "$actual" ] || die "vendor directory contents differ from the manifest:
$(diff <(echo "$listed") <(echo "$actual") || true)"

log "local integrity OK ($(wc -l < "$MANIFEST") files, tag ${SBE_VERSION})"

# ── Layer 2: upstream match (when the network allows) ───────────────────────
if [ "${SKIP_UPSTREAM_CHECK:-}" = "1" ]; then
    log "upstream re-fetch skipped (SKIP_UPSTREAM_CHECK=1)"
    exit 0
fi

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

if ! git clone --depth 1 --branch "$SBE_VERSION" --filter=blob:none --sparse \
        https://github.com/aeron-io/simple-binary-encoding.git "$tmp/s" >/dev/null 2>&1; then
    # A network failure is not a defect in the code. Say so rather than going red,
    # but never silently pass it off as a successful check.
    log "WARNING: could not reach upstream -- verified locally only, pin NOT re-checked"
    exit 0
fi
(cd "$tmp/s" && git sparse-checkout set csharp/sbe-dll >/dev/null 2>&1)

for f in "$VENDOR_DIR"/*.cs; do
    name=$(basename "$f")
    up="$tmp/s/csharp/sbe-dll/$name"
    [ -f "$up" ] || die "$name is not present upstream at tag ${SBE_VERSION}"
    cmp -s "$f" "$up" || die "$name differs from upstream at tag ${SBE_VERSION}"
done

log "upstream match OK at tag ${SBE_VERSION}"
