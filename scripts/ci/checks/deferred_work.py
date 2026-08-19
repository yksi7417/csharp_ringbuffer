#!/usr/bin/env python3
"""Enforce the deferred-work register.

Bidirectional, which is the point:
  - a `DEFERRED: T-n` marker in the tree with no register entry FAILS
  - a register entry missing **Why** or **Done when** FAILS

So a comment cannot outlive the work it points at: when T-n lands, the marker
goes with it or the build breaks. See
knowledge/practices/deferred-work-register.md.

    python3 scripts/ci/checks/deferred_work.py
"""

from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

REGISTER = Path("docs/TODO.md")
ENTRY = re.compile(r"^##\s+(T-\d+)\s*(?:[-—]\s*(.*))?$", re.MULTILINE)
MARKER = re.compile(r"DEFERRED:\s*(T-\d+)")
REQUIRED_SECTIONS = ("**Why:**", "**Done when:**")


def tracked_files() -> list[Path]:
    """Only scan tracked files -- build output and caches are not our problem."""
    try:
        out = subprocess.run(
            ["git", "ls-files"], capture_output=True, text=True, check=True
        ).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return [p for p in Path(".").rglob("*") if p.is_file() and ".git" not in p.parts]
    return [Path(line) for line in out.splitlines() if line]


def parse_register(text: str) -> dict[str, str]:
    """Map entry id -> that entry's body text."""
    entries: dict[str, str] = {}
    matches = list(ENTRY.finditer(text))
    for i, m in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        entries[m.group(1)] = text[m.end():end]
    return entries


def main() -> int:
    errors: list[str] = []

    if not REGISTER.exists():
        print(f"deferred-work: no register at {REGISTER}", file=sys.stderr)
        return 1

    entries = parse_register(REGISTER.read_text(encoding="utf-8"))

    # Direction 1: every entry is complete enough to act on.
    # An entry without a Why cannot be prioritised; one without a Done when
    # never closes, because nobody can tell whether it is finished.
    for entry_id, body in sorted(entries.items()):
        for section in REQUIRED_SECTIONS:
            if section not in body:
                errors.append(f"{REGISTER}: {entry_id} is missing a {section} section")

    # Direction 2: every marker in the tree points at a real entry.
    markers: dict[str, list[str]] = {}
    for path in tracked_files():
        if path == REGISTER or not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        for entry_id in MARKER.findall(text):
            markers.setdefault(entry_id, []).append(str(path))

    for entry_id, paths in sorted(markers.items()):
        if entry_id not in entries:
            for p in paths:
                errors.append(f"{p}: DEFERRED: {entry_id} has no entry in {REGISTER}")

    for err in errors:
        print(f"ERROR: {err}", file=sys.stderr)

    if errors:
        print(f"\ndeferred-work: FAILED -- {len(errors)} error(s)", file=sys.stderr)
        return 1

    marked = sum(len(v) for v in markers.values())
    print(
        f"deferred-work: OK -- {len(entries)} entr(y/ies), "
        f"{marked} marker(s) all resolved"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
