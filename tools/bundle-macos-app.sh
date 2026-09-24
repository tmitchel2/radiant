#!/usr/bin/env bash
set -euo pipefail

# Wraps a published Radiant application in a macOS .app bundle.
#
# Usage:
#   tools/bundle-macos-app.sh --name <Name> --executable <exe> --bundle-id <id> \
#       --publish-dir <dir> --out <dir> [--icon <file.icns>] [--version <x.y>] \
#       [--category <LSApplicationCategoryType>]
#
# The publish output of `dotnet publish -r osx-*` is a bare executable. macOS LaunchServices
# will not launch a bare executable as a GUI app from the Dock/Finder — it hands it to Terminal
# instead. Wrapping the same binary in a .app bundle (Info.plist + Contents/MacOS/ +
# Contents/Resources/) makes LaunchServices treat it as a first-class GUI app, so it launches
# directly with no Terminal window. The whole publish tree (executable, dylibs, any data
# directories) goes under Contents/MacOS/ as siblings of the executable, matching Native AOT's
# @executable_path rpath and any relative lookups the app makes — so no code changes are needed.
#
# Without --icon the bundle carries Radiant's own icon (src/Radiant.Host/Resources/Radiant.icns).
# The bundle is unsigned: fine for locally-built personal use; distribution needs codesign and
# notarization.

RADIANT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

NAME=""
EXE_NAME=""
BUNDLE_ID=""
PUBLISH_DIR=""
OUT_DIR=""
ICNS_SRC="$RADIANT_ROOT/src/Radiant.Host/Resources/Radiant.icns"
APP_VERSION="1.0"
CATEGORY="public.app-category.developer-tools"

while [ $# -gt 0 ]; do
    case "$1" in
        --name) NAME="$2"; shift 2 ;;
        --executable) EXE_NAME="$2"; shift 2 ;;
        --bundle-id) BUNDLE_ID="$2"; shift 2 ;;
        --publish-dir) PUBLISH_DIR="$2"; shift 2 ;;
        --out) OUT_DIR="$2"; shift 2 ;;
        --icon) ICNS_SRC="$2"; shift 2 ;;
        --version) APP_VERSION="$2"; shift 2 ;;
        --category) CATEGORY="$2"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 2 ;;
    esac
done

for REQUIRED in NAME EXE_NAME BUNDLE_ID PUBLISH_DIR OUT_DIR; do
    if [ -z "${!REQUIRED}" ]; then
        echo "Missing required argument for $REQUIRED (see the usage at the top of $0)." >&2
        exit 2
    fi
done

if [ ! -f "$PUBLISH_DIR/$EXE_NAME" ]; then
    echo "$PUBLISH_DIR/$EXE_NAME does not exist — publish the application first." >&2
    exit 1
fi

if [ ! -f "$ICNS_SRC" ]; then
    echo "Icon $ICNS_SRC does not exist." >&2
    exit 1
fi

APP="$OUT_DIR/$NAME.app"
ICON_NAME="$(basename "$ICNS_SRC" .icns)"

echo "--- Assembling $APP ---"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Copy the entire publish tree as siblings of the executable.
cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/$EXE_NAME"

cp "$ICNS_SRC" "$APP/Contents/Resources/$ICON_NAME.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>${NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>${NAME}</string>
    <key>CFBundleExecutable</key>
    <string>${EXE_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>${BUNDLE_ID}</string>
    <key>CFBundleIconFile</key>
    <string>${ICON_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${APP_VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${APP_VERSION}</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSApplicationCategoryType</key>
    <string>${CATEGORY}</string>
</dict>
</plist>
PLIST

echo "  Bundled -> $APP"
