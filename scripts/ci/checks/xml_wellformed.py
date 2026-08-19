#!/usr/bin/env python3
"""Assert every XML file in the tree parses.

Exists because `--` is illegal inside an XML comment and I wrote it three times
in one session -- in a .csproj, a .props and an SBE schema. MSBuild reports it as
"project file could not be loaded" and the SBE parser as a SAXParseException,
neither of which points at the comment. See TRAP-10.

    python3 scripts/ci/checks/xml_wellformed.py
"""

from __future__ import annotations

import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

SUFFIXES = {".xml", ".csproj", ".props", ".targets", ".sln.xml", ".config"}


def tracked_xml() -> list[Path]:
    try:
        out = subprocess.run(
            ["git", "ls-files"], capture_output=True, text=True, check=True
        ).stdout
        paths = [Path(p) for p in out.splitlines() if p]
    except (subprocess.CalledProcessError, FileNotFoundError):
        paths = [p for p in Path(".").rglob("*") if ".git" not in p.parts]
    return [p for p in paths if p.suffix in SUFFIXES and p.is_file()]


def main() -> int:
    errors: list[str] = []
    files = tracked_xml()

    for path in files:
        try:
            ET.parse(path)
        except ET.ParseError as exc:
            hint = ""
            # The error message for this one is famously unhelpful, so name it.
            if "not permitted within comments" in str(exc) or "--" in str(exc):
                hint = "  (a '--' inside an XML comment is illegal; use a single hyphen)"
            errors.append(f"{path}: {exc}{hint}")

    for err in errors:
        print(f"ERROR: {err}", file=sys.stderr)

    if errors:
        print(f"\nxml-wellformed: FAILED -- {len(errors)} file(s)", file=sys.stderr)
        return 1

    print(f"xml-wellformed: OK -- {len(files)} file(s) parse")
    return 0


if __name__ == "__main__":
    sys.exit(main())
