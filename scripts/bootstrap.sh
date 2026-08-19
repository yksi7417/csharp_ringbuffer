#!/usr/bin/env bash
# Bring a fresh machine or agent session to the point where `dotnet build` works.
#
# Idempotent: safe to re-run. Called by the SessionStart hook (task 0.2).
#
# NOTE: this deliberately does NOT use dotnet-install.sh. That script fetches from
# builds.dotnet.microsoft.com, which is denied by the egress proxy in sandboxed
# sessions. See knowledge/findings/f1-dotnet-install-egress-blocked.md.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SBE_VERSION="$(tr -d '[:space:]' < "$REPO_ROOT/sbe-version.txt")"
SBE_JAR="$REPO_ROOT/tools/sbe-csharp-gen/sbe-all-${SBE_VERSION}.jar"
MAVEN_BASE="https://repo1.maven.org/maven2/uk/co/real-logic/sbe-all"

log() { printf '\033[1mbootstrap:\033[0m %s\n' "$*"; }
die() { printf '\033[31mbootstrap: %s\033[0m\n' "$*" >&2; exit 1; }

# ── .NET SDK ────────────────────────────────────────────────────────────────
# Presence is not enough: the SDK MAJOR VERSION must match global.json. GitHub
# runners ship .NET 10, where VSTest has been removed, so a repo developed on 8
# passes locally and fails in CI. See TRAP-8.
REQUIRED_SDK_MAJOR=8

have_required_sdk() {
    command -v dotnet >/dev/null 2>&1 || return 1
    dotnet --list-sdks 2>/dev/null | grep -q "^${REQUIRED_SDK_MAJOR}\."
}

if have_required_sdk; then
    log ".NET SDK ${REQUIRED_SDK_MAJOR}.x present: $(dotnet --list-sdks | grep "^${REQUIRED_SDK_MAJOR}\." | tail -1 | cut -d' ' -f1)"
elif command -v dotnet >/dev/null 2>&1; then
    log "dotnet present ($(dotnet --version)) but no ${REQUIRED_SDK_MAJOR}.x SDK; installing one"
    ${SUDO:-} apt-get update -qq
    ${SUDO:-} apt-get install -y -qq "dotnet-sdk-${REQUIRED_SDK_MAJOR}.0" \
        || die "could not install the .NET ${REQUIRED_SDK_MAJOR} SDK required by global.json"
    export PATH="$PATH:/usr/lib/dotnet"
else
    log "installing .NET SDK 8 via apt"
    # apt-get update is REQUIRED first. Without it the install fails with 404s on
    # stale package URLs, which looks like a broken mirror rather than a stale index.
    ${SUDO:-} apt-get update -qq
    ${SUDO:-} apt-get install -y -qq dotnet-sdk-8.0 \
        || die "apt install failed. Do NOT fall back to dotnet-install.sh -- it is egress-blocked."
    export PATH="$PATH:/usr/lib/dotnet"
    command -v dotnet >/dev/null 2>&1 || die "dotnet still not on PATH after install"
    log ".NET SDK installed: $(dotnet --version)"
fi

# /usr/lib/dotnet is where the apt package lands; it is not on PATH by default.
if ! command -v dotnet >/dev/null 2>&1 && [ -x /usr/lib/dotnet/dotnet ]; then
    export PATH="$PATH:/usr/lib/dotnet"
fi

# ── JDK ─────────────────────────────────────────────────────────────────────
# Required even though this is a C# project: the SBE codec generator is Java.
# See knowledge/findings/f2-csharp-codegen-requires-shim.md.
if command -v java >/dev/null 2>&1; then
    log "JDK present: $(java -version 2>&1 | grep -i version | head -1)"
else
    die "no JDK found. The SBE codec generator is Java -- install a JDK 17+ and re-run."
fi

# ── SBE generator jar ───────────────────────────────────────────────────────
mkdir -p "$(dirname "$SBE_JAR")"
if [ -f "$SBE_JAR" ]; then
    log "SBE generator jar present: sbe-all-${SBE_VERSION}.jar"
else
    # Maven Central rate-limits (HTTP 429) when both matrix legs fetch at once.
    # Retry with backoff rather than failing the build on someone else's quota.
    fetched=0
    for attempt in 1 2 3 4 5; do
        log "fetching sbe-all-${SBE_VERSION}.jar from Maven Central (attempt ${attempt})"
        if curl -fsSL --retry 3 --retry-connrefused --retry-delay 2 \
                -o "$SBE_JAR.tmp" "$MAVEN_BASE/${SBE_VERSION}/sbe-all-${SBE_VERSION}.jar"; then
            fetched=1
            break
        fi
        rm -f "$SBE_JAR.tmp"
        [ "$attempt" -lt 5 ] && sleep $((attempt * attempt * 2))
    done
    [ "$fetched" -eq 1 ] || die "could not fetch the SBE jar from Maven Central after 5 attempts"

    # A truncated download is worse than none: it fails later, somewhere else.
    if ! unzip -l "$SBE_JAR.tmp" >/dev/null 2>&1; then
        rm -f "$SBE_JAR.tmp"
        die "downloaded jar is not a valid archive"
    fi
    mv "$SBE_JAR.tmp" "$SBE_JAR"
    log "fetched $(du -h "$SBE_JAR" | cut -f1)"
fi

# ── python, for the gate's checks ───────────────────────────────────────────
python3 -c 'import yaml' 2>/dev/null || {
    log "installing pyyaml (needed by the OKF validator)"
    python3 -m pip install --quiet pyyaml 2>/dev/null \
        || ${SUDO:-} apt-get install -y -qq python3-yaml \
        || die "could not install pyyaml"
}

log "ready. Next: scripts/ci/gate.sh fast"
