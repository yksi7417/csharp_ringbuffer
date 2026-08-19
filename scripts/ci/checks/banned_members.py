#!/usr/bin/env python3
"""Ban allocating SBE overloads from the hot path.

The ergonomic call is the allocating one: GetSymbol() returns a string,
GetSymbol(Span<byte>) does not. Reaching for the nicer name is a reflex, and the
cost is invisible in review -- so it is enforced here rather than in a guideline.
See knowledge/findings/f4-directbuffer-native-pointer.md (R5).

Scopes: src/, excluding the vendored runtime and generated codecs, which we do
not write and cannot fix.

    python3 scripts/ci/checks/banned_members.py
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

SRC = Path("src")
EXCLUDED_DIRS = {"RingBuffer.Sbe", "RingBuffer.Codecs", "bin", "obj"}

# (pattern, why, what to use instead)
BANNED = [
    (re.compile(r"\.Get([A-Z]\w*)\(\s*\)"),
     "parameterless SBE getter allocates a string or array",
     "the Span<byte> overload, or the ReadOnlySpan property"),
    (re.compile(r"\.Get(\w+)Bytes\(\s*\)"),
     "GetXxxBytes() allocates a new byte[] every call",
     "GetXxx(Span<byte>)"),
    (re.compile(r"\bnew\s+byte\s*\[")
     , "allocating a byte[] on the hot path",
     "a pre-allocated buffer, stackalloc, or a Span into the ring"),
    (re.compile(r"\.ToArray\(\s*\)"),
     "ToArray() copies and allocates",
     "operate on the Span directly"),
    (re.compile(r"\bstring\.Format\b|\$\"[^\"]*\{"),
     "string interpolation or Format allocates",
     "a pre-formatted constant, or move it off the hot path"),
]

# A line carrying this marker is exempt, and must say why on the same line.
ALLOW = re.compile(r"//\s*ALLOW-ALLOC:\s*(\S.*)")


def scanned_files() -> list[Path]:
    if not SRC.is_dir():
        return []
    return [
        p for p in SRC.rglob("*.cs")
        if not (EXCLUDED_DIRS & set(p.parts)) and p.is_file()
    ]


def main() -> int:
    findings: list[str] = []
    files = scanned_files()

    for path in files:
        for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            allow = ALLOW.search(line)
            if allow:
                # An exemption must carry a reason; a bare marker is how a ban
                # quietly stops meaning anything.
                if not allow.group(1).strip():
                    findings.append(f"{path}:{lineno}: ALLOW-ALLOC with no reason given")
                continue
            for pattern, why, instead in BANNED:
                if pattern.search(line):
                    findings.append(
                        f"{path}:{lineno}: {why}\n"
                        f"    {stripped}\n"
                        f"    use {instead}, or mark the line "
                        f"`// ALLOW-ALLOC: <reason>` if it is genuinely off the hot path"
                    )
                    break

    for f in findings:
        print(f"ERROR: {f}", file=sys.stderr)

    if findings:
        print(f"\nbanned-members: FAILED -- {len(findings)} finding(s)", file=sys.stderr)
        return 1

    print(f"banned-members: OK -- {len(files)} file(s) scanned")
    return 0


if __name__ == "__main__":
    sys.exit(main())
