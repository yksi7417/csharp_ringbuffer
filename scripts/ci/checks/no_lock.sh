#!/usr/bin/env bash
# D3: no `lock`, no `Monitor` on the publish or consume path.
#
# Scans src/, excluding vendored upstream code (which we do not control and which
# is not on the hot path). Deliberately blunt: there is no legitimate use of a
# kernel mutex in this library, so there is no whitelist to maintain.
#
# See knowledge/decisions/d3-non-blocking-backpressure.md.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$REPO_ROOT" || exit 1

[ -d src ] || { echo "no src/ yet"; exit 0; }

# `lock (` as a statement, or any Monitor./Mutex./SemaphoreSlim use.
PATTERN='(^|[^[:alnum:]_])lock[[:space:]]*\(|\bMonitor\.|\bMutex\b|\bSemaphoreSlim\b|\bReaderWriterLock'

hits=$(grep -rInE "$PATTERN" src \
    --include='*.cs' \
    --exclude-dir=RingBuffer.Sbe \
    --exclude-dir=RingBuffer.Codecs \
    --exclude-dir=bin --exclude-dir=obj 2>/dev/null || true)

if [ -n "$hits" ]; then
    printf 'blocking synchronisation found in src/ -- D3 forbids it:\n%s\n' "$hits" >&2
    exit 1
fi

scanned=$(find src -name '*.cs' -not -path '*/RingBuffer.Sbe/*' -not -path '*/RingBuffer.Codecs/*' \
    -not -path '*/bin/*' -not -path '*/obj/*' 2>/dev/null | wc -l)
echo "no blocking synchronisation in ${scanned} scanned file(s)"
