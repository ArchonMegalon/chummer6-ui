#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import tempfile
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path
from typing import Any, Iterable


HTTP_HEADERS = {
    "User-Agent": "ChummerLivePayloadVerifier/1.0",
    "Accept": "application/json, application/zip, application/octet-stream, */*;q=0.8",
    "Cache-Control": "no-cache",
    "Pragma": "no-cache",
}


def load_json_url(url: str, timeout: int) -> dict[str, Any]:
    request = urllib.request.Request(url, headers=HTTP_HEADERS)
    with urllib.request.urlopen(request, timeout=timeout) as response:
        payload = json.load(response)
    if not isinstance(payload, dict):
        raise ValueError(f"{url} did not return a JSON object")
    return payload


def load_json_file(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise ValueError(f"{path} did not contain a JSON object")
    return payload


def iter_manifest_downloads(payload: dict[str, Any]) -> Iterable[dict[str, Any]]:
    for key in ("downloads", "artifacts"):
        rows = payload.get(key)
        if not isinstance(rows, list):
            continue
        for row in rows:
            if isinstance(row, dict):
                yield row


def normalize_file_name(row: dict[str, Any]) -> str:
    file_name = str(row.get("fileName") or "").strip()
    if file_name:
        return file_name
    raw_url = str(row.get("downloadUrl") or row.get("url") or "").strip()
    if not raw_url:
        return ""
    return Path(urllib.parse.urlsplit(raw_url).path).name


def is_windows_bootstrap_installer(row: dict[str, Any]) -> bool:
    file_name = normalize_file_name(row).lower()
    mode = str(row.get("installerMode") or "").strip().lower()
    return (
        file_name.startswith("chummer-")
        and "-win-" in file_name
        and file_name.endswith("-installer.exe")
        and mode == "bootstrap"
    )


def normalized_origin(parsed: urllib.parse.SplitResult) -> tuple[str, str, int | None]:
    return (parsed.scheme.lower(), (parsed.hostname or "").lower(), parsed.port)


def resolve_public_url(value: str, manifest_url: str, label: str) -> str:
    raw = value.strip()
    if not raw:
        raise RuntimeError(f"{label} is missing")

    manifest = urllib.parse.urlsplit(manifest_url)
    resolved = urllib.parse.urlsplit(urllib.parse.urljoin(manifest_url, raw))
    if (
        resolved.username is not None
        or resolved.password is not None
        or resolved.fragment
        or not resolved.hostname
    ):
        raise RuntimeError(f"{label} must not contain credentials or a fragment")
    if resolved.scheme.lower() == "https":
        pass
    elif resolved.scheme.lower() == "http" and resolved.hostname in {"127.0.0.1", "localhost"}:
        pass
    else:
        raise RuntimeError(f"{label} must resolve to HTTPS or loopback HTTP")

    if normalized_origin(resolved) != normalized_origin(manifest):
        raise RuntimeError(f"{label} must stay on the release-manifest origin")
    return urllib.parse.urlunsplit(resolved)


def sidecar_url_for(payload_url: str) -> str:
    parsed = urllib.parse.urlsplit(payload_url)
    if parsed.path.endswith("/payload"):
        sidecar_path = parsed.path[: -len("/payload")] + "/metadata"
    else:
        sidecar_path = parsed.path + ".json"
    return urllib.parse.urlunsplit(
        (parsed.scheme, parsed.netloc, sidecar_path, parsed.query, parsed.fragment)
    )


def sha256_is_hex(value: str) -> bool:
    return len(value) == 64 and all(character in "0123456789abcdefABCDEF" for character in value)


def parse_positive_int(value: Any) -> int | None:
    try:
        parsed = int(value)
    except (TypeError, ValueError):
        return None
    return parsed if parsed > 0 else None


def normalize_sha256(value: Any) -> str:
    return str(value or "").strip().lower().removeprefix("sha256:")


def artifact_id(row: dict[str, Any]) -> str:
    return str(row.get("artifactId") or row.get("id") or "").strip()


def release_identity(payload: dict[str, Any]) -> tuple[str, str]:
    return (
        str(payload.get("version") or payload.get("releaseVersion") or "").strip(),
        str(payload.get("channel") or payload.get("channelId") or "").strip().lower(),
    )


def material_binding(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "artifactId": artifact_id(row),
        "head": str(row.get("head") or "").strip().lower(),
        "platform": str(row.get("platform") or row.get("platformId") or "").strip().lower(),
        "rid": str(row.get("rid") or "").strip().lower(),
        "kind": str(row.get("kind") or row.get("format") or "").strip().lower(),
        "fileName": normalize_file_name(row),
        "sha256": normalize_sha256(row.get("sha256")),
        "sizeBytes": parse_positive_int(row.get("sizeBytes")),
        "installerMode": str(row.get("installerMode") or "").strip().lower(),
        "payloadFileName": str(row.get("payloadFileName") or "").strip(),
        "payloadSha256": normalize_sha256(row.get("payloadSha256")),
        "payloadSizeBytes": parse_positive_int(row.get("payloadSizeBytes")),
    }


def windows_bootstrap_rows_by_id(payload: dict[str, Any], label: str) -> dict[str, dict[str, Any]]:
    rows_by_id: dict[str, dict[str, Any]] = {}
    for row in iter_manifest_downloads(payload):
        if not is_windows_bootstrap_installer(row):
            continue
        identifier = artifact_id(row)
        if not identifier:
            raise RuntimeError(f"{label} Windows bootstrap artifacts must have artifactId values")
        prior = rows_by_id.get(identifier)
        if prior is not None and material_binding(prior) != material_binding(row):
            raise RuntimeError(
                f"{label} Windows bootstrap artifact {identifier} has inconsistent duplicate rows"
            )
        rows_by_id[identifier] = row
    return rows_by_id


def bind_expected_release(
    live_manifest: dict[str, Any],
    expected_manifest: dict[str, Any],
) -> dict[str, dict[str, Any]]:
    live_version, live_channel = release_identity(live_manifest)
    expected_version, expected_channel = release_identity(expected_manifest)
    if not expected_version:
        raise RuntimeError("expected release manifest is missing version")
    if live_version != expected_version:
        raise RuntimeError(
            f"live release version mismatch: expected {expected_version!r}, observed {live_version or '<missing>'!r}"
        )
    if expected_channel and live_channel != expected_channel:
        raise RuntimeError(
            f"live release channel mismatch: expected {expected_channel!r}, observed {live_channel or '<missing>'!r}"
        )

    expected_by_id = windows_bootstrap_rows_by_id(expected_manifest, "expected")
    live_by_id = windows_bootstrap_rows_by_id(live_manifest, "live")
    if set(live_by_id) != set(expected_by_id):
        missing = sorted(set(expected_by_id) - set(live_by_id))
        unexpected = sorted(set(live_by_id) - set(expected_by_id))
        raise RuntimeError(
            "live Windows bootstrap artifact set mismatch: "
            f"missing={missing or []}, unexpected={unexpected or []}"
        )

    for expected_id, expected_row in expected_by_id.items():
        expected_binding = material_binding(expected_row)
        live_binding = material_binding(live_by_id[expected_id])
        mismatches = [
            key for key, expected_value in expected_binding.items()
            if live_binding.get(key) != expected_value
        ]
        if mismatches:
            raise RuntimeError(
                f"live Windows bootstrap artifact {expected_id} changed staged material binding: "
                + ", ".join(mismatches)
            )
    return live_by_id


def expected_launch_executable(payload_file_name: str) -> str:
    lowered = payload_file_name.lower()
    if lowered.startswith("chummer-blazor-desktop-"):
        return "Chummer.Blazor.Desktop.exe"
    return "Chummer.Avalonia.exe"


def download_file(url: str, destination: Path, timeout: int, expected_size: int | None) -> tuple[int, str]:
    hasher = hashlib.sha256()
    total = 0
    first_chunk = b""
    request = urllib.request.Request(url, headers=HTTP_HEADERS)
    with urllib.request.urlopen(request, timeout=timeout) as response:
        status = getattr(response, "status", None) or response.getcode()
        if status != 200:
            raise RuntimeError(f"{url} returned HTTP {status}")
        with destination.open("wb") as handle:
            while True:
                chunk = response.read(1024 * 1024)
                if not chunk:
                    break
                if not first_chunk:
                    first_chunk = chunk[:64]
                total += len(chunk)
                if expected_size is not None and total > expected_size:
                    raise RuntimeError(
                        f"{url} exceeded expected size {expected_size} bytes while downloading"
                    )
                hasher.update(chunk)
                handle.write(chunk)
    prefix = first_chunk.lstrip().lower()
    if prefix.startswith(b"<!doctype html") or prefix.startswith(b"<html"):
        raise RuntimeError(f"{url} returned an HTML document instead of a payload zip")
    return total, hasher.hexdigest().lower()


def verify_zip_payload(path: Path, expected_launch: str) -> None:
    try:
        with zipfile.ZipFile(path, "r") as archive:
            names = [Path(info.filename).name.lower() for info in archive.infolist() if not info.is_dir()]
            if expected_launch.lower() not in names:
                raise RuntimeError(f"payload zip is missing launch executable: {expected_launch}")
            bad_member = archive.testzip()
            if bad_member:
                raise RuntimeError(f"payload zip member failed CRC check: {bad_member}")
    except zipfile.BadZipFile as exc:
        raise RuntimeError(f"payload is not a readable zip: {exc}") from exc


def verify_sidecar(
    *,
    sidecar_url: str,
    installer_file_name: str,
    payload_file_name: str,
    payload_download_url: str,
    payload_sha256: str,
    payload_size_bytes: int,
    release_version: str,
    timeout: int,
) -> None:
    payload = load_json_url(sidecar_url, timeout)
    expected = {
        "contractName": "chummer6-ui.windows_bootstrap_payload",
        "fileName": payload_file_name,
        "sha256": payload_sha256,
        "installerFileName": installer_file_name,
        "releaseVersion": release_version,
    }
    for key, expected_value in expected.items():
        observed = str(payload.get(key) or "").strip()
        if observed != expected_value:
            raise RuntimeError(
                f"payload sidecar {sidecar_url} field {key} mismatch: "
                f"expected {expected_value!r}, observed {observed!r}"
            )
    observed_download_url = resolve_public_url(
        str(payload.get("downloadUrl") or ""),
        sidecar_url,
        f"payload sidecar {sidecar_url} downloadUrl",
    )
    if observed_download_url != payload_download_url:
        raise RuntimeError(
            f"payload sidecar {sidecar_url} field downloadUrl mismatch: "
            f"expected {payload_download_url!r}, observed {observed_download_url!r}"
        )
    observed_size = parse_positive_int(payload.get("sizeBytes"))
    if observed_size != payload_size_bytes:
        raise RuntimeError(
            f"payload sidecar {sidecar_url} sizeBytes mismatch: "
            f"expected {payload_size_bytes}, observed {observed_size}"
        )


def verify_row(row: dict[str, Any], timeout: int, manifest_url: str, release_version: str) -> None:
    installer_file_name = normalize_file_name(row)
    installer_download_value = str(row.get("downloadUrl") or row.get("url") or "").strip()
    installer_download_url = resolve_public_url(
        installer_download_value,
        manifest_url,
        f"{installer_file_name or '<unknown>'}: installer download URL",
    )
    installer_sha256 = normalize_sha256(row.get("sha256"))
    installer_size_bytes = parse_positive_int(row.get("sizeBytes"))
    payload_file_name = str(row.get("payloadFileName") or "").strip()
    payload_download_value = str(row.get("payloadDownloadUrl") or "").strip()
    payload_download_url = resolve_public_url(
        payload_download_value,
        manifest_url,
        f"{installer_file_name}: payloadDownloadUrl",
    )
    payload_sha256 = normalize_sha256(row.get("payloadSha256"))
    payload_size_bytes = parse_positive_int(row.get("payloadSizeBytes"))

    if not sha256_is_hex(installer_sha256):
        raise RuntimeError(f"{installer_file_name}: sha256 must be a 64-character hex digest")
    if installer_size_bytes is None:
        raise RuntimeError(f"{installer_file_name}: sizeBytes must be greater than zero")

    expected_payload_file_name = installer_file_name[: -len("-installer.exe")] + "-payload.zip"
    if payload_file_name != expected_payload_file_name:
        raise RuntimeError(
            f"{installer_file_name}: payloadFileName must be {expected_payload_file_name}, got {payload_file_name or '<missing>'}"
        )
    payload_url_file_name = Path(urllib.parse.urlsplit(payload_download_url).path).name
    if payload_url_file_name != payload_file_name and payload_url_file_name != "payload":
        raise RuntimeError(f"{installer_file_name}: payloadDownloadUrl file name does not match payloadFileName")
    if not sha256_is_hex(payload_sha256):
        raise RuntimeError(f"{installer_file_name}: payloadSha256 must be a 64-character hex digest")
    if payload_size_bytes is None:
        raise RuntimeError(f"{installer_file_name}: payloadSizeBytes must be greater than zero")

    sidecar_url = sidecar_url_for(payload_download_url)
    verify_sidecar(
        sidecar_url=sidecar_url,
        installer_file_name=installer_file_name,
        payload_file_name=payload_file_name,
        payload_download_url=payload_download_url,
        payload_sha256=payload_sha256,
        payload_size_bytes=payload_size_bytes,
        release_version=release_version,
        timeout=timeout,
    )

    with tempfile.TemporaryDirectory(prefix="chummer-live-payload-") as temp_dir:
        installer_path = Path(temp_dir) / installer_file_name
        actual_installer_size, actual_installer_sha256 = download_file(
            installer_download_url,
            installer_path,
            timeout,
            installer_size_bytes,
        )
        if actual_installer_size != installer_size_bytes:
            raise RuntimeError(
                f"{installer_file_name}: live installer size mismatch for {installer_download_url}: "
                f"expected {installer_size_bytes}, actual {actual_installer_size}"
            )
        if actual_installer_sha256 != installer_sha256:
            raise RuntimeError(
                f"{installer_file_name}: live installer sha256 mismatch for {installer_download_url}: "
                f"expected {installer_sha256}, actual {actual_installer_sha256}"
            )

        payload_path = Path(temp_dir) / payload_file_name
        actual_size, actual_sha256 = download_file(
            payload_download_url,
            payload_path,
            timeout,
            payload_size_bytes,
        )
        if actual_size != payload_size_bytes:
            raise RuntimeError(
                f"{installer_file_name}: live payload size mismatch for {payload_download_url}: "
                f"expected {payload_size_bytes}, actual {actual_size}"
            )
        if actual_sha256 != payload_sha256:
            raise RuntimeError(
                f"{installer_file_name}: live payload sha256 mismatch for {payload_download_url}: "
                f"expected {payload_sha256}, actual {actual_sha256}"
            )
        verify_zip_payload(payload_path, expected_launch_executable(payload_file_name))


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description="Verify live Windows bootstrap payload URLs return the exact ZIP bytes advertised in the release manifest."
    )
    parser.add_argument("--manifest-url", required=True, help="Public RELEASE_CHANNEL.generated.json or releases.json URL.")
    parser.add_argument(
        "--expected-manifest",
        type=Path,
        help="Staged manifest whose version, channel, and Windows material bindings must match live truth.",
    )
    parser.add_argument("--timeout", type=int, default=60, help="Per-request timeout in seconds.")
    parser.add_argument("--allow-empty", action="store_true", help="Pass when the manifest exposes no Windows bootstrap installers.")
    args = parser.parse_args(argv)

    try:
        manifest = load_json_url(args.manifest_url, args.timeout)
        rows_by_id = windows_bootstrap_rows_by_id(manifest, "live")
        if args.expected_manifest is not None:
            expected_manifest = load_json_file(args.expected_manifest)
            bind_expected_release(manifest, expected_manifest)
    except Exception as exc:  # noqa: BLE001 - emit one bounded verifier failure.
        print(f"live_windows_bootstrap_payloads:fail {exc}", file=sys.stderr)
        return 1
    rows = list(rows_by_id.values())
    if not rows:
        if args.allow_empty:
            print("live_windows_bootstrap_payloads:ok no_windows_bootstrap_installers")
            return 0
        print("live_windows_bootstrap_payloads:fail no Windows bootstrap installers found", file=sys.stderr)
        return 1

    failures: list[str] = []
    release_version, _ = release_identity(manifest)
    for row in rows:
        try:
            verify_row(row, args.timeout, args.manifest_url, release_version)
        except Exception as exc:  # noqa: BLE001 - every row failure should be reported.
            failures.append(str(exc))

    if failures:
        print("live_windows_bootstrap_payloads:fail", file=sys.stderr)
        for failure in failures:
            print(f" - {failure}", file=sys.stderr)
        return 1

    print(f"live_windows_bootstrap_payloads:ok checked={len(rows)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
