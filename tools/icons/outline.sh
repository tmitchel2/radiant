#!/usr/bin/env bash
# Builds Radiant's outline icons from Lucide (ISC): the Lucide glyphs outline-map.txt names, cut
# out of the icon font, and a map from each embedded icon name to its glyph's character.
#
# Needs Python 3; installs fontTools into a throwaway virtual environment.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
version="1.48.0" # lucide-static
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

python3 -m venv "$work/venv"
"$work/venv/bin/pip" install -q fonttools
curl -sSL -o "$work/lucide.tgz" "https://registry.npmjs.org/lucide-static/-/lucide-static-$version.tgz"
tar xzf "$work/lucide.tgz" -C "$work"
out="$repo/src/Radiant.Text/Fonts"
cp "$work/package/LICENSE" "$out/LucideIcons-LICENSE.txt"

"$work/venv/bin/python" - "$work/package/font" "$here/outline-map.txt" "$out" <<'PY'
import json, sys
from fontTools.ttLib import TTFont
from fontTools.subset import Subsetter, Options

font_dir, listing, out = sys.argv[1:4]
codepoints = json.load(open(f"{font_dir}/codepoints.json"))
pairs = [l.split() for l in open(listing) if l.strip() and not l.startswith('#')]
missing = sorted({lucide for _, lucide in pairs if lucide not in codepoints})
if missing:
    sys.exit(f"not in Lucide: {', '.join(missing)}")

font = TTFont(f"{font_dir}/lucide.ttf")
options = Options()
options.notdef_outline = True
options.name_IDs = ['*']
subsetter = Subsetter(options)
subsetter.populate(unicodes=sorted({codepoints[lucide] for _, lucide in pairs}))
subsetter.subset(font)
font.save(f"{out}/LucideIcons.ttf")

with open(f"{out}/OutlineIcons.map", "w") as f:
    f.write("# Embedded icon name, then the Lucide glyph drawn for it (tools/icons/outline.sh).\n")
    for name, lucide in pairs:
        f.write(f"{name} {codepoints[lucide]:X}\n")
print(f"{len(pairs)} icons -> {out}/LucideIcons.ttf")
PY
