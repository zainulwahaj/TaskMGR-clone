#!/usr/bin/env bash
set -euo pipefail

# Converts src/TaskMGR.UI/Assets/AppIcon.svg into src/TaskMGR.UI/Assets/AppIcon.icns
# using built-in macOS tooling (qlmanage, sips, iconutil).

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SVG_PATH="${1:-$REPO_ROOT/src/TaskMGR.UI/Assets/AppIcon.svg}"
ICNS_PATH="${2:-$REPO_ROOT/src/TaskMGR.UI/Assets/AppIcon.icns}"

if [[ ! -f "$SVG_PATH" ]]; then
    echo "SVG not found: $SVG_PATH" >&2
    exit 1
fi

for cmd in qlmanage sips iconutil; do
    if ! command -v "$cmd" >/dev/null 2>&1; then
        echo "Required command not found: $cmd" >&2
        exit 1
    fi
done

TMP_DIR="$(mktemp -d)"
ICONSET_DIR="$TMP_DIR/AppIcon.iconset"
mkdir -p "$ICONSET_DIR"

cleanup() {
    rm -rf "$TMP_DIR"
}
trap cleanup EXIT

# Quick Look renders the SVG to a PNG preview. Output naming format is <filename>.png.
qlmanage -t -s 1024 -o "$TMP_DIR" "$SVG_PATH" >/dev/null 2>&1
BASE_PNG="$TMP_DIR/$(basename "$SVG_PATH").png"

if [[ ! -f "$BASE_PNG" ]]; then
    echo "Could not rasterize SVG via qlmanage: $SVG_PATH" >&2
    exit 1
fi

make_icon() {
    local px="$1"
    local out="$2"
    sips -z "$px" "$px" "$BASE_PNG" --out "$ICONSET_DIR/$out" >/dev/null
}

make_icon 16 icon_16x16.png
make_icon 32 icon_16x16@2x.png
make_icon 32 icon_32x32.png
make_icon 64 icon_32x32@2x.png
make_icon 128 icon_128x128.png
make_icon 256 icon_128x128@2x.png
make_icon 256 icon_256x256.png
make_icon 512 icon_256x256@2x.png
make_icon 512 icon_512x512.png
make_icon 1024 icon_512x512@2x.png

mkdir -p "$(dirname "$ICNS_PATH")"
iconutil -c icns "$ICONSET_DIR" -o "$ICNS_PATH"

echo "Generated: $ICNS_PATH"
