#!/usr/bin/env bash
set -euo pipefail

downloads_root="${1:-}"
if [[ -z "$downloads_root" || ! -d "$downloads_root" || -L "$downloads_root" ]]; then
  echo "public_download_modes:error:downloads root must be a regular directory" >&2
  exit 2
fi

if find "$downloads_root" -xdev -type l -print -quit | grep -q .; then
  echo "public_download_modes:error:symlinks are not accepted" >&2
  exit 2
fi
if find "$downloads_root" -xdev ! -type d ! -type f -print -quit | grep -q .; then
  echo "public_download_modes:error:special files are not accepted" >&2
  exit 2
fi

find "$downloads_root" -xdev -type d -exec chmod 0755 {} +
find "$downloads_root" -xdev -type f ! -perm /111 -exec chmod 0644 {} +
find "$downloads_root" -xdev -type f -perm /111 -exec chmod 0755 {} +

if find "$downloads_root" -xdev -type d ! -perm 0755 -print -quit | grep -q .; then
  echo "public_download_modes:error:directory mode verification failed" >&2
  exit 2
fi
if find "$downloads_root" -xdev -type f ! -perm /111 ! -perm 0644 -print -quit | grep -q .; then
  echo "public_download_modes:error:regular file mode verification failed" >&2
  exit 2
fi
if find "$downloads_root" -xdev -type f -perm /111 ! -perm 0755 -print -quit | grep -q .; then
  echo "public_download_modes:error:executable file mode verification failed" >&2
  exit 2
fi

echo "public_download_modes:ready:$downloads_root"
