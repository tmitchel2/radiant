#!/usr/bin/env bash
# Regenerates Radiant.Text's Unicode tables and conformance-test data from the Unicode Character
# Database. Change the version here to move to a newer Unicode, then regenerate and run the tests.
set -euo pipefail

version="16.0.0"
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
out="$repo/src/Radiant.Text/Unicode"
tests="$repo/src/Radiant.Text.Tests/UnicodeData"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

curl -sSfL -o "$work/UCD.zip" "https://www.unicode.org/Public/$version/ucd/UCD.zip"
unzip -q "$work/UCD.zip" -d "$work/ucd"
ucd="$work/ucd"
mkdir -p "$tests"

gen() { python3 "$here/generate.py" --ucd "$ucd" --out "$out" --version "$version" "$@"; }
conformance() { gzip -9 -n -c "$ucd/$1" > "$tests/$(basename "$1").gz"; }

# Each algorithm adds its tables and conformance files below.

# Text segmentation (UAX #29): word and grapheme cluster boundaries.
gen --file auxiliary/WordBreakProperty.txt --alias WB --enum WordBreakProperty --table WordBreak
gen --file auxiliary/GraphemeBreakProperty.txt --alias GCB --enum GraphemeBreakProperty --table GraphemeBreak
gen --file DerivedCoreProperties.txt --alias InCB --property InCB --enum IndicConjunctBreak --table IndicConjunctBreak
gen --file emoji/emoji-data.txt --binary Extended_Pictographic --enum ExtendedPictographicValue --table ExtendedPictographic
conformance auxiliary/WordBreakTest.txt
conformance auxiliary/GraphemeBreakTest.txt

# Line breaking (UAX #14): Line_Break, plus the East_Asian_Width and General_Category values its
# rules consult (only the few that matter, to keep the tables small). It shares Extended_Pictographic
# with segmentation above.
gen --file extracted/DerivedLineBreak.txt --alias lb --enum LineBreakClass --table LineBreak
gen --file extracted/DerivedEastAsianWidth.txt --alias ea --enum EastAsianWidth --table EastAsianWidth --values "N F H W"
gen --file extracted/DerivedGeneralCategory.txt --alias gc --enum GeneralCategory --table GeneralCategory \
  --values "Unlisted Cn Mc Mn Pf Pi" --default Unlisted
conformance auxiliary/LineBreakTest.txt

# Bidirectional Algorithm (UAX #9): Bidi_Class, Bidi_Mirrored (L4), paired brackets (N0).
gen --file extracted/DerivedBidiClass.txt --alias bc --enum BidiClass --table BidiClass
gen --file extracted/DerivedBinaryProperties.txt --binary Bidi_Mirrored --enum BidiMirrored --table BidiMirrored
python3 "$here/brackets.py" --ucd "$ucd" --out "$out" --version "$version"
conformance BidiTest.txt
conformance BidiCharacterTest.txt
