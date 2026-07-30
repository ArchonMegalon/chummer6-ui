#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
edition="${1:-}"

case "$edition" in
  sr4)
    ledger_path="$repo_root/docs/SR4_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/CHUMMER4_SR4_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr4_workflow_family_verification_receipt"
    proof_kind="sr4_family_oracle"
    ;;
  sr6)
    ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
    oracle_path="$repo_root/docs/SR6_DESKTOP_WORKFLOW_PARITY_ORACLE.json"
    contract_name="chummer6-ui.sr6_workflow_family_verification_receipt"
    proof_kind="sr6_family_carry_forward"
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

import hashlib
import json
import os
import stat
import sys
import tempfile
import uuid
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
    build_workflow_stage_manifest,
    snapshot_output_tree,
    validate_api_probe_contract,
    validate_trx_contract,
    validate_trx_record_contract,
    validate_workflow_stage_manifest,
    workflow_stage_manifest_path,
    workflow_stage_receipt_record,
)
expected_execution_proof_kind = "sr4_family_oracle" if edition == "sr4" else "sr6_family_release_gated_execution"
expected_execution_contract = f"chummer6-ui.{edition}_workflow_family_execution_receipt"
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


def parse_strict_timestamp(value: object, label: str) -> str:
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        raise ValueError(f"{label} must be a nonblank canonical offset timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ValueError(f"{label} is not an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ValueError(f"{label} must include a UTC offset")
    delta_seconds = (datetime.now(timezone.utc) - parsed.astimezone(timezone.utc)).total_seconds()
    if delta_seconds > RECEIPT_MAX_AGE_SECONDS:
        raise ValueError(f"{label} is stale ({int(delta_seconds)}s old)")
    if delta_seconds < -MAX_FUTURE_SKEW_SECONDS:
        raise ValueError(f"{label} is too far in the future ({int(-delta_seconds)}s ahead)")
    return value


def read_regular_bytes(path: Path, label: str) -> bytes:
    if path.is_symlink():
        raise ValueError(f"{label} must not be a symlink: {path}")
    try:
        fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    except OSError as exc:
        raise ValueError(f"{label} is missing or unreadable: {path}: {exc}") from exc
    try:
        before = os.fstat(fd)
        if not stat.S_ISREG(before.st_mode):
            raise ValueError(f"{label} is not a regular file: {path}")
        if before.st_size > MAX_REGULAR_INPUT_BYTES:
            raise ValueError(f"{label} exceeds the {MAX_REGULAR_INPUT_BYTES}-byte safety limit: {path}")
        chunks = []
        total_bytes = 0
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            total_bytes += len(chunk)
            if total_bytes > MAX_REGULAR_INPUT_BYTES:
                raise ValueError(
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
        raise ValueError(f"{label} changed while it was being read: {path}")
    return data


def load_regular_json(path: Path, label: str) -> tuple[dict, bytes]:
    raw = read_regular_bytes(path, label)
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"{label} is not valid JSON: {path}") from exc
    if not isinstance(payload, dict):
        raise ValueError(f"{label} root must be an object: {path}")
    return payload, raw


def binding_for_bytes(path: Path, raw: bytes) -> dict[str, object]:
    return {"path": str(path.resolve()), "sha256": hashlib.sha256(raw).hexdigest(), "sizeBytes": len(raw)}


def file_binding(path: Path, label: str) -> dict[str, object]:
    return binding_for_bytes(path, read_regular_bytes(path, label))


def workflow_epoch_id_for(
    release_identity_value: dict[str, object],
    candidate_identity_value: dict[str, object],
) -> str:
    shared_candidate = {
        key: candidate_identity_value.get(key)
        for key in (
            "testSources",
            "trxContractSource",
            "buildProjects",
            "apiProject",
            "toolchain",
            "buildOutputs",
            "testAssembly",
        )
    }
    return hashlib.sha256(
        json.dumps(
            {
                "releaseIdentity": release_identity_value,
                **shared_candidate,
            },
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def atomic_write_json(path: Path, payload: dict) -> None:
    if path.is_symlink():
        raise SystemExit(f"refusing to replace symlink receipt path: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(payload, indent=2) + "\n").encode("utf-8")
    fd, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
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


def validate_bound_file(binding: object, label: str, expected_path: Path) -> None:
    if not isinstance(binding, dict):
        raise ValueError(f"{label} binding must be an object")
    bound_path = binding.get("path")
    if not isinstance(bound_path, str) or not Path(bound_path).is_absolute():
        raise ValueError(f"{label} binding path must be absolute")
    expected_resolved = expected_path.resolve()
    if Path(bound_path) != expected_resolved:
        raise ValueError(f"{label} binding path is misplaced: {bound_path}")
    if binding != file_binding(expected_resolved, label):
        raise ValueError(f"{label} binding does not match current bytes")


ledger, ledger_bytes = load_regular_json(ledger_path, "workflow parity ledger")
oracle, oracle_bytes = load_regular_json(oracle_path, "workflow parity oracle")
release_channel, release_channel_bytes = load_regular_json(release_channel_path, "release channel receipt")
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
if ledger.get("scope") != f"{edition}_desktop_head":
    raise SystemExit(f"workflow parity ledger scope must equal {edition}_desktop_head")
families = ledger.get("requiredFamilies")
if not isinstance(families, list) or not families or not all(isinstance(item, dict) for item in families):
    raise SystemExit("workflow parity ledger requiredFamilies must be a non-empty object array")
family_ids = [item.get("id") for item in families]
if any(not isinstance(value, str) or not value or value != value.strip() for value in family_ids):
    raise SystemExit("workflow parity ledger family IDs must be canonical nonblank strings")
if len(family_ids) != len(set(family_ids)) or set(family_ids) != CANONICAL_FAMILY_IDS:
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
    if family.get("executionReceipts") != [f".codex-studio/published/workflow-family-parity/executed/{edition}/{{familyId}}.generated.json"]:
        raise SystemExit(f"workflow parity ledger family {family_id} executionReceipts target is not canonical")
    if family.get("verificationReceipts") != [f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"]:
        raise SystemExit(f"workflow parity ledger family {family_id} verificationReceipts target is not canonical")
    if family.get("parityReceipts") != [f".codex-studio/published/workflow-family-parity/{edition.upper()}_WORKFLOW_FAMILY_{family_id}.generated.json"]:
        raise SystemExit(f"workflow parity ledger family {family_id} parityReceipts target is not canonical")
if type(oracle.get("version")) is not int or oracle.get("version") != 1:
    raise SystemExit("workflow parity oracle version must be integer 1")
if oracle.get("scope") != f"{edition}_desktop_head":
    raise SystemExit(f"workflow parity oracle scope must equal {edition}_desktop_head")
if edition == "sr4":
    oracle_family_ids = oracle.get("workflowFamilies")
else:
    raw_oracle_families = oracle.get("requiredFamilies")
    oracle_family_ids = (
        [item.get("id") for item in raw_oracle_families]
        if isinstance(raw_oracle_families, list)
        and all(isinstance(item, dict) for item in raw_oracle_families)
        else []
    )
if (
    not isinstance(oracle_family_ids, list)
    or any(not isinstance(value, str) or not value or value != value.strip() for value in oracle_family_ids)
    or len(oracle_family_ids) != len(set(oracle_family_ids))
    or set(oracle_family_ids) != CANONICAL_FAMILY_IDS
):
    raise SystemExit("workflow parity oracle canonical family inventory is not exact")

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
try:
    release_generated_at = parse_strict_timestamp(
        release_generated_at_value or release_generated_at_alias,
        "release channel generatedAt",
    )
except ValueError as exc:
    raise SystemExit(str(exc)) from exc
release_identity = {
    "channelId": channel_id,
    "releaseVersion": release_version,
    "generatedAt": release_generated_at,
    **binding_for_bytes(release_channel_path, release_channel_bytes),
}
ledger_binding = binding_for_bytes(ledger_path, ledger_bytes)
oracle_binding = binding_for_bytes(oracle_path, oracle_bytes)
test_source_paths = [
    repo_root / "Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs",
    repo_root / "Chummer.Tests/Compliance/MigrationComplianceTests.cs",
    repo_root / "Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs",
    repo_root / "Chummer.Tests/Presentation/WorkflowParityGateTests.cs",
]
test_source_bindings = [file_binding(path, "workflow parity test source") for path in test_source_paths]
trx_contract_source_binding = file_binding(
    trx_contract_source_path, "workflow-family TRX validator source"
)
test_corpus = "\n".join(read_regular_bytes(path, "workflow parity test source").decode("utf-8") for path in test_source_paths)
test_assembly_path = repo_root / "Chummer.Tests/bin/Release/net10.0/Chummer.Tests.dll"
dotnet_host_path = Path("/usr/bin/dotnet").resolve(strict=True)
if not str(dotnet_host_path).startswith("/usr/"):
    raise SystemExit("canonical dotnet host must resolve under /usr")
dotnet_host_binding = file_binding(dotnet_host_path, "canonical dotnet host")
test_build_projects = [
    ("Chummer.Avalonia", repo_root / "Chummer.Avalonia/Chummer.Avalonia.csproj"),
    ("Chummer.Portal", repo_root / "Chummer.Portal/Chummer.Portal.csproj"),
    ("Chummer.Tests", repo_root / "Chummer.Tests/Chummer.Tests.csproj"),
]
build_project_bindings = [
    file_binding(project_path, f"{project_label} project contract")
    for project_label, project_path in test_build_projects
]
api_project_path = repo_root / "Chummer.Api/Chummer.Api.csproj"
api_project_binding = file_binding(api_project_path, "canonical API autostart project")
build_output_roots = {
    "Chummer.Api": repo_root / "Chummer.Api/bin/Release/net10.0",
    "Chummer.Avalonia": repo_root / "Chummer.Avalonia/bin/Release/net10.0",
    "Chummer.Portal": repo_root / "Chummer.Portal/bin/Release/net10.0",
    "Chummer.Tests": repo_root / "Chummer.Tests/bin/Release/net10.0",
}
build_output_bindings = {
    label: snapshot_output_tree(root, f"{label} release build output")
    for label, root in build_output_roots.items()
}

expected_execution_stage_receipts = {
    str(family["id"]): (
        repo_root
        / ".codex-studio/published/workflow-family-parity/executed"
        / edition
        / f"{family['id']}.generated.json"
    )
    for family in families
}
execution_stage_manifest_path = workflow_stage_manifest_path(
    repo_root, edition, "execution"
)
execution_stage_manifest_error = ""
execution_stage_manifest: dict[str, object] = {}
execution_stage_manifest_binding: dict[str, object] = {}
try:
    execution_stage_manifest_result = validate_workflow_stage_manifest(
        manifest_path=execution_stage_manifest_path,
        repo_root=repo_root,
        edition=edition,
        stage="execution",
        expected_receipts=expected_execution_stage_receipts,
        expected_release_identity=release_identity,
        expected_upstream_stage_manifests=[],
    )
    execution_stage_manifest = execution_stage_manifest_result["manifest"]
    execution_stage_manifest_binding = execution_stage_manifest_result["binding"]
except (ValueError, OSError) as exc:
    execution_stage_manifest_error = str(exc)

sr4_oracle_families = {str(value).strip() for value in (oracle.get("workflowFamilies") or []) if str(value).strip()}
sr6_oracle_map = {
    str(item.get("id") or "").strip(): item
    for item in (oracle.get("requiredFamilies") or [])
    if isinstance(item, dict) and str(item.get("id") or "").strip()
}

any_fail = False
pending_outputs: list[tuple[Path, dict]] = []
observed_run_ids: set[str] = set()
observed_candidate_digests: set[str] = set()
observed_workflow_epoch_ids: set[str] = set()
observed_execution_run_digests: set[str] = set()
observed_execution_started_at: set[str] = set()
observed_execution_completed_at: set[str] = set()
seen_output_paths: set[Path] = set()
for family in families:
    family_id = str(family.get("id") or "").strip()
    if not family_id:
        continue

    reasons = []
    if execution_stage_manifest_error:
        reasons.append(
            "Execution epoch manifest is missing or invalid: "
            + execution_stage_manifest_error
        )
    status = str(family.get("status") or "").strip().lower()
    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    verification_receipts = [
        str(value).strip() for value in (family.get("verificationReceipts") or []) if str(value).strip()
    ]
    execution_receipts = [
        str(value).strip() for value in (family.get("executionReceipts") or []) if str(value).strip()
    ]

    if status != "ready":
        reasons.append(f"Ledger family is not ready: {status or 'missing'}")
    if not audit_tests:
        reasons.append("Missing auditTests for family.")
    else:
        unresolved = [name for name in audit_tests if name not in test_corpus]
        if unresolved:
            reasons.append("Missing executable test references: " + ", ".join(unresolved))
    if not execution_receipts:
        reasons.append(
            "Missing executionReceipts for family. Verification receipts must be backed by executed family-scoped proof."
        )
    if not verification_receipts:
        reasons.append("Missing verificationReceipts for family.")

    oracle_detail = {}
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
            classification = str(oracle_entry.get("classification") or "").strip()
            rationale = str(oracle_entry.get("rationale") or "").strip()
            release_gate_tests = [
                str(value).strip() for value in (oracle_entry.get("releaseGateTests") or []) if str(value).strip()
            ]
            if not classification:
                reasons.append("SR6 carry-forward oracle entry is missing classification.")
            if not rationale:
                reasons.append("SR6 carry-forward oracle entry is missing rationale.")
            if not release_gate_tests:
                reasons.append("SR6 carry-forward oracle entry is missing releaseGateTests.")
            oracle_detail = {
                "classification": classification,
                "rationale": rationale,
                "releaseGateTests": release_gate_tests,
            }

    if not verification_receipts:
        verification_receipts = [
            f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"
        ]

    expected_execution_ref = f".codex-studio/published/workflow-family-parity/executed/{edition}/{{familyId}}.generated.json"
    expected_verification_ref = f".codex-studio/published/workflow-family-parity/{edition}/{family_id}.generated.json"
    if execution_receipts != [expected_execution_ref]:
        reasons.append("executionReceipts must contain the exact canonical family execution target")
    if verification_receipts != [expected_verification_ref]:
        reasons.append("verificationReceipts must contain the exact canonical family verification target")

    execution_failures = []
    validated_execution_receipts = []
    upstream_execution_bindings = []
    execution_external_blockers = []
    producer_run_id = ""
    candidate_identity: dict[str, object] = {}
    candidate_digest = ""
    workflow_epoch_id = ""
    candidate_snapshot_id = ""
    execution_run_digest = ""
    execution_started_at = ""
    execution_completed_at = ""
    for execution_ref in execution_receipts:
        execution_ref = execution_ref.replace("{familyId}", family_id)
        execution_path = Path(execution_ref)
        if not execution_path.is_absolute():
            execution_path = repo_root / execution_path
        try:
            execution_data, execution_bytes = load_regular_json(execution_path, "execution receipt")
        except ValueError as exc:
            execution_failures.append(f"{execution_path} ({exc})")
            continue
        execution_status = execution_data.get("status")
        execution_evidence = execution_data.get("evidence")
        if not isinstance(execution_evidence, dict):
            execution_failures.append(f"{execution_path} (evidence must be an object)")
            continue
        execution_external_blocker = str(execution_evidence.get("external_blocker") or "").strip().lower()
        if execution_status != "pass":
            if execution_external_blocker:
                execution_external_blockers.append(execution_external_blocker)
                execution_failures.append(
                    f"{execution_path} ({execution_status or 'missing status'}; external_blocker={execution_external_blocker})"
                )
            else:
                execution_failures.append(f"{execution_path} ({execution_status or 'missing status'})")
            continue
        try:
            if type(execution_data.get("schemaVersion")) is not int or execution_data.get("schemaVersion") != SCHEMA_VERSION:
                raise ValueError("schemaVersion must equal integer 1")
            if execution_data.get("contract_name") != expected_execution_contract:
                raise ValueError("contract_name does not identify the execution contract")
            parse_strict_timestamp(execution_data.get("generatedAt"), "execution receipt generatedAt")
            producer_run_id = execution_data.get("producerRunId")
            if not isinstance(producer_run_id, str) or str(uuid.UUID(producer_run_id)) != producer_run_id:
                raise ValueError("producerRunId must be a canonical UUID")
            if execution_evidence.get("producerRunId") != producer_run_id:
                raise ValueError("evidence producerRunId does not match the receipt")
            if execution_evidence.get("releaseIdentity") != release_identity:
                raise ValueError("releaseIdentity does not match the selected release receipt")
            candidate_identity = execution_evidence.get("candidateIdentity")
            if not isinstance(candidate_identity, dict):
                raise ValueError("candidateIdentity must be an object")
            expected_assembly_binding = file_binding(test_assembly_path, "workflow parity test assembly")
            expected_candidate_identity = {
                "edition": edition,
                "ledger": ledger_binding,
                "oracle": oracle_binding,
                "testSources": test_source_bindings,
                "trxContractSource": trx_contract_source_binding,
                "buildProjects": build_project_bindings,
                "apiProject": api_project_binding,
                "toolchain": dotnet_host_binding,
                "buildOutputs": build_output_bindings,
                "testAssembly": expected_assembly_binding,
            }
            if candidate_identity != expected_candidate_identity:
                raise ValueError("candidateIdentity does not match current source, ledger, oracle, and assembly bytes")
            candidate_digest = execution_evidence.get("candidateDigest")
            expected_candidate_digest = hashlib.sha256(
                json.dumps(
                    {"releaseIdentity": release_identity, "candidateIdentity": expected_candidate_identity},
                    sort_keys=True,
                    separators=(",", ":"),
                ).encode("utf-8")
            ).hexdigest()
            if candidate_digest != expected_candidate_digest:
                raise ValueError("candidateDigest is invalid")
            workflow_epoch_id = execution_data.get("workflowEpochId")
            candidate_snapshot_id = execution_data.get("candidateSnapshotId")
            if (
                not isinstance(workflow_epoch_id, str)
                or len(workflow_epoch_id) != 64
                or any(character not in "0123456789abcdef" for character in workflow_epoch_id)
                or workflow_epoch_id
                != workflow_epoch_id_for(release_identity, expected_candidate_identity)
                or execution_evidence.get("workflowEpochId") != workflow_epoch_id
                or candidate_snapshot_id != workflow_epoch_id
                or execution_evidence.get("candidateSnapshotId")
                != candidate_snapshot_id
            ):
                raise ValueError("candidateSnapshotId/workflowEpochId is invalid")
            execution_run_digest = execution_data.get("executionRunDigest")
            execution_started_at = execution_evidence.get("runStartedAt")
            execution_completed_at = execution_evidence.get("runCompletedAt")
            if (
                not isinstance(execution_run_digest, str)
                or len(execution_run_digest) != 64
                or any(
                    character not in "0123456789abcdef"
                    for character in execution_run_digest
                )
                or execution_evidence.get("executionRunDigest")
                != execution_run_digest
                or execution_stage_manifest.get("producerRunId")
                != producer_run_id
                or execution_stage_manifest.get("candidateSnapshotId")
                != candidate_snapshot_id
                or execution_stage_manifest.get("executionRunDigest")
                != execution_run_digest
                or execution_stage_manifest.get("candidateDigest")
                != candidate_digest
                or execution_stage_manifest.get("executionStartedAt")
                != execution_started_at
                or execution_stage_manifest.get("executionCompletedAt")
                != execution_completed_at
            ):
                raise ValueError("executionRunDigest does not match the committed execution manifest")
            if execution_evidence.get("sourceBindings") != test_source_bindings:
                raise ValueError("sourceBindings do not match candidateIdentity")
            if execution_evidence.get("testAssembly") != expected_assembly_binding:
                raise ValueError("testAssembly does not match candidateIdentity")
            validate_api_probe_contract(
                execution_evidence.get("apiProbe"),
                dotnet_host_path,
                api_project_path,
            )
            if execution_evidence.get("auditTests") != audit_tests:
                raise ValueError("execution auditTests do not match the ledger family")
            test_executions = execution_evidence.get("testExecutions")
            if not isinstance(test_executions, dict) or set(test_executions) != set(audit_tests):
                raise ValueError("testExecutions must exactly cover the ledger auditTests")
            expected_run_root = (
                repo_root / ".codex-studio/out/workflow-family-parity/executed" / edition / producer_run_id
            ).resolve()
            dotnet_test = execution_evidence.get("dotnetTest")
            if not isinstance(dotnet_test, dict):
                raise ValueError("dotnetTest must be an object")
            per_test_trx_paths = dotnet_test.get("perTestTrxPaths")
            if not isinstance(per_test_trx_paths, dict) or set(per_test_trx_paths) != set(audit_tests):
                raise ValueError("dotnetTest perTestTrxPaths must exactly cover auditTests")
            observed_trx_paths: set[str] = set()
            for test_name in audit_tests:
                record = test_executions.get(test_name)
                if not isinstance(record, dict) or record.get("testName") != test_name:
                    raise ValueError(f"test execution identity is invalid for {test_name}")
                if type(record.get("exitCode")) is not int or record.get("exitCode") != 0:
                    raise ValueError(f"test execution exit is not zero for {test_name}")
                if record.get("attemptCount") != 1:
                    raise ValueError(f"test execution was not a first-attempt pass for {test_name}")
                if record.get("outcomes") != ["Passed"] or record.get("resultCount") != 1:
                    raise ValueError(f"test execution outcome is not exactly Passed for {test_name}")
                if record.get("unexpectedTestNames") != []:
                    raise ValueError(f"TRX contains unrelated or substring-only results for {test_name}")
                if (
                    record.get("summaryValid") is not True
                    or record.get("summaryOutcome") != "Completed"
                    or not isinstance(record.get("counters"), dict)
                ):
                    raise ValueError(f"TRX completed-run summary is invalid for {test_name}")
                trx_binding = record.get("trx")
                if not isinstance(trx_binding, dict) or not isinstance(trx_binding.get("path"), str):
                    raise ValueError(f"TRX binding is missing for {test_name}")
                trx_path = Path(trx_binding["path"])
                if per_test_trx_paths.get(test_name) != str(trx_path):
                    raise ValueError(f"dotnetTest TRX path does not match the binding for {test_name}")
                if str(trx_path) in observed_trx_paths:
                    raise ValueError("distinct audit tests must not share one TRX path")
                observed_trx_paths.add(str(trx_path))
                validated_trx = validate_trx_contract(
                    trx_path,
                    test_name,
                    trx_binding,
                    expected_run_root,
                    record.get("attemptStartedAt"),
                    record.get("attemptCompletedAt"),
                )
                validate_trx_record_contract(record, validated_trx)
            if not isinstance(dotnet_test, dict) or type(dotnet_test.get("exitCode")) is not int or dotnet_test.get("exitCode") != 0:
                raise ValueError("dotnetTest exitCode must be integer zero")
            if dotnet_test.get("runnerCommand") != [str(dotnet_host_path), str(test_assembly_path)]:
                raise ValueError("dotnetTest runnerCommand must use the bound test assembly")
        except (ValueError, OSError) as exc:
            execution_failures.append(f"{execution_path} ({exc})")
            continue
        execution_edition = str(execution_evidence.get("edition") or "").strip().lower()
        execution_family = str(execution_evidence.get("familyId") or "").strip()
        execution_proof_kind = str(execution_evidence.get("proofKind") or "").strip().lower()
        if execution_edition != edition:
            execution_failures.append(f"{execution_path} (edition={execution_edition or 'missing'})")
            continue
        if execution_family != family_id:
            execution_failures.append(f"{execution_path} (familyId={execution_family or 'missing'})")
            continue
        if execution_proof_kind != expected_execution_proof_kind:
            execution_failures.append(
                f"{execution_path} (proofKind={execution_proof_kind or 'missing'}, expected={expected_execution_proof_kind})"
            )
            continue
        if (
            execution_evidence.get("upstreamReceipts")
            or execution_evidence.get("baselineReceipts")
            or execution_evidence.get("sourceReceipts")
        ):
            execution_failures.append(
                f"{execution_path} (uses upstream/baseline/source receipts instead of executed family evidence)"
            )
            continue
        validated_execution_receipts.append(str(execution_path.resolve()))
        upstream_execution_bindings.append(binding_for_bytes(execution_path, execution_bytes))
        observed_run_ids.add(producer_run_id)
        observed_candidate_digests.add(candidate_digest)
        observed_workflow_epoch_ids.add(workflow_epoch_id)
        observed_execution_run_digests.add(execution_run_digest)
        observed_execution_started_at.add(str(execution_started_at))
        observed_execution_completed_at.add(str(execution_completed_at))

    if execution_failures:
        reasons.append("Execution receipts are missing or not passing: " + ", ".join(execution_failures))

    payload = {
        "schemaVersion": SCHEMA_VERSION,
        "producerRunId": producer_run_id,
        "candidateSnapshotId": candidate_snapshot_id,
        "workflowEpochId": workflow_epoch_id,
        "executionRunDigest": execution_run_digest,
        "generatedAt": now_iso(),
        "contract_name": contract_name,
        "status": "pass" if not reasons else "fail",
        "summary": (
            f"{edition.upper()} workflow-family verification evidence is explicitly grounded for {family_id}."
            if not reasons
            else f"{edition.upper()} workflow-family verification evidence is incomplete for {family_id}."
        ),
        "reasons": reasons,
        "evidence": {
            "edition": edition,
            "familyId": family_id,
            "proofKind": proof_kind,
            "producerRunId": producer_run_id,
            "candidateSnapshotId": candidate_snapshot_id,
            "workflowEpochId": workflow_epoch_id,
            "executionRunDigest": execution_run_digest,
            "executionStartedAt": execution_started_at,
            "executionCompletedAt": execution_completed_at,
            "releaseIdentity": release_identity,
            "candidateIdentity": candidate_identity if producer_run_id else {},
            "candidateDigest": candidate_digest,
            "ledgerPath": str(ledger_path),
            "oraclePath": str(oracle_path),
            "auditTests": audit_tests,
            "oracle": oracle_detail,
            "executionReceipts": validated_execution_receipts,
            "upstreamExecutionBindings": upstream_execution_bindings,
            "upstreamExecutionEpochManifest": execution_stage_manifest_binding,
            "executionFailures": execution_failures,
            "executionExternalBlockers": sorted(set(execution_external_blockers)),
        },
    }

    for receipt_ref in verification_receipts:
        receipt_ref = receipt_ref.replace("{familyId}", family_id)
        output_path = Path(receipt_ref)
        if not output_path.is_absolute():
            output_path = repo_root / output_path
        normalized_output_path = output_path.resolve(strict=False)
        if normalized_output_path in seen_output_paths:
            raise SystemExit(f"duplicate workflow-family verification receipt target: {output_path}")
        seen_output_paths.add(normalized_output_path)
        pending_outputs.append((output_path, payload))

    if reasons:
        any_fail = True

try:
    if [file_binding(path, "workflow parity test source") for path in test_source_paths] != test_source_bindings:
        raise ValueError("workflow parity test sources changed before verification receipt publication")
    if file_binding(
        trx_contract_source_path, "workflow-family TRX validator source"
    ) != trx_contract_source_binding:
        raise ValueError("workflow-family TRX validator source changed before verification receipt publication")
    if [
        file_binding(project_path, f"{project_label} project contract")
        for project_label, project_path in test_build_projects
    ] != build_project_bindings:
        raise ValueError("workflow parity build projects changed before verification receipt publication")
    if file_binding(api_project_path, "canonical API autostart project") != api_project_binding:
        raise ValueError("canonical API project changed before verification receipt publication")
    if file_binding(dotnet_host_path, "canonical dotnet host") != dotnet_host_binding:
        raise ValueError("canonical dotnet host changed before verification receipt publication")
    if {
        label: snapshot_output_tree(root, f"{label} release build output")
        for label, root in build_output_roots.items()
    } != build_output_bindings:
        raise ValueError("workflow parity build outputs changed before verification receipt publication")
    if file_binding(ledger_path, "workflow parity ledger") != ledger_binding:
        raise ValueError("workflow parity ledger changed before verification receipt publication")
    if file_binding(oracle_path, "workflow parity oracle") != oracle_binding:
        raise ValueError("workflow parity oracle changed before verification receipt publication")
    if file_binding(release_channel_path, "release channel receipt") != {
        key: release_identity[key] for key in ("path", "sha256", "sizeBytes")
    }:
        raise ValueError("release channel receipt changed before verification receipt publication")
    for _, pending_payload in pending_outputs:
        if pending_payload.get("status") != "pass":
            continue
        pending_evidence = pending_payload["evidence"]
        current_assembly = file_binding(test_assembly_path, "workflow parity test assembly")
        if current_assembly != pending_evidence["candidateIdentity"].get("testAssembly"):
            raise ValueError("workflow parity test assembly changed before verification receipt publication")
        execution_paths = pending_evidence.get("executionReceipts") or []
        execution_bindings = pending_evidence.get("upstreamExecutionBindings") or []
        if len(execution_paths) != 1 or len(execution_bindings) != 1:
            raise ValueError("verification execution chain changed before publication")
        execution_path = Path(execution_paths[0])
        execution_data, execution_bytes = load_regular_json(execution_path, "execution receipt")
        if execution_bindings[0] != binding_for_bytes(execution_path, execution_bytes):
            raise ValueError("execution receipt changed before verification publication")
        execution_evidence = execution_data.get("evidence")
        if not isinstance(execution_evidence, dict):
            raise ValueError("execution evidence changed before verification publication")
        test_executions = execution_evidence.get("testExecutions")
        if not isinstance(test_executions, dict):
            raise ValueError("execution testExecutions changed before verification publication")
        for test_name, record in test_executions.items():
            if not isinstance(record, dict) or not isinstance(record.get("trx"), dict):
                raise ValueError(f"TRX binding changed before verification publication: {test_name}")
            trx_binding = record["trx"]
            producer_run_id = execution_data.get("producerRunId")
            if not isinstance(producer_run_id, str):
                raise ValueError("execution producerRunId changed before verification publication")
            expected_run_root = (
                repo_root
                / ".codex-studio/out/workflow-family-parity/executed"
                / edition
                / producer_run_id
            )
            validated_trx = validate_trx_contract(
                Path(str(trx_binding.get("path"))),
                test_name,
                trx_binding,
                expected_run_root,
                record.get("attemptStartedAt"),
                record.get("attemptCompletedAt"),
            )
            validate_trx_record_contract(record, validated_trx)
except (ValueError, OSError) as exc:
    raise SystemExit(str(exc)) from exc

if (
    len(observed_run_ids) != 1
    or len(observed_candidate_digests) != 1
    or len(observed_workflow_epoch_ids) != 1
    or len(observed_execution_run_digests) != 1
    or len(observed_execution_started_at) != 1
    or len(observed_execution_completed_at) != 1
):
    any_fail = True
    reason = (
        "All family verification receipts must share one producerRunId, "
        "candidateDigest, candidateSnapshotId, and executionRunDigest."
    )
    for _, payload in pending_outputs:
        if reason not in payload["reasons"]:
            payload["reasons"].append(reason)
        payload["status"] = "fail"
        payload["summary"] = f"{edition.upper()} workflow-family verification evidence is incomplete for {payload['evidence']['familyId']}."

stage_identity_ready = (
    not execution_stage_manifest_error
    and len(observed_run_ids) == 1
    and len(observed_candidate_digests) == 1
    and len(observed_workflow_epoch_ids) == 1
    and len(observed_execution_run_digests) == 1
    and len(observed_execution_started_at) == 1
    and len(observed_execution_completed_at) == 1
)
stage_generated_at = now_iso()
stage_producer_run_id = next(iter(observed_run_ids)) if stage_identity_ready else ""
stage_candidate_digest = (
    next(iter(observed_candidate_digests)) if stage_identity_ready else ""
)
stage_candidate_snapshot_id = (
    next(iter(observed_workflow_epoch_ids)) if stage_identity_ready else ""
)
stage_execution_run_digest = (
    next(iter(observed_execution_run_digests)) if stage_identity_ready else ""
)
stage_execution_started_at = (
    next(iter(observed_execution_started_at))
    if stage_identity_ready
    else stage_generated_at
)
stage_execution_completed_at = (
    next(iter(observed_execution_completed_at))
    if stage_identity_ready
    else stage_generated_at
)
stage_upstream_manifests = (
    [execution_stage_manifest_binding] if stage_identity_ready else []
)
if not stage_identity_ready:
    for _, payload in pending_outputs:
        payload["producerRunId"] = ""
        payload["candidateSnapshotId"] = ""
        payload["workflowEpochId"] = ""
        payload["executionRunDigest"] = ""
        payload["evidence"]["producerRunId"] = ""
        payload["evidence"]["candidateSnapshotId"] = ""
        payload["evidence"]["workflowEpochId"] = ""
        payload["evidence"]["executionRunDigest"] = ""
        payload["evidence"]["executionStartedAt"] = stage_execution_started_at
        payload["evidence"]["executionCompletedAt"] = stage_execution_completed_at
        payload["evidence"]["candidateIdentity"] = {}
        payload["evidence"]["candidateDigest"] = ""
        payload["evidence"]["upstreamExecutionEpochManifest"] = {}

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
verification_stage_manifest_path = workflow_stage_manifest_path(
    repo_root, edition, "verification"
)
verification_stage_manifest = build_workflow_stage_manifest(
    edition=edition,
    stage="verification",
    status="fail" if any_fail else "pass",
    generated_at=stage_generated_at,
    producer_run_id=stage_producer_run_id,
    candidate_snapshot_id=stage_candidate_snapshot_id,
    execution_run_digest=stage_execution_run_digest,
    execution_started_at=stage_execution_started_at,
    execution_completed_at=stage_execution_completed_at,
    candidate_digest=stage_candidate_digest,
    release_identity=release_identity,
    receipt_records=stage_receipt_records,
    upstream_stage_manifests=stage_upstream_manifests,
)
atomic_write_json(verification_stage_manifest_path, verification_stage_manifest)
validate_workflow_stage_manifest(
    manifest_path=verification_stage_manifest_path,
    repo_root=repo_root,
    edition=edition,
    stage="verification",
    expected_receipts=expected_stage_receipts,
    expected_release_identity=release_identity,
    expected_upstream_stage_manifests=stage_upstream_manifests,
    require_pass=not any_fail,
)

if any_fail:
    raise SystemExit(43)
PY

echo "[materialize-${edition}-workflow-family-verification-receipts] PASS"
