#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import re
import stat
import struct
import sys
import zipfile
import zlib
from dataclasses import dataclass
from io import BytesIO
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


APPENDED_PAYLOAD_MAGIC = b"CHUMMER6PAYLOAD1"
FOOTER_LENGTH = len(APPENDED_PAYLOAD_MAGIC) + 8
DEFAULT_MAX_BOOTSTRAP_INSTALLER_BYTES = 15 * 1024 * 1024
BOOTSTRAP_METADATA_MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"

# These are hard ceilings, not tuning defaults. Environment overrides may only
# lower them so a release job cannot make the payload inspection unbounded.
BOOTSTRAP_ZIP_POLICY_VERSION = "chummer6.windows-bootstrap-zip-admission.v1"
DEFAULT_MAX_PAYLOAD_ZIP_ARCHIVE_BYTES = 256 * 1024 * 1024
DEFAULT_MAX_PAYLOAD_ZIP_ENTRIES = 2048
DEFAULT_MAX_PAYLOAD_ZIP_ENTRY_BYTES = 128 * 1024 * 1024
DEFAULT_MAX_PAYLOAD_ZIP_TOTAL_BYTES = 512 * 1024 * 1024
DEFAULT_MAX_PAYLOAD_ZIP_COMPRESSION_RATIO = 100.0
DEFAULT_MAX_PAYLOAD_ZIP_CENTRAL_DIRECTORY_BYTES = 16 * 1024 * 1024
MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES = 16 * 1024 * 1024
MAX_PAYLOAD_ZIP_ENTRY_NAME_BYTES = 1024
MAX_PAYLOAD_ZIP_PATH_SEGMENT_BYTES = 255
PAYLOAD_INSPECTION_PREFIX_BYTES = 4096
PAYLOAD_BINARY_SCAN_TAIL_BYTES = 4096
ZIP_END_OF_CENTRAL_DIRECTORY_SIGNATURE = b"PK\x05\x06"
ZIP_END_OF_CENTRAL_DIRECTORY_LENGTH = 22
ZIP_MAX_COMMENT_BYTES = 65535
ZIP_LOCAL_FILE_HEADER_SIGNATURE = b"PK\x03\x04"
ZIP_LOCAL_FILE_HEADER_LENGTH = 30
ZIP_UTF8_FILENAME_FLAG = 0x800
ZIP_ENCRYPTED_FLAGS = 0x41
ZIP_ALLOWED_COMPRESSION = frozenset({zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED})
ZIP_WINDOWS_INVALID_SEGMENT_CHARACTERS = frozenset('<>:"\\|?*')
ZIP_WINDOWS_RESERVED_STEMS = frozenset(
    {
        "con",
        "prn",
        "aux",
        "nul",
        *(f"com{number}" for number in range(1, 10)),
        *(f"lpt{number}" for number in range(1, 10)),
    }
)

PRIVATE_KEY_CONTAINER_SUFFIXES = {
    ".jks",
    ".key",
    ".keystore",
    ".p12",
    ".pfx",
    ".pk8",
    ".pkcs12",
    ".ppk",
    ".snk",
}
PRIVATE_KEY_ID_NAME = re.compile(r"^id_(?:dsa|ecdsa|ed25519|rsa)$", re.IGNORECASE)
PRIVATE_KEY_NAME = re.compile(r"(?:^|[-_.])private[-_.]?key(?:[-_.]|$)", re.IGNORECASE)
SERVICE_ACCOUNT_NAME = re.compile(
    r"(?:^|[-_.])(?:google[-_.]?)?service[-_.]?account(?:[-_.]|$)",
    re.IGNORECASE,
)
CLIENT_CREDENTIAL_NAME = re.compile(
    r"(?:^|[-_.])(?:client[-_.]?secrets?|google[-_.]?credentials)(?:[-_.]|$)",
    re.IGNORECASE,
)
PRIVATE_KEY_MARKER = re.compile(
    rb"-----BEGIN(?:[ \t]+[A-Z0-9]+)*[ \t]+PRIVATE[ \t]+KEY(?:[ \t]+BLOCK)?-----",
    re.IGNORECASE,
)
BINARY_BEARER_TOKEN_ASSIGNMENT = re.compile(
    rb"authorization[\"']?[ \t]*[:=][ \t]*[\"']?bearer[ \t]+[^\x00-\x20\"']",
    re.IGNORECASE,
)
BINARY_CREDENTIAL_ASSIGNMENT = re.compile(
    rb"(?<![A-Za-z0-9])(?:bearer(?:[_-]?token)?|refresh[_-]?token|access[_-]?token|"
    rb"client[_-]?secret|private[_-]?key(?:[_-]?id)?)[\"']?[ \t]*[:=][ \t]*"
    rb"(?:[\"'][ \t]*[^\x00-\x20\"']|[^\x00-\x20\"'])",
    re.IGNORECASE,
)
BINARY_CONNECTION_ASSIGNMENT = re.compile(
    rb"(?<![A-Za-z0-9])(?:connection[_-]?strings?(?:(?:__|:)[A-Za-z0-9_.-]+)?|"
    rb"default[_-]?connection)[\"']?[ \t]*[:=][ \t]*"
    rb"(?:[\"'][ \t]*[^\x00-\x20\"']|[^\x00-\x20\"'])",
    re.IGNORECASE,
)
BINARY_SECRET_CONTENT_RULES = (
    ("content.private_key_marker", PRIVATE_KEY_MARKER),
    ("content.bearer_assignment", BINARY_BEARER_TOKEN_ASSIGNMENT),
    ("content.credential_assignment", BINARY_CREDENTIAL_ASSIGNMENT),
    ("content.connection_string_assignment", BINARY_CONNECTION_ASSIGNMENT),
)
BEARER_TOKEN_ASSIGNMENT = re.compile(
    r"(?i)\bauthorization\s*[\"']?\s*[:=]\s*[\"']?\s*bearer\s+"
    r"(?P<value>[A-Za-z0-9._~+/=-]{8,})"
)
SENSITIVE_TEXT_ASSIGNMENT = re.compile(
    r"(?im)(?:^|[\r\n{,;<])\s*[\"']?"
    r"(?P<key>bearer(?:[-_]?token)?|refresh[-_]?token|access[-_]?token|client[-_]?secret|"
    r"private[-_]?key(?:[-_]?id)?|"
    r"connection[-_]?strings?|database[-_]?url|default[-_]?connection)"
    r"[\"']?\s*[:=]\s*[\"']?(?P<value>[^\r\n]{1,4096})"
)
XML_CONNECTION_STRING_ASSIGNMENT = re.compile(
    r"(?i)\bconnectionString\s*=\s*[\"'](?P<value>[^\"']+)[\"']"
)
SENSITIVE_JSON_KEYS = {
    "accesstoken": "content.credential_assignment",
    "authorization": "content.bearer_assignment",
    "bearer": "content.bearer_assignment",
    "bearertoken": "content.bearer_assignment",
    "clientsecret": "content.credential_assignment",
    "connectionstring": "content.connection_string_assignment",
    "connectionstrings": "content.connection_string_assignment",
    "databaseurl": "content.connection_string_assignment",
    "defaultconnection": "content.connection_string_assignment",
    "privatekey": "content.credential_assignment",
    "privatekeyid": "content.credential_assignment",
    "refreshtoken": "content.credential_assignment",
}

DEFAULT_LAUNCH_EXECUTABLES = {
    "avalonia": "Chummer.Avalonia.exe",
    "blazor-desktop": "Chummer.Blazor.Desktop.exe",
}


@dataclass(frozen=True)
class ManifestRow:
    file_name: str
    download_url: str
    payload_file_name: str
    payload_download_url: str
    payload_sha256: str
    payload_size_bytes: int | None
    installer_mode: str
    payload_acquisition_mode: str


@dataclass(frozen=True)
class PayloadCandidate:
    mode: str
    source: str
    data: bytes


@dataclass(frozen=True)
class BootstrapInstallerMetadata:
    payload_download_url: str
    payload_sha256: str
    payload_size_bytes: int | None
    payload_acquisition_mode: str


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().lower()


def normalize_zip_name(value: str) -> str:
    return value.replace("\\", "/").lstrip("/")


def is_truthy(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"1", "true", "yes", "on"}


def is_windows_installer_name(name: str) -> bool:
    lowered = name.lower()
    return lowered.startswith("chummer-") and lowered.endswith("-win-x64-installer.exe") or (
        lowered.startswith("chummer-") and "-win-" in lowered and lowered.endswith("-installer.exe")
    )


def expected_payload_name(installer_name: str) -> str:
    lowered = installer_name.lower()
    if not lowered.endswith("-installer.exe"):
        return ""
    return installer_name[: -len("-installer.exe")] + "-payload.zip"


def infer_head_id(installer_name: str) -> str:
    lowered = installer_name.lower()
    if lowered.startswith("chummer-blazor-desktop-"):
        return "blazor-desktop"
    if lowered.startswith("chummer-avalonia-"):
        return "avalonia"
    return ""


def infer_launch_executables(installer_name: str) -> list[str]:
    head_id = infer_head_id(installer_name)
    if head_id in DEFAULT_LAUNCH_EXECUTABLES:
        return [DEFAULT_LAUNCH_EXECUTABLES[head_id]]
    return []


def read_manifest_rows(manifest_paths: list[Path]) -> dict[str, ManifestRow]:
    rows: dict[str, ManifestRow] = {}
    for manifest_path in manifest_paths:
        if not manifest_path.is_file():
            continue
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        if not isinstance(payload, dict):
            continue
        for collection_name in ("artifacts", "downloads"):
            collection = payload.get(collection_name)
            if not isinstance(collection, list):
                continue
            for item in collection:
                if not isinstance(item, dict):
                    continue
                file_name = resolve_file_name(item)
                if not file_name or not is_windows_installer_name(file_name):
                    continue
                rows[file_name] = ManifestRow(
                    file_name=file_name,
                    download_url=str(item.get("downloadUrl") or item.get("url") or "").strip(),
                    payload_file_name=str(item.get("payloadFileName") or "").strip(),
                    payload_download_url=str(item.get("payloadDownloadUrl") or "").strip(),
                    payload_sha256=str(item.get("payloadSha256") or "").strip().lower(),
                    payload_size_bytes=try_int(item.get("payloadSizeBytes")),
                    installer_mode=str(item.get("installerMode") or "").strip().lower(),
                    payload_acquisition_mode=str(item.get("payloadAcquisitionMode") or "").strip().lower(),
                )
    return rows


def resolve_file_name(item: dict[str, Any]) -> str:
    file_name = str(item.get("fileName") or "").strip()
    if file_name:
        return file_name
    raw_url = str(item.get("downloadUrl") or item.get("url") or "").strip()
    if not raw_url:
        return ""
    parsed = urlparse(raw_url)
    return Path(parsed.path or raw_url).name


def try_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def max_bootstrap_installer_bytes() -> int:
    configured = try_int(os.environ.get("CHUMMER_WINDOWS_BOOTSTRAP_MAX_INSTALLER_BYTES"))
    if configured is not None and configured > 0:
        return configured
    return DEFAULT_MAX_BOOTSTRAP_INSTALLER_BYTES


def bounded_positive_int(name: str, hard_maximum: int) -> int:
    configured = try_int(os.environ.get(name))
    if configured is None or configured <= 0:
        return hard_maximum
    return min(configured, hard_maximum)


def bounded_positive_float(name: str, hard_maximum: float) -> float:
    raw_value = str(os.environ.get(name) or "").strip()
    if not raw_value:
        return hard_maximum
    try:
        configured = float(raw_value)
    except ValueError:
        return hard_maximum
    if configured <= 0:
        return hard_maximum
    return min(configured, hard_maximum)


def max_payload_zip_archive_bytes() -> int:
    return bounded_positive_int(
        "CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_ARCHIVE_BYTES",
        DEFAULT_MAX_PAYLOAD_ZIP_ARCHIVE_BYTES,
    )


def max_payload_zip_entries() -> int:
    return bounded_positive_int(
        "CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_ENTRIES",
        DEFAULT_MAX_PAYLOAD_ZIP_ENTRIES,
    )


def max_payload_zip_entry_bytes() -> int:
    return bounded_positive_int(
        "CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_ENTRY_BYTES",
        DEFAULT_MAX_PAYLOAD_ZIP_ENTRY_BYTES,
    )


def max_payload_zip_total_bytes() -> int:
    return bounded_positive_int(
        "CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_TOTAL_BYTES",
        DEFAULT_MAX_PAYLOAD_ZIP_TOTAL_BYTES,
    )


def max_payload_zip_compression_ratio() -> float:
    return bounded_positive_float(
        "CHUMMER_WINDOWS_PAYLOAD_ZIP_MAX_COMPRESSION_RATIO",
        DEFAULT_MAX_PAYLOAD_ZIP_COMPRESSION_RATIO,
    )


def zip_end_of_central_directory_summary(data: bytes) -> tuple[int, int, int, int, int] | None:
    """Return disk/count/central-directory metadata without opening all entries."""

    if len(data) < ZIP_END_OF_CENTRAL_DIRECTORY_LENGTH:
        return None
    search_start = max(
        0,
        len(data) - ZIP_END_OF_CENTRAL_DIRECTORY_LENGTH - ZIP_MAX_COMMENT_BYTES,
    )
    search_end = len(data)
    while True:
        offset = data.rfind(
            ZIP_END_OF_CENTRAL_DIRECTORY_SIGNATURE,
            search_start,
            search_end,
        )
        if offset < 0:
            return None
        if offset + ZIP_END_OF_CENTRAL_DIRECTORY_LENGTH <= len(data):
            (
                signature,
                disk_number,
                central_directory_disk,
                entries_on_disk,
                total_entries,
                central_directory_size,
                _central_directory_offset,
                comment_length,
            ) = struct.unpack_from("<4s4H2LH", data, offset)
            if (
                signature == ZIP_END_OF_CENTRAL_DIRECTORY_SIGNATURE
                and offset + ZIP_END_OF_CENTRAL_DIRECTORY_LENGTH + comment_length
                == len(data)
            ):
                return (
                    disk_number,
                    central_directory_disk,
                    entries_on_disk,
                    total_entries,
                    central_directory_size,
                )
        search_end = offset


def zip_local_header_consistency_rule(
    data: bytes,
    info: zipfile.ZipInfo,
    raw_name: str,
    central_directory_start: int,
) -> str | None:
    """Validate the bounded local header bound to one central-directory entry."""

    header_offset = info.header_offset
    if (
        not isinstance(header_offset, int)
        or header_offset < 0
        or header_offset + ZIP_LOCAL_FILE_HEADER_LENGTH > len(data)
        or header_offset + ZIP_LOCAL_FILE_HEADER_LENGTH > central_directory_start
    ):
        return "entry.local_header_bounds"

    try:
        (
            signature,
            _extract_version,
            local_flags,
            local_compression_method,
            _modified_time,
            _modified_date,
            _local_crc32,
            _local_compressed_size,
            _local_uncompressed_size,
            local_name_length,
            local_extra_length,
        ) = struct.unpack_from("<4s5H3L2H", data, header_offset)
    except struct.error:
        return "entry.local_header_bounds"

    if signature != ZIP_LOCAL_FILE_HEADER_SIGNATURE:
        return "entry.local_header_signature"

    local_name_start = header_offset + ZIP_LOCAL_FILE_HEADER_LENGTH
    local_name_end = local_name_start + local_name_length
    local_data_start = local_name_end + local_extra_length
    local_data_end = local_data_start + max(info.compress_size, 0)
    if (
        local_name_end > len(data)
        or local_data_start > len(data)
        or local_data_end > len(data)
        or local_name_end > central_directory_start
        or local_data_start > central_directory_start
        or local_data_end > central_directory_start
    ):
        return "entry.local_header_bounds"

    if local_flags & ZIP_ENCRYPTED_FLAGS:
        return "entry.encrypted"
    if local_flags != info.flag_bits:
        return "entry.local_flags"
    if local_compression_method != info.compress_type:
        return "entry.local_compression_method"

    local_name_bytes = data[local_name_start:local_name_end]
    local_name_encoding = (
        "utf-8" if local_flags & ZIP_UTF8_FILENAME_FLAG else "cp437"
    )
    try:
        local_name = local_name_bytes.decode(local_name_encoding)
    except UnicodeError:
        return "entry.local_filename"
    if local_name != raw_name:
        return "entry.local_filename"
    return None


def sensitive_zip_entry_name_rule(name: str) -> str | None:
    normalized = name.replace("\\", "/")
    for segment in normalized.rstrip("/").split("/"):
        lowered = segment.casefold()
        if lowered == ".env" or lowered.startswith(".env."):
            return "name.sensitive"
        if Path(lowered).suffix in PRIVATE_KEY_CONTAINER_SUFFIXES:
            return "name.sensitive"
        stem = Path(lowered).stem
        if PRIVATE_KEY_ID_NAME.fullmatch(stem):
            return "name.sensitive"
        if PRIVATE_KEY_NAME.search(lowered):
            return "name.sensitive"
        if SERVICE_ACCOUNT_NAME.search(lowered):
            return "name.sensitive"
        if CLIENT_CREDENTIAL_NAME.search(lowered) or lowered in {
            "application_default_credentials.json",
            "credentials.json",
        }:
            return "name.sensitive"
    return None


def normalized_secret_key(value: object) -> str:
    return re.sub(r"[^a-z0-9]", "", str(value).casefold())


def json_value_is_non_empty(value: object) -> bool:
    if value is None:
        return False
    if isinstance(value, str):
        return bool(value.strip())
    if isinstance(value, (dict, list)):
        return bool(value)
    return True


def json_secret_rule(value: object, parent_key: str = "") -> str | None:
    if isinstance(value, dict):
        normalized_items = {normalized_secret_key(key): item for key, item in value.items()}
        normalized_keys = set(normalized_items)
        service_account_type = str(normalized_items.get("type") or "").casefold()
        if (
            service_account_type == "service_account"
            and {"privatekey", "clientemail"}.issubset(normalized_keys)
        ) or {
            "privatekey",
            "privatekeyid",
            "clientemail",
            "tokenuri",
        }.issubset(normalized_keys):
            return "content.google_service_account_json"

        for key, item in value.items():
            normalized_key = normalized_secret_key(key)
            if (
                normalized_key.startswith("connectionstrings")
                and json_value_is_non_empty(item)
            ):
                return "content.connection_string_assignment"
            rule = SENSITIVE_JSON_KEYS.get(normalized_key)
            if rule is not None and json_value_is_non_empty(item):
                return rule
            nested_rule = json_secret_rule(item, str(key))
            if nested_rule is not None:
                return nested_rule
        return None
    if isinstance(value, list):
        for item in value:
            nested_rule = json_secret_rule(item, parent_key)
            if nested_rule is not None:
                return nested_rule
    return None


def decode_payload_text(data: bytes) -> str:
    if data.startswith((b"\xff\xfe", b"\xfe\xff")):
        try:
            return data.decode("utf-16")
        except UnicodeDecodeError:
            return ""
    return data.decode("utf-8-sig", errors="ignore")


def has_known_binary_magic(data: bytes) -> bool:
    return data.startswith((b"MZ", b"\x7fELF", b"\x89PNG", b"\xff\xd8\xff", b"%PDF"))


def looks_like_payload_text(data: bytes) -> bool:
    sample = data[: 64 * 1024]
    if not sample:
        return True
    if has_known_binary_magic(sample):
        return False
    try:
        decoded = sample.decode("utf-8-sig")
    except UnicodeError:
        decoded = ""
    if not decoded and len(sample) % 2 == 0:
        for encoding in ("utf-16", "utf-16-le", "utf-16-be"):
            try:
                candidate = sample.decode(encoding)
            except UnicodeError:
                continue
            if candidate:
                decoded = candidate
                break
    if not decoded:
        return False
    acceptable = sum(
        character.isprintable() or character in "\r\n\t"
        for character in decoded
    )
    return acceptable / len(decoded) >= 0.95


def binary_secret_content_rule(data: bytes) -> str | None:
    for rule, pattern in BINARY_SECRET_CONTENT_RULES:
        if pattern.search(data) is not None:
            return rule
    return None


def secret_content_rule(data: bytes) -> str | None:
    text = decode_payload_text(data)
    stripped = text.lstrip()
    parsed_json = False
    if stripped.startswith(("{", "[")):
        try:
            json_payload = json.loads(stripped)
        except (json.JSONDecodeError, RecursionError):
            json_payload = None
        if json_payload is not None:
            parsed_json = True
            rule = json_secret_rule(json_payload)
            if rule is not None:
                return rule

    if PRIVATE_KEY_MARKER.search(data) is not None:
        return "content.private_key_marker"

    if BEARER_TOKEN_ASSIGNMENT.search(text) is not None:
        return "content.bearer_assignment"

    # Valid JSON is inspected structurally above. Running the line-oriented
    # fallback too would mistake a placeholder nested below ConnectionStrings
    # for a literal value assigned to the parent object.
    if parsed_json:
        return None

    if XML_CONNECTION_STRING_ASSIGNMENT.search(text) is not None:
        return "content.connection_string_assignment"

    for match in SENSITIVE_TEXT_ASSIGNMENT.finditer(text):
        key = match.group("key")
        normalized_key = normalized_secret_key(key)
        return SENSITIVE_JSON_KEYS.get(
            normalized_key,
            "content.connection_string_assignment",
        )
    return None


def unsafe_zip_entry_path_rule(raw_name: str) -> str | None:
    if not raw_name:
        return "path.non_empty"
    if "\\" in raw_name:
        return "path.forward_slash"
    if any(ord(character) < 0x20 or ord(character) > 0x7E for character in raw_name):
        return "path.ascii_printable"
    encoded_name = raw_name.encode("ascii")
    if len(encoded_name) > MAX_PAYLOAD_ZIP_ENTRY_NAME_BYTES:
        return "path.length"
    if raw_name.startswith("/") or re.match(r"^[A-Za-z]:", raw_name):
        return "path.relative"
    path_without_directory_marker = raw_name[:-1] if raw_name.endswith("/") else raw_name
    parts = path_without_directory_marker.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        return "path.relative"
    for part in parts:
        if len(part.encode("utf-8")) > MAX_PAYLOAD_ZIP_PATH_SEGMENT_BYTES:
            return "path.segment_length"
        if (
            any(character in ZIP_WINDOWS_INVALID_SEGMENT_CHARACTERS for character in part)
            or part.endswith((".", " "))
        ):
            return "path.windows_invalid_segment"
        reserved_stem = part.split(".", 1)[0].casefold()
        if reserved_stem in ZIP_WINDOWS_RESERVED_STEMS:
            return "path.windows_reserved_device"
    return None


def zip_entry_diagnostic_reference(ordinal: int, raw_name: str) -> str:
    """Identify an untrusted ZIP name without rendering any of its characters."""

    digest = hashlib.sha256(
        raw_name.encode("utf-8", errors="surrogatepass")
    ).hexdigest()
    return f"entry_ordinal={ordinal} entry_name_sha256={digest}"


def zip_entry_failure(ordinal: int, raw_name: str, rule: str, detail: str = "") -> str:
    suffix = f" {detail}" if detail else ""
    return (
        f"payload zip violates policy={BOOTSTRAP_ZIP_POLICY_VERSION} rule={rule} "
        f"{zip_entry_diagnostic_reference(ordinal, raw_name)}{suffix}"
    )


def is_sha256_hex(value: str) -> bool:
    return len(value) == 64 and all(character in "0123456789abcdefABCDEF" for character in value)


def url_file_name(value: str) -> str:
    parsed = urlparse(value)
    return Path(parsed.path).name if parsed.path else ""


def is_absolute_https_url(value: str) -> bool:
    parsed = urlparse(value)
    return parsed.scheme.lower() == "https" and bool(parsed.netloc)


def is_absolute_payload_url(value: str) -> bool:
    parsed = urlparse(value)
    if parsed.scheme.lower() in {"http", "https", "file"} and parsed.scheme:
        return bool(parsed.netloc) or parsed.scheme.lower() == "file"
    return False


def same_origin(left: str, right: str) -> bool:
    left_uri = urlparse(left)
    right_uri = urlparse(right)
    return (
        left_uri.scheme.lower(),
        left_uri.netloc.lower(),
    ) == (
        right_uri.scheme.lower(),
        right_uri.netloc.lower(),
    )


def find_installers(files_dir: Path | None, explicit_installers: list[Path]) -> list[Path]:
    installers: list[Path] = [path.resolve() for path in explicit_installers]
    if files_dir is not None and files_dir.is_dir():
        installers.extend(
            sorted(path.resolve() for path in files_dir.glob("chummer-*-win-*-installer.exe"))
        )
    seen: set[Path] = set()
    unique: list[Path] = []
    for installer in installers:
        if installer in seen:
            continue
        seen.add(installer)
        unique.append(installer)
    return unique


def read_appended_payload(installer_path: Path) -> PayloadCandidate | None:
    file_size = installer_path.stat().st_size
    if file_size < FOOTER_LENGTH:
        return None

    with installer_path.open("rb") as handle:
        handle.seek(file_size - FOOTER_LENGTH)
        footer = handle.read(FOOTER_LENGTH)
        payload_length = struct.unpack("<q", footer[:8])[0]
        magic = footer[8:]
        if magic != APPENDED_PAYLOAD_MAGIC:
            return None
        payload_offset = file_size - FOOTER_LENGTH - payload_length
        if payload_length <= 0 or payload_offset < 0:
            raise ValueError(f"{installer_path.name}: appended payload footer is invalid")
        archive_bytes_limit = max_payload_zip_archive_bytes()
        if payload_length > archive_bytes_limit:
            raise ValueError(
                f"{installer_path.name}: appended payload zip violates "
                "resource-limit:archive-bytes "
                f"({payload_length} > {archive_bytes_limit})"
            )
        handle.seek(payload_offset)
        data = handle.read(payload_length)
        if len(data) != payload_length:
            raise ValueError(f"{installer_path.name}: appended payload is truncated")
    return PayloadCandidate("bundled", "appended-footer", data)


def read_sidecar_payload(
    installer_path: Path,
    files_dir: Path | None,
    explicit_payload: Path | None,
    manifest_row: ManifestRow | None,
) -> PayloadCandidate | None:
    candidates: list[Path] = []
    if explicit_payload is not None:
        candidates.append(explicit_payload)
    if manifest_row is not None and manifest_row.payload_file_name:
        if files_dir is not None:
            candidates.append(files_dir / manifest_row.payload_file_name)
        candidates.append(installer_path.parent / manifest_row.payload_file_name)
    payload_name = expected_payload_name(installer_path.name)
    if payload_name:
        if files_dir is not None:
            candidates.append(files_dir / payload_name)
        candidates.append(installer_path.parent / payload_name)

    seen: set[Path] = set()
    for candidate in candidates:
        candidate = candidate.resolve()
        if candidate in seen:
            continue
        seen.add(candidate)
        if candidate.is_file():
            payload_size = candidate.stat().st_size
            archive_bytes_limit = max_payload_zip_archive_bytes()
            if payload_size > archive_bytes_limit:
                raise ValueError(
                    f"payload sidecar {candidate.name} violates "
                    "resource-limit:archive-bytes "
                    f"({payload_size} > {archive_bytes_limit})"
                )
            return PayloadCandidate("bootstrap", str(candidate), candidate.read_bytes())
    return None


def extract_bootstrap_installer_metadata(installer_bytes: bytes) -> BootstrapInstallerMetadata | None:
    marker_offset = installer_bytes.rfind(BOOTSTRAP_METADATA_MARKER)
    if marker_offset < 0:
        return None

    metadata_bytes = installer_bytes[marker_offset + len(BOOTSTRAP_METADATA_MARKER) :]
    metadata_text = metadata_bytes.decode("utf-8", errors="ignore")
    values: dict[str, str] = {}
    for raw_line in metadata_text.splitlines():
        line = raw_line.strip()
        if not line or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip()

    return BootstrapInstallerMetadata(
        payload_download_url=values.get("payloadDownloadUrl", ""),
        payload_sha256=values.get("payloadSha256", "").lower(),
        payload_size_bytes=try_int(values.get("payloadSizeBytes")),
        payload_acquisition_mode=values.get("payloadAcquisitionMode", "").lower(),
    )


def validate_manifest_payload_metadata(candidate: PayloadCandidate, manifest_row: ManifestRow | None) -> list[str]:
    if manifest_row is None:
        return []
    failures: list[str] = []
    if manifest_row.installer_mode == "bootstrap":
        expected_name = expected_payload_name(manifest_row.file_name)
        if not manifest_row.payload_file_name:
            failures.append("manifest says installerMode=bootstrap but payloadFileName is missing")
        elif expected_name and manifest_row.payload_file_name != expected_name:
            failures.append(
                f"manifest payloadFileName {manifest_row.payload_file_name} does not match expected {expected_name}"
            )
        if not manifest_row.payload_download_url:
            failures.append("manifest says installerMode=bootstrap but payloadDownloadUrl is missing")
        elif not is_absolute_https_url(manifest_row.payload_download_url):
            failures.append("manifest payloadDownloadUrl must be an absolute HTTPS URL")
        elif manifest_row.payload_file_name and url_file_name(manifest_row.payload_download_url) != manifest_row.payload_file_name:
            failures.append("manifest payloadDownloadUrl file name must match payloadFileName")
        if manifest_row.download_url and is_absolute_https_url(manifest_row.download_url) and is_absolute_https_url(manifest_row.payload_download_url):
            if not same_origin(manifest_row.download_url, manifest_row.payload_download_url):
                failures.append("manifest payloadDownloadUrl must use the same origin as the installer downloadUrl")
        if not manifest_row.payload_sha256 or not is_sha256_hex(manifest_row.payload_sha256):
            failures.append("manifest bootstrap payloadSha256 must be a 64-character hex digest")
        if manifest_row.payload_size_bytes is None or manifest_row.payload_size_bytes <= 0:
            failures.append("manifest bootstrap payloadSizeBytes must be greater than zero")
        if manifest_row.payload_acquisition_mode not in {"", "download", "embedded"}:
            failures.append("manifest bootstrap payloadAcquisitionMode must be download or embedded")
    if manifest_row.installer_mode == "bootstrap" and candidate.mode != "bootstrap":
        failures.append("manifest says installerMode=bootstrap but the payload was not a sidecar payload")
    if manifest_row.installer_mode == "bundled" and candidate.mode != "bundled":
        failures.append("manifest says installerMode=bundled but the payload was not appended")
    if candidate.mode == "bootstrap":
        source_name = Path(candidate.source).name
        if manifest_row.payload_file_name and manifest_row.payload_file_name != source_name:
            failures.append(
                f"manifest payloadFileName {manifest_row.payload_file_name} does not match sidecar {source_name}"
            )
        if manifest_row.payload_sha256 and manifest_row.payload_sha256 != sha256_bytes(candidate.data):
            failures.append("manifest payloadSha256 does not match sidecar bytes")
        if manifest_row.payload_size_bytes is not None and manifest_row.payload_size_bytes != len(candidate.data):
            failures.append(
                f"manifest payloadSizeBytes {manifest_row.payload_size_bytes} does not match sidecar size {len(candidate.data)}"
            )
    return failures


def validate_bootstrap_installer_shape(installer_path: Path, candidate: PayloadCandidate) -> list[str]:
    if candidate.mode != "bootstrap":
        return []

    installer_size = installer_path.stat().st_size
    installer_metadata = extract_bootstrap_installer_metadata(installer_path.read_bytes())
    if installer_metadata is not None and installer_metadata.payload_acquisition_mode == "embedded":
        return []
    max_size = max_bootstrap_installer_bytes()
    if installer_size > max_size:
        return [
            "bootstrap installer is too large: "
            f"{installer_size} bytes exceeds the {max_size} byte limit"
        ]
    return []


def validate_bootstrap_installer_metadata(installer_path: Path, candidate: PayloadCandidate, manifest_row: ManifestRow | None) -> list[str]:
    if candidate.mode != "bootstrap":
        return []

    expected_payload_download_url = manifest_row.payload_download_url if manifest_row is not None else ""
    expected_payload_sha256 = manifest_row.payload_sha256 if manifest_row is not None else ""
    expected_payload_size_bytes = manifest_row.payload_size_bytes if manifest_row is not None else None
    expected_payload_acquisition_mode = manifest_row.payload_acquisition_mode if manifest_row is not None else ""

    if (
        not expected_payload_download_url
        or not expected_payload_sha256
        or expected_payload_size_bytes is None
    ):
        sidecar_path = Path(candidate.source + ".json")
        if sidecar_path.is_file():
            try:
                sidecar = json.loads(sidecar_path.read_text(encoding="utf-8-sig"))
            except json.JSONDecodeError:
                sidecar = {}
            if isinstance(sidecar, dict):
                expected_payload_download_url = expected_payload_download_url or str(sidecar.get("downloadUrl") or "").strip()
                expected_payload_sha256 = expected_payload_sha256 or str(sidecar.get("sha256") or "").strip().lower()
                expected_payload_size_bytes = (
                    expected_payload_size_bytes
                    if expected_payload_size_bytes is not None
                    else try_int(sidecar.get("sizeBytes"))
                )
                expected_payload_acquisition_mode = expected_payload_acquisition_mode or str(
                    sidecar.get("payloadAcquisitionMode") or ""
                ).strip().lower()

    installer_metadata = extract_bootstrap_installer_metadata(installer_path.read_bytes())
    failures: list[str] = []
    if installer_metadata is None:
        return ["bootstrap installer does not contain embedded payloadDownloadUrl metadata"]

    if not installer_metadata.payload_download_url:
        failures.append("bootstrap installer does not contain embedded payloadDownloadUrl metadata")
    elif not is_absolute_payload_url(installer_metadata.payload_download_url):
        failures.append("bootstrap installer embedded payloadDownloadUrl must be an absolute file, http, or https URL")
    elif Path(urlparse(installer_metadata.payload_download_url).path).name != Path(candidate.source).name:
        failures.append("bootstrap installer embedded payloadDownloadUrl file name must match the payload sidecar")

    if not installer_metadata.payload_sha256:
        failures.append("bootstrap installer does not contain embedded payloadSha256 metadata")
    elif not is_sha256_hex(installer_metadata.payload_sha256):
        failures.append("bootstrap installer embedded payloadSha256 must be a 64-character hex digest")

    if installer_metadata.payload_size_bytes is None:
        failures.append("bootstrap installer does not contain embedded payloadSizeBytes metadata")
    elif installer_metadata.payload_size_bytes <= 0:
        failures.append("bootstrap installer embedded payloadSizeBytes must be greater than zero")

    if expected_payload_download_url and installer_metadata.payload_download_url != expected_payload_download_url:
        failures.append("bootstrap installer embedded payloadDownloadUrl does not match manifest/sidecar metadata")
    if expected_payload_sha256 and installer_metadata.payload_sha256 != expected_payload_sha256:
        failures.append("bootstrap installer embedded payloadSha256 does not match manifest/sidecar metadata")
    if (
        expected_payload_size_bytes is not None
        and installer_metadata.payload_size_bytes != expected_payload_size_bytes
    ):
        failures.append("bootstrap installer embedded payloadSizeBytes does not match manifest/sidecar metadata")
    if installer_metadata.payload_acquisition_mode not in {"", "download", "embedded"}:
        failures.append("bootstrap installer embedded payloadAcquisitionMode must be download or embedded")
    if (
        expected_payload_acquisition_mode
        and installer_metadata.payload_acquisition_mode != expected_payload_acquisition_mode
    ):
        failures.append("bootstrap installer embedded payloadAcquisitionMode does not match manifest/sidecar metadata")
    if expected_payload_acquisition_mode == "embedded" and installer_metadata.payload_acquisition_mode != "embedded":
        failures.append("bootstrap installer does not contain payloadAcquisitionMode=embedded")

    return failures


def validate_bootstrap_sidecar_metadata(
    installer_path: Path,
    candidate: PayloadCandidate,
    manifest_row: ManifestRow | None,
) -> list[str]:
    if candidate.mode != "bootstrap":
        return []

    sidecar_path = Path(candidate.source + ".json")
    if not sidecar_path.is_file():
        return [f"bootstrap payload sidecar metadata is missing: {sidecar_path.name}"]

    try:
        payload = json.loads(sidecar_path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        return [f"bootstrap payload sidecar metadata is invalid JSON: {sidecar_path.name}: {exc}"]

    if not isinstance(payload, dict):
        return [f"bootstrap payload sidecar metadata is not a JSON object: {sidecar_path.name}"]

    failures: list[str] = []
    expected_file_name = Path(candidate.source).name
    if str(payload.get("contractName") or "").strip() != "chummer6-ui.windows_bootstrap_payload":
        failures.append("bootstrap payload sidecar metadata has unexpected contractName")
    if str(payload.get("fileName") or "").strip() != expected_file_name:
        failures.append(
            f"bootstrap payload sidecar metadata fileName does not match payload: expected {expected_file_name}"
        )
    if str(payload.get("installerFileName") or "").strip() != installer_path.name:
        failures.append(
            f"bootstrap payload sidecar metadata installerFileName does not match installer: expected {installer_path.name}"
        )
    sidecar_acquisition_mode = str(payload.get("payloadAcquisitionMode") or "").strip().lower()
    if sidecar_acquisition_mode not in {"", "download", "embedded"}:
        failures.append("bootstrap payload sidecar metadata payloadAcquisitionMode must be download or embedded")
    if (
        manifest_row is not None
        and manifest_row.payload_acquisition_mode
        and sidecar_acquisition_mode != manifest_row.payload_acquisition_mode
    ):
        failures.append("bootstrap payload sidecar metadata payloadAcquisitionMode does not match manifest")
    download_url = str(payload.get("downloadUrl") or "").strip()
    if not download_url:
        failures.append("bootstrap payload sidecar metadata downloadUrl is missing")
    elif not is_absolute_https_url(download_url):
        failures.append("bootstrap payload sidecar metadata downloadUrl must be an absolute HTTPS URL")
    elif url_file_name(download_url) != expected_file_name:
        failures.append("bootstrap payload sidecar metadata downloadUrl file name must match payload fileName")

    observed_sha256 = sha256_bytes(candidate.data)
    if str(payload.get("sha256") or "").strip().lower() != observed_sha256:
        failures.append("bootstrap payload sidecar metadata sha256 does not match payload bytes")

    observed_size = len(candidate.data)
    try:
        metadata_size = int(payload.get("sizeBytes"))
    except (TypeError, ValueError):
        metadata_size = None
    if metadata_size != observed_size:
        failures.append(
            f"bootstrap payload sidecar metadata sizeBytes does not match payload size {observed_size}"
        )

    if manifest_row is not None:
        if manifest_row.payload_file_name and manifest_row.payload_file_name != str(payload.get("fileName") or "").strip():
            failures.append("bootstrap payload sidecar metadata fileName does not match manifest payloadFileName")
        if manifest_row.payload_download_url and manifest_row.payload_download_url != str(payload.get("downloadUrl") or "").strip():
            failures.append("bootstrap payload sidecar metadata downloadUrl does not match manifest payloadDownloadUrl")
        if manifest_row.payload_sha256 and manifest_row.payload_sha256 != str(payload.get("sha256") or "").strip().lower():
            failures.append("bootstrap payload sidecar metadata sha256 does not match manifest payloadSha256")
        if manifest_row.payload_size_bytes is not None and manifest_row.payload_size_bytes != metadata_size:
            failures.append("bootstrap payload sidecar metadata sizeBytes does not match manifest payloadSizeBytes")

    return failures


def parse_heads_json_base64(value: str) -> list[str]:
    if not value.strip():
        return []
    decoded = base64.b64decode(value)
    payload = json.loads(decoded.decode("utf-8"))
    if not isinstance(payload, list):
        return []
    entries: list[str] = []
    for item in payload:
        if not isinstance(item, dict):
            continue
        launch = str(item.get("launchExecutable") or "").strip()
        root = str(item.get("relativeRoot") or "").strip().strip("/\\")
        if not launch:
            continue
        entries.append(normalize_zip_name(f"{root}/{launch}" if root else launch))
    return entries


def validate_zip_payload(
    installer_name: str,
    candidate: PayloadCandidate,
    expected_launches: list[str],
    expected_entries: list[str],
    require_sample: bool,
) -> list[str]:
    failures: list[str] = []
    archive_bytes_limit = max_payload_zip_archive_bytes()
    entry_count_limit = max_payload_zip_entries()
    entry_bytes_limit = max_payload_zip_entry_bytes()
    total_bytes_limit = max_payload_zip_total_bytes()
    compression_ratio_limit = max_payload_zip_compression_ratio()

    if len(candidate.data) > archive_bytes_limit:
        return [
            "payload zip violates resource-limit:archive-bytes "
            f"({len(candidate.data)} > {archive_bytes_limit})"
        ]

    eocd_summary = zip_end_of_central_directory_summary(candidate.data)
    if eocd_summary is None:
        return ["payload is not a readable zip (zip-structure:end-of-central-directory)"]
    (
        disk_number,
        central_directory_disk,
        entries_on_disk,
        declared_entry_count,
        central_directory_size,
    ) = eocd_summary
    if disk_number != 0 or central_directory_disk != 0 or entries_on_disk != declared_entry_count:
        return ["payload is not a readable zip (zip-structure:multi-disk)"]
    if declared_entry_count == 0xFFFF:
        return ["payload zip violates resource-limit:entry-count (ZIP64 count is not accepted)"]
    if declared_entry_count > entry_count_limit:
        return [
            "payload zip violates resource-limit:entry-count "
            f"({declared_entry_count} > {entry_count_limit})"
        ]
    if central_directory_size > DEFAULT_MAX_PAYLOAD_ZIP_CENTRAL_DIRECTORY_BYTES:
        return [
            "payload zip violates resource-limit:central-directory-bytes "
            f"({central_directory_size} > {DEFAULT_MAX_PAYLOAD_ZIP_CENTRAL_DIRECTORY_BYTES})"
        ]

    try:
        with zipfile.ZipFile(BytesIO(candidate.data), "r") as archive:
            infos = archive.infolist()
            if len(infos) != declared_entry_count:
                return ["payload is not a readable zip (zip-structure:entry-count-mismatch)"]
            if len(infos) > entry_count_limit:
                return [
                    "payload zip violates resource-limit:entry-count "
                    f"({len(infos)} > {entry_count_limit})"
                ]

            names: list[str] = []
            file_infos: list[tuple[zipfile.ZipInfo, int, str]] = []
            seen_names: set[str] = set()
            seen_portable_names: set[str] = set()
            declared_total_bytes = 0
            declared_total_compressed_bytes = 0
            central_directory_start = archive.start_dir

            for ordinal, info in enumerate(infos, start=1):
                raw_name = getattr(info, "orig_filename", info.filename)
                path_rule = unsafe_zip_entry_path_rule(raw_name)
                if path_rule is not None:
                    failures.append(zip_entry_failure(ordinal, raw_name, path_rule))
                    continue

                local_header_rule = zip_local_header_consistency_rule(
                    candidate.data,
                    info,
                    raw_name,
                    central_directory_start,
                )
                if local_header_rule is not None:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        local_header_rule,
                    ))
                    continue

                collision_name = raw_name.rstrip("/")
                canonical_name = collision_name
                portable_name = collision_name.lower()
                if collision_name in seen_names:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "path.duplicate",
                    ))
                elif portable_name in seen_portable_names:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "path.portable_collision",
                    ))
                seen_names.add(collision_name)
                seen_portable_names.add(portable_name)

                unix_mode = (info.external_attr >> 16) & 0xFFFF
                file_type = stat.S_IFMT(unix_mode)
                if file_type == stat.S_IFLNK:
                    failures.append(zip_entry_failure(ordinal, raw_name, "entry.symlink"))
                elif file_type not in (0, stat.S_IFREG, stat.S_IFDIR):
                    failures.append(zip_entry_failure(ordinal, raw_name, "entry.regular_type"))
                if info.flag_bits & ZIP_ENCRYPTED_FLAGS:
                    failures.append(zip_entry_failure(ordinal, raw_name, "entry.encrypted"))
                if info.compress_type not in ZIP_ALLOWED_COMPRESSION:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "entry.compression_method",
                    ))
                if info.file_size < 0 or info.compress_size < 0:
                    failures.append(zip_entry_failure(ordinal, raw_name, "entry.compressed_size"))
                if raw_name.endswith("/") != info.is_dir():
                    failures.append(zip_entry_failure(ordinal, raw_name, "entry.directory"))

                if info.is_dir():
                    continue

                names.append(canonical_name)
                file_infos.append((info, ordinal, raw_name))
                declared_total_bytes += max(info.file_size, 0)
                declared_total_compressed_bytes += max(info.compress_size, 0)
                if info.file_size > entry_bytes_limit:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "entry.decompressed_size",
                        f"({info.file_size} > {entry_bytes_limit})",
                    ))
                entry_ratio = (
                    float("inf")
                    if info.file_size > 0 and info.compress_size == 0
                    else info.file_size / max(info.compress_size, 1)
                )
                if entry_ratio > compression_ratio_limit:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "entry.compression_ratio",
                        f"({entry_ratio:.2f} > {compression_ratio_limit:.2f})",
                    ))
                sensitive_name_rule = sensitive_zip_entry_name_rule(canonical_name)
                if sensitive_name_rule is not None:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        sensitive_name_rule,
                    ))

            if not names:
                if failures:
                    return failures
                return ["payload zip contains no files"]
            if declared_total_bytes > total_bytes_limit:
                failures.append(
                    "payload zip violates resource-limit:total-bytes "
                    f"({declared_total_bytes} > {total_bytes_limit})"
                )
            aggregate_ratio = (
                float("inf")
                if declared_total_bytes > 0 and declared_total_compressed_bytes == 0
                else declared_total_bytes / max(declared_total_compressed_bytes, 1)
            )
            if aggregate_ratio > compression_ratio_limit:
                failures.append(
                    "payload zip violates resource-limit:aggregate-compression-ratio "
                    f"({aggregate_ratio:.2f} > {compression_ratio_limit:.2f})"
                )

            name_set = set(names)
            basename_set = {Path(name).name.lower() for name in names}
            for expected_entry in expected_entries:
                normalized = normalize_zip_name(expected_entry)
                if normalized not in name_set:
                    failures.append(f"payload zip is missing expected entry: {normalized}")
            launches = expected_launches or infer_launch_executables(installer_name)
            for launch in launches:
                if Path(launch).name.lower() not in basename_set:
                    failures.append(f"payload zip is missing launch executable: {Path(launch).name}")
            if require_sample and "soma-career.chum5" not in basename_set:
                failures.append("payload zip is missing bundled sample character: Soma-Career.chum5")

            # Do not decompress anything after a structural/resource/name failure.
            if failures:
                return failures

            observed_total_bytes = 0
            for info, ordinal, raw_name in file_infos:
                collect_entry = (
                    info.file_size <= MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES
                )
                entry_data = bytearray() if collect_entry else None
                entry_prefix = bytearray()
                binary_scan_tail = b""
                streamed_sensitive_rule: str | None = None
                observed_entry_bytes = 0
                try:
                    with archive.open(info, "r") as entry_stream:
                        while True:
                            chunk = entry_stream.read(128 * 1024)
                            if not chunk:
                                break
                            observed_entry_bytes += len(chunk)
                            observed_total_bytes += len(chunk)
                            if len(entry_prefix) < PAYLOAD_INSPECTION_PREFIX_BYTES:
                                entry_prefix.extend(
                                    chunk[
                                        : PAYLOAD_INSPECTION_PREFIX_BYTES
                                        - len(entry_prefix)
                                    ]
                                )
                            if entry_data is not None:
                                if (
                                    len(entry_data) + len(chunk)
                                    <= MAX_PAYLOAD_ZIP_INSPECTABLE_CONTENT_BYTES
                                ):
                                    entry_data.extend(chunk)
                                else:
                                    entry_data = None

                            scan_window = binary_scan_tail + chunk
                            binary_rule = binary_secret_content_rule(scan_window)
                            if binary_rule is not None:
                                if binary_rule == "content.private_key_marker":
                                    failures.append(zip_entry_failure(
                                        ordinal,
                                        raw_name,
                                        binary_rule,
                                    ))
                                    break
                                if streamed_sensitive_rule is None:
                                    streamed_sensitive_rule = binary_rule
                            binary_scan_tail = scan_window[-PAYLOAD_BINARY_SCAN_TAIL_BYTES:]

                            if observed_entry_bytes > entry_bytes_limit:
                                failures.append(zip_entry_failure(
                                    ordinal,
                                    raw_name,
                                    "entry.decompressed_size",
                                ))
                                break
                            if observed_total_bytes > total_bytes_limit:
                                failures.append(
                                    "payload zip violates resource-limit:total-bytes-observed"
                                )
                                break
                except (
                    EOFError,
                    NotImplementedError,
                    RuntimeError,
                    ValueError,
                    zipfile.BadZipFile,
                    zlib.error,
                ):
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "entry.integrity",
                    ))
                    continue

                if failures:
                    break
                if observed_entry_bytes != info.file_size:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        "entry.declared_size",
                    ))
                    break
                if entry_data is None:
                    prefix_bytes = bytes(entry_prefix)
                    if prefix_bytes.startswith(b"\xef\xbb\xbf"):
                        prefix_bytes = prefix_bytes[3:]
                    if prefix_bytes.lstrip().startswith((b"{", b"[")):
                        failures.append(zip_entry_failure(
                            ordinal,
                            raw_name,
                            "content.json_inspection_size",
                        ))
                    elif streamed_sensitive_rule is not None:
                        failures.append(zip_entry_failure(
                            ordinal,
                            raw_name,
                            streamed_sensitive_rule,
                        ))
                    elif not has_known_binary_magic(prefix_bytes):
                        failures.append(zip_entry_failure(
                            ordinal,
                            raw_name,
                            "content.text_inspection_size",
                        ))
                    continue

                entry_bytes = bytes(entry_data)
                prefix_bytes = bytes(entry_prefix)
                if prefix_bytes.startswith(b"\xef\xbb\xbf"):
                    prefix_bytes = prefix_bytes[3:]
                if prefix_bytes.lstrip().startswith((b"{", b"[")):
                    try:
                        json_payload = json.loads(entry_bytes.decode("utf-8-sig"))
                    except (UnicodeError, json.JSONDecodeError, RecursionError):
                        json_payload = None
                    if json_payload is not None:
                        content_rule = json_secret_rule(json_payload)
                        if content_rule is not None:
                            diagnostic_rule = (
                                content_rule
                                if content_rule == "content.google_service_account_json"
                                else streamed_sensitive_rule or content_rule
                            )
                            failures.append(zip_entry_failure(
                                ordinal,
                                raw_name,
                                diagnostic_rule,
                            ))
                        continue

                if streamed_sensitive_rule is not None:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        streamed_sensitive_rule,
                    ))
                    continue

                content_rule = secret_content_rule(entry_bytes)
                if content_rule is not None:
                    failures.append(zip_entry_failure(
                        ordinal,
                        raw_name,
                        content_rule,
                    ))
    except (
        EOFError,
        NotImplementedError,
        RuntimeError,
        ValueError,
        zipfile.BadZipFile,
        zlib.error,
    ):
        failures.append("payload is not a readable zip (zip-structure:invalid)")
    return failures


def verify_installer(
    installer_path: Path,
    files_dir: Path | None,
    explicit_payload: Path | None,
    manifest_row: ManifestRow | None,
    expected_launches: list[str],
    expected_entries: list[str],
    require_sample: bool,
    require_embedded_bootstrap_metadata: bool,
    require_manifest_row: bool,
) -> list[str]:
    failures: list[str] = []
    if not installer_path.is_file():
        return [f"installer does not exist: {installer_path}"]
    if installer_path.stat().st_size <= FOOTER_LENGTH:
        return [f"installer is too small to contain a payload-aware executable: {installer_path}"]
    if require_manifest_row and manifest_row is None:
        return [f"{installer_path.name}: Windows installer is missing from the supplied release manifest"]

    try:
        candidate = read_appended_payload(installer_path)
        if candidate is None:
            candidate = read_sidecar_payload(installer_path, files_dir, explicit_payload, manifest_row)
    except (OSError, ValueError) as exc:
        return [f"{installer_path.name}: {exc}"]

    if candidate is None:
        payload_name = expected_payload_name(installer_path.name) or "<unknown>"
        return [
            f"{installer_path.name}: no appended payload and no bootstrap sidecar '{payload_name}' was found"
        ]

    failures.extend(validate_manifest_payload_metadata(candidate, manifest_row))
    failures.extend(validate_bootstrap_sidecar_metadata(installer_path, candidate, manifest_row))
    failures.extend(validate_bootstrap_installer_shape(installer_path, candidate))
    if require_embedded_bootstrap_metadata:
        failures.extend(validate_bootstrap_installer_metadata(installer_path, candidate, manifest_row))
    failures.extend(
        validate_zip_payload(
            installer_path.name,
            candidate,
            expected_launches,
            expected_entries,
            require_sample,
        )
    )
    return [f"{installer_path.name}: {failure}" for failure in failures]


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Fail if a Windows Chummer installer cannot reach its bundled/bootstrap payload."
    )
    parser.add_argument("--files-dir", type=Path, help="Bundle files directory containing installers and payload sidecars.")
    parser.add_argument("--manifest", type=Path, action="append", default=[], help="Release manifest to cross-check payload metadata.")
    parser.add_argument("--installer", type=Path, action="append", default=[], help="Specific installer .exe to check.")
    parser.add_argument("--payload", type=Path, help="Specific payload zip to use for an explicit installer check.")
    parser.add_argument("--expected-launch", action="append", default=[], help="Launch executable basename expected in the payload zip.")
    parser.add_argument("--expected-entry", action="append", default=[], help="Exact zip entry expected in the payload zip.")
    parser.add_argument("--heads-json-base64", default="", help="Installer heads JSON metadata used to derive exact payload entries.")
    parser.add_argument("--require-sample", action="store_true", help="Require the legacy Soma sample character in the payload.")
    parser.add_argument(
        "--require-embedded-bootstrap-metadata",
        action="store_true",
        help="Require bootstrap installers to contain the manifest payload URL, SHA-256, and size metadata.",
    )
    parser.add_argument(
        "--require-manifest-row",
        action="store_true",
        help="Require every checked Windows installer to have a matching row in one supplied release manifest.",
    )
    parser.add_argument("--allow-empty", action="store_true", help="Pass when no Windows installers are present.")
    args = parser.parse_args()

    files_dir = args.files_dir.resolve() if args.files_dir else None
    manifest_rows = read_manifest_rows([path.resolve() for path in args.manifest])
    installers = find_installers(files_dir, args.installer)
    if not installers:
        if args.allow_empty:
            print("windows_installer_payload_gate:ok no_windows_installers")
            return 0
        print("windows_installer_payload_gate:fail no Windows installers found", file=sys.stderr)
        return 1

    expected_entries = [normalize_zip_name(entry) for entry in args.expected_entry]
    expected_entries.extend(parse_heads_json_base64(args.heads_json_base64))
    require_sample = args.require_sample or is_truthy(os.environ.get("CHUMMER_WINDOWS_INSTALLER_REQUIRE_SAMPLE_PAYLOAD"))
    require_embedded_bootstrap_metadata = (
        args.require_embedded_bootstrap_metadata
        or is_truthy(os.environ.get("CHUMMER_WINDOWS_INSTALLER_REQUIRE_EMBEDDED_BOOTSTRAP_METADATA"))
    )
    failures: list[str] = []
    for installer_path in installers:
        manifest_row = manifest_rows.get(installer_path.name)
        failures.extend(
            verify_installer(
                installer_path,
                files_dir,
                args.payload.resolve() if args.payload else None,
                manifest_row,
                [str(item).strip() for item in args.expected_launch if str(item).strip()],
                expected_entries,
                require_sample,
                require_embedded_bootstrap_metadata,
                args.require_manifest_row,
            )
        )

    if failures:
        print("windows_installer_payload_gate:fail", file=sys.stderr)
        for failure in failures:
            print(f" - {failure}", file=sys.stderr)
        return 1

    print(f"windows_installer_payload_gate:ok checked={len(installers)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
