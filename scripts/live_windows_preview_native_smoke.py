#!/usr/bin/env python3
"""Prepare and verify native startup evidence for exact live Windows preview bytes."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any


LIVE_ORIGIN = "https://chummer.run"
LIVE_MANIFEST_URL = f"{LIVE_ORIGIN}/downloads/RELEASE_CHANNEL.generated.json"
INSTALLER_FILE_NAME = "chummer-avalonia-win-x64-installer.exe"
INSTALLER_ID = "avalonia-win-x64-installer"
MAX_MANIFEST_BYTES = 1024 * 1024
MAX_INSTALLER_BYTES = 64 * 1024 * 1024
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
INSTALLER_PATH_RE = re.compile(
    r"^/downloads/(?:files|g/[A-Za-z0-9._-]{1,128}/files)/"
    + re.escape(INSTALLER_FILE_NAME)
    + r"$"
)


class EvidenceError(RuntimeError):
    """The live bytes or native receipt do not satisfy the preview contract."""


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # type: ignore[no-untyped-def]
        return None


def fail(message: str) -> None:
    raise EvidenceError(message)


def sha256_bytes(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            fail(f"duplicate JSON key {key!r}")
        result[key] = value
    return result


def strict_json_object(raw: bytes, *, source: str) -> dict[str, Any]:
    if not raw or raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
        fail(f"{source} is not canonical UTF-8 JSON")
    try:
        loaded = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=lambda value: fail(
                f"{source} contains non-finite number {value}"
            ),
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{source} is invalid JSON: {exc}")
    if not isinstance(loaded, dict):
        fail(f"{source} must be a JSON object")
    return loaded


def fetch_exact(url: str, *, max_bytes: int) -> bytes:
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "application/octet-stream, application/json",
            "Accept-Encoding": "identity",
            "User-Agent": "chummer6-ui-live-windows-native-smoke/1",
        },
        method="GET",
    )
    opener = urllib.request.build_opener(NoRedirectHandler())
    try:
        with opener.open(request, timeout=60) as response:
            if response.status != 200 or response.geturl() != url:
                fail("live source returned a redirect or non-200 response")
            encoding = response.headers.get("Content-Encoding")
            if encoding not in {None, "identity"}:
                fail("live source returned encoded bytes")
            content_length = response.headers.get("Content-Length")
            if content_length is not None:
                try:
                    declared_size = int(content_length)
                except ValueError:
                    fail("live source content length is invalid")
                if declared_size < 1 or declared_size > max_bytes:
                    fail("live source content length is outside the fixed bound")
            raw = response.read(max_bytes + 1)
    except urllib.error.HTTPError as exc:
        fail(f"live source returned HTTP {exc.code}")
    except urllib.error.URLError as exc:
        fail(f"live source could not be fetched: {exc.reason}")
    if not raw or len(raw) > max_bytes:
        fail("live source bytes are outside the fixed bound")
    return raw


def exact_installer_url(raw_url: Any) -> str:
    if not isinstance(raw_url, str) or raw_url != raw_url.strip():
        fail("Windows installer downloadUrl must be one exact string")
    raw_parsed = urllib.parse.urlsplit(raw_url)
    if (
        "%" in raw_url
        or "\\" in raw_url
        or any(character.isspace() for character in raw_url)
        or any(segment in {"", ".", ".."} for segment in raw_parsed.path.split("/")[1:])
    ):
        fail("Windows installer downloadUrl is outside the exact live same-origin route")
    resolved = urllib.parse.urljoin(f"{LIVE_ORIGIN}/", raw_url)
    parsed = urllib.parse.urlsplit(resolved)
    if (
        parsed.scheme != "https"
        or parsed.netloc != "chummer.run"
        or parsed.username is not None
        or parsed.password is not None
        or parsed.port not in {None, 443}
        or parsed.query
        or parsed.fragment
        or INSTALLER_PATH_RE.fullmatch(parsed.path) is None
    ):
        fail("Windows installer downloadUrl is outside the exact live same-origin route")
    return resolved


def validate_expected_inputs(
    *,
    version: str,
    manifest_sha256: str,
    installer_sha256: str,
    installer_size_bytes: int,
) -> None:
    if VERSION_RE.fullmatch(version) is None or ".." in version:
        fail("release version is invalid")
    for value, label in (
        (manifest_sha256, "manifest SHA-256"),
        (installer_sha256, "installer SHA-256"),
    ):
        if SHA256_RE.fullmatch(value) is None:
            fail(f"{label} is invalid")
    if not 256 <= installer_size_bytes <= MAX_INSTALLER_BYTES:
        fail("installer size is outside the fixed bound")


def prepare(
    *,
    version: str,
    manifest_sha256: str,
    installer_sha256: str,
    installer_size_bytes: int,
    output: Path,
) -> dict[str, Any]:
    validate_expected_inputs(
        version=version,
        manifest_sha256=manifest_sha256,
        installer_sha256=installer_sha256,
        installer_size_bytes=installer_size_bytes,
    )
    if output.exists() or output.is_symlink():
        fail("installer output must not already exist")

    manifest_raw = fetch_exact(LIVE_MANIFEST_URL, max_bytes=MAX_MANIFEST_BYTES)
    if sha256_bytes(manifest_raw) != manifest_sha256:
        fail("live manifest SHA-256 differs from reviewed input")
    manifest = strict_json_object(manifest_raw, source="live release manifest")
    if (
        manifest.get("status") != "published"
        or manifest.get("channelId") != "preview"
        or manifest.get("version") != version
    ):
        fail("live release identity differs from reviewed preview input")
    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        fail("live release artifacts must be an array")
    matches = [
        row
        for row in artifacts
        if isinstance(row, dict)
        and (row.get("artifactId") or row.get("id")) == INSTALLER_ID
        and row.get("fileName") == INSTALLER_FILE_NAME
        and row.get("head") == "avalonia"
        and row.get("platform") == "windows"
        and row.get("rid") == "win-x64"
        and row.get("kind") == "installer"
    ]
    if len(matches) != 1:
        fail("live release must contain one exact Windows installer row")
    row = matches[0]
    if (
        row.get("sha256") != installer_sha256
        or row.get("sizeBytes") != installer_size_bytes
        or row.get("version") != version
        or row.get("releaseVersion") != version
    ):
        fail("live Windows installer row differs from reviewed inputs")

    installer_url = exact_installer_url(row.get("downloadUrl"))
    installer_raw = fetch_exact(installer_url, max_bytes=installer_size_bytes)
    if len(installer_raw) != installer_size_bytes:
        fail("live Windows installer size differs from reviewed input")
    if sha256_bytes(installer_raw) != installer_sha256:
        fail("live Windows installer SHA-256 differs from reviewed input")
    if installer_raw[:2] != b"MZ":
        fail("live Windows installer lacks the PE MZ signature")
    pe_offset = int.from_bytes(installer_raw[60:64], "little")
    if pe_offset < 64 or installer_raw[pe_offset : pe_offset + 4] != b"PE\x00\x00":
        fail("live Windows installer lacks the PE signature")

    output.parent.mkdir(parents=True, exist_ok=True)
    descriptor = os.open(
        output,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_CLOEXEC", 0),
        0o600,
    )
    try:
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            descriptor = -1
            handle.write(installer_raw)
            handle.flush()
            os.fsync(handle.fileno())
    finally:
        if descriptor >= 0:
            os.close(descriptor)

    return {
        "artifactId": INSTALLER_ID,
        "downloadUrl": installer_url,
        "fileName": INSTALLER_FILE_NAME,
        "manifestSha256": manifest_sha256,
        "releaseVersion": version,
        "sha256": installer_sha256,
        "sizeBytes": installer_size_bytes,
        "status": "prepared",
    }


def verify_receipt(
    *,
    receipt: Path,
    version: str,
    installer_sha256: str,
) -> dict[str, Any]:
    validate_expected_inputs(
        version=version,
        manifest_sha256="0" * 64,
        installer_sha256=installer_sha256,
        installer_size_bytes=256,
    )
    loaded = strict_json_object(receipt.read_bytes(), source="native startup receipt")
    evidence = loaded.get("nativeHostEvidence")
    if not isinstance(evidence, dict):
        fail("native startup receipt lacks nativeHostEvidence")
    runner = str(evidence.get("runner") or "").strip()
    if (
        loaded.get("status") != "pass"
        or loaded.get("readyCheckpoint") != "pre_ui_event_loop"
        or loaded.get("headId") != "avalonia"
        or loaded.get("platform") != "windows"
        or loaded.get("rid") != "win-x64"
        or loaded.get("arch") != "x64"
        or loaded.get("channelId") != "preview"
        or loaded.get("releaseVersion") != version
        or loaded.get("artifactId") != INSTALLER_ID
        or loaded.get("artifactFileName") != INSTALLER_FILE_NAME
        or loaded.get("artifactDigest") != f"sha256:{installer_sha256}"
        or loaded.get("executionEnvironment") != "native_windows"
        or loaded.get("verificationScope") != "native_windows_startup"
        or evidence.get("contractName")
        != "chummer6-ui.native_windows_host_evidence"
        or evidence.get("status") != "verified"
        or evidence.get("isNativeWindows") is not True
        or evidence.get("hostPlatform") != "windows"
        or not str(evidence.get("hostKernel") or "").strip()
        or not runner
        or "wine" in runner.lower()
        or not str(evidence.get("evidenceSource") or "").strip()
    ):
        fail("native startup receipt differs from the exact Windows preview contract")
    return {
        "artifactId": INSTALLER_ID,
        "receiptSha256": sha256_bytes(receipt.read_bytes()),
        "releaseVersion": version,
        "status": "verified",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    prepare_parser = subparsers.add_parser("prepare")
    prepare_parser.add_argument("--version", required=True)
    prepare_parser.add_argument("--manifest-sha256", required=True)
    prepare_parser.add_argument("--installer-sha256", required=True)
    prepare_parser.add_argument("--installer-size-bytes", required=True, type=int)
    prepare_parser.add_argument("--output", required=True, type=Path)

    verify_parser = subparsers.add_parser("verify-receipt")
    verify_parser.add_argument("--receipt", required=True, type=Path)
    verify_parser.add_argument("--version", required=True)
    verify_parser.add_argument("--installer-sha256", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "prepare":
        result = prepare(
            version=args.version,
            manifest_sha256=args.manifest_sha256,
            installer_sha256=args.installer_sha256,
            installer_size_bytes=args.installer_size_bytes,
            output=args.output,
        )
    else:
        result = verify_receipt(
            receipt=args.receipt,
            version=args.version,
            installer_sha256=args.installer_sha256,
        )
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceError, OSError) as exc:
        raise SystemExit(str(exc)) from exc
