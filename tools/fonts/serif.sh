#!/usr/bin/env bash
# Builds Radiant's embedded Source Serif 4 (SIL Open Font License) from Adobe's variable fonts:
# upright and italic, with the wght and opsz axes kept, cut down to Latin text (Basic Latin,
# Latin-1, Latin Extended-A and B, punctuation, currency and common symbols).
#
# Needs Python 3; installs fontTools into a throwaway virtual environment.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
release="4.005R" # adobe-fonts/source-serif
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

python3 -m venv "$work/venv"
"$work/venv/bin/pip" install -q fonttools
curl -sSL -o "$work/fonts.zip" \
  "https://github.com/adobe-fonts/source-serif/releases/download/$release/source-serif-${release%R}_Desktop.zip"
unzip -q "$work/fonts.zip" -d "$work"
var="$work/source-serif-${release%R}_Desktop/VAR"
out="$repo/src/Radiant.Text/Fonts"
cp "$work/source-serif-${release%R}_Desktop/LICENSE.md" "$out/SourceSerif4-LICENSE.txt"

unicodes="U+0000-024F,U+02B0-02FF,U+0300-036F,U+1E00-1EFF,U+2000-206F,U+20A0-20CF,U+2100-214F,U+2190-21FF,U+2200-22FF,U+25A0-25FF,U+FB00-FB06"
for style in Roman Italic; do
  "$work/venv/bin/pyftsubset" "$var/SourceSerif4Variable-$style.ttf" \
    --unicodes="$unicodes" --layout-features='*' --name-IDs='*' --notdef-outline \
    --output-file="$out/SourceSerif4Variable-$style.ttf"
done
ls -l "$out"/SourceSerif4Variable-*.ttf
