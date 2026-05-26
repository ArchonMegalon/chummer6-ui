#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
release_channel_path="${CHUMMER_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
flagship_gate_path="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_PATH:-$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
receipt_path="${CHUMMER_NEXT90_M101_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json}"
authority_repo_root="${CHUMMER_NEXT90_M101_AUTHORITY_REPO_ROOT:-$repo_root}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$registry_path" "$queue_path" "$design_queue_path" "$release_channel_path" "$flagship_gate_path" "$receipt_path" "$repo_root" "$authority_repo_root" <<'PY'
from __future__ import annotations

import json
import base64
import binascii
import gzip
import html
import re
import subprocess
import sys
import urllib.parse
import zlib
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

(
    registry_path_text,
    queue_path_text,
    design_queue_path_text,
    release_channel_path_text,
    flagship_gate_path_text,
    receipt_path_text,
    repo_root_text,
    authority_repo_root_text,
) = sys.argv[1:]

registry_path = Path(registry_path_text)
queue_path = Path(queue_path_text)
design_queue_path = Path(design_queue_path_text)
release_channel_path = Path(release_channel_path_text)
flagship_gate_path = Path(flagship_gate_path_text)
receipt_path = Path(receipt_path_text)
repo_root = Path(repo_root_text)
authority_repo_root = Path(authority_repo_root_text)
verify_script_path = repo_root / "scripts" / "ai" / "verify.sh"
authority_repo_available = (authority_repo_root / ".git").exists()
authority_repo_has_dedicated_history = authority_repo_available and authority_repo_root != repo_root
git_history_root = authority_repo_root if authority_repo_has_dedicated_history else repo_root

PACKAGE_ID = "next90-m101-ui-release-train"
FRONTIER_ID = 2450443084
MILESTONE_ID = 101
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "desktop_release_train:avalonia",
    "flagship_route_truth:desktop",
]
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_SOURCE_REGISTRY_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml"
EXPECTED_PROGRAM_WAVE = "next_90_day_product_advance"
EXPECTED_QUEUE_STATUS = "live_parallel_successor"
EXPECTED_SOURCE_QUEUE_FINGERPRINT = "next90-staging-20260415-next-big-wins-widening"
EXPECTED_PACKAGE_TITLE = "Keep native-host release proof independent for the primary desktop head"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = (
    "M101 chummer6-ui is complete; future shards must verify this receipt, registry row, queue row, and design-queue row instead of reopening the Avalonia primary-route package."
)
REQUIRED_QUEUE_PROOF_ITEMS = [
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m101-ui-release-train-check.sh",
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M101ReleaseTrainGuardTests.cs",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/verify.sh now fail-closes design-owned queue proof drift against the Fleet-completed M101 row, so future shards cannot treat one queue copy as authoritative closure proof.",
    "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh",
    "source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true",
]
EXPECTED_QUEUE_PROOF_TOKENS = [
    *REQUIRED_QUEUE_PROOF_ITEMS,
    "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh",
    "source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true",
]
PRIMARY_HEAD = "avalonia"
FALLBACK_HEAD = "blazor-desktop"
REQUIRED_TUPLES = {
    "linux": {"rid": "linux-x64", "arch": "x64", "host": "linux"},
    "windows": {"rid": "win-x64", "arch": "x64", "host": "windows"},
    "macos": {"rid": "osx-arm64", "arch": "arm64", "host": "macos"},
}
EXPECTED_REQUIRED_PLATFORM_HEAD_RID_TUPLES = sorted(
    f"{PRIMARY_HEAD}:{row['rid']}:{platform}" for platform, row in REQUIRED_TUPLES.items()
)
EXPECTED_REQUIRED_DESKTOP_PLATFORMS = sorted(REQUIRED_TUPLES)
EXPECTED_REQUIRED_DESKTOP_HEADS = [PRIMARY_HEAD]
EXPECTED_PRIMARY_ROLLBACK_STATE_BY_FALLBACK_POSTURE = {
    True: "fallback_available",
    False: "primary_reinstall_available",
}
EXPECTED_PRIMARY_ROLLBACK_REASON_CODE_BY_FALLBACK_POSTURE = {
    True: "promoted_fallback_available",
    False: "primary_installer_reinstall_available",
}
FALLBACK_PROOF_TEXT_TOKENS = [
    FALLBACK_HEAD,
    "blazor",
    "blazor-desktop",
]
PRIMARY_ROUTE_TRUTH_PROOF_FIELDS = [
    "artifactId",
    "installPostureReason",
    "promotionReason",
    "publicInstallRoute",
    "routeRoleReason",
    "tupleId",
    "updateEligibilityReason",
]
ALLOWED_DESKTOP_ROUTE_TRUTH_KEYS = sorted(
    {
        "arch",
        "artifactId",
        "head",
        "installPosture",
        "installPostureReason",
        "parityPosture",
        "platform",
        "promotionReason",
        "promotionReasonCode",
        "promotionState",
        "publicInstallRoute",
        "revokeReason",
        "revokeReasonCode",
        "revokeSource",
        "revokeState",
        "rid",
        "rollbackReason",
        "rollbackReasonCode",
        "rollbackState",
        "routeRole",
        "routeRoleReason",
        "routeRoleReasonCode",
        "tupleId",
        "updateEligibility",
        "updateEligibilityReason",
    }
)
PRIMARY_ROUTE_TRUTH_FALLBACK_DISTINCT_FIELDS = [
    "artifactId",
    "publicInstallRoute",
    "routeRoleReason",
    "rollbackReason",
    "tupleId",
    "updateEligibilityReason",
]
LANDED_COMMIT = "c9c0d84f"
EXPECTED_CURRENT_PACKAGE_PROOF_FLOOR_COMMIT = "362686fb"
EXPECTED_RESOLVING_PROOF_COMMITS = [
    LANDED_COMMIT,
    "da549ef8",
    "5844ad03",
    "2e87dce3",
    "c61a8fb5",
    "79760cc1",
    "a3bf058e",
    "0954e2a1",
    "e519ca4b",
    "7e0c8d07",
    "54766b3a",
    "f3e0e90b",
    "a8944fa5",
    "b481d3ef",
    "52b118ff",
    "24eb3732",
    "48970414",
    "2ef1a22d",
    "8bc1fb02",
    "9629b207",
    "6c032e2c",
    "5c069924",
    "8115735b",
    "0605657d",
    "53b701e2",
    "007182bc",
    "a0303d5f",
    "0fa3ce01",
    "b0c0b732",
    "3f99eb0a",
    "b21ca671",
    "0849d8c2",
    "e64db32c",
    "bb268a79",
    "7945695d",
    "9e3d931a",
    "492e8f83",
    "31cb7cf7",
    "5a282824",
    "8e8d97a4",
    "bd340416",
    "faba38da",
    "237e039d",
    "49a5466c",
    "90c0a763",
    "60092e8d",
    "5403219b",
    "871c7f7b",
    "8b0e1801",
    "eae55383",
    "287c7538",
    "fa67f014",
    "c63379a3",
    "44ac83db",
    "0c239ada",
    "52086c9d",
    "82df294e",
    "bb90dca8",
    "20487c22",
    "bc01c725",
    "8ac6d072",
    "1c7b5819",
    "aa394d32",
    "8db934d3",
    "cb1fe210",
    "75b38965",
    "56d9733a",
    "db4fc1e1",
    "4a4079f5",
    "9b97ab1a",
    "f563293f",
    "c9f49b5b",
    "f11cff77",
    "22380dee",
    "93f7dcea",
    "de600a43",
    "6dd1064f",
    "355b497e",
    "b99e13fd",
    "28533e61",
    "82334376",
    "466e0fc0",
    "b8dcab2d",
    "757783c4",
    "0b8414d7",
    "b958e116",
    "46a9f070",
    "46eb74e2",
    "ccc77950",
    "4f103b72",
    "deff0535",
    "2e8f29b7",
    "342bff22",
    "0e894712",
    "e923acd0",
    "0758c4a1",
    "235f6db6",
    "eef780a5",
    "84959efa",
    "c896be32",
    "9846ce73",
    "a3917b15",
    "1c8aa33c",
    "4779b4c9",
    "a0decd1a",
    "9a0a00b6",
    "c7b4a56f",
    "58bc9f1b",
    "217d67fe",
    "a7ff93b4",
    "fb8ad231",
    "15ee0fff",
    "35433ce3",
    "d7c9b1ec",
    "79fb7eb9",
    "362686fb",
    EXPECTED_CURRENT_PACKAGE_PROOF_FLOOR_COMMIT,
    "f7fcf1a9",
    "f3779b5d",
]
EXPECTED_PROOF_COMMIT_PATH_PREFIXES = [
    "Chummer.Avalonia/",
    "Chummer.Desktop.Runtime/",
    "Chummer.Tests/",
    "scripts/",
    ".codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json",
]
EXPECTED_PROOF_PATH_PREFIXES = [
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Desktop.Runtime/",
    "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/",
    "/docker/chummercomplete/chummer6-ui-finish/scripts/",
    "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M101_UI_RELEASE_TRAIN.generated.json",
]
ALLOWED_AUTHORITY_PROOF_ITEM_PREFIXES = [
    *EXPECTED_PROOF_PATH_PREFIXES,
    "/docker/chummercomplete/chummer6-ui-finish commit ",
]
ALLOWED_AUTHORITY_PROOF_ITEMS = [
    "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh",
    "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh exits 0.",
    "source assertion check for M101 guard tokens and primaryProofIndependentFromFallback=true",
    "source assertion check for the M101 guard tokens and generated primaryProofIndependentFromFallback=true exits 0.",
]
EXPECTED_QUEUE_PROOF_COMMIT_TOKENS = sorted(
    {
        match.group(1)
        for token in EXPECTED_QUEUE_PROOF_TOKENS
        for match in [re.search(r"\bcommit\s+([0-9a-f]{7,40})\b", token)]
        if match
    }
)
DISALLOWED_ACTIVE_RUN_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "TASK_LOCAL_TELEMETRY.generated",
    "TASK_LOCAL_TELEMETRY",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "ACTIVE_RUN_HANDOFF.generated",
    "ACTIVE_RUN_HANDOFF",
    "active_run_handoff",
    "/var/lib/codex-fleet/chummer_design_supervisor",
    "Prompt path:",
    "Recent stderr tail",
    "Active Run",
    "scripts/ooda_design_supervisor.py",
    "scripts/run_ooda_design_supervisor_until_quiet.py",
    "run_ooda_design_supervisor",
    "supervisor status",
    "status helper",
    "operator telemetry",
    "operator telemetry or active-run helper commands",
    "operator telemetry helper",
    "operator/OODA",
    "operator/OODA loop",
    "OODA loop",
    "Do not query supervisor",
    "active-run helper",
    "active-run helper commands",
    "active run helper",
    "run-state helper",
    "worker-safe resume context",
    "worker-state helper",
    "status_query_supported",
    "polling_disabled",
    "supervisor telemetry",
    "supervisor eta",
    "successor-wave telemetry",
    "successor telemetry",
    "remaining milestones",
    "remaining queue items",
    "critical path",
    "scope_label",
    "frontier_briefs",
    "Open milestone ids",
    "Successor frontier ids",
    "Successor frontier detail",
    "eta:",
]


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def scalar(value: Any) -> str:
    return str(value).strip() if value is not None else ""


def norm(value: Any) -> str:
    return scalar(value).lower()


def read_text(path: Path, reasons: list[str], label: str) -> str:
    if not path.is_file():
        reasons.append(f"{label} is missing: {path}")
        return ""
    return path.read_text(encoding="utf-8-sig")


def extract_list_item_block(text: str, marker: str, next_item_prefix: str) -> str:
    lines = text.splitlines()
    start = next((index for index, line in enumerate(lines) if marker in line), None)
    if start is None:
        return ""
    end = len(lines)
    for index in range(start + 1, len(lines)):
        line = lines[index]
        if line.startswith(next_item_prefix) and marker not in line:
            end = index
            break
    return "\n".join(lines[start:end])


def block_contains(block: str, needle: str) -> bool:
    return needle in block


def block_contains_ci(block: str, needle: str) -> bool:
    return needle.casefold() in block.casefold()


def block_contains_encoded_ci(block: str, needle: str) -> bool:
    needle_folded = needle.casefold()
    for match in re.finditer(r"[A-Za-z0-9+/=_-]{16,}", block):
        encoded = match.group(0).replace("-", "+").replace("_", "/")
        encoded += "=" * (-len(encoded) % 4)
        try:
            raw_decoded = base64.b64decode(encoded, validate=True)
        except (binascii.Error, ValueError):
            continue
        decoded_candidates = [raw_decoded.decode("utf-8", errors="ignore")]
        for decompress in (gzip.decompress, zlib.decompress):
            try:
                decoded_candidates.append(decompress(raw_decoded).decode("utf-8", errors="ignore"))
            except (OSError, zlib.error, EOFError):
                continue
        if any(needle_folded in decoded.casefold() for decoded in decoded_candidates):
            return True
    return False


def block_contains_hex_encoded_ci(block: str, needle: str) -> bool:
    needle_folded = needle.casefold()
    for match in re.finditer(r"\b(?:[0-9a-fA-F]{2}){8,}\b", block):
        try:
            decoded = bytes.fromhex(match.group(0)).decode("utf-8", errors="ignore")
        except ValueError:
            continue
        if needle_folded in decoded.casefold():
            return True
    return False


def block_contains_escaped_ci(block: str, needle: str) -> bool:
    needle_folded = needle.casefold()

    def decode_json_unicode_escapes(value: str) -> str:
        def replace_unicode(match: re.Match[str]) -> str:
            return chr(int(match.group(1), 16))

        def replace_hex(match: re.Match[str]) -> str:
            return chr(int(match.group(1), 16))

        value = re.sub(r"\\u([0-9a-fA-F]{4})", replace_unicode, value)
        return re.sub(r"\\x([0-9a-fA-F]{2})", replace_hex, value)

    decoded_candidates = [block]
    seen = {block}
    for _ in range(4):
        next_decoded = {
            urllib.parse.unquote(candidate)
            for candidate in decoded_candidates
        } | {
            html.unescape(candidate)
            for candidate in decoded_candidates
        } | {
            decode_json_unicode_escapes(candidate)
            for candidate in decoded_candidates
        }
        next_decoded = {candidate for candidate in next_decoded if candidate not in seen}
        if not next_decoded:
            break
        seen.update(next_decoded)
        decoded_candidates.extend(sorted(next_decoded))
    return any(needle_folded in candidate.casefold() for candidate in decoded_candidates)


def top_level_item_block(text: str, marker: str) -> str:
    lines = text.splitlines()
    marker_index = next((index for index, line in enumerate(lines) if marker in line), None)
    if marker_index is None:
        return ""
    start = marker_index
    for index in range(marker_index, -1, -1):
        if re.match(r"^\s*-\s+title:\s+", lines[index]):
            start = index
            break
    end = len(lines)
    for index in range(marker_index + 1, len(lines)):
        if re.match(r"^\s*-\s+title:\s+", lines[index]):
            end = index
            break
    return "\n".join(lines[start:end])


def count_top_level_item_blocks(text: str, marker: str) -> int:
    return sum(
        1
        for block in re.split(r"\n\s*-\s+title:\s+", text)
        if marker in block
    )


def extract_commit_tokens(block: str) -> list[str]:
    return sorted(set(re.findall(r"\bcommit\s+([0-9a-f]{7,40})\b", block)))


def extract_ui_repo_path_tokens(block: str) -> list[str]:
    return sorted(set(re.findall(r"/docker/chummercomplete/chummer6-ui-finish/[^\s,]+", block)))


def extract_yaml_list_items_after_key(block: str, key: str) -> list[str]:
    lines = block.splitlines()
    header_index = next((index for index, line in enumerate(lines) if line.strip() == f"{key}:"), None)
    if header_index is None:
        return []
    header_line = lines[header_index]
    header_indent = len(header_line) - len(header_line.lstrip())
    items: list[str] = []
    current_item: str | None = None
    current_indent = 0
    for line in lines[header_index + 1:]:
        if not line.strip():
            continue
        indent = len(line) - len(line.lstrip())
        match = re.match(r"^\s+-\s+(.+?)\s*$", line)
        if match:
            if indent < header_indent:
                break
            if current_item is not None:
                items.append(current_item)
            current_item = match.group(1).strip()
            current_indent = indent
            continue
        if indent <= header_indent:
            break
        if current_item is not None and indent > current_indent:
            current_item = f"{current_item} {line.strip()}"
    if current_item is not None:
        items.append(current_item)
    return items


def normalize_whitespace(text: str) -> str:
    return re.sub(r"\s+", " ", text.strip())


def extract_block_scalar_value(block: str, key: str) -> str:
    lines = block.splitlines()
    pattern = re.compile(rf"^(?P<indent>\s*)(?:-\s+)?{re.escape(key)}:\s*(?P<value>.*)$")
    for index, line in enumerate(lines):
        match = pattern.match(line)
        if not match:
            continue
        indent = len(match.group("indent"))
        value_parts = [match.group("value").strip()]
        for continuation in lines[index + 1:]:
            if not continuation.strip():
                continue
            continuation_indent = len(continuation) - len(continuation.lstrip())
            if continuation_indent <= indent:
                break
            if re.match(r"^\s*-\s+\w", continuation):
                break
            if re.match(r"^\s*\w[\w_]*:\s*", continuation):
                break
            value_parts.append(continuation.strip())
        return normalize_whitespace(" ".join(part for part in value_parts if part))
    return ""


def authority_proof_item_in_scope(item: str) -> bool:
    return item in ALLOWED_AUTHORITY_PROOF_ITEMS or any(
        item == prefix.rstrip("/") or item.startswith(prefix)
        for prefix in ALLOWED_AUTHORITY_PROOF_ITEM_PREFIXES
    )


def parse_top_level_scalars(text: str) -> dict[str, str]:
    scalars: dict[str, str] = {}
    for raw_line in text.splitlines():
        if raw_line.startswith(" ") or raw_line.startswith("-"):
            continue
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("#") or ": " not in stripped:
            continue
        key, value = stripped.split(": ", 1)
        scalars[key] = value.strip()
    return scalars


def registry_work_task_block(text: str, marker: str) -> str:
    return extract_list_item_block(text, marker, "      - id: ")


def proof_item_requires_disallowed_scan(item: str) -> bool:
    normalized = item.strip()
    return not bool(re.match(r"^/docker/chummercomplete/[^ ]+\s+commit\s+[0-9a-f]{7,40}\b", normalized))


def load_json(path: Path, reasons: list[str], label: str) -> dict[str, Any]:
    if not path.is_file():
        reasons.append(f"{label} is missing: {path}")
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # noqa: BLE001 - verifier needs the exact parse failure.
        reasons.append(f"{label} is unreadable JSON: {path} ({exc})")
        return {}
    if not isinstance(payload, dict):
        reasons.append(f"{label} must be a JSON object: {path}")
        return {}
    return payload


def scalar_leaf_values(value: Any, path: str = "$") -> list[tuple[str, str]]:
    if isinstance(value, dict):
        values: list[tuple[str, str]] = []
        for key, child in value.items():
            values.extend(scalar_leaf_values(child, f"{path}.{key}"))
        return values
    if isinstance(value, list):
        values: list[tuple[str, str]] = []
        for index, child in enumerate(value):
            values.extend(scalar_leaf_values(child, f"{path}[{index}]"))
        return values
    if value is None:
        return []
    return [(path, scalar(value))]


def fallback_token_hits(value: Any, allowed_paths: set[str] | None = None) -> list[str]:
    allowed_paths = allowed_paths or set()
    hits: list[str] = []
    for path, text in scalar_leaf_values(value):
        if path in allowed_paths:
            continue
        text_folded = text.casefold()
        path_folded = path.casefold()
        if any(
            token in text_folded
            or token in path_folded
            or block_contains_encoded_ci(text, token)
            or block_contains_encoded_ci(path, token)
            or block_contains_hex_encoded_ci(text, token)
            or block_contains_hex_encoded_ci(path, token)
            or block_contains_escaped_ci(text, token)
            or block_contains_escaped_ci(path, token)
            for token in FALLBACK_PROOF_TEXT_TOKENS
        ):
            hits.append(path)
    return sorted(set(hits))


def scalar_contains_fallback_token(value: Any) -> bool:
    text = scalar(value)
    text_folded = text.casefold()
    return any(
        token in text_folded
        or block_contains_encoded_ci(text, token)
        or block_contains_hex_encoded_ci(text, token)
        or block_contains_escaped_ci(text, token)
        for token in FALLBACK_PROOF_TEXT_TOKENS
    )


def fallback_distinct_field_hits(primary_row: dict[str, Any], fallback_row: dict[str, Any]) -> list[str]:
    hits: list[str] = []
    for field in PRIMARY_ROUTE_TRUTH_FALLBACK_DISTINCT_FIELDS:
        primary_value = scalar(primary_row.get(field)).casefold()
        fallback_value = scalar(fallback_row.get(field)).casefold()
        if primary_value and fallback_value and primary_value == fallback_value:
            hits.append(field)
    return hits


def git_result(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=git_history_root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )


def find_receipt(platform: str, rid: str, expected_digest: str) -> tuple[Path | None, dict[str, Any] | None]:
    receipt_name = f"startup-smoke-{PRIMARY_HEAD}-{rid}.receipt.json"
    roots = [
        release_channel_path.parent / "startup-smoke",
        release_channel_path.parent.parent / "startup-smoke",
        repo_root / "Docker" / "Downloads" / "startup-smoke",
        repo_root / ".codex-studio" / "published" / "startup-smoke",
        repo_root / ".codex-studio" / "out",
    ]
    candidates: list[Path] = []
    for root in roots:
        if not root.exists():
            continue
        if root.name == "out":
            candidates.extend(sorted(root.rglob(receipt_name)))
        else:
            candidate = root / receipt_name
            if candidate.is_file():
                candidates.append(candidate)

    for candidate in candidates:
        try:
            payload = json.loads(candidate.read_text(encoding="utf-8-sig"))
        except Exception:
            continue
        if not isinstance(payload, dict):
            continue
        if norm(payload.get("headId") or payload.get("head")) != PRIMARY_HEAD:
            continue
        if norm(payload.get("platform")) != platform:
            continue
        if norm(payload.get("rid")) != rid:
            continue
        if norm(payload.get("status")) not in {"pass", "passed", "ready"}:
            continue
        if scalar(payload.get("readyCheckpoint")) != "pre_ui_event_loop":
            continue
        digest = scalar(payload.get("artifactDigest"))
        if digest == expected_digest:
            return candidate, payload
    return None, None


reasons: list[str] = []
evidence: dict[str, Any] = {
    "packageId": PACKAGE_ID,
    "frontierId": FRONTIER_ID,
    "milestoneId": MILESTONE_ID,
    "completionAction": EXPECTED_COMPLETION_ACTION,
    "doNotReopenClosedPackage": True,
    "doNotReopenReason": EXPECTED_DO_NOT_REOPEN_REASON,
    "packageTitle": EXPECTED_PACKAGE_TITLE,
    "ownedSurfaces": EXPECTED_SURFACES,
    "allowedPaths": EXPECTED_ALLOWED_PATHS,
    "registryPath": str(registry_path),
    "queuePath": str(queue_path),
    "designQueuePath": str(design_queue_path),
    "releaseChannelPath": str(release_channel_path),
    "flagshipGatePath": str(flagship_gate_path),
    "verifyScriptPath": str(verify_script_path),
    "authorityRepoRoot": str(git_history_root),
    "authorityRepoAvailable": authority_repo_available,
    "authorityRepoHasDedicatedHistory": authority_repo_has_dedicated_history,
    "landedCommit": LANDED_COMMIT,
    "currentPackageProofFloorCommit": EXPECTED_CURRENT_PACKAGE_PROOF_FLOOR_COMMIT,
}

ancestor_result = git_result(["merge-base", "--is-ancestor", LANDED_COMMIT, "HEAD"]) if authority_repo_available else None
git_checks = {
    "checked_ref": "HEAD",
    "landed_commit_is_ancestor": ancestor_result.returncode == 0 if ancestor_result is not None else None,
    "authority_repo_available": authority_repo_available,
    "authority_repo_has_dedicated_history": authority_repo_has_dedicated_history,
    "resolving_proof_commits": {},
    "queue_proof_commit_tokens": EXPECTED_QUEUE_PROOF_COMMIT_TOKENS,
    "authority_row_proof_commit_tokens": [],
    "queue_proof_commit_tokens_resolve": {},
    "authority_row_proof_commit_tokens_resolve": {},
    "proof_commit_paths": {},
    "proof_commit_scope": {},
    "proof_commit_scope_allowed_prefixes": EXPECTED_PROOF_COMMIT_PATH_PREFIXES,
}
if authority_repo_has_dedicated_history:
    if ancestor_result is not None and ancestor_result.returncode != 0:
        git_checks["landed_commit_ancestor_error"] = ancestor_result.stderr.strip()
        reasons.append(f"Package landed commit {LANDED_COMMIT} is not an ancestor of local HEAD.")
    for commit in EXPECTED_RESOLVING_PROOF_COMMITS:
        commit_result = git_result(["cat-file", "-e", f"{commit}^{{commit}}"])
        commit_resolves = commit_result.returncode == 0
        git_checks["resolving_proof_commits"][commit] = commit_resolves
        if not commit_resolves:
            reasons.append(f"Package proof commit {commit} does not resolve in local chummer6-ui history.")
            git_checks["proof_commit_paths"][commit] = []
            git_checks["proof_commit_scope"][commit] = False
            continue
        paths_result = git_result(["show", "--name-only", "--format=", commit])
        changed_paths = [
            line.strip()
            for line in paths_result.stdout.splitlines()
            if line.strip()
        ]
        disallowed_paths = [
            path
            for path in changed_paths
            if not any(path == prefix.rstrip("/") or path.startswith(prefix) for prefix in EXPECTED_PROOF_COMMIT_PATH_PREFIXES)
        ]
        git_checks["proof_commit_paths"][commit] = changed_paths
        git_checks["proof_commit_scope"][commit] = not disallowed_paths and bool(changed_paths)
        if not changed_paths:
            reasons.append(f"Package proof commit {commit} has no changed paths.")
        if disallowed_paths:
            reasons.append(
                f"Package proof commit {commit} changed paths outside M101 package/proof scope: "
                + ", ".join(disallowed_paths)
            )
else:
    git_checks["landed_commit_ancestor_skipped_reason"] = "authority_repo_missing_or_not_dedicated"
    for commit in EXPECTED_RESOLVING_PROOF_COMMITS:
        git_checks["resolving_proof_commits"][commit] = None
        git_checks["proof_commit_paths"][commit] = []
        git_checks["proof_commit_scope"][commit] = None
resolving_commit_set = set(EXPECTED_RESOLVING_PROOF_COMMITS)
for commit in EXPECTED_QUEUE_PROOF_COMMIT_TOKENS:
    commit_scope_checked = True if not authority_repo_has_dedicated_history else (
        commit in resolving_commit_set
        and git_checks["resolving_proof_commits"].get(commit) is True
        and git_checks["proof_commit_scope"].get(commit) is True
    )
    git_checks["queue_proof_commit_tokens_resolve"][commit] = commit_scope_checked
    if authority_repo_has_dedicated_history and commit not in resolving_commit_set:
        reasons.append(f"Queue proof cites commit {commit} without adding it to the resolving proof floor.")
    elif authority_repo_has_dedicated_history and not commit_scope_checked:
        reasons.append(f"Queue proof cites commit {commit} but it did not resolve inside M101 package/proof scope.")
evidence["gitChecks"] = git_checks

registry_text = read_text(registry_path, reasons, "Next-90 registry")
queue_text = read_text(queue_path, reasons, "Next-90 queue staging")
design_queue_text = read_text(design_queue_path, reasons, "Next-90 design queue staging")
verify_script_text = read_text(verify_script_path, reasons, "standard AI verify script")
registry_task_block = registry_work_task_block(registry_text, "id: 101.3")
milestone_101_block = extract_list_item_block(registry_text, "id: 101", "  - id: ")
queue_package_block = top_level_item_block(queue_text, "package_id: next90-m101-ui-release-train")
design_queue_package_block = top_level_item_block(design_queue_text, "package_id: next90-m101-ui-release-train")
queue_proof_items = sorted(extract_yaml_list_items_after_key(queue_package_block, "proof"))
design_queue_proof_items = sorted(extract_yaml_list_items_after_key(design_queue_package_block, "proof"))
authority_row_proof_commit_tokens = sorted(
    set(extract_commit_tokens(registry_task_block))
    | set(extract_commit_tokens(queue_package_block))
    | set(extract_commit_tokens(design_queue_package_block))
)
authority_row_proof_path_tokens = sorted(
    set(extract_ui_repo_path_tokens(registry_task_block))
    | set(extract_ui_repo_path_tokens(queue_package_block))
    | set(extract_ui_repo_path_tokens(design_queue_package_block))
)
authority_row_proof_items = sorted(
    set(extract_yaml_list_items_after_key(registry_task_block, "evidence"))
    | set(queue_proof_items)
    | set(design_queue_proof_items)
)
git_checks["authority_row_proof_commit_tokens"] = authority_row_proof_commit_tokens
git_checks["authority_row_proof_path_tokens"] = authority_row_proof_path_tokens
git_checks["authority_row_proof_items"] = authority_row_proof_items
git_checks["proof_path_scope_allowed_prefixes"] = EXPECTED_PROOF_PATH_PREFIXES
git_checks["authority_proof_item_scope_allowed_prefixes"] = ALLOWED_AUTHORITY_PROOF_ITEM_PREFIXES
git_checks["authority_proof_item_scope_allowed_items"] = ALLOWED_AUTHORITY_PROOF_ITEMS
git_checks["authority_row_proof_path_scope"] = {}
git_checks["authority_row_proof_item_scope"] = {}
for path_token in authority_row_proof_path_tokens:
    path_scope_checked = any(
        path_token == prefix.rstrip("/") or path_token.startswith(prefix)
        for prefix in EXPECTED_PROOF_PATH_PREFIXES
    )
    git_checks["authority_row_proof_path_scope"][path_token] = path_scope_checked
    if not path_scope_checked:
        reasons.append(
            f"Canonical M101 authority proof cites path outside M101 package/proof scope: {path_token}."
        )
for item in authority_row_proof_items:
    item_scope_checked = authority_proof_item_in_scope(item)
    git_checks["authority_row_proof_item_scope"][item] = item_scope_checked
    if not item_scope_checked:
        reasons.append(
            f"Canonical M101 authority proof item is outside M101 package/proof scope: {item}."
        )
for commit in authority_row_proof_commit_tokens:
    commit_scope_checked = True if not authority_repo_has_dedicated_history else (
        commit in resolving_commit_set
        and git_checks["resolving_proof_commits"].get(commit) is True
        and git_checks["proof_commit_scope"].get(commit) is True
    )
    git_checks["authority_row_proof_commit_tokens_resolve"][commit] = commit_scope_checked
    if authority_repo_has_dedicated_history and commit not in resolving_commit_set:
        reasons.append(
            f"Canonical M101 authority proof cites commit {commit} without adding it to the resolving proof floor."
        )
    elif authority_repo_has_dedicated_history and not commit_scope_checked:
        reasons.append(
            f"Canonical M101 authority proof cites commit {commit} but it did not resolve inside M101 package/proof scope."
        )
queue_package_row_count = count_top_level_item_blocks(queue_text, "package_id: next90-m101-ui-release-train")
design_queue_package_row_count = count_top_level_item_blocks(design_queue_text, "package_id: next90-m101-ui-release-train")
queue_top_level = parse_top_level_scalars(queue_text)
design_queue_top_level = parse_top_level_scalars(design_queue_text)
queue_package_title = extract_block_scalar_value(queue_package_block, "title")
queue_package_task = extract_block_scalar_value(queue_package_block, "task")
queue_do_not_reopen_reason = extract_block_scalar_value(queue_package_block, "do_not_reopen_reason")
design_queue_package_title = extract_block_scalar_value(design_queue_package_block, "title")
design_queue_package_task = extract_block_scalar_value(design_queue_package_block, "task")
design_queue_do_not_reopen_reason = extract_block_scalar_value(design_queue_package_block, "do_not_reopen_reason")
queue_allowed_path_items = sorted(extract_yaml_list_items_after_key(queue_package_block, "allowed_paths"))
queue_owned_surface_items = sorted(extract_yaml_list_items_after_key(queue_package_block, "owned_surfaces"))
design_queue_allowed_path_items = sorted(extract_yaml_list_items_after_key(design_queue_package_block, "allowed_paths"))
design_queue_owned_surface_items = sorted(extract_yaml_list_items_after_key(design_queue_package_block, "owned_surfaces"))
evidence["queueTopLevel"] = queue_top_level
evidence["designQueueTopLevel"] = design_queue_top_level
evidence["queuePackageRowCount"] = queue_package_row_count
evidence["designQueuePackageRowCount"] = design_queue_package_row_count
evidence["queueProofItems"] = queue_proof_items
evidence["designQueueProofItems"] = design_queue_proof_items
evidence["queueProofItemsMatchDesignQueue"] = queue_proof_items == design_queue_proof_items
evidence["queueAllowedPathItems"] = queue_allowed_path_items
evidence["queueOwnedSurfaceItems"] = queue_owned_surface_items
evidence["designQueueAllowedPathItems"] = design_queue_allowed_path_items
evidence["designQueueOwnedSurfaceItems"] = design_queue_owned_surface_items
if queue_proof_items != design_queue_proof_items:
    reasons.append("Fleet and design-owned M101 queue proof items must match exactly.")
    queue_only_items = sorted(set(queue_proof_items) - set(design_queue_proof_items))
    design_queue_only_items = sorted(set(design_queue_proof_items) - set(queue_proof_items))
    if queue_only_items:
        reasons.append(
            "Fleet M101 queue proof item(s) are missing from the design-owned queue row: "
            + ", ".join(queue_only_items)
        )
    if design_queue_only_items:
        reasons.append(
            "Design-owned M101 queue proof item(s) are missing from the Fleet queue row: "
            + ", ".join(design_queue_only_items)
        )

registry_checks = {
    "milestone_101_present": block_contains(milestone_101_block, "id: 101"),
    "milestone_101_complete": block_contains(milestone_101_block, "status: complete"),
    "ui_work_task_present": block_contains(registry_task_block, "id: 101.3")
    and block_contains(registry_task_block, "Keep Avalonia primary-route proof independent from Blazor fallback proof on every promoted tuple."),
    "ui_work_task_complete": block_contains(registry_task_block, "status: complete"),
    "ui_work_task_landed_commit_matches": block_contains(registry_task_block, f"landed_commit: {LANDED_COMMIT}"),
    "ui_work_task_frontier_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 5844ad03 pins successor frontier 2450443084 into the M101 verifier, generated receipt, and compliance guard so future shards do not repeat the closed package under a different frontier.",
    ),
    "ui_work_task_latest_receipt_refresh_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 79760cc1 refreshes the M101 release train receipt after queue closure proof tightening.",
    ),
    "ui_work_task_proof_commit_resolution_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit a3bf058e tightens M101 proof commit resolution so stale proof anchors cannot keep the closed package green.",
    ),
    "ui_work_task_latest_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0954e2a1 pins M101 proof resolution guard into verifier, receipt, and compliance proof.",
    ),
    "ui_work_task_blocked_helper_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit f3e0e90b tightens the M101 blocked-helper proof guard so closed-package evidence cannot cite active-run telemetry or operator helper commands.",
    ),
    "ui_work_task_blocked_helper_anchor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit a8944fa5 pins the M101 blocked-helper proof anchor into the verifier, receipt, and compliance guard.",
    ),
    "ui_work_task_blocked_helper_receipt_refresh_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit b481d3ef refreshes the M101 release train receipt after blocked-helper anchor proof tightening.",
    ),
    "ui_work_task_latest_release_train_anchor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 52b118ff pins the latest M101 release train proof anchors.",
    ),
    "ui_work_task_queue_fingerprint_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 24eb3732 tightens the M101 queue source-fingerprint proof.",
    ),
    "ui_work_task_queue_fingerprint_anchor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 48970414 pins M101 queue fingerprint proof guard.",
    ),
    "ui_work_task_latest_queue_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 2ef1a22d pins M101 latest queue proof guard.",
    ),
    "ui_work_task_current_queue_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 8bc1fb02 pins M101 latest queue proof guard.",
    ),
    "ui_work_task_current_queue_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 9629b207 pins M101 current queue proof guard.",
    ),
    "ui_work_task_current_queue_proof_floor_anchor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 6c032e2c pins M101 current queue proof floor.",
    ),
    "ui_work_task_current_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 5c069924 pins M101 current proof floor.",
    ),
    "ui_work_task_current_proof_floor_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 8115735b pins M101 current proof floor guard.",
    ),
    "ui_work_task_latest_proof_floor_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0605657d pins M101 811 proof floor guard.",
    ),
    "ui_work_task_current_local_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 53b701e2 pins M101 060 proof floor guard.",
    ),
    "ui_work_task_current_release_train_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0fa3ce01 pins the current M101 release train proof floor.",
    ),
    "ui_work_task_current_release_train_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit b0c0b732 pins M101 current release train proof floor.",
    ),
    "ui_work_task_blocked_helper_scan_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 3f99eb0a tightens the M101 blocked-helper proof scan.",
    ),
    "ui_work_task_blocked_helper_scan_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit b21ca671 pins M101 blocked-helper scan proof floor.",
    ),
    "ui_work_task_proof_commit_scope_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0849d8c2 tightens M101 proof commit scope so closure evidence cannot cite unrelated repo changes.",
    ),
    "ui_work_task_standard_verify_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit e64db32c pins M101 release train standard verify guard.",
    ),
    "ui_work_task_refreshed_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit bb268a79 refreshes the M101 release train proof receipt after canonical successor queue verification.",
    ),
    "ui_work_task_refreshed_receipt_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 7945695d pins the refreshed M101 release train proof receipt into the verifier and compliance guard.",
    ),
    "ui_work_task_receipt_top_level_package_proof_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 9e3d931a pins M101 package identity, allowed scope, owned surfaces, landed commit, and Avalonia independence at the receipt top level.",
    ),
    "ui_work_task_top_level_package_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 492e8f83 records the M101 top-level package-proof floor in the verifier, compliance guard, and generated receipt.",
    ),
    "ui_work_task_release_train_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 31cb7cf7 tightens the M101 release train proof floor.",
    ),
    "ui_work_task_release_train_floor_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 5a282824 pins the M101 release train verifier, compliance guard, and generated receipt to proof floor 31cb7cf7.",
    ),
    "ui_work_task_latest_release_train_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 49a5466c pins M101 latest release train proof floor.",
    ),
    "ui_work_task_current_release_train_receipt_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 90c0a763 pins the M101 verifier, generated receipt, and compliance guard to proof floor 49a5466c.",
    ),
    "ui_work_task_canonical_release_train_receipt_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 60092e8d pins the M101 release train verifier, generated receipt, and compliance guard to the canonical 90c0a763 proof floor.",
    ),
    "ui_work_task_receipt_timestamp_stability_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 5403219b stabilizes the M101 release train receipt timestamp so repeated proof checks do not reopen the completed package.",
    ),
    "ui_work_task_current_proof_floor_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 871c7f7b pins the M101 release train proof floor.",
    ),
    "ui_work_task_current_release_train_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 8b0e1801 pins the current M101 release train proof floor.",
    ),
    "ui_work_task_current_release_train_proof_floor_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit eae55383 pins the current M101 release train proof floor.",
    ),
    "ui_work_task_latest_completed_package_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 287c7538 pins the M101 proof floor to the latest completed-package guard.",
    ),
    "ui_work_task_queue_row_uniqueness_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit fa67f014 tightens the M101 queue-row uniqueness guard so future shards reject duplicate completed-package rows instead of repeating the closed slice.",
    ),
    "ui_work_task_queue_uniqueness_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit c63379a3 pins M101 queue uniqueness proof floor.",
    ),
    "ui_work_task_queue_uniqueness_receipt_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 44ac83db pins the M101 queue uniqueness proof floor into the verifier, compliance guard, and generated receipt.",
    ),
    "ui_work_task_operator_ooda_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0c239ada tightens the M101 run-control proof guard so future shards reject worker-unsafe closure citations.",
    ),
    "ui_work_task_active_run_field_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 52086c9d tightens the M101 active-run field proof guard so copied task-local status fields cannot close the completed package.",
    ),
    "ui_work_task_active_run_field_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 82df294e pins the M101 active-run field proof floor.",
    ),
    "ui_work_task_verify_entrypoint_hygiene_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit bb90dca8 tightens M101 verify entrypoint hygiene.",
    ),
    "ui_work_task_verify_entrypoint_floor_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 20487c22 pins M101 verify entrypoint proof floor.",
    ),
    "ui_work_task_current_release_train_floor_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit bc01c725 pins the M101 release train proof floor.",
    ),
    "ui_work_task_latest_release_train_floor_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 8ac6d072 pins the latest M101 release train proof floor.",
    ),
    "ui_work_task_queue_proof_commit_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 1c7b5819 tightens M101 queue proof commit guard so completed queue proof commit citations must resolve locally inside package scope.",
    ),
    "ui_work_task_queue_proof_commit_guard_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit aa394d32 pins the M101 queue proof commit guard.",
    ),
    "ui_work_task_receipt_independence_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 8db934d3 tightens M101 Avalonia startup-smoke receipt independence proof.",
    ),
    "ui_work_task_receipt_independence_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit cb1fe210 pins M101 receipt independence proof.",
    ),
    "ui_work_task_blocked_helper_source_traceability_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 75b38965 tightens M101 blocked-helper proof source traceability.",
    ),
    "ui_work_task_worker_context_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit db4fc1e1 tightens M101 worker-context proof guard.",
    ),
    "ui_work_task_current_release_train_proof_floor_receipt_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 4a4079f5 pins the latest M101 release train proof floor.",
    ),
    "ui_work_task_primary_route_truth_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 9b97ab1a tightens M101 primary route-truth proof so Avalonia primary evidence cannot smuggle fallback tokens into proof-bearing fields.",
    ),
    "ui_work_task_primary_route_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit f563293f pins M101 primary route proof floor.",
    ),
    "ui_work_task_closed_package_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit c9f49b5b tightens M101 closed-package proof so future shards verify the completed package instead of reopening the Avalonia primary-route slice.",
    ),
    "ui_work_task_authority_path_scope_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit f11cff77 tightens M101 authority proof path scope so canonical proof citations cannot drift outside the Avalonia release-train package.",
    ),
    "ui_work_task_authority_proof_item_scope_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 22380dee tightens M101 authority proof item scope so canonical registry and queue proof/evidence items cannot drift outside the Avalonia release-train package.",
    ),
    "ui_work_task_authority_proof_item_guard_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 93f7dcea pins the M101 authority proof item guard.",
    ),
    "ui_work_task_authority_proof_guard_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit de600a43 pins the M101 authority proof guard floor.",
    ),
    "ui_work_task_primary_route_desktop_executable_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 6dd1064f tightens the M101 primary-route desktop executable proof guard.",
    ),
    "ui_work_task_primary_route_verify_mutation_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit b958e116 tightens M101 standard verify mutation coverage so Avalonia primary route-truth rows cannot cite Blazor fallback proof.",
    ),
    "ui_work_task_primary_route_artifact_identity_verify_mutation_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 46a9f070 tightens M101 standard verify artifact-identity mutation coverage so Avalonia primary route-truth artifact IDs cannot cite Blazor fallback proof.",
    ),
    "ui_work_task_artifact_mutation_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit ccc77950 pins M101 artifact mutation proof floor.",
    ),
    "ui_work_task_current_release_train_proof_floor_v2_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 4f103b72 pins M101 current release train proof floor.",
    ),
    "ui_work_task_current_release_train_proof_floor_v3_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit deff0535 pins the current M101 release train proof floor.",
    ),
    "ui_work_task_active_run_proof_guard_floor_v2_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 342bff22 pins M101 active-run proof guard floor.",
    ),
    "ui_work_task_active_run_guard_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0e894712 pins M101 active-run guard proof floor.",
    ),
    "ui_work_task_current_proof_floor_receipt_pin_v2_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit e923acd0 pins M101 current proof floor.",
    ),
    "ui_work_task_current_proof_floor_receipt_pin_v3_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 0758c4a1 pins M101 current proof floor.",
    ),
    "ui_work_task_release_train_proof_floor_v4_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 235f6db6 pins M101 release train proof floor.",
    ),
    "ui_work_task_required_desktop_platform_head_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit eef780a5 tightens M101 required desktop platform and head proof.",
    ),
    "ui_work_task_avalonia_artifact_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 9846ce73 pins M101 Avalonia artifact proof floor.",
    ),
    "ui_work_task_current_release_train_proof_floor_v5_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit a3917b15 pins M101 current release train proof floor.",
    ),
    "ui_work_task_closed_queue_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 1c8aa33c tightens M101 closed queue proof guard.",
    ),
    "ui_work_task_queue_title_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit f7fcf1a9 tightens M101 queue title proof.",
    ),
    "ui_work_task_queue_title_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit f3779b5d pins M101 queue title proof floor.",
    ),
    "ui_work_task_encoded_helper_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 4779b4c9 tightens M101 encoded worker-context proof guard.",
    ),
    "ui_work_task_hex_encoded_helper_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit a0decd1a tightens M101 hex-encoded helper proof guard.",
    ),
    "ui_work_task_hex_helper_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 9a0a00b6 pins M101 hex helper proof floor.",
    ),
    "ui_work_task_escaped_helper_proof_guard_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit c7b4a56f tightens M101 escaped helper proof guard.",
    ),
    "ui_work_task_escaped_helper_proof_floor_pin_present": block_contains(
        registry_task_block,
        "/docker/chummercomplete/chummer6-ui-finish commit 58bc9f1b pins M101 escaped helper proof guard.",
    ),
}
queue_checks = {
    "mode_append": queue_top_level.get("mode") == "append",
    "program_wave_matches": queue_top_level.get("program_wave") == EXPECTED_PROGRAM_WAVE,
    "status_live_parallel_successor": queue_top_level.get("status") == EXPECTED_QUEUE_STATUS,
    "source_registry_path_matches": queue_top_level.get("source_registry_path") == EXPECTED_SOURCE_REGISTRY_PATH,
    "source_queue_fingerprint_present": bool(queue_top_level.get("source_queue_fingerprint")),
    "source_queue_fingerprint_matches_design_queue": queue_top_level.get("source_queue_fingerprint") == design_queue_top_level.get("source_queue_fingerprint"),
    "package_present": bool(queue_package_block),
    "package_row_count_exactly_one": queue_package_row_count == 1,
    "frontier_matches": extract_block_scalar_value(queue_package_block, "frontier_id") == scalar(FRONTIER_ID),
    "milestone_matches": extract_block_scalar_value(queue_package_block, "milestone_id") == scalar(MILESTONE_ID),
    "repo_matches": extract_block_scalar_value(queue_package_block, "repo") == "chummer6-ui",
    "title_matches": queue_package_title == EXPECTED_PACKAGE_TITLE,
    "task_matches": queue_package_task == "Prove Avalonia as the primary desktop route on Windows, macOS, and Linux without leaning on Blazor fallback receipts.",
    "package_complete": extract_block_scalar_value(queue_package_block, "status") == "complete",
    "package_landed_commit_matches": extract_block_scalar_value(queue_package_block, "landed_commit") == LANDED_COMMIT,
    "completion_action_verify_closed_package_only": extract_block_scalar_value(queue_package_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "do_not_reopen_reason_matches": queue_do_not_reopen_reason == EXPECTED_DO_NOT_REOPEN_REASON,
    "source_design_queue_path_missing_or_matches": not queue_top_level.get("source_design_queue_path") or queue_top_level.get("source_design_queue_path") == EXPECTED_DESIGN_QUEUE_PATH,
    "allowed_paths_exact": queue_allowed_path_items == sorted(EXPECTED_ALLOWED_PATHS),
    "owned_surfaces_exact": queue_owned_surface_items == sorted(EXPECTED_SURFACES),
}
for path in EXPECTED_ALLOWED_PATHS:
    queue_checks[f"allowed_path_{path}"] = path in queue_allowed_path_items
for surface in EXPECTED_SURFACES:
    queue_checks[f"owned_surface_{surface}"] = surface in queue_owned_surface_items
for token in REQUIRED_QUEUE_PROOF_ITEMS:
    queue_checks[f"proof_{token}"] = token in queue_proof_items
design_queue_checks = {
    "mode_append": design_queue_top_level.get("mode") == "append",
    "program_wave_matches": design_queue_top_level.get("program_wave") == EXPECTED_PROGRAM_WAVE,
    "status_live_parallel_successor": design_queue_top_level.get("status") == EXPECTED_QUEUE_STATUS,
    "source_registry_path_matches": design_queue_top_level.get("source_registry_path") == EXPECTED_SOURCE_REGISTRY_PATH,
    "source_queue_fingerprint_present": bool(design_queue_top_level.get("source_queue_fingerprint")),
    "source_queue_fingerprint_matches_fleet_queue": design_queue_top_level.get("source_queue_fingerprint") == queue_top_level.get("source_queue_fingerprint"),
    "package_present": bool(design_queue_package_block),
    "package_row_count_exactly_one": design_queue_package_row_count == 1,
    "frontier_matches": extract_block_scalar_value(design_queue_package_block, "frontier_id") == scalar(FRONTIER_ID),
    "milestone_matches": extract_block_scalar_value(design_queue_package_block, "milestone_id") == scalar(MILESTONE_ID),
    "repo_matches": extract_block_scalar_value(design_queue_package_block, "repo") == "chummer6-ui",
    "title_matches": design_queue_package_title == EXPECTED_PACKAGE_TITLE,
    "task_matches": design_queue_package_task == "Prove Avalonia as the primary desktop route on Windows, macOS, and Linux without leaning on Blazor fallback receipts.",
    "package_complete": extract_block_scalar_value(design_queue_package_block, "status") == "complete",
    "package_landed_commit_matches": extract_block_scalar_value(design_queue_package_block, "landed_commit") == LANDED_COMMIT,
    "completion_action_verify_closed_package_only": extract_block_scalar_value(design_queue_package_block, "completion_action") == EXPECTED_COMPLETION_ACTION,
    "do_not_reopen_reason_matches": design_queue_do_not_reopen_reason == EXPECTED_DO_NOT_REOPEN_REASON,
    "allowed_paths_exact": design_queue_allowed_path_items == sorted(EXPECTED_ALLOWED_PATHS),
    "owned_surfaces_exact": design_queue_owned_surface_items == sorted(EXPECTED_SURFACES),
}
for path in EXPECTED_ALLOWED_PATHS:
    design_queue_checks[f"allowed_path_{path}"] = path in design_queue_allowed_path_items
for surface in EXPECTED_SURFACES:
    design_queue_checks[f"owned_surface_{surface}"] = surface in design_queue_owned_surface_items
for token in REQUIRED_QUEUE_PROOF_ITEMS:
    design_queue_checks[f"proof_{token}"] = token in design_queue_proof_items
blocked_proof_hits: list[str] = []
encoded_blocked_proof_hits: list[str] = []
hex_encoded_blocked_proof_hits: list[str] = []
escaped_blocked_proof_hits: list[str] = []
registry_evidence_items = sorted(extract_yaml_list_items_after_key(registry_task_block, "evidence"))
blocked_proof_sources = [
    {
        "label": "registry",
        "path": str(registry_path),
        "marker": "id: 101.3",
        "present": bool(registry_task_block),
        "items": registry_evidence_items,
    },
    {
        "label": "queue",
        "path": str(queue_path),
        "marker": "package_id: next90-m101-ui-release-train",
        "present": bool(queue_package_block),
        "items": queue_proof_items,
    },
    {
        "label": "design_queue",
        "path": str(design_queue_path),
        "marker": "package_id: next90-m101-ui-release-train",
        "present": bool(design_queue_package_block),
        "items": design_queue_proof_items,
    },
]
for source in blocked_proof_sources:
    proof_items_to_scan = [item for item in source["items"] if proof_item_requires_disallowed_scan(item)]
    for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS:
        if any(block_contains_ci(item, token) for item in proof_items_to_scan):
            blocked_proof_hits.append(f"{source['label']}:{token}")
        if any(block_contains_encoded_ci(item, token) for item in proof_items_to_scan):
            encoded_blocked_proof_hits.append(f"{source['label']}:{token}")
        if any(block_contains_hex_encoded_ci(item, token) for item in proof_items_to_scan):
            hex_encoded_blocked_proof_hits.append(f"{source['label']}:{token}")
        if any(block_contains_escaped_ci(item, token) for item in proof_items_to_scan):
            escaped_blocked_proof_hits.append(f"{source['label']}:{token}")
evidence["registryChecks"] = registry_checks
evidence["queueChecks"] = queue_checks
evidence["designQueueChecks"] = design_queue_checks
evidence["disallowedActiveRunProofTokens"] = DISALLOWED_ACTIVE_RUN_PROOF_TOKENS
evidence["blockedActiveRunProofSources"] = blocked_proof_sources
evidence["blockedActiveRunProofScanMode"] = "case_insensitive"
evidence["blockedActiveRunProofHits"] = blocked_proof_hits
evidence["encodedBlockedActiveRunProofHits"] = encoded_blocked_proof_hits
evidence["hexEncodedBlockedActiveRunProofHits"] = hex_encoded_blocked_proof_hits
evidence["escapedBlockedActiveRunProofHits"] = escaped_blocked_proof_hits
for source in blocked_proof_sources:
    if not source["present"]:
        reasons.append(f"M101 blocked-helper proof source is missing: {source['label']} ({source['path']}).")
for name, passed in {**registry_checks, **queue_checks}.items():
    if not passed:
        reasons.append(f"Package registry/queue check failed: {name}.")
for name, passed in design_queue_checks.items():
    if not passed:
        reasons.append(f"Design-owned package queue check failed: {name}.")
for hit in blocked_proof_hits:
    reasons.append(f"M101 package proof cites blocked active-run/operator helper evidence: {hit}.")
for hit in encoded_blocked_proof_hits:
    reasons.append(f"M101 package proof cites encoded blocked active-run/operator helper evidence: {hit}.")
for hit in hex_encoded_blocked_proof_hits:
    reasons.append(f"M101 package proof cites hex-encoded blocked active-run/operator helper evidence: {hit}.")
for hit in escaped_blocked_proof_hits:
    reasons.append(f"M101 package proof cites escaped blocked active-run/operator helper evidence: {hit}.")

standard_verify_blocked_active_run_helper_hits = [
    token
    for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS
    if block_contains_ci(verify_script_text, token)
]
standard_verify_encoded_blocked_active_run_helper_hits = [
    token
    for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS
    if block_contains_encoded_ci(verify_script_text, token)
]
standard_verify_hex_encoded_blocked_active_run_helper_hits = [
    token
    for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS
    if block_contains_hex_encoded_ci(verify_script_text, token)
]
standard_verify_escaped_blocked_active_run_helper_hits = [
    token
    for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS
    if block_contains_escaped_ci(verify_script_text, token)
]
standard_verify_checks = {
    "m101_guard_wired": "bash scripts/ai/milestones/next90-m101-ui-release-train-check.sh" in verify_script_text,
    "verify_entrypoint_avoids_active_run_helpers": not standard_verify_blocked_active_run_helper_hits,
    "verify_entrypoint_avoids_encoded_active_run_helpers": not standard_verify_encoded_blocked_active_run_helper_hits,
    "verify_entrypoint_avoids_hex_encoded_active_run_helpers": not standard_verify_hex_encoded_blocked_active_run_helper_hits,
    "verify_entrypoint_avoids_escaped_active_run_helpers": not standard_verify_escaped_blocked_active_run_helper_hits,
}
evidence["standardVerifyChecks"] = standard_verify_checks
evidence["standardVerifyBlockedActiveRunHelperHits"] = standard_verify_blocked_active_run_helper_hits
evidence["standardVerifyEncodedBlockedActiveRunHelperHits"] = standard_verify_encoded_blocked_active_run_helper_hits
evidence["standardVerifyHexEncodedBlockedActiveRunHelperHits"] = standard_verify_hex_encoded_blocked_active_run_helper_hits
evidence["standardVerifyEscapedBlockedActiveRunHelperHits"] = standard_verify_escaped_blocked_active_run_helper_hits
for name, passed in standard_verify_checks.items():
    if not passed:
        reasons.append(f"Standard verify wiring check failed: {name}.")
for hit in standard_verify_blocked_active_run_helper_hits:
    reasons.append(f"Standard verify script cites blocked active-run/operator helper evidence: {hit}.")
for hit in standard_verify_encoded_blocked_active_run_helper_hits:
    reasons.append(f"Standard verify script cites encoded blocked active-run/operator helper evidence: {hit}.")
for hit in standard_verify_hex_encoded_blocked_active_run_helper_hits:
    reasons.append(f"Standard verify script cites hex-encoded blocked active-run/operator helper evidence: {hit}.")
for hit in standard_verify_escaped_blocked_active_run_helper_hits:
    reasons.append(f"Standard verify script cites escaped blocked active-run/operator helper evidence: {hit}.")

release_channel = load_json(release_channel_path, reasons, "Release channel")
flagship_gate = load_json(flagship_gate_path, reasons, "Flagship UI release gate")
desktop_tuple_coverage = release_channel.get("desktopTupleCoverage") if isinstance(release_channel.get("desktopTupleCoverage"), dict) else {}
desktop_route_truth = desktop_tuple_coverage.get("desktopRouteTruth") if isinstance(desktop_tuple_coverage.get("desktopRouteTruth"), list) else []
promoted_platform_heads = desktop_tuple_coverage.get("promotedPlatformHeads") if isinstance(desktop_tuple_coverage.get("promotedPlatformHeads"), dict) else {}
promoted_installer_tuples = desktop_tuple_coverage.get("promotedInstallerTuples") if isinstance(desktop_tuple_coverage.get("promotedInstallerTuples"), list) else []
artifacts = release_channel.get("artifacts") if isinstance(release_channel.get("artifacts"), list) else []

required_heads = [norm(row) for row in desktop_tuple_coverage.get("requiredDesktopHeads", []) if scalar(row)]
required_platforms = [norm(row) for row in desktop_tuple_coverage.get("requiredDesktopPlatforms", []) if scalar(row)]
required_platform_head_rid_tuples = sorted(
    norm(row) for row in desktop_tuple_coverage.get("requiredDesktopPlatformHeadRidTuples", []) if scalar(row)
)
flagship_heads = [norm(row) for row in flagship_gate.get("desktopHeads", []) if scalar(row)]
evidence["requiredDesktopHeads"] = required_heads
evidence["requiredDesktopPlatforms"] = required_platforms
evidence["expectedRequiredDesktopHeads"] = EXPECTED_REQUIRED_DESKTOP_HEADS
evidence["expectedRequiredDesktopPlatforms"] = EXPECTED_REQUIRED_DESKTOP_PLATFORMS
evidence["requiredDesktopPlatformHeadRidTuples"] = required_platform_head_rid_tuples
evidence["expectedRequiredDesktopPlatformHeadRidTuples"] = EXPECTED_REQUIRED_PLATFORM_HEAD_RID_TUPLES
evidence["flagshipDesktopHeads"] = flagship_heads

if sorted(required_heads) != EXPECTED_REQUIRED_DESKTOP_HEADS:
    reasons.append(
        "Release channel desktopTupleCoverage.requiredDesktopHeads must be exactly avalonia for M101 primary-route proof."
    )
if sorted(required_platforms) != EXPECTED_REQUIRED_DESKTOP_PLATFORMS:
    reasons.append(
        "Release channel desktopTupleCoverage.requiredDesktopPlatforms must be exactly linux, macos, and windows for M101 primary-route proof."
    )
if required_platform_head_rid_tuples != EXPECTED_REQUIRED_PLATFORM_HEAD_RID_TUPLES:
    reasons.append(
        "Release channel desktopTupleCoverage.requiredDesktopPlatformHeadRidTuples must be exactly the Avalonia primary-route tuples for linux, windows, and macos."
    )
if PRIMARY_HEAD not in flagship_heads:
    reasons.append("Flagship UI release gate does not carry avalonia head proof.")

artifact_by_platform: dict[str, dict[str, Any]] = {}
for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    if norm(artifact.get("head")) != PRIMARY_HEAD:
        continue
    platform = norm(artifact.get("platform"))
    if platform in REQUIRED_TUPLES and norm(artifact.get("kind")) == "installer":
        artifact_by_platform[platform] = artifact

route_rows_by_platform: dict[str, dict[str, Any]] = {}
fallback_rows_by_platform: dict[str, dict[str, Any]] = {}
primary_route_truth_row_counts = {platform: 0 for platform in REQUIRED_TUPLES}
fallback_route_truth_row_counts = {platform: 0 for platform in REQUIRED_TUPLES}
unexpected_route_truth_rows: list[str] = []
unexpected_route_truth_keys: dict[str, list[str]] = {}
non_object_route_truth_rows: list[str] = []
for index, row in enumerate(desktop_route_truth):
    if not isinstance(row, dict):
        non_object_route_truth_rows.append(f"{index}:{scalar(row)}")
        continue
    platform = norm(row.get("platform"))
    head = norm(row.get("head"))
    if platform not in REQUIRED_TUPLES and head in {PRIMARY_HEAD, FALLBACK_HEAD}:
        unexpected_route_truth_rows.append(
            f"{head}:{platform}:{scalar(row.get('tupleId'))}"
        )
        continue
    if head == PRIMARY_HEAD:
        primary_route_truth_row_counts[platform] += 1
        route_rows_by_platform[platform] = row
    if head == FALLBACK_HEAD:
        fallback_route_truth_row_counts[platform] += 1
        fallback_rows_by_platform[platform] = row
    if platform in REQUIRED_TUPLES and head in {PRIMARY_HEAD, FALLBACK_HEAD}:
        unexpected_keys = sorted(set(row) - set(ALLOWED_DESKTOP_ROUTE_TRUTH_KEYS))
        if unexpected_keys:
            unexpected_route_truth_keys[f"{head}:{platform}:{scalar(row.get('tupleId'))}"] = unexpected_keys

evidence["routeTruthRowCardinality"] = {
    "primaryHead": PRIMARY_HEAD,
    "fallbackHead": FALLBACK_HEAD,
    "expectedPlatforms": sorted(REQUIRED_TUPLES),
    "primaryRouteTruthRowCounts": primary_route_truth_row_counts,
    "fallbackRouteTruthRowCounts": fallback_route_truth_row_counts,
    "unexpectedPrimaryOrFallbackRouteTruthRows": unexpected_route_truth_rows,
    "nonObjectDesktopRouteTruthRows": non_object_route_truth_rows,
    "allowedDesktopRouteTruthKeys": ALLOWED_DESKTOP_ROUTE_TRUTH_KEYS,
    "unexpectedPrimaryOrFallbackRouteTruthKeys": unexpected_route_truth_keys,
}
for platform in REQUIRED_TUPLES:
    if primary_route_truth_row_counts[platform] != 1:
        reasons.append(
            f"{platform}: desktopRouteTruth must contain exactly one avalonia primary row, found {primary_route_truth_row_counts[platform]}."
        )
    if fallback_route_truth_row_counts[platform] != 1:
        reasons.append(
            f"{platform}: desktopRouteTruth must contain exactly one blazor-desktop fallback row, found {fallback_route_truth_row_counts[platform]}."
        )
if unexpected_route_truth_rows:
    reasons.append(
        "desktopRouteTruth contains unexpected primary/fallback route rows outside the required M101 platforms: "
        + ", ".join(unexpected_route_truth_rows)
    )
if non_object_route_truth_rows:
    reasons.append(
        "desktopRouteTruth contains non-object row(s) that cannot prove head/platform/route-role independence: "
        + ", ".join(non_object_route_truth_rows)
    )
if unexpected_route_truth_keys:
    reasons.append(
        "desktopRouteTruth primary/fallback row has unexpected proof key(s): "
        + "; ".join(
            f"{row_id}: {', '.join(keys)}"
            for row_id, keys in sorted(unexpected_route_truth_keys.items())
        )
    )

tuple_rows_by_platform = {
    norm(row.get("platform")): row
    for row in promoted_installer_tuples
    if isinstance(row, dict) and norm(row.get("head")) == PRIMARY_HEAD
}

platform_results: dict[str, Any] = {}
for platform, expected in REQUIRED_TUPLES.items():
    platform_reasons: list[str] = []
    promoted_heads = [norm(row) for row in promoted_platform_heads.get(platform, [])] if isinstance(promoted_platform_heads.get(platform), list) else []
    if PRIMARY_HEAD not in promoted_heads:
        platform_reasons.append("promotedPlatformHeads does not include avalonia")
    if promoted_heads and promoted_heads[0] != PRIMARY_HEAD:
        platform_reasons.append("promotedPlatformHeads does not list avalonia first as primary desktop route")
    if promoted_heads.count(PRIMARY_HEAD) != 1:
        platform_reasons.append("promotedPlatformHeads must list avalonia exactly once")

    artifact = artifact_by_platform.get(platform)
    if not artifact:
        platform_reasons.append("release channel is missing avalonia installer artifact")
        expected_digest = ""
    else:
        if norm(artifact.get("rid")) != expected["rid"]:
            platform_reasons.append("avalonia artifact rid does not match required tuple")
        if norm(artifact.get("arch")) != expected["arch"]:
            platform_reasons.append("avalonia artifact arch does not match required tuple")
        if not scalar(artifact.get("sha256")):
            platform_reasons.append("avalonia artifact sha256 is missing")
        expected_digest = f"sha256:{scalar(artifact.get('sha256'))}" if scalar(artifact.get("sha256")) else ""

    tuple_row = tuple_rows_by_platform.get(platform)
    if not tuple_row:
        platform_reasons.append("promotedInstallerTuples is missing avalonia tuple")
    elif norm(tuple_row.get("rid")) != expected["rid"]:
        platform_reasons.append("promotedInstallerTuples avalonia rid does not match required tuple")

    route_row = route_rows_by_platform.get(platform)
    if not route_row:
        platform_reasons.append("desktopRouteTruth is missing avalonia primary row")
        primary_route_truth_required_fields_present = {
            f"primary_route_truth_{field}_present": False
            for field in PRIMARY_ROUTE_TRUTH_PROOF_FIELDS
        }
        primary_route_truth_independence_checks = {
            f"primary_route_truth_{field}_avoids_fallback_head": False
            for field in PRIMARY_ROUTE_TRUTH_PROOF_FIELDS
        }
        primary_route_truth_fallback_token_hits = ["$"]
        primary_route_truth_fallback_distinct_field_hits = ["$"]
    else:
        if norm(route_row.get("routeRole")) != "primary":
            platform_reasons.append("avalonia desktopRouteTruth row is not routeRole=primary")
        if norm(route_row.get("routeRoleReasonCode")) != "primary_flagship_head":
            platform_reasons.append("avalonia desktopRouteTruth routeRoleReasonCode is not primary_flagship_head")
        if norm(route_row.get("promotionState")) != "promoted":
            platform_reasons.append("avalonia desktopRouteTruth row is not promotionState=promoted")
        if norm(route_row.get("parityPosture")) != "flagship_primary":
            platform_reasons.append("avalonia desktopRouteTruth row is not parityPosture=flagship_primary")
        if norm(route_row.get("rid")) != expected["rid"]:
            platform_reasons.append("avalonia desktopRouteTruth rid does not match required tuple")
        if norm(route_row.get("arch")) != expected["arch"]:
            platform_reasons.append("avalonia desktopRouteTruth arch does not match required tuple")
        route_artifact_id = scalar(route_row.get("artifactId"))
        promoted_artifact_id = scalar(artifact.get("artifactId") or artifact.get("id")) if artifact else ""
        expected_route_tuple_id = f"{PRIMARY_HEAD}:{platform}:{expected['rid']}"
        expected_public_install_route = f"/downloads/install/{promoted_artifact_id}" if promoted_artifact_id else ""
        if scalar(route_row.get("tupleId")) != expected_route_tuple_id:
            platform_reasons.append("avalonia desktopRouteTruth tupleId does not match required primary tuple")
        if promoted_artifact_id and route_artifact_id != promoted_artifact_id:
            platform_reasons.append("avalonia desktopRouteTruth artifactId does not match promoted avalonia installer artifact")
        if expected_public_install_route and scalar(route_row.get("publicInstallRoute")) != expected_public_install_route:
            platform_reasons.append("avalonia desktopRouteTruth publicInstallRoute does not match promoted avalonia installer route")
        reason_text = scalar(route_row.get("routeRoleReason")).lower()
        expected_fallback_tuple_id = f"{FALLBACK_HEAD}:{platform}:{expected['rid']}"
        rollback_reason_text = scalar(route_row.get("rollbackReason"))
        if "independent startup-smoke proof" not in reason_text:
            platform_reasons.append("avalonia routeRoleReason does not require independent startup-smoke proof")
        primary_route_truth_required_fields_present = {
            f"primary_route_truth_{field}_present": bool(scalar(route_row.get(field)))
            for field in PRIMARY_ROUTE_TRUTH_PROOF_FIELDS
        }
        for check_name, passed in primary_route_truth_required_fields_present.items():
            if not passed:
                platform_reasons.append(f"avalonia primary route-truth required field is blank: {check_name}")
        primary_route_truth_independence_checks = {
            f"primary_route_truth_{field}_avoids_fallback_head": not scalar_contains_fallback_token(route_row.get(field))
            for field in PRIMARY_ROUTE_TRUTH_PROOF_FIELDS
        }
        fallback_row_for_comparison = fallback_rows_by_platform.get(platform)
        fallback_row_promoted_for_rollback = (
            fallback_row_for_comparison is not None
            and norm(fallback_row_for_comparison.get("promotionState")) == "promoted"
            and bool(scalar(fallback_row_for_comparison.get("artifactId")))
            and norm(fallback_row_for_comparison.get("installPosture")) == "installer_first"
        )
        primary_route_truth_independence_checks["primary_route_truth_rollback_reason_names_primary_tuple"] = (
            expected_route_tuple_id in rollback_reason_text
        )
        primary_route_truth_independence_checks["primary_route_truth_rollback_reason_names_fallback_tuple"] = (
            expected_fallback_tuple_id in rollback_reason_text
        )
        expected_rollback_state = EXPECTED_PRIMARY_ROLLBACK_STATE_BY_FALLBACK_POSTURE[fallback_row_promoted_for_rollback]
        expected_rollback_reason_code = EXPECTED_PRIMARY_ROLLBACK_REASON_CODE_BY_FALLBACK_POSTURE[fallback_row_promoted_for_rollback]
        primary_route_truth_independence_checks["primary_route_truth_rollback_state_matches_fallback_promotion_truth"] = (
            scalar(route_row.get("rollbackState")) == expected_rollback_state
        )
        primary_route_truth_independence_checks["primary_route_truth_rollback_reason_code_matches_fallback_promotion_truth"] = (
            norm(route_row.get("rollbackReasonCode")) == expected_rollback_reason_code
        )
        primary_route_truth_independence_checks["primary_route_truth_rollback_reason_matches_fallback_promotion_truth"] = (
            (
                rollback_reason_text.casefold().startswith("a promoted fallback route ")
                and expected_fallback_tuple_id in rollback_reason_text
                and expected_route_tuple_id in rollback_reason_text
                and f"on {platform}/{expected['rid']}" in rollback_reason_text
            )
            if fallback_row_promoted_for_rollback
            else (
                rollback_reason_text.casefold().startswith("fallback route ")
                and f"{expected_fallback_tuple_id} remains an unpromoted compatibility lane for {platform}/{expected['rid']}" in rollback_reason_text
                and expected_route_tuple_id in rollback_reason_text
                and promoted_artifact_id in rollback_reason_text
                and "until a separately proved fallback is published." in rollback_reason_text
            )
        )
        for check_name, passed in primary_route_truth_independence_checks.items():
            if not passed:
                platform_reasons.append(f"avalonia primary route-truth independence check failed: {check_name}")
        allowed_primary_route_fallback_reference_paths = {"$.rollbackReason"}
        primary_route_truth_fallback_token_hits = fallback_token_hits(
            route_row,
            allowed_primary_route_fallback_reference_paths,
        )
        if primary_route_truth_fallback_token_hits:
            platform_reasons.append(
                "avalonia primary route-truth scalar field(s) cite fallback proof: "
                + ", ".join(primary_route_truth_fallback_token_hits)
            )
        primary_route_truth_independence_checks["primary_route_truth_all_scalar_fields_avoid_fallback_head"] = (
            not primary_route_truth_fallback_token_hits
        )
        primary_route_truth_fallback_distinct_field_hits = (
            fallback_distinct_field_hits(route_row, fallback_row_for_comparison)
            if fallback_row_for_comparison
            else ["$"]
        )
        primary_route_truth_independence_checks["primary_route_truth_proof_fields_distinct_from_fallback_row"] = (
            not primary_route_truth_fallback_distinct_field_hits
        )
        if primary_route_truth_fallback_distinct_field_hits:
            platform_reasons.append(
                "avalonia primary route-truth proof field(s) reuse fallback row value: "
                + ", ".join(primary_route_truth_fallback_distinct_field_hits)
            )

    fallback_row = fallback_rows_by_platform.get(platform)
    if not fallback_row:
        platform_reasons.append("desktopRouteTruth is missing blazor-desktop fallback row")
    elif norm(fallback_row.get("routeRole")) != "fallback":
        platform_reasons.append("blazor-desktop fallback row is not routeRole=fallback")
    if fallback_row and norm(fallback_row.get("routeRoleReasonCode")) == "primary_flagship_head":
        platform_reasons.append("blazor-desktop fallback row is incorrectly marked with primary_flagship_head reason code")
    if fallback_row and norm(fallback_row.get("parityPosture")) == "flagship_primary":
        platform_reasons.append("blazor-desktop fallback row is incorrectly marked flagship_primary")

    receipt_path_for_platform, receipt = find_receipt(platform, expected["rid"], expected_digest)
    receipt_independence_checks: dict[str, bool] = {
        "receipt_path_avoids_fallback_head": True,
        "receipt_artifact_id_matches_primary_artifact_when_present": True,
        "receipt_primary_artifact_locator_present": False,
        "receipt_primary_artifact_locator_names_primary_head": False,
        "receipt_process_path_avoids_fallback_head": True,
        "receipt_artifact_path_avoids_fallback_head": True,
        "receipt_file_name_avoids_fallback_head": True,
        "receipt_all_scalar_fields_avoid_fallback_head": True,
    }
    receipt_fallback_token_hits: list[str] = []
    if not receipt_path_for_platform or not receipt:
        platform_reasons.append("matching avalonia startup-smoke receipt is missing")
    else:
        if norm(receipt.get("headId") or receipt.get("head")) != PRIMARY_HEAD:
            platform_reasons.append("startup-smoke receipt is not for avalonia")
        host_class = norm(receipt.get("hostClass"))
        operating_system = norm(receipt.get("operatingSystem"))
        if expected["host"] not in host_class and expected["host"] not in operating_system:
            platform_reasons.append("startup-smoke receipt host evidence does not match required host")
        if scalar(receipt.get("artifactDigest")) != expected_digest:
            platform_reasons.append("startup-smoke receipt artifactDigest does not match promoted avalonia artifact")
        receipt_path_text = str(receipt_path_for_platform)
        receipt_artifact_id = scalar(receipt.get("artifactId"))
        receipt_process_path = scalar(receipt.get("processPath"))
        receipt_artifact_path = scalar(receipt.get("artifactPath") or receipt.get("artifactRelativePath"))
        receipt_file_name = scalar(receipt.get("artifactFileName"))
        receipt_primary_artifact_locator = " ".join(
            value
            for value in [
                receipt_artifact_id,
                receipt_artifact_path,
                receipt_file_name,
            ]
            if value
        )
        receipt_independence_checks = {
            "receipt_path_avoids_fallback_head": not any(
                token in receipt_path_text.casefold()
                or block_contains_encoded_ci(receipt_path_text, token)
                or block_contains_hex_encoded_ci(receipt_path_text, token)
                or block_contains_escaped_ci(receipt_path_text, token)
                for token in FALLBACK_PROOF_TEXT_TOKENS
            ),
            "receipt_artifact_id_matches_primary_artifact_when_present": (
                not receipt_artifact_id or receipt_artifact_id == scalar(artifact.get("artifactId") or artifact.get("id"))
            ),
            "receipt_primary_artifact_locator_present": bool(receipt_primary_artifact_locator),
            "receipt_primary_artifact_locator_names_primary_head": PRIMARY_HEAD in receipt_primary_artifact_locator.casefold(),
            "receipt_process_path_avoids_fallback_head": not scalar_contains_fallback_token(receipt_process_path),
            "receipt_artifact_path_avoids_fallback_head": not scalar_contains_fallback_token(receipt_artifact_path),
            "receipt_file_name_avoids_fallback_head": not scalar_contains_fallback_token(receipt_file_name),
        }
        receipt_fallback_token_hits = fallback_token_hits(receipt)
        receipt_independence_checks["receipt_all_scalar_fields_avoid_fallback_head"] = not receipt_fallback_token_hits
        for check_name, passed in receipt_independence_checks.items():
            if not passed:
                platform_reasons.append(f"startup-smoke receipt independence check failed: {check_name}")
        if receipt_fallback_token_hits:
            platform_reasons.append(
                "startup-smoke receipt scalar field(s) cite fallback proof: "
                + ", ".join(receipt_fallback_token_hits)
            )

    platform_results[platform] = {
        "status": "pass" if not platform_reasons else "fail",
        "reasons": platform_reasons,
        "promotedHeads": promoted_heads,
        "primaryPromotedHead": promoted_heads[0] if promoted_heads else "",
        "artifactId": scalar(artifact.get("artifactId") or artifact.get("id")) if artifact else "",
        "artifactDigest": expected_digest,
        "proofHead": PRIMARY_HEAD,
        "expectedRouteTupleId": f"{PRIMARY_HEAD}:{platform}:{expected['rid']}",
        "routeTupleId": scalar(route_row.get("tupleId")) if route_row else "",
        "routeRid": scalar(route_row.get("rid")) if route_row else "",
        "routeArch": scalar(route_row.get("arch")) if route_row else "",
        "expectedPublicInstallRoute": f"/downloads/install/{scalar(artifact.get('artifactId') or artifact.get('id'))}" if artifact else "",
        "publicInstallRoute": scalar(route_row.get("publicInstallRoute")) if route_row else "",
        "fallbackRouteRole": scalar(fallback_row.get("routeRole")) if fallback_row else "",
        "fallbackPromotionState": scalar(fallback_row.get("promotionState")) if fallback_row else "",
        "startupSmokeReceiptPath": str(receipt_path_for_platform) if receipt_path_for_platform else "",
        "startupSmokeReceiptIndependenceChecks": receipt_independence_checks,
        "startupSmokeReceiptFallbackTokenHits": receipt_fallback_token_hits,
        "primaryRouteTruthIndependenceChecks": primary_route_truth_independence_checks,
        "primaryRouteTruthFallbackTokenHits": primary_route_truth_fallback_token_hits,
        "primaryRouteTruthFallbackDistinctFieldHits": primary_route_truth_fallback_distinct_field_hits,
        "primaryRouteTruthRequiredFieldChecks": primary_route_truth_required_fields_present,
    }
    reasons.extend(f"{platform}: {reason}" for reason in platform_reasons)

evidence["platformResults"] = platform_results
evidence["blazorFallbackRowsPresent"] = sorted(fallback_rows_by_platform)
evidence["primaryProofIndependentFromFallback"] = all(
    result.get("status") == "pass"
    and result.get("proofHead") == PRIMARY_HEAD
    and result.get("fallbackRouteRole") == "fallback"
    for result in platform_results.values()
)
evidence["releaseChannelStatus"] = norm(release_channel.get("status"))
evidence["releaseChannelVersion"] = scalar(release_channel.get("version") or release_channel.get("releaseVersion"))
if norm(release_channel.get("status")) != "published":
    reasons.append("Release channel is not published.")

payload = {
    "contract_name": "chummer6-ui.next90_m101_ui_release_train",
    "generatedAt": now_iso(),
    "packageId": PACKAGE_ID,
    "frontierId": FRONTIER_ID,
    "milestoneId": MILESTONE_ID,
    "landedCommit": LANDED_COMMIT,
    "currentPackageProofFloorCommit": EXPECTED_CURRENT_PACKAGE_PROOF_FLOOR_COMMIT,
    "packageTitle": EXPECTED_PACKAGE_TITLE,
    "completionAction": EXPECTED_COMPLETION_ACTION,
    "doNotReopenClosedPackage": True,
    "doNotReopenReason": EXPECTED_DO_NOT_REOPEN_REASON,
    "ownedSurfaces": EXPECTED_SURFACES,
    "allowedPaths": EXPECTED_ALLOWED_PATHS,
    "primaryProofIndependentFromFallback": evidence["primaryProofIndependentFromFallback"],
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Next-90 milestone 101.3 proves Avalonia as the independent primary desktop route on Windows, macOS, and Linux without counting Blazor fallback proof."
        if not reasons
        else "Next-90 milestone 101.3 is missing independent Avalonia primary-route proof."
    ),
    "reasons": reasons,
    "evidence": evidence,
}

if receipt_path.is_file():
    try:
        previous_payload = json.loads(receipt_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        previous_payload = None
    if isinstance(previous_payload, dict):
        semantic_payload = dict(payload)
        previous_semantic_payload = dict(previous_payload)
        semantic_payload.pop("generatedAt", None)
        previous_semantic_payload.pop("generatedAt", None)
        if semantic_payload == previous_semantic_payload and isinstance(previous_payload.get("generatedAt"), str):
            payload["generatedAt"] = previous_payload["generatedAt"]

receipt_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if reasons:
    print("[next90-m101-ui-release-train] FAIL")
    for reason in reasons:
        print(f" - {reason}")
    sys.exit(1)

print("[next90-m101-ui-release-train] PASS")
print(str(receipt_path))
PY
