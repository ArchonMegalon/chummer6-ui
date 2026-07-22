#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
DEFAULT_TOOLCHAIN_LOCK_PATH="$REPO_ROOT/config/windows-native-bootstrap-toolchain.lock.json"

process_toolchain_lock() {
  local mode="$1"
  local lock_path="$2"
  local cache_path="${3:-}"

  python3 - "$mode" "$lock_path" "$cache_path" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
import urllib.parse
import urllib.request
from pathlib import Path, PurePosixPath


MODE, LOCK_PATH_TEXT, CACHE_PATH_TEXT = sys.argv[1:]
LOCK_PATH = Path(LOCK_PATH_TEXT)

EXPECTED_PACKAGE_DEPENDENCIES = {
    "gcc-12-base": [],
    "libc6": ["libgcc-s1"],
    "libgcc-s1": ["gcc-12-base", "libc6"],
    "libstdc++6": ["gcc-12-base", "libc6", "libgcc-s1"],
    "nsis": ["libc6", "libgcc-s1", "libstdc++6", "nsis-common", "zlib1g"],
    "nsis-common": [],
    "p7zip": ["libc6", "libgcc-s1", "libstdc++6"],
    "p7zip-full": ["libc6", "libgcc-s1", "libstdc++6", "p7zip"],
    "zlib1g": ["libc6"],
}
EXPECTED_ROOTS = ["nsis", "p7zip-full"]
HEX_64 = re.compile(r"^[0-9a-f]{64}$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
PACKAGE_NAME = re.compile(r"^[a-z0-9][a-z0-9+.-]*$")
SNAPSHOT_TIMESTAMP = re.compile(r"^[0-9]{8}T[0-9]{6}Z$")


def fail(message: str) -> None:
    raise SystemExit(f"Invalid Windows native bootstrap toolchain lock: {message}")


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            fail(f"duplicate JSON key {key!r}")
        result[key] = value
    return result


def require_object(value: object, label: str) -> dict[str, object]:
    if not isinstance(value, dict):
        fail(f"{label} must be an object")
    return value


def require_exact_keys(value: dict[str, object], expected: set[str], label: str) -> None:
    actual = set(value)
    if actual != expected:
        fail(
            f"{label} keys differ; missing={sorted(expected - actual)!r} "
            f"unexpected={sorted(actual - expected)!r}"
        )


try:
    with LOCK_PATH.open("r", encoding="utf-8") as handle:
        lock = json.load(handle, object_pairs_hook=reject_duplicate_keys)
except (OSError, json.JSONDecodeError) as exc:
    fail(f"cannot read {LOCK_PATH}: {exc}")

lock = require_object(lock, "root")
require_exact_keys(
    lock,
    {
        "contract_name",
        "schema_version",
        "platform",
        "container_image",
        "debian_snapshot",
        "packages",
    },
    "root",
)
if lock["contract_name"] != "chummer6-ui.windows_native_bootstrap_toolchain_lock":
    fail("contract_name is not recognized")
if type(lock["schema_version"]) is not int or lock["schema_version"] != 1:
    fail("schema_version must be integer 1")

platform = require_object(lock["platform"], "platform")
require_exact_keys(platform, {"os", "architecture"}, "platform")
if platform != {"os": "linux", "architecture": "amd64"}:
    fail("platform must be exactly linux/amd64")

image = require_object(lock["container_image"], "container_image")
require_exact_keys(
    image,
    {"reference", "index_digest", "platform_manifest_digest"},
    "container_image",
)
for key in ("index_digest", "platform_manifest_digest"):
    value = image[key]
    if not isinstance(value, str) or not DIGEST.fullmatch(value):
        fail(f"container_image.{key} must be a lowercase SHA-256 digest")
expected_image_reference = f"docker.io/library/debian@{image['index_digest']}"
if image["reference"] != expected_image_reference:
    fail(f"container_image.reference must be {expected_image_reference!r}")

snapshot = require_object(lock["debian_snapshot"], "debian_snapshot")
require_exact_keys(
    snapshot,
    {
        "timestamp",
        "archive_base_url",
        "suite",
        "component",
        "metadata_url",
        "install_roots",
        "include_recommends",
    },
    "debian_snapshot",
)
timestamp = snapshot["timestamp"]
if not isinstance(timestamp, str) or not SNAPSHOT_TIMESTAMP.fullmatch(timestamp):
    fail("debian_snapshot.timestamp must use YYYYMMDDTHHMMSSZ")
archive_base_url = f"https://snapshot.debian.org/archive/debian/{timestamp}"
if snapshot["archive_base_url"] != archive_base_url:
    fail("debian_snapshot.archive_base_url does not match timestamp")
if snapshot["suite"] != "bookworm" or snapshot["component"] != "main":
    fail("Debian suite/component must be exactly bookworm/main")
expected_metadata_url = f"{archive_base_url}/dists/bookworm/main/binary-amd64/Packages.xz"
if snapshot["metadata_url"] != expected_metadata_url:
    fail("debian_snapshot.metadata_url is not the exact snapshot Packages.xz URL")
if snapshot["install_roots"] != EXPECTED_ROOTS:
    fail(f"debian_snapshot.install_roots must be exactly {EXPECTED_ROOTS!r}")
if snapshot["include_recommends"] is not False:
    fail("debian_snapshot.include_recommends must be false")

packages = lock["packages"]
if not isinstance(packages, list) or not packages:
    fail("packages must be a non-empty array")
package_by_name: dict[str, dict[str, object]] = {}
seen_paths: set[str] = set()
seen_urls: set[str] = set()
seen_hashes: set[str] = set()
for index, raw_package in enumerate(packages):
    package = require_object(raw_package, f"packages[{index}]")
    require_exact_keys(
        package,
        {"name", "version", "architecture", "path", "url", "size", "sha256", "dependencies"},
        f"packages[{index}]",
    )
    name = package["name"]
    version = package["version"]
    architecture = package["architecture"]
    package_path = package["path"]
    url = package["url"]
    size = package["size"]
    sha256 = package["sha256"]
    dependencies = package["dependencies"]
    if not isinstance(name, str) or not PACKAGE_NAME.fullmatch(name):
        fail(f"packages[{index}].name is invalid")
    if name in package_by_name:
        fail(f"package name {name!r} is duplicated")
    if not isinstance(version, str) or not version or any(char.isspace() for char in version):
        fail(f"package {name!r} has an invalid version")
    if architecture not in {"all", "amd64"}:
        fail(f"package {name!r} architecture must be all or amd64")
    if not isinstance(package_path, str):
        fail(f"package {name!r} path must be a string")
    path_parts = PurePosixPath(package_path).parts
    if (
        len(path_parts) < 5
        or path_parts[:2] != ("pool", "main")
        or any(part in {"", ".", ".."} for part in path_parts)
        or not package_path.endswith(f"_{architecture}.deb")
    ):
        fail(f"package {name!r} has an invalid Debian pool path")
    expected_url = f"{archive_base_url}/{urllib.parse.quote(package_path, safe='/-._~')}"
    if url != expected_url:
        fail(f"package {name!r} URL does not exactly bind its snapshot path")
    if type(size) is not int or size <= 0:
        fail(f"package {name!r} size must be a positive integer")
    if not isinstance(sha256, str) or not HEX_64.fullmatch(sha256):
        fail(f"package {name!r} SHA-256 must be 64 lowercase hex characters")
    if not isinstance(dependencies, list) or any(not isinstance(item, str) for item in dependencies):
        fail(f"package {name!r} dependencies must be a string array")
    if dependencies != sorted(set(dependencies)):
        fail(f"package {name!r} dependencies must be unique and sorted")
    if package_path in seen_paths or url in seen_urls or sha256 in seen_hashes:
        fail(f"package {name!r} reuses another package path, URL, or digest")
    seen_paths.add(package_path)
    seen_urls.add(url)
    seen_hashes.add(sha256)
    package_by_name[name] = package

expected_package_names = sorted(EXPECTED_PACKAGE_DEPENDENCIES)
if [package["name"] for package in packages] != expected_package_names:
    fail(f"packages must be ordered as the exact closure {expected_package_names!r}")
for name, expected_dependencies in EXPECTED_PACKAGE_DEPENDENCIES.items():
    if package_by_name[name]["dependencies"] != expected_dependencies:
        fail(f"package {name!r} dependencies do not match the locked no-recommends closure")

reachable: set[str] = set()
pending = list(EXPECTED_ROOTS)
while pending:
    name = pending.pop()
    if name in reachable:
        continue
    if name not in package_by_name:
        fail(f"dependency {name!r} is absent from packages")
    reachable.add(name)
    pending.extend(package_by_name[name]["dependencies"])
if reachable != set(package_by_name):
    fail("packages contains entries outside the install-root dependency closure")

if MODE == "validate":
    print(
        "\t".join(
            (
                str(image["reference"]),
                str(platform["os"]),
                str(platform["architecture"]),
                str(image["platform_manifest_digest"]),
            )
        )
    )
    raise SystemExit(0)
if MODE != "prefetch":
    fail(f"unsupported lock processing mode {MODE!r}")
if not CACHE_PATH_TEXT:
    fail("prefetch mode requires a cache path")

cache_root = Path(CACHE_PATH_TEXT)
if cache_root.is_symlink():
    fail(f"cache path must not be a symlink: {cache_root}")
package_dir = cache_root / "debs"
package_dir.mkdir(parents=True, exist_ok=True)
if package_dir.is_symlink():
    fail(f"package cache must not be a symlink: {package_dir}")


def file_identity(path: Path) -> tuple[int, str]:
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            size += len(chunk)
            digest.update(chunk)
    return size, digest.hexdigest()


for package in packages:
    filename = PurePosixPath(str(package["path"])).name
    destination = package_dir / filename
    if destination.is_symlink():
        fail(f"cached package must not be a symlink: {destination}")
    if destination.exists():
        actual_size, actual_sha256 = file_identity(destination)
        if actual_size != package["size"] or actual_sha256 != package["sha256"]:
            fail(f"cached package does not match lock: {destination}")
        print(f"Verified cached Debian package {filename}", file=sys.stderr)
        continue

    print(f"Fetching locked Debian package {package['url']}", file=sys.stderr)
    request = urllib.request.Request(
        str(package["url"]),
        headers={"User-Agent": "chummer6-ui-native-bootstrap-toolchain/1"},
    )
    temporary_path: Path | None = None
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            final_url = urllib.parse.urlparse(response.geturl())
            if final_url.scheme != "https" or final_url.hostname != "snapshot.debian.org":
                fail(f"package {package['name']!r} redirected outside HTTPS snapshot.debian.org")
            with tempfile.NamedTemporaryFile(
                mode="wb",
                dir=package_dir,
                prefix=f".{filename}.",
                suffix=".part",
                delete=False,
            ) as temporary:
                temporary_path = Path(temporary.name)
                shutil.copyfileobj(response, temporary, length=1024 * 1024)
        actual_size, actual_sha256 = file_identity(temporary_path)
        if actual_size != package["size"] or actual_sha256 != package["sha256"]:
            fail(
                f"downloaded package {package['name']!r} identity mismatch: "
                f"size={actual_size} sha256={actual_sha256}"
            )
        os.replace(temporary_path, destination)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)

checksums_path = cache_root / "SHA256SUMS"
packages_path = cache_root / "PACKAGES.tsv"
checksums_text = "".join(
    f"{package['sha256']}  debs/{PurePosixPath(str(package['path'])).name}\n"
    for package in packages
)
packages_text = "".join(
    "\t".join(
        (
            PurePosixPath(str(package["path"])).name,
            str(package["name"]),
            str(package["version"]),
            str(package["architecture"]),
        )
    )
    + "\n"
    for package in packages
)
checksums_path.write_text(checksums_text, encoding="utf-8")
packages_path.write_text(packages_text, encoding="utf-8")

print(
    "\t".join(
        (
            str(image["reference"]),
            str(platform["os"]),
            str(platform["architecture"]),
            str(image["platform_manifest_digest"]),
        )
    )
)
PY
}

prefetch_pinned_asset() {
  local url="$1"
  local sha256="$2"
  local output_path="$3"

  python3 - "$url" "$sha256" "$output_path" <<'PY'
from __future__ import annotations

import hashlib
import os
import re
import shutil
import sys
import tempfile
import urllib.parse
import urllib.request
from pathlib import Path


url, expected_sha256, output_path_text = sys.argv[1:]
output_path = Path(output_path_text)
if not re.fullmatch(r"[0-9a-f]{64}", expected_sha256):
    raise SystemExit("Pinned Windows asset SHA-256 must be 64 lowercase hex characters")
parsed_url = urllib.parse.urlparse(url)
if parsed_url.scheme != "https" or not parsed_url.hostname:
    raise SystemExit("Pinned Windows asset URL must use HTTPS")
output_path.parent.mkdir(parents=True, exist_ok=True)
if output_path.is_symlink():
    raise SystemExit(f"Pinned Windows asset cache must not be a symlink: {output_path}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


if output_path.exists():
    actual_sha256 = sha256(output_path)
    if actual_sha256 != expected_sha256:
        raise SystemExit(
            f"Cached Windows asset SHA-256 mismatch for {output_path}: {actual_sha256}"
        )
    print(f"Verified cached Windows asset {output_path.name}", file=sys.stderr)
    raise SystemExit(0)

request = urllib.request.Request(
    url,
    headers={"User-Agent": "chummer6-ui-native-bootstrap-toolchain/1"},
)
temporary_path: Path | None = None
try:
    with urllib.request.urlopen(request, timeout=120) as response:
        final_url = urllib.parse.urlparse(response.geturl())
        if final_url.scheme != "https":
            raise SystemExit("Pinned Windows asset redirected away from HTTPS")
        with tempfile.NamedTemporaryFile(
            mode="wb",
            dir=output_path.parent,
            prefix=f".{output_path.name}.",
            suffix=".part",
            delete=False,
        ) as temporary:
            temporary_path = Path(temporary.name)
            shutil.copyfileobj(response, temporary, length=1024 * 1024)
    actual_sha256 = sha256(temporary_path)
    if actual_sha256 != expected_sha256:
        raise SystemExit(
            f"Downloaded Windows asset SHA-256 mismatch for {url}: {actual_sha256}"
        )
    os.replace(temporary_path, output_path)
    temporary_path = None
finally:
    if temporary_path is not None:
        temporary_path.unlink(missing_ok=True)
PY
}

validate_container_manifest() {
  local expected_os="$1"
  local expected_architecture="$2"
  local expected_digest="$3"
  local manifest_json="$4"

  python3 - "$expected_os" "$expected_architecture" "$expected_digest" "$manifest_json" <<'PY'
from __future__ import annotations

import json
import sys


expected_os, expected_architecture, expected_digest, manifest_text = sys.argv[1:]
try:
    manifest = json.loads(manifest_text)
except json.JSONDecodeError as exc:
    raise SystemExit(f"Locked container index manifest is not valid JSON: {exc}") from exc
if not isinstance(manifest, dict) or not isinstance(manifest.get("manifests"), list):
    raise SystemExit("Locked container reference did not resolve to a multi-platform index")

matches = []
for descriptor in manifest["manifests"]:
    if not isinstance(descriptor, dict):
        continue
    platform = descriptor.get("platform")
    if not isinstance(platform, dict):
        continue
    if platform.get("os") == expected_os and platform.get("architecture") == expected_architecture:
        matches.append(descriptor)
if len(matches) != 1:
    raise SystemExit(
        f"Locked container index must contain exactly one {expected_os}/{expected_architecture} "
        f"manifest, found {len(matches)}"
    )
actual_digest = matches[0].get("digest")
if actual_digest != expected_digest:
    raise SystemExit(
        f"Locked {expected_os}/{expected_architecture} container manifest digest mismatch: "
        f"expected {expected_digest}, got {actual_digest}"
    )
PY
}

TOOLCHAIN_LOCK_PATH="${CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_LOCK:-$DEFAULT_TOOLCHAIN_LOCK_PATH}"
if [[ "${1:-}" == "--validate-toolchain-lock-only" ]]; then
  if [[ $# -gt 2 ]]; then
    echo "Usage: $0 --validate-toolchain-lock-only [lock-path]" >&2
    exit 2
  fi
  TOOLCHAIN_LOCK_PATH="${2:-$TOOLCHAIN_LOCK_PATH}"
  process_toolchain_lock validate "$TOOLCHAIN_LOCK_PATH" >/dev/null
  echo "Windows native bootstrap toolchain lock is valid: $TOOLCHAIN_LOCK_PATH"
  exit 0
fi

STAGE_DIR="${1:?stage directory is required}"
OUTPUT_PATH="${2:?output path is required}"

STAGE_DIR="$(python3 - "$STAGE_DIR" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).resolve())
PY
)"
OUTPUT_PATH="$(python3 - "$OUTPUT_PATH" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).resolve())
PY
)"

CONFIG_PATH="$STAGE_DIR/bootstrap-config.nsh"
if [[ ! -f "$CONFIG_PATH" ]]; then
  echo "Missing bootstrap config: $CONFIG_PATH" >&2
  exit 1
fi

mkdir -p "$STAGE_DIR/7zip" "$(dirname "$OUTPUT_PATH")"
mkdir -p "$STAGE_DIR/curl"
TOOLCHAIN_CACHE_DIR="${CHUMMER_WINDOWS_NATIVE_BOOTSTRAP_TOOLCHAIN_CACHE_DIR:-${STAGE_DIR}.windows-native-bootstrap-toolchain}"
TOOLCHAIN_CACHE_DIR="$(python3 - "$TOOLCHAIN_CACHE_DIR" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).resolve())
PY
)"
if [[ "$TOOLCHAIN_CACHE_DIR" == "$STAGE_DIR" || "$TOOLCHAIN_CACHE_DIR" == "$STAGE_DIR/"* ]]; then
  echo "Native bootstrap toolchain cache must be outside the writable stage directory: $TOOLCHAIN_CACHE_DIR" >&2
  exit 1
fi

SEVENZIP_EXTRA_URL="${CHUMMER_WINDOWS_7ZIP_EXTRA_URL:-https://github.com/ip7z/7zip/releases/download/26.02/7z2602-extra.7z}"
SEVENZIP_EXTRA_SHA256="${CHUMMER_WINDOWS_7ZIP_EXTRA_SHA256:-081df9e9311dfd9c9e0e98c1c80180b99bb51e4cb24156b5f3057fe3c259d70a}"
CURL_WINDOWS_URL="${CHUMMER_WINDOWS_CURL_URL:-https://curl.se/windows/dl-8.21.0_1/curl-8.21.0_1-win64-mingw.zip}"
CURL_WINDOWS_SHA256="${CHUMMER_WINDOWS_CURL_SHA256:-157068447d5b0b178dcc650f29d4746049fa4c7cc12db5f2bc050c0b84e48e7a}"

TOOLCHAIN_DESCRIPTOR="$(process_toolchain_lock prefetch "$TOOLCHAIN_LOCK_PATH" "$TOOLCHAIN_CACHE_DIR")"
IFS=$'\t' read -r TOOLCHAIN_IMAGE TOOLCHAIN_OS TOOLCHAIN_ARCH TOOLCHAIN_PLATFORM_MANIFEST_DIGEST <<<"$TOOLCHAIN_DESCRIPTOR"
if [[ -z "$TOOLCHAIN_IMAGE" || -z "$TOOLCHAIN_OS" || -z "$TOOLCHAIN_ARCH" || -z "$TOOLCHAIN_PLATFORM_MANIFEST_DIGEST" ]]; then
  echo "Toolchain lock did not produce a complete container descriptor" >&2
  exit 1
fi

prefetch_pinned_asset "$SEVENZIP_EXTRA_URL" "$SEVENZIP_EXTRA_SHA256" "$TOOLCHAIN_CACHE_DIR/assets/7zip-extra.7z"
prefetch_pinned_asset "$CURL_WINDOWS_URL" "$CURL_WINDOWS_SHA256" "$TOOLCHAIN_CACHE_DIR/assets/curl-win64.zip"

python3 "$REPO_ROOT/scripts/finalize-windows-bootstrap-installer.py" \
  --config "$CONFIG_PATH" \
  --validate-payload-only

IMAGE_MANIFEST_JSON="$(docker manifest inspect "$TOOLCHAIN_IMAGE")"
validate_container_manifest \
  "$TOOLCHAIN_OS" \
  "$TOOLCHAIN_ARCH" \
  "$TOOLCHAIN_PLATFORM_MANIFEST_DIGEST" \
  "$IMAGE_MANIFEST_JSON"
docker pull --quiet --platform "$TOOLCHAIN_OS/$TOOLCHAIN_ARCH" "$TOOLCHAIN_IMAGE" >/dev/null
IMAGE_PLATFORM="$(docker image inspect "$TOOLCHAIN_IMAGE" --format '{{.Os}}/{{.Architecture}}')"
if [[ "$IMAGE_PLATFORM" != "$TOOLCHAIN_OS/$TOOLCHAIN_ARCH" ]]; then
  echo "Locked bootstrap image platform mismatch: expected $TOOLCHAIN_OS/$TOOLCHAIN_ARCH, got $IMAGE_PLATFORM" >&2
  exit 1
fi

docker run --rm --pull never --network none --platform "$TOOLCHAIN_OS/$TOOLCHAIN_ARCH" \
  -e HOST_UID="$(id -u)" \
  -e HOST_GID="$(id -g)" \
  -e SEVENZIP_EXTRA_SHA256="$SEVENZIP_EXTRA_SHA256" \
  -e CURL_WINDOWS_SHA256="$CURL_WINDOWS_SHA256" \
  -v "$REPO_ROOT:/repo:ro" \
  -v "$STAGE_DIR:/work" \
  -v "$TOOLCHAIN_CACHE_DIR:/toolchain:ro" \
  -w /work \
  "$TOOLCHAIN_IMAGE" \
  bash -lc '
    set -euo pipefail
    export DEBIAN_FRONTEND=noninteractive
    cd /toolchain
    sha256sum --check --strict SHA256SUMS >/dev/null
    while IFS=$'\''\t'\'' read -r filename package version architecture; do
      actual_package="$(dpkg-deb --field "/toolchain/debs/$filename" Package)"
      actual_version="$(dpkg-deb --field "/toolchain/debs/$filename" Version)"
      actual_architecture="$(dpkg-deb --field "/toolchain/debs/$filename" Architecture)"
      if [[ "$actual_package" != "$package" || "$actual_version" != "$version" || "$actual_architecture" != "$architecture" ]]; then
        echo "Locked Debian package metadata mismatch for $filename" >&2
        exit 1
      fi
    done </toolchain/PACKAGES.tsv

    dpkg --unpack /toolchain/debs/*.deb >/dev/null
    dpkg --configure --pending >/dev/null
    command -v 7z >/dev/null
    command -v makensis >/dev/null

    echo "${SEVENZIP_EXTRA_SHA256}  /toolchain/assets/7zip-extra.7z" | sha256sum --check --strict - >/dev/null
    echo "${CURL_WINDOWS_SHA256}  /toolchain/assets/curl-win64.zip" | sha256sum --check --strict - >/dev/null
    7z e -aoa -o/work/7zip /toolchain/assets/7zip-extra.7z 7za.exe 7za.dll 7zxa.dll License.txt >/dev/null
    7z e -aoa -o/work/curl /toolchain/assets/curl-win64.zip "*/bin/curl.exe" "*/bin/libcurl-x64.dll" "*/bin/curl-ca-bundle.crt" "*/COPYING.txt" >/dev/null

    makensis \
      -DCHUMMER_BOOTSTRAP_CONFIG=/work/bootstrap-config.nsh \
      -DCHUMMER_OUTPUT_PATH=/work/output-installer.exe \
      /repo/scripts/windows-bootstrap/installer.nsi >/work/makensis.log

    if command -v chown >/dev/null 2>&1; then
      chown -R "${HOST_UID}:${HOST_GID}" /work
    fi
  '

if [[ ! -f "$STAGE_DIR/output-installer.exe" ]]; then
  echo "NSIS bootstrap build did not produce $STAGE_DIR/output-installer.exe" >&2
  exit 1
fi

python3 "$REPO_ROOT/scripts/finalize-windows-bootstrap-installer.py" \
  --installer "$STAGE_DIR/output-installer.exe" \
  --config "$CONFIG_PATH"

mv -f "$STAGE_DIR/output-installer.exe" "$OUTPUT_PATH"
