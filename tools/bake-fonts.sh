#!/usr/bin/env bash
# Rebakes Radiant's embedded MSDF font atlases from the OFL sources in src/Radiant/Assets/Fonts.
#
# Every atlas uses the same glyph size, distance range and atlas size, so text drawn in any of them
# is equally sharp. The distance range is recorded in each manifest and read by the shader, so a
# different range only has to be changed here.
#
# Symbols the primary fonts lack (⌀ ⌒ ⊥ ∠ ∥ ◎ … used in engineering annotations) are baked from Noto
# fallbacks, so text such as "S⌀ 5.00 mm" draws in one font. The fallbacks are downloaded from a
# pinned google/fonts commit rather than committed, because only a handful of their glyphs are used.
#
# The drafting-* atlases (GD&T frame symbols, Noto) are not rebaked here.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
fonts="$repo/src/Radiant/Assets/Fonts"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

google_fonts="https://github.com/google/fonts/raw/23e54b51ddffbc7713c583748e3bd86f62b1fa4a/ofl"
curl -sSfL -o "$work/NotoSansSymbols.ttf" "$google_fonts/notosanssymbols/NotoSansSymbols%5Bwght%5D.ttf"
curl -sSfL -o "$work/NotoSansMath.ttf" "$google_fonts/notosansmath/NotoSansMath-Regular.ttf"
curl -sSfL -o "$work/NotoSansSymbols2.ttf" "$google_fonts/notosanssymbols2/NotoSansSymbols2-Regular.ttf"

export MSBUILDDISABLENODEREUSE=1
dotnet build "$repo/src/MsdfBaker/MsdfBaker.csproj" -c Release -p:UseSharedCompilation=false --nodeReuse:false --verbosity quiet
baker="$repo/src/MsdfBaker/bin/Release/net10.0/MsdfBaker"

bake() {
    local source="$1" name="$2"
    "$baker" --font "$fonts/$source" --out "$work" --name "$name" --codepoints default --size 40 --range 6 --atlas 1024 \
        --fallback "$work/NotoSansSymbols.ttf" --fallback "$work/NotoSansMath.ttf" --fallback "$work/NotoSansSymbols2.ttf"
    cp "$work/$name.png" "$work/$name.json" "$fonts/"
}

bake Inter-Regular.ttf inter-regular
bake Inter-Medium.ttf inter-medium
bake Inter-SemiBold.ttf inter-semibold
bake JetBrainsMono-Regular.ttf jetbrains-mono-regular
