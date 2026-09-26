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
