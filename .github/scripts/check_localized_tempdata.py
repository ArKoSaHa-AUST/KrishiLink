#!/usr/bin/env python3
"""Fail when a controller puts text into TempData without going through the localizer.

A Bangla-speaking user otherwise sees English exactly at the moments that matter (payment, rejection, verification).
Every assignment must read  TempData[...] = _localizer[...]  (dynamic service messages too: _localizer[error]).
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
ASSIGNMENT = re.compile(r"TempData\[[^\]]+\]\s*=(?!=)\s*(?P<rhs>[^;]*);", re.S)

offenders = []
for path in sorted((ROOT / "Controllers").rglob("*.cs")):
    text = path.read_text(encoding="utf-8-sig")
    for match in ASSIGNMENT.finditer(text):
        if not match.group("rhs").lstrip().startswith("_localizer["):
            line = text.count("\n", 0, match.start()) + 1
            offenders.append(f"{path.relative_to(ROOT).as_posix()}:{line}: {match.group(0).strip()[:120]}")

if offenders:
    print("TempData messages must be localized (TempData[...] = _localizer[\"...\"].Value):")
    print("\n".join(f"  {o}" for o in offenders))
    sys.exit(1)
print("TempData localization check passed.")
