#!/usr/bin/env bash
# Builds Radiant's embedded Material Symbols (Apache-2.0) from Google's variable font: only the
# icons in icons.txt, with FILL, opsz and wght kept and GRAD pinned to 0.
#
# Needs Python 3; installs fontTools into a throwaway virtual environment.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
commit="bd8cb85bd4bad964fe6918f79665bb40c3a8efef" # google/material-design-icons, variablefont/
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

python3 -m venv "$work/venv"
"$work/venv/bin/pip" install -q fonttools
curl -sSL -o "$work/full.ttf" \
  "https://github.com/google/material-design-icons/raw/$commit/variablefont/MaterialSymbolsRounded%5BFILL,GRAD,opsz,wght%5D.ttf"

"$work/venv/bin/python" - "$work/full.ttf" "$here/icons.txt" "$repo/src/Radiant.Text/Fonts/MaterialSymbolsRounded.ttf" <<'PY'
import sys
from fontTools.ttLib import TTFont
from fontTools.subset import Subsetter, Options
from fontTools.varLib import instancer

full, listing, out = sys.argv[1:4]
font = TTFont(full)
names = [l.strip() for l in open(listing) if l.strip() and not l.startswith('#')]
order = set(font.getGlyphOrder())
missing = [n for n in names if n not in order]
if missing:
    sys.exit(f"not in the font: {', '.join(missing)}")

# The icons, their filled forms, and the characters their names are spelt with (the ligatures'
# inputs). Layout closure is off, so only ligatures that make these icons survive.
glyphs = set(names) | {n + '.fill' for n in names if n + '.fill' in order}
options = Options()
options.layout_features = ['*']
options.layout_closure = False
options.notdef_outline = True
options.name_IDs = ['*']
subsetter = Subsetter(options)
subsetter.populate(glyphs=sorted(glyphs), text='abcdefghijklmnopqrstuvwxyz0123456789_')
subsetter.subset(font)

font = instancer.instantiateVariableFont(font, {'GRAD': 0})
font.save(out)
print(f"{len(names)} icons -> {out}")
PY
