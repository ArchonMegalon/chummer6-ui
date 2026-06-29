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


def is_allowed_payload_url(value: str) -> bool:
    parsed = urllib.parse.urlsplit(value)
    if parsed.scheme.lower() == "https" and bool(parsed.netloc):
        return True
    if parsed.scheme.lower() == "http" and parsed.hostname in {"127.0.0.1", "localhost"}:
        return True
    return False


def sidecar_url_for(payload_url: str) -> str:
    parsed = urllib.parse.urlsplit(payload_url)
    return urllib.parse.urlunsplit(
        (parsed.scheme, parsed.netloc, parsed.path + ".json", parsed.query, parsed.fragment)
    )


def sha256_is_hex(value: str) -> bool:
    return len(value) == 64 and all(character in "0123456789abcdefABCDEF" for character in value)


def parse_positive_int(value: Any) -> int | None:
    try:
        parsed = int(value)
    except (TypeError, ValueError):
        return None
    return parsed if parsed > 0 else None


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
    timeout: int,
) -> None:
    payload = load_json_url(sidecar_url, timeout)
    expected = {
        "contractName": "chummer6-ui.windows_bootstrap_payload",
        "fileName": payload_file_name,
        "downloadUrl": payload_download_url,
        "sha256": payload_sha256,
        "installerFileName": installer_file_name,
    }
    for key, expected_value in expected.items():
        observed = str(payload.get(key) or "").strip()
        if observed != expected_value:
            raise RuntimeError(
                f"payload sidecar {sidecar_url} field {key} mismatch: "
                f"expected {expected_value!r}, observed {observed!r}"
            )
    observed_size = parse_positive_int(payload.get("sizeBytes"))
    if observed_size != payload_size_bytes:
        raise RuntimeError(
            f"payload sidecar {sidecar_url} sizeBytes mismatch: "
            f"expected {payload_size_bytes}, observed {observed_size}"
        )


def verify_row(row: dict[str, Any], timeout: int) -> None:
    installer_file_name = normalize_file_name(row)
    payload_file_name = str(row.get("payloadFileName") or "").strip()
    payload_download_url = str(row.get("payloadDownloadUrl") or "").strip()
    payload_sha256 = str(row.get("payloadSha256") or "").strip().lower()
    payload_size_bytes = parse_positive_int(row.get("payloadSizeBytes"))

    expected_payload_file_name = installer_file_name[: -len("-installer.exe")] + "-payload.zip"
    if payload_file_name != expected_payload_file_name:
        raise RuntimeError(
            f"{installer_file_name}: payloadFileName must be {expected_payload_file_name}, got {payload_file_name or '<missing>'}"
        )
    if not is_allowed_payload_url(payload_download_url):
        raise RuntimeError(
            f"{installer_file_name}: payloadDownloadUrl must be absolute HTTPS "
            "or loopback HTTP for local tests"
        )
    if Path(urllib.parse.urlsplit(payload_download_url).path).name != payload_file_name:
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
        timeout=timeout,
    )

    with tempfile.TemporaryDirectory(prefix="chummer-live-payload-") as temp_dir:
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
    parser.add_argument("--timeout", type=int, default=60, help="Per-request timeout in seconds.")
    parser.add_argument("--allow-empty", action="store_true", help="Pass when the manifest exposes no Windows bootstrap installers.")
    args = parser.parse_args(argv)

    manifest = load_json_url(args.manifest_url, args.timeout)
    rows = [row for row in iter_manifest_downloads(manifest) if is_windows_bootstrap_installer(row)]
    if not rows:
        if args.allow_empty:
            print("live_windows_bootstrap_payloads:ok no_windows_bootstrap_installers")
            return 0
        print("live_windows_bootstrap_payloads:fail no Windows bootstrap installers found", file=sys.stderr)
        return 1

    failures: list[str] = []
    for row in rows:
        try:
            verify_row(row, args.timeout)
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
