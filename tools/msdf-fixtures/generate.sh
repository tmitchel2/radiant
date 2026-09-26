#!/usr/bin/env bash
# Regenerates the msdfgen parity fixtures in src/Radiant.Text.Tests/MsdfFixtures.
#
# Radiant.Text's MSDF generator (src/Radiant.Text/Msdf) is a port of msdfgen's core. This builds
# upstream msdfgen at the commit the port follows, with fixture.cpp as the driver, in a temporary
# directory (nothing is installed or committed but the fixtures), and runs it on every .shape in
# the fixtures directory to write the .msdf.gz the parity tests compare against.
#
# Needs git and a C++ compiler (clang++ or g++); no CMake. Floating-point contraction is turned
# off so the reference is computed operation by operation, as .NET computes the port.

set -euo pipefail

MSDFGEN_COMMIT=1c106ed8117893bf943e577f62eb0665fb271e46
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FIXTURES="$ROOT/src/Radiant.Text.Tests/MsdfFixtures"
CXX="${CXX:-clang++}"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

git clone --quiet https://github.com/Chlumsky/msdfgen.git "$WORK/msdfgen"
git -C "$WORK/msdfgen" checkout --quiet "$MSDFGEN_COMMIT"

"$CXX" -std=c++11 -O2 -ffp-contract=off -DMSDFGEN_PUBLIC= -DMSDFGEN_USE_CPP11 \
  -I"$WORK/msdfgen" \
  "$ROOT/tools/msdf-fixtures/fixture.cpp" "$WORK"/msdfgen/core/*.cpp \
  -o "$WORK/fixture"

for shape in "$FIXTURES"/*.shape; do
  name="$(basename "$shape" .shape)"
  "$WORK/fixture" < "$shape" | gzip -9 -n > "$FIXTURES/$name.msdf.gz"
  echo "$name: $(gzip -dc "$FIXTURES/$name.msdf.gz" | head -n 1)"
done
