#!/usr/bin/env bash
# Regenerates the fixtures in src/Radiant.ColorSystem.Tests/Fixtures from upstream
# material-color-utilities at the commit Radiant.ColorSystem is ported from.
#
# Needs git, Node (npm) and a JDK. Most fixtures come from the TypeScript port; the quantize/score
# fixture comes from the Java port, whose k-means is seeded (the TypeScript one uses Math.random).
set -euo pipefail

commit="5b3618b16fdc3825e21d5679bafd144662088ea1"
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
out="$repo/src/Radiant.ColorSystem.Tests/Fixtures"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

git -C "$work" init -q upstream
git -C "$work/upstream" fetch -q --depth 1 https://github.com/material-foundation/material-color-utilities.git "$commit"
git -C "$work/upstream" checkout -q FETCH_HEAD

# TypeScript: compile, then add the ".js" some relative imports lack (Node's ES module loader
# requires it; upstream's bundler does not).
(cd "$work/upstream/typescript" && npm install --no-audit --no-fund --silent && npx tsc)
find "$work/upstream/typescript" -name '*.js' -not -path '*/node_modules/*' \
    -exec perl -pi -e "s#(from\s+'\.{1,2}/[^']*?)(?<!\.js)'#\$1.js'#g" {} +
mkdir -p "$out" "$work/raw"
node "$here/generate.mjs" "$work/upstream/typescript" "$out"
mv "$out/quantize-inputs.txt" "$work/raw/"

# Java: compile just the modules quantization and scoring need, with stubs for the annotation
# libraries upstream builds against.
java_src="$work/upstream/java"
mkdir -p "$work/classes"
javac -d "$work/classes" -nowarn \
    $(find "$here/java/stubs" -name '*.java') \
    "$java_src"/utils/*.java "$java_src"/hct/*.java "$java_src"/quantize/*.java \
    "$java_src"/score/*.java "$java_src"/dislike/*.java "$here/java/QuantizeFixtures.java"
java -cp "$work/classes" QuantizeFixtures "$work/raw/quantize-inputs.txt" "$work/raw/quantize.json"
gzip -9 -n -c "$work/raw/quantize.json" > "$out/quantize.json.gz"

echo "Fixtures written to $out (upstream $commit)"
