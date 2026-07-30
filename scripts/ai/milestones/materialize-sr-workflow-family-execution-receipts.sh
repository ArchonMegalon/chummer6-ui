#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
edition="${1:-}"

case "$edition" in
  sr4)
    ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr4_workflow_family_execution_receipt"
    proof_kind="sr4_family_oracle"
    ;;
  sr6)
    ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr6_workflow_family_execution_receipt"
    proof_kind="sr6_family_release_gated_execution"
    ;;
  *)
    echo "usage: $0 <sr4|sr6>" >&2
    exit 64
    ;;
esac

hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
run_services_release_channel_path="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"
bundled_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
explicit_release_channel_path="${CHUMMER_WORKFLOW_FAMILY_RELEASE_CHANNEL_PATH:-${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-}}"
if [[ -n "$explicit_release_channel_path" ]]; then
  release_channel_path="$explicit_release_channel_path"
elif [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path="$canonical_release_channel_path"
elif [[ -f "$verified_release_channel_path" ]]; then
  release_channel_path="$verified_release_channel_path"
elif [[ -f "$run_services_release_channel_path" ]]; then
  release_channel_path="$run_services_release_channel_path"
else
  release_channel_path="$bundled_release_channel_path"
fi

python3 - <<'PY' "$edition" "$ledger_path" "$oracle_path" "$repo_root" "$contract_name" "$proof_kind" "$release_channel_path"
from __future__ import annotations

import atexit
import fcntl
import hashlib
import ipaddress
import json
import os
import secrets
import socket
import stat
import subprocess
import sys
import tempfile
import time
import uuid
from urllib.parse import urlparse
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

edition = sys.argv[1].strip().lower()
ledger_path = Path(sys.argv[2])
oracle_path = Path(sys.argv[3])
repo_root = Path(sys.argv[4])
contract_name = sys.argv[5].strip()
proof_kind = sys.argv[6].strip().lower()
release_channel_path = Path(sys.argv[7])
trx_contract_source_path = (
    repo_root / "scripts" / "ai" / "milestones" / "workflow_family_trx_contract.py"
)
sys.path.insert(0, str(trx_contract_source_path.parent))
from workflow_family_trx_contract import (
    CANONICAL_API_BASE_URL,
    CANONICAL_TEST_CLASS_BY_NAME,
    build_workflow_stage_manifest,
    execution_run_digest_for,
    snapshot_output_tree,
    validate_api_probe_contract,
    validate_trx_contract,
    validate_trx_record_contract,
    validate_workflow_stage_manifest,
    workflow_stage_manifest_path,
    workflow_stage_receipt_record,
)

SCHEMA_VERSION = 1
RECEIPT_MAX_AGE_SECONDS = 86400
MAX_FUTURE_SKEW_SECONDS = 300
MAX_REGULAR_INPUT_BYTES = 64 * 1024 * 1024
CANONICAL_LEDGER_SHA256 = {
    "sr4": "76267549b18bd866a7776f9d2792da6a613e1c47c2797ff1142d8b7f4531723d",
    "sr6": "f8bfb1cf834bd0f7679ca8336fe1e934d3906546521caa314655d59fbc4620c3",
}
CANONICAL_ORACLE_SHA256 = {
    "sr4": "c3d64935f7dd74ac4967ab8dd055daca825578279fc8fa2fe2ffdf9e0d7a5088",
    "sr6": "fbaf455e245219f0ff7f7fc0d82ee52ce3893fa1ddcdca6b61fc9a683ec8d587",
}
CANONICAL_FAMILY_IDS = {
    "create-open-import-save-save-as-print-export",
    "metatype-priorities-karma-entry",
    "attributes-skills-skill-groups-specializations-knowledge-languages",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
    "cyberware-bioware-modular-hierarchies-nested-plugins",
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
    "improvements-explain-result-parity",
    "recovery-reload-migration-roundtrips",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_strict_timestamp(value: object, label: str) -> tuple[str, datetime]:
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        raise SystemExit(f"{label} must be a nonblank canonical offset timestamp")
    raw = value
    try:
        parsed = datetime.fromisoformat(raw.replace("Z", "+00:00"))
    except ValueError as exc:
        raise SystemExit(f"{label} is not an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise SystemExit(f"{label} must include a UTC offset")
    parsed_utc = parsed.astimezone(timezone.utc)
    delta_seconds = (datetime.now(timezone.utc) - parsed_utc).total_seconds()
    if delta_seconds > RECEIPT_MAX_AGE_SECONDS:
        raise SystemExit(f"{label} is stale ({int(delta_seconds)}s old)")
    if delta_seconds < -MAX_FUTURE_SKEW_SECONDS:
        raise SystemExit(f"{label} is too far in the future ({int(-delta_seconds)}s ahead)")
    return raw, parsed_utc


def read_regular_bytes(path: Path, label: str) -> bytes:
    if path.is_symlink():
        raise SystemExit(f"{label} must not be a symlink: {path}")
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        fd = os.open(path, flags)
    except OSError as exc:
        raise SystemExit(f"{label} is missing or unreadable: {path}: {exc}") from exc
    try:
        before = os.fstat(fd)
        if not stat.S_ISREG(before.st_mode):
            raise SystemExit(f"{label} is not a regular file: {path}")
        if before.st_size > MAX_REGULAR_INPUT_BYTES:
            raise SystemExit(
                f"{label} exceeds the {MAX_REGULAR_INPUT_BYTES}-byte safety limit: {path}"
            )
        chunks = []
        total_bytes = 0
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            total_bytes += len(chunk)
            if total_bytes > MAX_REGULAR_INPUT_BYTES:
                raise SystemExit(
                    f"{label} exceeds the {MAX_REGULAR_INPUT_BYTES}-byte safety limit while reading: {path}"
                )
            chunks.append(chunk)
        after = os.fstat(fd)
    finally:
        os.close(fd)
    data = b"".join(chunks)
    if (
        before.st_dev != after.st_dev
        or before.st_ino != after.st_ino
        or before.st_size != after.st_size
        or before.st_mtime_ns != after.st_mtime_ns
        or len(data) != after.st_size
    ):
        raise SystemExit(f"{label} changed while it was being read: {path}")
    return data


def load_regular_json(path: Path, label: str) -> tuple[dict, bytes]:
    raw = read_regular_bytes(path, label)
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise SystemExit(f"{label} is not valid JSON: {path}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"{label} root must be an object: {path}")
    return payload, raw


def file_binding(path: Path, label: str) -> dict[str, object]:
    raw = read_regular_bytes(path, label)
    return {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "sizeBytes": len(raw),
    }


def atomic_write_json(path: Path, payload: dict) -> None:
    if path.is_symlink():
        raise SystemExit(f"refusing to replace symlink receipt path: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
    fd, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fchmod(handle.fileno(), 0o644)
            os.fsync(handle.fileno())
        if path.is_symlink():
            raise SystemExit(f"refusing to replace symlink receipt path: {path}")
        os.replace(temporary_path, path)
        directory_fd = os.open(path.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        temporary_path.unlink(missing_ok=True)

ledger, ledger_bytes = load_regular_json(ledger_path, "workflow parity ledger")
oracle, oracle_bytes = load_regular_json(oracle_path, "workflow parity oracle")
release_channel, release_channel_bytes = load_regular_json(
    release_channel_path, "release channel receipt"
)
if hashlib.sha256(ledger_bytes).hexdigest() != CANONICAL_LEDGER_SHA256[edition]:
    raise SystemExit(f"{edition.upper()} workflow parity ledger bytes are not the reviewed canonical contract")
if hashlib.sha256(oracle_bytes).hexdigest() != CANONICAL_ORACLE_SHA256[edition]:
    raise SystemExit(f"{edition.upper()} workflow parity oracle bytes are not the reviewed canonical contract")
release_contract_aliases = {
    key: str(release_channel.get(key) or "").strip()
    for key in ("contract_name", "contractName")
    if key in release_channel
}
if set(release_contract_aliases) != {"contract_name", "contractName"} or any(
    value != "Chummer.Hub.Registry.Contracts"
    for value in release_contract_aliases.values()
):
    raise SystemExit("release channel contract aliases must both equal Chummer.Hub.Registry.Contracts")
if type(release_channel.get("schemaVersion")) is not int or release_channel.get("schemaVersion") != 1:
    raise SystemExit("release channel schemaVersion must equal integer 1")
if str(release_channel.get("status") or "").strip().lower() != "published":
    raise SystemExit("release channel status must be published")
if type(ledger.get("version")) is not int or ledger.get("version") != 1:
    raise SystemExit("workflow parity ledger version must be integer 1")
expected_scope = f"{edition}_desktop_head"
if ledger.get("scope") != expected_scope:
    raise SystemExit(f"workflow parity ledger scope must equal {expected_scope}")
raw_families = ledger.get("requiredFamilies")
if not isinstance(raw_families, list) or not raw_families:
    raise SystemExit("workflow parity ledger requiredFamilies must be a non-empty array")
families = []
ledger_family_ids: list[str] = []
for index, item in enumerate(raw_families):
    if not isinstance(item, dict):
        raise SystemExit(f"workflow parity ledger family {index} must be an object")
    family_id = item.get("id")
    if not isinstance(family_id, str) or not family_id or family_id != family_id.strip():
        raise SystemExit(f"workflow parity ledger family {index} has an invalid id")
    if family_id in ledger_family_ids:
        raise SystemExit(f"workflow parity ledger contains duplicate family id: {family_id}")
    ledger_family_ids.append(family_id)
    families.append(item)
if set(ledger_family_ids) != CANONICAL_FAMILY_IDS:
    raise SystemExit("workflow parity ledger canonical family inventory is not exact")
for family in families:
    family_id = family["id"]
    audit_tests = family.get("auditTests")
    if (
        not isinstance(audit_tests, list)
        or not audit_tests
        or any(not isinstance(value, str) or not value or value != value.strip() for value in audit_tests)
        or len(audit_tests) != len(set(audit_tests))
    ):
        raise SystemExit(f"workflow parity ledger family {family_id} has an invalid auditTests contract")
    expected_execution = [
        f".codex-studio/published/workflow-family-parity/executed/{edition}/{{familyId}}.generated.json"
    ]
    expected_verification = [
        f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"
    ]
    expected_parity = [
        f".codex-studio/published/workflow-family-parity/{edition.upper()}_WORKFLOW_FAMILY_{family_id}.generated.json"
    ]
    if family.get("executionReceipts") != expected_execution:
        raise SystemExit(f"workflow parity ledger family {family_id} executionReceipts target is not canonical")
    if family.get("verificationReceipts") != expected_verification:
        raise SystemExit(f"workflow parity ledger family {family_id} verificationReceipts target is not canonical")
    if family.get("parityReceipts") != expected_parity:
        raise SystemExit(f"workflow parity ledger family {family_id} parityReceipts target is not canonical")

if type(oracle.get("version")) is not int or oracle.get("version") != 1:
    raise SystemExit("workflow parity oracle version must be integer 1")
if oracle.get("scope") != expected_scope:
    raise SystemExit(f"workflow parity oracle scope must equal {expected_scope}")
if edition == "sr4":
    oracle_family_ids = oracle.get("workflowFamilies")
    if (
        not isinstance(oracle_family_ids, list)
        or any(
            not isinstance(value, str) or not value or value != value.strip()
            for value in oracle_family_ids
        )
        or len(oracle_family_ids) != len(set(oracle_family_ids))
        or set(oracle_family_ids) != CANONICAL_FAMILY_IDS
    ):
        raise SystemExit("SR4 workflow parity oracle family inventory is not exact")
    source_repo = oracle.get("sourceRepo")
    if not isinstance(source_repo, dict):
        raise SystemExit("SR4 workflow parity oracle sourceRepo must be an object")
    source_repo_path = str(source_repo.get("path") or "").strip()
    source_repo_head = str(source_repo.get("head") or "").strip()
    if (
        not source_repo_path
        or len(source_repo_head) != 40
        or any(character not in "0123456789abcdef" for character in source_repo_head)
    ):
        raise SystemExit("SR4 workflow parity oracle sourceRepo binding is invalid")
else:
    oracle_families = oracle.get("requiredFamilies")
    if not isinstance(oracle_families, list) or not oracle_families:
        raise SystemExit("SR6 workflow parity oracle requiredFamilies must be a non-empty array")
    oracle_family_ids = []
    for index, item in enumerate(oracle_families):
        if not isinstance(item, dict):
            raise SystemExit(f"SR6 workflow parity oracle family {index} must be an object")
        family_id = item.get("id")
        if (
            not isinstance(family_id, str)
            or not family_id
            or family_id != family_id.strip()
            or family_id in oracle_family_ids
        ):
            raise SystemExit(f"SR6 workflow parity oracle family {index} has an invalid id")
        release_gate_tests = item.get("releaseGateTests")
        if (
            not str(item.get("classification") or "").strip()
            or not str(item.get("rationale") or "").strip()
            or not isinstance(release_gate_tests, list)
            or not release_gate_tests
            or any(
                not isinstance(value, str) or not value or value != value.strip()
                for value in release_gate_tests
            )
            or len(release_gate_tests) != len(set(release_gate_tests))
        ):
            raise SystemExit(
                f"SR6 workflow parity oracle family {family_id} has an invalid release contract"
            )
        oracle_family_ids.append(family_id)
    if set(oracle_family_ids) != CANONICAL_FAMILY_IDS:
        raise SystemExit("SR6 workflow parity oracle family inventory is not exact")

channel_id = str(release_channel.get("channelId") or "").strip()
channel_alias = str(release_channel.get("channel") or "").strip()
if not channel_id or not channel_alias:
    raise SystemExit("release channel must declare both channelId and channel")
if channel_id.lower() != channel_alias.lower():
    raise SystemExit("release channel carries conflicting channelId/channel aliases")
channel_id = channel_id.lower()
release_version = str(release_channel.get("releaseVersion") or "").strip()
version_alias = str(release_channel.get("version") or "").strip()
if not release_version or not version_alias:
    raise SystemExit("release channel must declare both releaseVersion and version")
if release_version != version_alias:
    raise SystemExit("release channel carries conflicting releaseVersion/version aliases")
release_generated_at_value = release_channel.get("generatedAt")
release_generated_at_alias = release_channel.get("generated_at")
if (
    release_generated_at_value is not None
    and release_generated_at_alias is not None
    and release_generated_at_value != release_generated_at_alias
):
    raise SystemExit("release channel carries conflicting generatedAt/generated_at aliases")
release_generated_at, _ = parse_strict_timestamp(
    release_generated_at_value or release_generated_at_alias,
    "release channel generatedAt",
)
release_identity = {
    "channelId": channel_id,
    "releaseVersion": release_version,
    "generatedAt": release_generated_at,
    "path": str(release_channel_path.resolve()),
    "sha256": hashlib.sha256(release_channel_bytes).hexdigest(),
    "sizeBytes": len(release_channel_bytes),
}
family_filter_ids = {
    str(value).strip()
    for value in (os.environ.get("CHUMMER_WORKFLOW_FAMILY_FILTER_IDS") or "").split(",")
    if str(value).strip()
}
if family_filter_ids:
    if family_filter_ids != CANONICAL_FAMILY_IDS:
        raise SystemExit(
            "CHUMMER_WORKFLOW_FAMILY_FILTER_IDS must select the full canonical family inventory; partial filters cannot publish proof"
        )

producer_run_id = str(uuid.uuid4())
run_started_at = now_iso()
run_root = (
    repo_root
    / ".codex-studio"
    / "out"
    / "workflow-family-parity"
    / "executed"
    / edition
    / producer_run_id
)
run_root.mkdir(parents=True, exist_ok=True)
lock_dir = repo_root / ".codex-studio" / "locks"
lock_dir.mkdir(parents=True, exist_ok=True)
lock_path = lock_dir / f"workflow-family-dotnet-test-{edition}.lock"
max_test_attempts = 1

unique_tests: list[str] = []
for family in families:
    for test_name in family.get("auditTests") or []:
        value = str(test_name).strip()
        if value and value not in unique_tests:
            unique_tests.append(value)
if any(test_name not in CANONICAL_TEST_CLASS_BY_NAME for test_name in unique_tests):
    unknown_tests = sorted(
        test_name
        for test_name in unique_tests
        if test_name not in CANONICAL_TEST_CLASS_BY_NAME
    )
    raise SystemExit(
        "workflow parity ledger references tests outside the canonical class map: "
        + ", ".join(unknown_tests)
    )

run_error = ""
run_exit = 0
external_blocker = ""
api_probe: dict[str, object] = {}
dotnet_attempt_count = 0
build_attempt_count = 0
api_server_proc: subprocess.Popen[str] | None = None
api_server_command: list[str] = []
api_server_log_path = run_root / f"{edition}-local-api.log"
per_test_trx_paths: dict[str, str] = {}
per_test_exit_codes: dict[str, int] = {}
per_test_attempt_counts: dict[str, int] = {}
per_test_attempt_started_at: dict[str, str] = {}
per_test_attempt_completed_at: dict[str, str] = {}
api_project_override = str(os.environ.get("CHUMMER_API_AUTOSTART_PROJECT") or "").strip()
default_api_project = repo_root / "Chummer.Api" / "Chummer.Api.csproj"
api_project_path = default_api_project
if api_project_override and Path(api_project_override).resolve(strict=False) != default_api_project.resolve(strict=False):
    raise SystemExit("CHUMMER_API_AUTOSTART_PROJECT may not redirect the canonical workflow proof lane")
try:
    api_project_path.resolve(strict=False).relative_to(repo_root.resolve())
except ValueError as exc:
    raise SystemExit("canonical API autostart project resolves outside the repository") from exc
default_api_build_output = (
    api_project_path.parent / "bin" / "Release" / "net10.0" / f"{api_project_path.stem}.dll"
)
test_project_path = repo_root / "Chummer.Tests" / "Chummer.Tests.csproj"
dotnet_host_path = Path("/usr/bin/dotnet").resolve(strict=True)
if not str(dotnet_host_path).startswith("/usr/"):
    raise SystemExit("canonical dotnet host must resolve under /usr")
dotnet_host_binding = file_binding(dotnet_host_path, "canonical dotnet host")
test_build_output_dir = repo_root / "Chummer.Tests" / "bin" / "Release" / "net10.0"
test_runner_apphost = test_build_output_dir / "Chummer.Tests"
test_runner_dll = test_build_output_dir / "Chummer.Tests.dll"
test_source_paths = [
    repo_root / "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
    repo_root / "Chummer.Tests/Compliance/MigrationComplianceTests.cs",
    repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    repo_root / "Chummer.Tests/Presentation/WorkflowParityGateTests.cs",
]
source_bindings = [file_binding(path, "workflow parity test source") for path in test_source_paths]
trx_contract_source_binding = file_binding(
    trx_contract_source_path, "workflow-family TRX validator source"
)
test_build_projects = [
    ("Chummer.Avalonia", repo_root / "Chummer.Avalonia" / "Chummer.Avalonia.csproj"),
    ("Chummer.Portal", repo_root / "Chummer.Portal" / "Chummer.Portal.csproj"),
    ("Chummer.Tests", test_project_path),
]
build_project_bindings = [
    file_binding(project_path, f"{project_label} project contract")
    for project_label, project_path in test_build_projects
]
api_project_binding = file_binding(api_project_path, "canonical API autostart project")
build_output_roots = {
    "Chummer.Api": repo_root / "Chummer.Api" / "bin" / "Release" / "net10.0",
    "Chummer.Avalonia": repo_root / "Chummer.Avalonia" / "bin" / "Release" / "net10.0",
    "Chummer.Portal": repo_root / "Chummer.Portal" / "bin" / "Release" / "net10.0",
    "Chummer.Tests": test_build_output_dir,
}


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file_pointer, code, message, headers, new_url):
        return None


api_probe_opener = urllib.request.build_opener(
    urllib.request.ProxyHandler({}),
    NoRedirectHandler(),
)


def canonical_subprocess_environment(extra: dict[str, str] | None = None) -> dict[str, str]:
    allowed_keys = (
        "HOME",
        "LANG",
        "LC_ALL",
        "TZ",
        "TMPDIR",
        "TMP",
        "TEMP",
        "DOTNET_ROOT",
        "DOTNET_CLI_HOME",
        "NUGET_PACKAGES",
        "SSL_CERT_FILE",
        "SSL_CERT_DIR",
    )
    environment = {
        key: os.environ[key]
        for key in allowed_keys
        if key in os.environ and os.environ[key]
    }
    environment.update(
        {
            "PATH": "/usr/bin:/bin",
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_NOLOGO": "1",
            "NO_PROXY": "127.0.0.1,localhost,::1",
        }
    )
    if extra:
        environment.update(extra)
    return environment


def probe_api_surface(base_url: str, path: str) -> tuple[bool, int, str]:
    target = f"{base_url.rstrip('/')}{path}"
    request = urllib.request.Request(target, method="GET")
    try:
        with api_probe_opener.open(request, timeout=2) as response:
            return True, int(response.getcode()), ""
    except urllib.error.HTTPError as ex:
        code = int(getattr(ex, "code", 0) or 0)
        # Auth-gated or method mismatch still proves the route exists.
        if code in {401, 403, 405}:
            return True, code, ""
        return False, code, str(ex)
    except Exception as ex:  # noqa: BLE001
        return False, 0, str(ex)


def collect_api_probe(base_url: str) -> tuple[dict[str, object], bool]:
    api_probe_results = []
    for probe_path in api_probe_paths:
        ok, status_code, error = probe_api_surface(base_url, probe_path)
        api_probe_results.append(
            {
                "path": probe_path,
                "ok": bool(ok),
                "statusCode": status_code,
                "error": error,
            }
        )
    return (
        {
            "baseUrl": base_url,
            "results": api_probe_results,
        },
        all(bool(item.get("ok")) for item in api_probe_results),
    )


def warm_api_surface(base_url: str, attempts: int = 5, delay_seconds: float = 0.5) -> tuple[dict[str, object], bool]:
    last_probe: dict[str, object] = {}
    last_ready = False
    for _ in range(max(1, attempts)):
        last_probe, last_ready = collect_api_probe(base_url)
        if last_ready:
            time.sleep(max(0.0, delay_seconds))
            confirm_probe, confirm_ready = collect_api_probe(base_url)
            if confirm_ready:
                confirm_probe["warmed"] = True
                return confirm_probe, True
            last_probe = confirm_probe
            last_ready = confirm_ready
        time.sleep(max(0.0, delay_seconds))
    if last_probe:
        last_probe["warmed"] = False
    return last_probe, last_ready


def can_autostart_local_api(base_url: str) -> bool:
    parsed = urlparse(base_url)
    return parsed.scheme in {"http", "https"} and url_resolves_only_to_loopback(parsed)


def url_resolves_only_to_loopback(parsed) -> bool:
    hostname = str(parsed.hostname or "").strip().lower()
    if not hostname:
        return False
    try:
        return ipaddress.ip_address(hostname).is_loopback
    except ValueError:
        if hostname != "localhost":
            return False
    try:
        addresses = {
            ipaddress.ip_address(sockaddr[0])
            for _family, _type, _proto, _canonname, sockaddr in socket.getaddrinfo(
                hostname,
                parsed.port or (443 if parsed.scheme == "https" else 80),
                type=socket.SOCK_STREAM,
            )
        }
    except (OSError, ValueError):
        return False
    return bool(addresses) and all(address.is_loopback for address in addresses)


def terminate_local_api() -> None:
    global api_server_proc
    if api_server_proc is None:
        return
    if api_server_proc.poll() is None:
        api_server_proc.terminate()
        try:
            api_server_proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            api_server_proc.kill()
            api_server_proc.wait(timeout=10)
    api_server_proc = None


atexit.register(terminate_local_api)


def ensure_local_api(base_url: str) -> tuple[dict[str, object], bool]:
    global api_server_command, api_server_proc
    initial_probe, initial_ready = collect_api_probe(base_url)
    if initial_ready:
        owned_process_is_alive = (
            api_server_proc is not None and api_server_proc.poll() is None
        )
        initial_probe["autostarted"] = owned_process_is_alive
        initial_probe["autostartCommand"] = list(api_server_command)
        initial_probe["autostartPid"] = (
            api_server_proc.pid if owned_process_is_alive and api_server_proc else None
        )
        initial_probe["processAliveAtProof"] = owned_process_is_alive
        if owned_process_is_alive:
            return initial_probe, True
        initial_probe["untrustedPreexistingService"] = True
        return initial_probe, False

    autostart_enabled = str(os.environ.get("CHUMMER_API_AUTOSTART") or "1").strip().lower() not in {"0", "false", "no"}
    if not autostart_enabled or not can_autostart_local_api(base_url):
        initial_probe["autostarted"] = False
        return initial_probe, False
    if not api_project_path.is_file():
        initial_probe["autostarted"] = False
        initial_probe["autostartProjectPath"] = str(api_project_path)
        initial_probe["autostartFailure"] = "autostart_project_missing"
        return initial_probe, False

    api_server_log_path.parent.mkdir(parents=True, exist_ok=True)
    api_log_handle = api_server_log_path.open("w", encoding="utf-8")
    portal_owner_shared_key = str(
        os.environ.get("CHUMMER_API_AUTOSTART_PORTAL_OWNER_SHARED_KEY") or ""
    ).strip()
    if portal_owner_shared_key and len(portal_owner_shared_key.encode("utf-8")) < 32:
        raise SystemExit(
            "CHUMMER_API_AUTOSTART_PORTAL_OWNER_SHARED_KEY must contain at least "
            "32 UTF-8 bytes when supplied"
        )
    if not portal_owner_shared_key:
        portal_owner_shared_key = secrets.token_urlsafe(48)
    env = canonical_subprocess_environment(
        {
            "ASPNETCORE_URLS": base_url,
            "CHUMMER_PORTAL_OWNER_SHARED_KEY": portal_owner_shared_key,
        }
    )
    build_output_path = default_api_build_output
    run_command = [
        str(dotnet_host_path),
        "run",
        "--project",
        str(api_project_path),
        "--configuration",
        "Release",
        "--no-launch-profile",
        "--no-restore",
    ]
    run_command.extend(["--urls", base_url])
    api_server_command = list(run_command)
    api_server_proc = subprocess.Popen(
        run_command,
        cwd=repo_root,
        stdout=api_log_handle,
        stderr=subprocess.STDOUT,
        text=True,
        env=env,
    )

    deadline = time.monotonic() + max(
        5,
        int(str(os.environ.get("CHUMMER_API_AUTOSTART_TIMEOUT_SECONDS") or "90").strip() or "90"),
    )
    while time.monotonic() < deadline:
        current_probe, current_ready = collect_api_probe(base_url)
        if current_ready:
            current_probe["autostarted"] = True
            current_probe["autostartLogPath"] = str(api_server_log_path)
            current_probe["autostartPid"] = api_server_proc.pid if api_server_proc else None
            current_probe["autostartCommand"] = list(api_server_command)
            current_probe["processAliveAtProof"] = (
                api_server_proc is not None and api_server_proc.poll() is None
            )
            return current_probe, True
        if api_server_proc.poll() is not None:
            break
        time.sleep(1)

    current_probe, current_ready = collect_api_probe(base_url)
    current_probe["autostarted"] = True
    current_probe["autostartLogPath"] = str(api_server_log_path)
    current_probe["autostartPid"] = api_server_proc.pid if api_server_proc else None
    current_probe["autostartCommand"] = list(api_server_command)
    current_probe["processAliveAtProof"] = (
        api_server_proc is not None and api_server_proc.poll() is None
    )
    current_probe["autostartBuildOutputPath"] = str(build_output_path)
    current_probe["autostartUsedNoBuild"] = False
    if api_server_proc is not None and api_server_proc.poll() is not None:
        current_probe["autostartExitCode"] = api_server_proc.returncode
    return current_probe, current_ready


def warm_api_surface_with_provenance(
    base_url: str, initial_probe: dict[str, object]
) -> tuple[dict[str, object], bool]:
    runtime_provenance = {
        key: initial_probe.get(key)
        for key in (
            "autostarted",
            "autostartCommand",
            "autostartLogPath",
            "autostartPid",
            "processAliveAtProof",
        )
    }
    warmed_probe, warmed_ready = warm_api_surface(base_url)
    warmed_probe.update(runtime_provenance)
    return warmed_probe, warmed_ready


def test_result_indicates_missing_api(trx_path: Path, output_text: str) -> bool:
    missing_api_tokens = (
        "Assert.Inconclusive failed. Chummer API runtime is not reachable",
        "Chummer API runtime socket error",
        "Chummer API runtime probe timed out",
    )
    if any(token in output_text for token in missing_api_tokens):
        return True
    if not trx_path.is_file():
        return False
    try:
        trx_text = read_regular_bytes(trx_path, "workflow-family diagnostic TRX").decode(
            "utf-8-sig"
        )
    except UnicodeError:
        return False
    return any(token in trx_text for token in missing_api_tokens)


api_probe_paths = ["/api/workspaces?maxCount=1", "/api/shell/bootstrap"]


api_base_url = CANONICAL_API_BASE_URL

api_probe, api_surface_ready = ensure_local_api(api_base_url)
if api_surface_ready:
    api_probe, api_surface_ready = warm_api_surface_with_provenance(
        api_base_url, api_probe
    )
test_process_env = canonical_subprocess_environment(
    {
        "CHUMMER_API_BASE_URL": api_base_url,
        "CHUMMER_WEB_BASE_URL": api_base_url,
        "CHUMMER_REPO_ROOT": str(repo_root),
        "CHUMMER_REQUIRE_DUAL_HEAD_RUNTIME": "1",
    }
)

if unique_tests:
    with lock_path.open("w", encoding="utf-8") as lock_handle:
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_EX)
        for project_label, project_path in test_build_projects:
            build_attempt_count += 1
            build_proc = subprocess.run(
                [
                    str(dotnet_host_path),
                    "build",
                    str(project_path),
                    "--configuration",
                    "Release",
                    "--nologo",
                    "--no-incremental",
                    "-p:UseSharedCompilation=false",
                    "-p:BuildInParallel=false",
                    "-maxcpucount:1",
                    "--no-restore",
                ],
                cwd=repo_root,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                check=False,
                env=test_process_env,
            )
            if build_proc.returncode != 0:
                output_lines = (build_proc.stdout or "").strip().splitlines()
                if output_lines:
                    run_error = output_lines[-1]
                if not run_error:
                    run_error = f"{project_label} release build failed"
                run_exit = int(build_proc.returncode)
                break

        test_runner_command: list[str] = []
        if run_exit == 0:
            if test_runner_dll.is_file():
                test_runner_command = [str(dotnet_host_path), str(test_runner_dll)]
            else:
                run_exit = 1
                run_error = f"freshly built test runner DLL was not found at {test_runner_dll}"

        observed_attempt_count = 0
        build_failed = run_exit != 0
        for index, test_name in enumerate(unique_tests, start=1):
            if build_failed:
                break
            safe_name = "".join(char if char.isalnum() or char in {"-", "_"} else "_" for char in test_name)
            per_test_trx = run_root / f"{index:02d}-{safe_name}.trx"
            per_test_trx_paths[test_name] = str(per_test_trx)
            proc = None
            fully_qualified_test_name = (
                f"{CANONICAL_TEST_CLASS_BY_NAME[test_name]}.{test_name}"
            )
            for attempt in range(1, max_test_attempts + 1):
                observed_attempt_count = max(observed_attempt_count, attempt)
                if per_test_trx.exists():
                    per_test_trx.unlink()
                attempt_started_at = now_iso()
                proc = subprocess.run(
                    test_runner_command + [
                        "--filter",
                        f"FullyQualifiedName={fully_qualified_test_name}",
                        "--results-directory",
                        str(run_root),
                        "--report-trx",
                        "--report-trx-filename",
                        per_test_trx.name,
                        "--output",
                        "Normal",
                    ],
                    cwd=repo_root,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    check=False,
                    env=test_process_env,
                )
                attempt_completed_at = now_iso()
                per_test_attempt_started_at[test_name] = attempt_started_at
                per_test_attempt_completed_at[test_name] = attempt_completed_at
                if proc.returncode == 0:
                    break
                if test_result_indicates_missing_api(per_test_trx, proc.stdout or ""):
                    api_probe, api_surface_ready = ensure_local_api(api_base_url)
                    if api_surface_ready:
                        api_probe, api_surface_ready = warm_api_surface_with_provenance(
                            api_base_url, api_probe
                        )

            if proc is None:
                raise SystemExit(f"workflow-family dotnet test process did not start for {test_name}")

            per_test_exit_codes[test_name] = int(proc.returncode)
            per_test_attempt_counts[test_name] = attempt

            output_lines = (proc.stdout or "").strip().splitlines()
            if output_lines:
                run_error = output_lines[-1]

            if proc.returncode != 0 and run_exit == 0:
                run_exit = int(proc.returncode)
        dotnet_attempt_count = observed_attempt_count
        fcntl.flock(lock_handle.fileno(), fcntl.LOCK_UN)
    if run_exit != 0 and not run_error:
        run_error = "dotnet test failed"

test_execution_records: dict[str, dict[str, object]] = {}
resolved_trx_paths = [str(Path(value).resolve()) for value in per_test_trx_paths.values()]
if len(resolved_trx_paths) != len(set(resolved_trx_paths)):
    raise SystemExit("distinct workflow tests must use distinct TRX paths")
for test_name, trx_candidate in per_test_trx_paths.items():
    trx_file = Path(trx_candidate)
    if not trx_file.is_file():
        test_execution_records[test_name] = {
            "testName": test_name,
            "exitCode": per_test_exit_codes.get(test_name),
            "attemptCount": per_test_attempt_counts.get(test_name, 0),
            "outcomes": [],
            "resultCount": 0,
            "unexpectedTestNames": [],
            "trx": {"path": str(trx_file.resolve()), "missing": True},
        }
        continue
    trx_binding = file_binding(trx_file, f"TRX for {test_name}")
    try:
        validated_trx = validate_trx_contract(
            trx_file,
            test_name,
            trx_binding,
            run_root,
            per_test_attempt_started_at.get(test_name),
            per_test_attempt_completed_at.get(test_name),
        )
    except ValueError as exc:
        test_execution_records[test_name] = {
            "testName": test_name,
            "exitCode": per_test_exit_codes.get(test_name),
            "attemptCount": per_test_attempt_counts.get(test_name, 0),
            "outcomes": [],
            "resultCount": 0,
            "unexpectedTestNames": [],
            "trxValidationError": str(exc),
            "summaryValid": False,
            "trx": trx_binding,
        }
        continue
    test_execution_records[test_name] = {
        "testName": test_name,
        "testMethodClassName": validated_trx["className"],
        "testId": validated_trx["testId"],
        "trxRunId": validated_trx["trxRunId"],
        "executionId": validated_trx["executionId"],
        "attemptStartedAt": validated_trx["attemptStartedAt"],
        "attemptCompletedAt": validated_trx["attemptCompletedAt"],
        "trxStartedAt": validated_trx["trxStartedAt"],
        "trxCompletedAt": validated_trx["trxCompletedAt"],
        "resultStartedAt": validated_trx["resultStartedAt"],
        "resultCompletedAt": validated_trx["resultCompletedAt"],
        "exitCode": per_test_exit_codes.get(test_name),
        "attemptCount": per_test_attempt_counts.get(test_name, 0),
        "outcomes": [validated_trx["outcome"]],
        "resultCount": 1,
        "unexpectedTestNames": [],
        "summaryOutcome": validated_trx["summaryOutcome"],
        "counters": validated_trx["counters"],
        "summaryValid": True,
        "trx": trx_binding,
    }

if not api_surface_ready and not any(
    record.get("resultCount") for record in test_execution_records.values()
):
    external_blocker = "missing_api_surface_contract"

try:
    validate_api_probe_contract(api_probe, dotnet_host_path, api_project_path)
except ValueError as exc:
    run_exit = run_exit or 1
    run_error = f"canonical API runtime proof is invalid: {exc}"

current_source_bindings = [
    file_binding(path, "workflow parity test source") for path in test_source_paths
]
if current_source_bindings != source_bindings:
    run_exit = run_exit or 1
    run_error = "workflow parity test sources changed during execution"

current_ledger_binding = file_binding(ledger_path, "workflow parity ledger")
current_oracle_binding = file_binding(oracle_path, "workflow parity oracle")
current_release_binding = file_binding(release_channel_path, "release channel receipt")
ledger_binding = {
    "path": str(ledger_path.resolve()),
    "sha256": hashlib.sha256(ledger_bytes).hexdigest(),
    "sizeBytes": len(ledger_bytes),
}
oracle_binding = {
    "path": str(oracle_path.resolve()),
    "sha256": hashlib.sha256(oracle_bytes).hexdigest(),
    "sizeBytes": len(oracle_bytes),
}
release_binding = {
    "path": str(release_channel_path.resolve()),
    "sha256": hashlib.sha256(release_channel_bytes).hexdigest(),
    "sizeBytes": len(release_channel_bytes),
}
if current_ledger_binding != ledger_binding:
    run_exit = run_exit or 1
    run_error = "workflow parity ledger changed during execution"
if current_oracle_binding != oracle_binding:
    run_exit = run_exit or 1
    run_error = "workflow parity oracle changed during execution"
if current_release_binding != release_binding:
    run_exit = run_exit or 1
    run_error = "release channel receipt changed during execution"

if test_runner_dll.is_file():
    test_assembly_binding = file_binding(test_runner_dll, "workflow parity test assembly")
else:
    test_assembly_binding = {
        "path": str(test_runner_dll.resolve()),
        "missing": True,
    }
build_output_bindings = (
    {
        label: snapshot_output_tree(root, f"{label} release build output")
        for label, root in build_output_roots.items()
    }
    if run_exit == 0
    else {}
)
candidate_identity = {
    "edition": edition,
    "ledger": ledger_binding,
    "oracle": oracle_binding,
    "testSources": source_bindings,
    "trxContractSource": trx_contract_source_binding,
    "buildProjects": build_project_bindings,
    "apiProject": api_project_binding,
    "toolchain": dotnet_host_binding,
    "buildOutputs": build_output_bindings,
    "testAssembly": test_assembly_binding,
}
candidate_digest = hashlib.sha256(
    json.dumps(
        {"releaseIdentity": release_identity, "candidateIdentity": candidate_identity},
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
).hexdigest()
workflow_epoch_id = hashlib.sha256(
    json.dumps(
        {
            "releaseIdentity": release_identity,
            "testSources": source_bindings,
            "trxContractSource": trx_contract_source_binding,
            "buildProjects": build_project_bindings,
            "apiProject": api_project_binding,
            "toolchain": dotnet_host_binding,
            "buildOutputs": build_output_bindings,
            "testAssembly": test_assembly_binding,
        },
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
).hexdigest()
run_generated_at = now_iso()
execution_run_digest = execution_run_digest_for(
    edition=edition,
    producer_run_id=producer_run_id,
    execution_started_at=run_started_at,
    execution_completed_at=run_generated_at,
    release_identity=release_identity,
    candidate_digest=candidate_digest,
    test_execution_records=test_execution_records,
    api_probe=api_probe,
)

sr4_oracle_families = {str(value).strip() for value in (oracle.get("workflowFamilies") or []) if str(value).strip()}
sr6_oracle_map = {
    str(item.get("id") or "").strip(): item
    for item in (oracle.get("requiredFamilies") or [])
    if isinstance(item, dict) and str(item.get("id") or "").strip()
}

execution_signal_tokens = (
    "save",
    "workflow",
    "execute",
    "dialog",
    "download",
    "export",
    "print",
    "roundtrip",
    "click",
)
execution_optional_family_ids = {
    "improvements-explain-result-parity",
}

any_fail = False
pending_outputs: list[tuple[Path, dict]] = []
seen_output_paths: set[Path] = set()
for family in families:
    family_id = str(family.get("id") or "").strip()
    if not family_id:
        continue

    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    output_refs = [str(value).strip() for value in (family.get("executionReceipts") or []) if str(value).strip()]
    if not output_refs:
        output_refs = [
            f".codex-studio/published/workflow-family-parity/executed/{edition}/{family_id}.generated.json"
        ]

    reasons = []
    if str(family.get("status") or "").strip().lower() != "ready":
        reasons.append(f"Ledger family is not ready: {family.get('status') or 'missing'}")
    if not audit_tests:
        reasons.append("Missing auditTests for family.")
    elif family_id not in execution_optional_family_ids and not any(
        any(token in test_name.lower() for token in execution_signal_tokens)
        for test_name in audit_tests
    ):
        reasons.append("Audit tests do not include any execution-oriented workflow proof.")

    oracle_detail: dict[str, object] = {}
    if edition == "sr4":
        if family_id not in sr4_oracle_families:
            reasons.append(f"Family is missing from SR4 oracle workflowFamilies: {family_id}")
        source_repo = dict(oracle.get("sourceRepo") or {})
        oracle_detail = {
            "sourceRepoPath": str(source_repo.get("path") or ""),
            "sourceRepoHead": str(source_repo.get("head") or ""),
        }
    else:
        oracle_entry = sr6_oracle_map.get(family_id)
        if not oracle_entry:
            reasons.append(f"Family is missing from SR6 carry-forward oracle requiredFamilies: {family_id}")
        else:
            oracle_detail = {
                "classification": str(oracle_entry.get("classification") or ""),
                "rationale": str(oracle_entry.get("rationale") or ""),
                "releaseGateTests": [str(value).strip() for value in (oracle_entry.get("releaseGateTests") or []) if str(value).strip()],
            }

    missing_tests: list[str] = []
    failed_tests: dict[str, list[str]] = {}
    passed_tests: list[str] = []
    for test_name in audit_tests:
        record = test_execution_records.get(test_name)
        outcomes = list(record.get("outcomes") or []) if record else []
        if not record or not outcomes:
            missing_tests.append(test_name)
            continue
        if (
            type(record.get("exitCode")) is not int
            or record.get("exitCode") != 0
            or record.get("attemptCount") != 1
            or record.get("resultCount") != 1
            or record.get("unexpectedTestNames")
            or record.get("summaryValid") is not True
            or outcomes != ["Passed"]
            or not isinstance(record.get("trx"), dict)
            or not record["trx"].get("sha256")
            or type(record["trx"].get("sizeBytes")) is not int
            or record["trx"].get("sizeBytes", 0) <= 0
        ):
            failed_tests[test_name] = outcomes
        else:
            passed_tests.append(test_name)

    if missing_tests:
        reasons.append("Audit tests not present in executed TRX results: " + ", ".join(missing_tests))
    if failed_tests:
        reasons.append(
            "Audit tests did not pass in executed TRX results: "
            + ", ".join(f"{name}={','.join(values)}" for name, values in sorted(failed_tests.items()))
        )
    if run_exit != 0:
        reasons.append(f"dotnet test execution failed (exit {run_exit}): {run_error or 'see TRX/log output'}")
    if external_blocker:
        reasons.append(
            "Dual-head workflow execution requires a chummer-api host exposing /api/workspaces and /api/shell/bootstrap "
            "(external blocker: missing_api_surface_contract)."
        )

    payload = {
        "schemaVersion": SCHEMA_VERSION,
        "producerRunId": producer_run_id,
        "candidateSnapshotId": workflow_epoch_id,
        "workflowEpochId": workflow_epoch_id,
        "executionRunDigest": execution_run_digest,
        "generatedAt": run_generated_at,
        "contract_name": contract_name,
        "status": "pass" if not reasons else "fail",
        "summary": (
            f"{edition.upper()} workflow-family execution evidence is explicitly grounded for {family_id}."
            if not reasons
            else f"{edition.upper()} workflow-family execution evidence is incomplete for {family_id}."
        ),
        "reasons": reasons,
        "evidence": {
            "edition": edition,
            "familyId": family_id,
            "proofKind": proof_kind,
            "producerRunId": producer_run_id,
            "candidateSnapshotId": workflow_epoch_id,
            "workflowEpochId": workflow_epoch_id,
            "executionRunDigest": execution_run_digest,
            "runStartedAt": run_started_at,
            "runCompletedAt": run_generated_at,
            "releaseIdentity": release_identity,
            "candidateIdentity": candidate_identity,
            "candidateDigest": candidate_digest,
            "ledgerPath": str(ledger_path),
            "oraclePath": str(oracle_path),
            "sourceBindings": source_bindings,
            "testAssembly": test_assembly_binding,
            "auditTests": audit_tests,
            "oracle": oracle_detail,
            "dotnetTest": {
                "project": "Chummer.Tests/Chummer.Tests.csproj",
                "configuration": "Release",
                "buildAttemptCount": build_attempt_count,
                "runnerCommand": test_runner_command,
                "perTestTrxPaths": {
                    test_name: per_test_trx_paths[test_name]
                    for test_name in audit_tests
                    if test_name in per_test_trx_paths
                },
                "exitCode": run_exit,
                "attemptCount": dotnet_attempt_count,
                "maxAttempts": max_test_attempts,
            },
            "apiProbe": api_probe,
            "external_blocker": external_blocker,
            "testExecutions": {
                test_name: test_execution_records.get(test_name, {})
                for test_name in audit_tests
            },
            "matchedPassedTests": passed_tests,
            "missingAuditTests": missing_tests,
            "failedAuditTests": failed_tests,
        },
    }

    for output_ref in output_refs:
        output_ref = output_ref.replace("{familyId}", family_id)
        output_ref = output_ref.replace(
            "workflow-family-parity/execution/",
            "workflow-family-parity/executed/",
        )
        output_path = Path(output_ref)
        if not output_path.is_absolute():
            output_path = repo_root / output_path
        normalized_output_path = output_path.resolve(strict=False)
        if normalized_output_path in seen_output_paths:
            raise SystemExit(f"duplicate workflow-family execution receipt target: {output_path}")
        seen_output_paths.add(normalized_output_path)
        pending_outputs.append((output_path, payload))

    if reasons:
        any_fail = True

if [file_binding(path, "workflow parity test source") for path in test_source_paths] != source_bindings:
    raise SystemExit("workflow parity test sources changed before receipt publication")
if file_binding(
    trx_contract_source_path, "workflow-family TRX validator source"
) != trx_contract_source_binding:
    raise SystemExit("workflow-family TRX validator source changed before receipt publication")
if [
    file_binding(project_path, f"{project_label} project contract")
    for project_label, project_path in test_build_projects
] != build_project_bindings:
    raise SystemExit("workflow parity build project contracts changed before receipt publication")
if file_binding(api_project_path, "canonical API autostart project") != api_project_binding:
    raise SystemExit("canonical API autostart project changed before receipt publication")
if file_binding(dotnet_host_path, "canonical dotnet host") != dotnet_host_binding:
    raise SystemExit("canonical dotnet host changed before receipt publication")
if file_binding(ledger_path, "workflow parity ledger") != ledger_binding:
    raise SystemExit("workflow parity ledger changed before receipt publication")
if file_binding(oracle_path, "workflow parity oracle") != oracle_binding:
    raise SystemExit("workflow parity oracle changed before receipt publication")
if file_binding(release_channel_path, "release channel receipt") != release_binding:
    raise SystemExit("release channel receipt changed before receipt publication")
if not test_assembly_binding.get("missing") and file_binding(
    test_runner_dll, "workflow parity test assembly"
) != test_assembly_binding:
    raise SystemExit("workflow parity test assembly changed before receipt publication")
if build_output_bindings and {
    label: snapshot_output_tree(root, f"{label} release build output")
    for label, root in build_output_roots.items()
} != build_output_bindings:
    raise SystemExit("workflow parity release build outputs changed before receipt publication")
for test_name, record in test_execution_records.items():
    trx_binding = record.get("trx")
    if isinstance(trx_binding, dict) and trx_binding.get("sha256"):
        trx_path = Path(str(trx_binding.get("path")))
        if file_binding(trx_path, f"TRX for {test_name}") != trx_binding:
            raise SystemExit(f"TRX changed before receipt publication: {test_name}")
        if record.get("summaryValid") is True:
            try:
                final_validated_trx = validate_trx_contract(
                    trx_path,
                    test_name,
                    trx_binding,
                    run_root,
                    record.get("attemptStartedAt"),
                    record.get("attemptCompletedAt"),
                )
                validate_trx_record_contract(record, final_validated_trx)
            except ValueError as exc:
                raise SystemExit(
                    f"TRX failed final contract validation for {test_name}: {exc}"
                ) from exc

for output_path, payload in pending_outputs:
    atomic_write_json(output_path, payload)

expected_stage_receipts = {
    str(payload["evidence"]["familyId"]): output_path
    for output_path, payload in pending_outputs
}
stage_receipt_records = [
    workflow_stage_receipt_record(output_path, payload)
    for output_path, payload in pending_outputs
]
stage_manifest_path = workflow_stage_manifest_path(repo_root, edition, "execution")
stage_manifest = build_workflow_stage_manifest(
    edition=edition,
    stage="execution",
    status="fail" if any_fail else "pass",
    generated_at=run_generated_at,
    producer_run_id=producer_run_id,
    candidate_snapshot_id=workflow_epoch_id,
    execution_run_digest=execution_run_digest,
    execution_started_at=run_started_at,
    execution_completed_at=run_generated_at,
    candidate_digest=candidate_digest,
    release_identity=release_identity,
    receipt_records=stage_receipt_records,
    upstream_stage_manifests=[],
)
atomic_write_json(stage_manifest_path, stage_manifest)
validate_workflow_stage_manifest(
    manifest_path=stage_manifest_path,
    repo_root=repo_root,
    edition=edition,
    stage="execution",
    expected_receipts=expected_stage_receipts,
    expected_release_identity=release_identity,
    expected_upstream_stage_manifests=[],
    require_pass=not any_fail,
)

if any_fail:
    terminate_local_api()
    raise SystemExit(43)

terminate_local_api()
PY

echo "[materialize-${edition}-workflow-family-execution-receipts] PASS"
