#!/usr/bin/env python3
"""Validate an Open Knowledge Format bundle.

Enforces OKF v0.2 conformance (SPEC section 11) plus this project's producer-side
house rules. See knowledge/playbooks/validate-the-bundle.md for the rationale.

    python3 scripts/ci/checks/okf_validate.py knowledge

Exit 0 clean, 1 on any error. Warnings never fail the build.
"""

from __future__ import annotations

import re
import sys
from datetime import date, datetime
from pathlib import Path

import yaml

RESERVED = {"index.md", "log.md"}
FRONTMATTER = re.compile(r"\A---\r?\n(.*?)\r?\n---\r?\n?", re.DOTALL)
# Markdown links, skipping images. Captures the target.
LINK = re.compile(r"(?<!!)\[[^\]]*\]\(([^)\s]+)")
DATE_HEADING = re.compile(r"^##\s+(\S+)", re.MULTILINE)
ISO_DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, path: Path, msg: str) -> None:
        self.errors.append(f"{path}: {msg}")

    def warn(self, path: Path, msg: str) -> None:
        self.warnings.append(f"{path}: {msg}")


def split_frontmatter(text: str) -> tuple[str | None, int]:
    """Return (frontmatter_text, body_start_offset). None when absent."""
    m = FRONTMATTER.match(text)
    if not m:
        return None, 0
    return m.group(1), m.end()


def parse_iso_datetime(value: object) -> bool:
    if isinstance(value, (datetime, date)):
        return True
    if not isinstance(value, str):
        return False
    try:
        datetime.fromisoformat(value.replace("Z", "+00:00"))
        return True
    except ValueError:
        return False


def check_actor_fields(path: Path, fm: dict, rep: Report) -> None:
    """SPEC section 5.2: generated.by is required; timestamps are ISO 8601."""
    generated = fm.get("generated")
    if generated is not None:
        if not isinstance(generated, dict):
            rep.error(path, "`generated` must be a mapping with `by` and `at`")
        else:
            if not generated.get("by"):
                rep.error(path, "`generated` requires a non-empty `by`")
            if "at" in generated and not parse_iso_datetime(generated["at"]):
                rep.error(path, f"`generated.at` is not ISO 8601: {generated['at']!r}")

    verified = fm.get("verified")
    if verified is not None:
        # SPEC section 11: a bare mapping is treated as a one-element list.
        entries = verified if isinstance(verified, list) else [verified]
        for entry in entries:
            if not isinstance(entry, dict):
                rep.error(path, "each `verified` entry must be a mapping")
                continue
            if not entry.get("by"):
                rep.error(path, "each `verified` entry requires a non-empty `by`")
            if "at" in entry and not parse_iso_datetime(entry["at"]):
                rep.error(path, f"`verified[].at` is not ISO 8601: {entry['at']!r}")


def check_lifecycle(path: Path, fm: dict, rep: Report) -> None:
    """SPEC sections 5.4 and 5.5."""
    status = fm.get("status")
    if status is not None and status not in {"draft", "stable", "deprecated"}:
        rep.error(path, f"`status` must be draft|stable|deprecated, got {status!r}")

    stale_after = fm.get("stale_after")
    if stale_after is None:
        return
    if isinstance(stale_after, date) and not isinstance(stale_after, datetime):
        expiry = stale_after
    elif isinstance(stale_after, str) and ISO_DATE.match(stale_after):
        expiry = date.fromisoformat(stale_after)
    else:
        rep.error(path, f"`stale_after` must be YYYY-MM-DD, got {stale_after!r}")
        return
    if expiry <= date.today():
        rep.warn(path, f"stale since {expiry.isoformat()} -- re-verify or extend")


def check_concept(path: Path, text: str, rep: Report) -> None:
    """SPEC section 11 rules 1 and 2."""
    raw, _ = split_frontmatter(text)
    if raw is None:
        rep.error(path, "no YAML frontmatter block (OKF 11.1)")
        return
    try:
        fm = yaml.safe_load(raw)
    except yaml.YAMLError as exc:
        rep.error(path, f"unparseable frontmatter (OKF 11.1): {exc}")
        return
    if not isinstance(fm, dict):
        rep.error(path, "frontmatter must be a YAML mapping")
        return
    if not str(fm.get("type") or "").strip():
        rep.error(path, "frontmatter has no non-empty `type` (OKF 11.2)")

    check_actor_fields(path, fm, rep)
    check_lifecycle(path, fm, rep)


def check_index(path: Path, text: str, is_root: bool, rep: Report) -> None:
    """SPEC section 8: no frontmatter, except okf_version at the bundle root."""
    raw, _ = split_frontmatter(text)
    if raw is None:
        return
    if not is_root:
        rep.error(path, "index.md must not carry frontmatter (OKF 8)")
        return
    try:
        fm = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        rep.error(path, f"unparseable frontmatter: {exc}")
        return
    extra = set(fm) - {"okf_version"}
    if extra:
        rep.error(path, f"root index.md may only carry `okf_version`, found {sorted(extra)}")


def check_log(path: Path, text: str, rep: Report) -> None:
    """SPEC section 9: date headings must be ISO 8601 YYYY-MM-DD."""
    if split_frontmatter(text)[0] is not None:
        rep.error(path, "log.md must not carry frontmatter (OKF 9)")
    for heading in DATE_HEADING.findall(text):
        if not ISO_DATE.match(heading):
            rep.error(path, f"log heading is not YYYY-MM-DD: {heading!r} (OKF 9)")


def check_links(path: Path, text: str, root: Path, rep: Report) -> None:
    """House rule: no broken bundle-relative or relative links.

    Stricter than the spec, which tells *consumers* to tolerate broken links
    (OKF 11). We are the producer; this is where they should be caught.
    """
    _, body_start = split_frontmatter(text)
    body = text[body_start:]
    for target in LINK.findall(body):
        if target.startswith(("http://", "https://", "mailto:", "#")):
            continue
        target = target.split("#", 1)[0]
        if not target:
            continue
        base = root if target.startswith("/") else path.parent
        resolved = (base / target.lstrip("/")).resolve()
        if resolved.is_dir():
            resolved = resolved / "index.md"
        if not resolved.exists():
            rep.error(path, f"broken link: {target}")


def check_reachability(root: Path, concepts: list[Path], rep: Report) -> None:
    """House rule: every concept is linked from some index.md.

    An unreachable concept is worse than an absent one -- it looks like
    documentation while no agent will ever load it.
    """
    linked: set[Path] = set()
    for index in root.rglob("index.md"):
        text = index.read_text(encoding="utf-8")
        _, body_start = split_frontmatter(text)
        for target in LINK.findall(text[body_start:]):
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            target = target.split("#", 1)[0]
            if not target:
                continue
            base = root if target.startswith("/") else index.parent
            resolved = (base / target.lstrip("/")).resolve()
            if resolved.is_dir():
                resolved = resolved / "index.md"
            linked.add(resolved)

    for concept in concepts:
        if concept.resolve() not in linked:
            rep.error(concept, "not linked from any index.md (unreachable)")


def main(argv: list[str]) -> int:
    root = Path(argv[1] if len(argv) > 1 else "knowledge").resolve()
    if not root.is_dir():
        print(f"okf-validate: no such bundle directory: {root}", file=sys.stderr)
        return 1

    rep = Report()
    concepts: list[Path] = []

    for path in sorted(root.rglob("*.md")):
        text = path.read_text(encoding="utf-8")
        rel = path.relative_to(root)

        if path.name == "index.md":
            check_index(path, text, is_root=(rel == Path("index.md")), rep=rep)
        elif path.name == "log.md":
            check_log(path, text, rep)
        else:
            concepts.append(path)
            check_concept(path, text, rep)

        check_links(path, text, root, rep)

    check_reachability(root, concepts, rep)

    for warning in rep.warnings:
        print(f"warn:  {warning}")
    for error in rep.errors:
        print(f"ERROR: {error}", file=sys.stderr)

    if rep.errors:
        print(
            f"\nokf-validate: FAILED -- {len(rep.errors)} error(s) "
            f"across {len(concepts)} concept(s)",
            file=sys.stderr,
        )
        return 1

    print(
        f"okf-validate: OK -- {len(concepts)} concepts, "
        f"{len(rep.warnings)} warning(s)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
