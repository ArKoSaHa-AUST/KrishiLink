"""Collect every data-on* handler (views + site.js), render Razor/template holes with sample values, and write JSON for
tests/js/inline-handlers.test.js. Fails if any inline on*= attribute remains: the enforced CSP would silently block it."""
import json
import pathlib
import re
import sys

EVENTS = "click|change|input|submit|keydown"
START = re.compile(r'(?<=\s)data-on(' + EVENTS + r')\s*=\s*(["\'])')


def skip_razor(text, i):
    """text[i] == '@'. Return index after the Razor expression."""
    j = i + 1
    if j < len(text) and text[j] == '(':
        depth = 0
        while j < len(text):
            c = text[j]
            if c == '"':
                j = text.index('"', j + 1)
            elif c == '(':
                depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    return j + 1
            j += 1
        raise ValueError("unbalanced @(")
    # @Identifier(.Member | (args) | [idx])*
    m = re.match(r'[A-Za-z_][\w]*', text[j:])
    if not m:
        return j
    j += m.end()
    while j < len(text):
        if text[j] == '.' and re.match(r'\.[A-Za-z_]', text[j:]):
            j += 1 + re.match(r'[A-Za-z_]\w*', text[j + 1:]).end()
        elif text[j] in '([':
            close = ')' if text[j] == '(' else ']'
            depth = 0
            while True:
                c = text[j]
                if c == '"':
                    j = text.index('"', j + 1)
                elif c in '([':
                    depth += 1
                elif c in ')]':
                    depth -= 1
                    if depth == 0:
                        j += 1
                        break
                j += 1
        else:
            break
    return j


def extract(text):
    """Yield (start, end_of_value, event, raw_value, rendered_value)."""
    for m in START.finditer(text):
        quote = m.group(2)
        i = m.end()
        rendered = []
        while True:
            c = text[i]
            if c == '@' and quote == '"' and text[i + 1:i + 2] not in ('@',):
                end = skip_razor(text, i)
                rendered.append('7')
                i = end
                continue
            if c == '$' and text[i + 1:i + 2] == '{':
                end = text.index('}', i)
                rendered.append('7')
                i = end + 1
                continue
            if c == quote:
                break
            rendered.append(c)
            i += 1
        yield m.start(), i, m.group(1), text[m.end():i], ''.join(rendered)


LEGACY = re.compile(r"""(?<=[\s"])on(?:click|change|input|submit|keydown|keyup|load|error|blur|focus|mouse\w+)\s*=\s*["']""", re.I)

if __name__ == "__main__":
    legacy = []
    for path in sorted(pathlib.Path("Views").rglob("*.cshtml")) + sorted(pathlib.Path("wwwroot/js").glob("*.js")):
        for m in LEGACY.finditer(path.read_text(encoding="utf-8-sig")):
            legacy.append(f"{path.as_posix()}: {m.group(0)}")
    if legacy:
        print("Inline event handlers are blocked by the Content-Security-Policy; use data-on<event> instead:")
        print("\n".join("  " + x for x in legacy))
        sys.exit(1)
    out = []
    files = sorted(pathlib.Path("Views").rglob("*.cshtml")) + [pathlib.Path("wwwroot/js/site.js")]
    for path in files:
        text = path.read_text(encoding="utf-8-sig")
        for start, end, event, raw, rendered in extract(text):
            out.append({"file": path.as_posix(), "event": event, "raw": raw, "rendered": rendered})
    json.dump(out, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(len(out), "handlers")
