#!/usr/bin/env python3
"""Fail when a view uses a utility-style class that no stylesheet defines.

Bootstrap 5.3 has no half-step spacing (gap-1.5, px-2.5 ...), no opacity-10, no shadow-xs; such names are silent
no-ops unless the compatibility section of wwwroot/css/site.css defines them. This check keeps that from recurring.
It only judges tokens that look like Bootstrap/Tailwind utilities, so component classes are never false positives.
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]

STYLESHEETS = [ROOT / "wwwroot/lib/bootstrap/dist/css/bootstrap.css", *sorted((ROOT / "wwwroot/css").glob("*.css"))]
VIEWS = sorted((ROOT / "Views").rglob("*.cshtml"))
SCOPED = sorted((ROOT / "Views").rglob("*.cshtml.css"))

BREAKPOINT = r"(?:-(?:sm|md|lg|xl|xxl))?"
UTILITY = re.compile(
    r"^(?:[mp][tbsexy]?|gap|row-gap|column-gap|g[xy]?|opacity|bg-opacity|text-opacity|border-opacity|link-opacity"
    r"|shadow|rounded(?:-(?:top|bottom|start|end|circle|pill))?|fs|fw|fst|lh|tracking|z|pointer-events"
    r"|w|h|mw|mh|min-vw|min-vh|vw|vh|top|bottom|start|end|border(?:-(?:top|bottom|start|end))?|order|flex|d|col|row-cols)"
    + BREAKPOINT + r"-[a-z0-9.]+$"
)
PLAIN_TOKEN = re.compile(r"^[a-z0-9][a-z0-9.-]*$")


def defined_classes(css: str) -> set:
    css = re.sub(r"/\*.*?\*/", "", css, flags=re.S)
    selectors = []
    buffer = []
    for ch in css:
        if ch == "{":
            selectors.append("".join(buffer))
            buffer = []
        elif ch == "}":
            buffer = []
        else:
            buffer.append(ch)
    names = set()
    for selector in selectors:
        if selector.strip().startswith("@"):
            continue
        for match in re.finditer(r"\.((?:[A-Za-z0-9_-]|\\.)+)", selector):
            names.add(re.sub(r"\\(.)", r"\1", match.group(1)))
    return names


def main() -> int:
    defined = set()
    for sheet in [*STYLESHEETS, *SCOPED]:
        defined |= defined_classes(sheet.read_text(encoding="utf-8"))
    for view in VIEWS:
        for block in re.findall(r"<style[^>]*>(.*?)</style>", view.read_text(encoding="utf-8"), flags=re.S):
            defined |= defined_classes(block)

    unknown = {}
    for view in VIEWS:
        text = view.read_text(encoding="utf-8")
        for attribute in re.findall(r'\bclass\s*=\s*"([^"]*)"', text) + re.findall(r"\bclass\s*=\s*'([^']*)'", text):
            for token in attribute.split():
                if PLAIN_TOKEN.match(token) and UTILITY.match(token) and token not in defined:
                    unknown.setdefault(token, set()).add(str(view.relative_to(ROOT)).replace("\\", "/"))

    if unknown:
        print("Utility classes used in views but defined by no stylesheet (add them to the compatibility")
        print("section of wwwroot/css/site.css, or use a Bootstrap class that exists):")
        for token in sorted(unknown):
            print(f"  {token}: {', '.join(sorted(unknown[token]))}")
        return 1
    print(f"CSS class check passed ({len(defined)} defined classes).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
