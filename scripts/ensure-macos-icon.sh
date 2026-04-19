#!/usr/bin/env bash
set -euo pipefail

PUBLISH_DIR="${1:?publish directory is required}"
REPO_ROOT="${2:?repo root is required}"

configured="${CHUMMER_MACOS_ICON_SOURCE:-}"

resolve_icon_source() {
  local candidates=()
  if [[ -n "$configured" ]]; then
    candidates+=("$configured")
  fi

  candidates+=(
    "$PUBLISH_DIR/chummer.icns"
    "$REPO_ROOT/Chummer/chummer.icns"
    "$PUBLISH_DIR/chummer.ico"
    "$REPO_ROOT/Chummer/chummer.ico"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" ]]; then
      printf '%s' "$candidate"
      return 0
    fi
  done

  return 1
}

render_iconset_png() {
  local source_path="$1"
  local destination_path="$2"
  local size="$3"

  sips -s format png -z "$size" "$size" "$source_path" --out "$destination_path" >/dev/null
}

generate_icns_from_source() {
  local source_path="$1"
  local target_path="$2"
  local work_root
  work_root="$(mktemp -d "${TMPDIR:-/tmp}/chummer-icon.XXXXXX")"
  local iconset_dir="$work_root/chummer.iconset"
  mkdir -p "$iconset_dir"

  cleanup() {
    rm -rf "$work_root"
  }
  trap cleanup RETURN

  render_iconset_png "$source_path" "$iconset_dir/icon_16x16.png" 16
  render_iconset_png "$source_path" "$iconset_dir/icon_16x16@2x.png" 32
  render_iconset_png "$source_path" "$iconset_dir/icon_32x32.png" 32
  render_iconset_png "$source_path" "$iconset_dir/icon_32x32@2x.png" 64
  render_iconset_png "$source_path" "$iconset_dir/icon_128x128.png" 128
  render_iconset_png "$source_path" "$iconset_dir/icon_128x128@2x.png" 256
  render_iconset_png "$source_path" "$iconset_dir/icon_256x256.png" 256
  render_iconset_png "$source_path" "$iconset_dir/icon_256x256@2x.png" 512
  render_iconset_png "$source_path" "$iconset_dir/icon_512x512.png" 512
  render_iconset_png "$source_path" "$iconset_dir/icon_512x512@2x.png" 1024

  iconutil -c icns "$iconset_dir" -o "$target_path"
  printf '%s' "$target_path"
}

if ! icon_source="$(resolve_icon_source)"; then
  echo "macOS packaging preflight failed: chummer.icns or chummer.ico not found." >&2
  echo "Set CHUMMER_MACOS_ICON_SOURCE, place chummer.icns in $PUBLISH_DIR, or add Chummer/chummer.ico." >&2
  exit 1
fi

case "$icon_source" in
  *.icns)
    printf '%s\n' "$icon_source"
    ;;
  *.ico)
    if ! command -v sips >/dev/null 2>&1; then
      echo "macOS packaging preflight failed: sips is required to generate chummer.icns from $icon_source." >&2
      exit 1
    fi
    if ! command -v iconutil >/dev/null 2>&1; then
      echo "macOS packaging preflight failed: iconutil is required to generate chummer.icns from $icon_source." >&2
      exit 1
    fi

    generated_icon_path="$PUBLISH_DIR/chummer.icns"
    echo "macOS packaging preflight: generating chummer.icns from $icon_source" >&2
    generate_icns_from_source "$icon_source" "$generated_icon_path"
    printf '\n'
    ;;
  *)
    echo "macOS packaging preflight failed: unsupported icon source $icon_source." >&2
    echo "Set CHUMMER_MACOS_ICON_SOURCE to a .icns or .ico file." >&2
    exit 1
    ;;
esac
