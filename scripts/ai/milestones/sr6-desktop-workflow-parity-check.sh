#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
ledger_path="$repo_root/docs/SR6_WORKFLOW_PARITY_LEDGER.json"
sr4_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
dual_head_tests_path="$repo_root/Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs"
compliance_tests_path="$repo_root/Chummer.Tests/Compliance/MigrationComplianceTests.cs"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
workflow_gate_tests_path="$repo_root/Chummer.Tests/Presentation/WorkflowParityGateTests.cs"
lock_dir="$repo_root/.codex-studio/locks"
workflow_family_chain_lock_path="$lock_dir/sr6-workflow-family-parity-chain.lock"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
run_services_release_channel_path="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"
bundled_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-}" ]]; then
  release_channel_path="$CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH"
  release_channel_source="explicit"
elif [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path="$canonical_release_channel_path"
  release_channel_source="canonical"
elif [[ -f "$verified_release_channel_path" ]]; then
  release_channel_path="$verified_release_channel_path"
  release_channel_source="verified"
elif [[ -f "$run_services_release_channel_path" ]]; then
  release_channel_path="$run_services_release_channel_path"
  release_channel_source="run-services"
else
  release_channel_path="$bundled_release_channel_path"
  release_channel_source="bundled"
fi
mkdir -p "$(dirname "$receipt_path")"
mkdir -p "$lock_dir"
exec 9>"$workflow_family_chain_lock_path"
flock 9
workflow_gate_exit=0
bash "$repo_root/scripts/ai/milestones/run-workflow-parity-gate-tests.sh" "$repo_root" >/dev/null || workflow_gate_exit=$?
execution_exit=0
verification_exit=0
materializer_exit=0
CHUMMER_WORKFLOW_FAMILY_RELEASE_CHANNEL_PATH="$release_channel_path" bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-execution-receipts.sh" sr6 >/dev/null || execution_exit=$?
CHUMMER_WORKFLOW_FAMILY_RELEASE_CHANNEL_PATH="$release_channel_path" bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-verification-receipts.sh" sr6 >/dev/null || verification_exit=$?
CHUMMER_WORKFLOW_FAMILY_RELEASE_CHANNEL_PATH="$release_channel_path" bash "$repo_root/scripts/ai/milestones/materialize-sr-workflow-family-receipts.sh" sr6 >/dev/null || materializer_exit=$?

python3 - <<'PY' "$repo_root" "$receipt_path" "$ledger_path" "$sr4_receipt_path" "$dual_head_tests_path" "$compliance_tests_path" "$ui_gate_tests_path" "$workflow_gate_tests_path" "$workflow_gate_exit" "$execution_exit" "$verification_exit" "$materializer_exit" "$release_channel_path" "$release_channel_source"
from __future__ import annotations

import hashlib
import json
import os
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

repo_root, receipt_path, ledger_path, sr4_receipt_path, dual_head_tests_path, compliance_tests_path, ui_gate_tests_path, workflow_gate_tests_path = [
    Path(value) for value in sys.argv[1:9]
]
workflow_gate_exit = int(sys.argv[9])
execution_exit = int(sys.argv[10])
verification_exit = int(sys.argv[11])
materializer_exit = int(sys.argv[12])
release_channel_path = Path(sys.argv[13])
release_channel_source = sys.argv[14]
trx_contract_source_path = (
    repo_root / "scripts" / "ai" / "milestones" / "workflow_family_trx_contract.py"
)
sys.path.insert(0, str(trx_contract_source_path.parent))
from workflow_family_trx_contract import (
    file_binding as workflow_file_binding,
    validate_workflow_stage_manifest,
    workflow_stage_manifest_path,
)
RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS") or "86400"
)
RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS") or "300"
)
JSON_INPUT_MAX_BYTES = 8 * 1024 * 1024
TEXT_INPUT_MAX_BYTES = 16 * 1024 * 1024


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def status_ok(value: object) -> bool:
    return normalize(value) in {"pass", "passed", "ready"}


def parse_iso(value: object) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        return None
    return parsed.astimezone(timezone.utc)


required_family_ids = [
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
]
required_recursive_gate_proof_area_ids = [
    "recursiveMenuWorkflows",
    "legacyUiControlWorkflows",
    "quickActionRoots",
    "returnSurfaceParityAfterExit",
]

payload = {
    "schemaVersion": 1,
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.sr6_desktop_workflow_parity",
    "channelId": "",
    "releaseVersion": "",
    "status": "fail",
    "summary": "SR6 desktop workflow carry-forward parity is not yet exhaustively proven.",
    "reasons": [],
    "evidence": {
        "releaseChannelPath": str(release_channel_path),
        "releaseChannelSource": release_channel_source,
        "releaseChannelExists": False,
        "ledgerPath": str(ledger_path),
        "sr4ReceiptPath": str(sr4_receipt_path),
        "dualHeadTestsPath": str(dual_head_tests_path),
        "complianceTestsPath": str(compliance_tests_path),
        "uiGateTestsPath": str(ui_gate_tests_path),
        "workflowGateTestsPath": str(workflow_gate_tests_path),
        "workflowGateExit": workflow_gate_exit,
        "releaseChannelMaxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
        "releaseChannelMaxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
        "executionExit": execution_exit,
        "verificationExit": verification_exit,
        "materializerExit": materializer_exit,
    },
}

source_artifact_reasons: list[str] = []
release_channel_reasons: list[str] = []
sr4_baseline_reasons: list[str] = []
workflow_family_reasons: list[str] = []
test_reference_reasons: list[str] = []
parity_receipt_reasons: list[str] = []
materialization_reasons: list[str] = []
workflow_gate_reasons: list[str] = []


def append_reason(message: str, *buckets: list[str]) -> None:
    if message not in payload["reasons"]:
        payload["reasons"].append(message)
    for bucket in buckets:
        if message not in bucket:
            bucket.append(message)


def snapshot_signature(metadata: os.stat_result) -> tuple[int, int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_size,
        metadata.st_mtime_ns,
    )


def read_regular_bytes(path: Path, label: str, max_bytes: int) -> bytes:
    if max_bytes <= 0:
        raise ValueError(f"{label} has an invalid snapshot byte limit")
    try:
        path_before = os.lstat(path)
    except FileNotFoundError:
        raise ValueError(f"{label} is missing: {path}") from None
    except OSError as exc:
        raise ValueError(f"{label} cannot be inspected safely: {path}: {exc.strerror}") from exc
    if not stat.S_ISREG(path_before.st_mode):
        raise ValueError(f"{label} is not a regular file (symlinks are forbidden): {path}")

    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0)
    flags |= getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(path, flags)
    except OSError as exc:
        raise ValueError(f"{label} cannot be opened safely: {path}: {exc.strerror}") from exc
    try:
        try:
            before = os.fstat(descriptor)
            if not stat.S_ISREG(before.st_mode):
                raise ValueError(f"{label} opened as a non-regular file: {path}")
            if snapshot_signature(path_before) != snapshot_signature(before):
                raise ValueError(f"{label} path binding changed before snapshot: {path}")
            if before.st_size > max_bytes:
                raise ValueError(
                    f"{label} exceeds the {max_bytes}-byte snapshot limit: {path}"
                )

            chunks: list[bytes] = []
            total = 0
            while total <= max_bytes:
                chunk = os.read(descriptor, min(64 * 1024, max_bytes + 1 - total))
                if not chunk:
                    break
                chunks.append(chunk)
                total += len(chunk)

            after = os.fstat(descriptor)
            if snapshot_signature(before) != snapshot_signature(after):
                raise ValueError(f"{label} changed while being snapshotted: {path}")
            if total != after.st_size:
                raise ValueError(f"{label} size changed or could not be read completely: {path}")
            if total > max_bytes:
                raise ValueError(
                    f"{label} exceeds the {max_bytes}-byte snapshot limit: {path}"
                )
            try:
                path_after = os.lstat(path)
            except OSError as exc:
                raise ValueError(f"{label} path binding changed during snapshot: {path}") from exc
            if snapshot_signature(path_after) != snapshot_signature(after):
                raise ValueError(f"{label} path binding changed during snapshot: {path}")
            return b"".join(chunks)
        except OSError as exc:
            raise ValueError(f"{label} could not be snapshotted safely: {path}: {exc.strerror}") from exc
    finally:
        os.close(descriptor)


def decode_regular_text(path: Path, label: str, max_bytes: int) -> str:
    raw = read_regular_bytes(path, label, max_bytes)
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise ValueError(
            f"{label} is not valid UTF-8 text at byte {exc.start}: {path}"
        ) from None


def parse_regular_json(path: Path, label: str) -> dict[str, object]:
    text = decode_regular_text(path, label, JSON_INPUT_MAX_BYTES)
    try:
        value = json.loads(text)
    except json.JSONDecodeError as exc:
        raise ValueError(
            f"{label} contains invalid JSON at line {exc.lineno}, column {exc.colno}: {path}"
        ) from None
    if not isinstance(value, dict):
        raise ValueError(f"{label} JSON root must be an object: {path}")
    return value


def binding_for_path(path: Path, label: str) -> dict[str, object]:
    raw = read_regular_bytes(path, label, JSON_INPUT_MAX_BYTES)
    return {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "sizeBytes": len(raw),
    }


def load_regular_json(
    path: Path, label: str, *reason_buckets: list[str]
) -> tuple[dict[str, object], bool]:
    try:
        return parse_regular_json(path, label), True
    except ValueError as exc:
        append_reason(str(exc), *reason_buckets)
        return {}, False


def load_regular_text(
    path: Path, label: str, *reason_buckets: list[str]
) -> tuple[str, bool]:
    try:
        return decode_regular_text(path, label, TEXT_INPUT_MAX_BYTES), True
    except ValueError as exc:
        append_reason(str(exc), *reason_buckets)
        return "", False


def write_receipt_atomically(path: Path, value: dict[str, object]) -> None:
    if path.is_symlink():
        raise RuntimeError(f"Refusing to replace symlink receipt path: {path}")

    serialized = json.dumps(value, indent=2) + "\n"
    temporary_fd = -1
    temporary_path: Path | None = None
    try:
        temporary_fd, temporary_name = tempfile.mkstemp(
            prefix=f".{path.name}.",
            suffix=".tmp",
            dir=str(path.parent),
        )
        temporary_path = Path(temporary_name)
        with os.fdopen(temporary_fd, "w", encoding="utf-8", newline="\n") as handle:
            temporary_fd = -1
            handle.write(serialized)
            handle.flush()
            os.fchmod(handle.fileno(), 0o644)
            os.fsync(handle.fileno())

        if path.is_symlink():
            raise RuntimeError(f"Refusing to replace symlink receipt path: {path}")
        os.replace(temporary_path, path)
        temporary_path = None

        directory_flags = (
            os.O_RDONLY
            | getattr(os, "O_DIRECTORY", 0)
            | getattr(os, "O_CLOEXEC", 0)
        )
        directory_fd = os.open(path.parent, directory_flags)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        if temporary_fd >= 0:
            os.close(temporary_fd)
        if temporary_path is not None:
            try:
                temporary_path.unlink()
            except FileNotFoundError:
                pass


release_channel, release_channel_exists = load_regular_json(
    release_channel_path, "Release channel receipt", release_channel_reasons
)
ledger, ledger_exists = load_regular_json(
    ledger_path, "SR6 workflow parity ledger", source_artifact_reasons
)
sr4_receipt, sr4_receipt_exists = load_regular_json(
    sr4_receipt_path,
    "SR4 workflow parity receipt",
    source_artifact_reasons,
    sr4_baseline_reasons,
)
sr4_receipt_binding: dict[str, object] = {}
if sr4_receipt_exists:
    try:
        rebound_sr4_receipt = parse_regular_json(
            sr4_receipt_path, "SR4 workflow parity receipt"
        )
        if rebound_sr4_receipt != sr4_receipt:
            raise ValueError("SR4 workflow parity receipt changed between snapshots")
        sr4_receipt_binding = binding_for_path(
            sr4_receipt_path, "SR4 workflow parity receipt"
        )
    except ValueError as exc:
        append_reason(str(exc), source_artifact_reasons, sr4_baseline_reasons)
dual_head_tests_text, dual_head_tests_exist = load_regular_text(
    dual_head_tests_path, "Dual-head acceptance tests", source_artifact_reasons
)
compliance_tests_text, compliance_tests_exist = load_regular_text(
    compliance_tests_path, "Migration compliance tests", source_artifact_reasons
)
ui_gate_tests_text, ui_gate_tests_exist = load_regular_text(
    ui_gate_tests_path, "Flagship UI gate tests", source_artifact_reasons
)
workflow_gate_tests_text, workflow_gate_tests_exist = load_regular_text(
    workflow_gate_tests_path, "Workflow parity gate tests", source_artifact_reasons
)
payload["evidence"]["releaseChannelExists"] = release_channel_exists
release_channel_aliases = {
    key: str(release_channel.get(key) or "").strip()
    for key in ("channelId", "channel")
    if isinstance(release_channel, dict) and key in release_channel
}
release_channel_channel_id = ""
release_version_aliases = {
    key: str(release_channel.get(key) or "").strip()
    for key in ("releaseVersion", "version")
    if isinstance(release_channel, dict) and key in release_channel
}
release_channel_release_version = ""
release_generated_at_aliases = {
    key: str(release_channel.get(key) or "").strip()
    for key in ("generatedAt", "generated_at")
    if isinstance(release_channel, dict) and key in release_channel
}
release_channel_generated_at_raw = ""
release_channel_generated_at = None

release_channel_age_seconds = None
release_channel_future_skew_seconds = None
if release_channel_exists and not release_channel:
    append_reason(
        f"Release channel receipt must be a non-empty JSON object: {release_channel_path}"
        , release_channel_reasons
    )
if str(release_channel.get("contract_name") or "").strip() != "Chummer.Hub.Registry.Contracts":
    append_reason(
        "Release channel receipt contract_name must be Chummer.Hub.Registry.Contracts.",
        release_channel_reasons,
    )
if normalize(release_channel.get("status")) != "published":
    append_reason(
        "Release channel receipt status must be published.",
        release_channel_reasons,
    )
if not release_channel_aliases:
    append_reason("Release channel receipt is missing channelId/channel.", release_channel_reasons)
elif any(not value for value in release_channel_aliases.values()):
    append_reason("Release channel receipt channelId/channel aliases must all be nonblank.", release_channel_reasons)
elif len({value.lower() for value in release_channel_aliases.values()}) != 1:
    append_reason("Release channel receipt has conflicting channelId/channel aliases.", release_channel_reasons)
else:
    release_channel_channel_id = normalize(next(iter(release_channel_aliases.values())))
if not release_version_aliases:
    append_reason(
        "Release channel receipt is missing releaseVersion/version.",
        release_channel_reasons,
    )
elif any(not value for value in release_version_aliases.values()):
    append_reason(
        "Release channel receipt releaseVersion/version aliases must all be nonblank.",
        release_channel_reasons,
    )
elif len(set(release_version_aliases.values())) != 1:
    append_reason(
        "Release channel receipt has conflicting releaseVersion/version aliases: "
        + ", ".join(
            f"{key}={value!r}" for key, value in release_version_aliases.items()
        ),
        release_channel_reasons,
    )
else:
    release_channel_release_version = next(iter(release_version_aliases.values()))
if not release_generated_at_aliases:
    append_reason(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp."
        , release_channel_reasons
    )
elif any(not value for value in release_generated_at_aliases.values()):
    append_reason(
        "Release channel receipt generatedAt/generated_at aliases must all be nonblank.",
        release_channel_reasons,
    )
elif len(set(release_generated_at_aliases.values())) != 1:
    append_reason(
        "Release channel receipt has conflicting generatedAt/generated_at aliases.",
        release_channel_reasons,
    )
else:
    release_channel_generated_at_raw = next(iter(release_generated_at_aliases.values()))
    release_channel_generated_at = parse_iso(release_channel_generated_at_raw)
if release_generated_at_aliases and release_channel_generated_at is None and not any(
    "generatedAt/generated_at" in reason for reason in release_channel_reasons
):
    append_reason(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp.",
        release_channel_reasons,
    )
if release_channel_generated_at is not None:
    now = datetime.now(timezone.utc)
    release_channel_delta_seconds = (now - release_channel_generated_at).total_seconds()
    release_channel_age_seconds = int(max(release_channel_delta_seconds, 0))
    release_channel_future_skew_seconds = int(max(-release_channel_delta_seconds, 0))
    if release_channel_age_seconds > RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS:
        append_reason(
            f"Release channel receipt generatedAt is stale by {release_channel_age_seconds} seconds.",
            release_channel_reasons,
        )
    if release_channel_future_skew_seconds > RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS:
        append_reason(
            f"Release channel receipt generatedAt is in the future by {release_channel_future_skew_seconds} seconds.",
            release_channel_reasons,
        )
families = {str(item.get("id") or "").strip(): item for item in (ledger.get("requiredFamilies") or []) if isinstance(item, dict)}

workflow_release_identity: dict[str, object] = {}
if (
    release_channel_exists
    and release_channel_channel_id
    and release_channel_release_version
    and release_channel_generated_at_raw
    and release_channel_generated_at is not None
):
    try:
        workflow_release_identity = {
            "channelId": release_channel_channel_id,
            "releaseVersion": release_channel_release_version,
            "generatedAt": release_channel_generated_at_raw,
            **workflow_file_binding(
                release_channel_path, "SR6 workflow release channel receipt"
            ),
        }
    except ValueError as exc:
        append_reason(str(exc), release_channel_reasons, materialization_reasons)


def exact_stage_receipt_map(stage: str) -> dict[str, Path]:
    if set(families) != set(required_family_ids):
        raise ValueError("SR6 workflow ledger family inventory is not exact")
    receipt_key_by_stage = {
        "execution": "executionReceipts",
        "verification": "verificationReceipts",
        "parity": "parityReceipts",
    }
    receipt_key = receipt_key_by_stage[stage]
    resolved: dict[str, Path] = {}
    for family_id in required_family_ids:
        family = families[family_id]
        expected_reference = {
            "execution": (
                ".codex-studio/published/workflow-family-parity/executed/"
                "sr6/{familyId}.generated.json"
            ),
            "verification": (
                ".codex-studio/published/workflow-family-parity/sr6/"
                f"{family_id}.generated.json"
            ),
            "parity": (
                ".codex-studio/published/workflow-family-parity/"
                f"SR6_WORKFLOW_FAMILY_{family_id}.generated.json"
            ),
        }[stage]
        references = family.get(receipt_key)
        if references != [expected_reference]:
            raise ValueError(
                f"SR6 workflow ledger {receipt_key} target is not canonical for {family_id}"
            )
        rendered_reference = expected_reference.replace("{familyId}", family_id)
        resolved[family_id] = repo_root / rendered_reference
    return resolved


expected_stage_receipts: dict[str, dict[str, Path]] = {}
stage_receipt_map_error = ""
try:
    expected_stage_receipts = {
        stage: exact_stage_receipt_map(stage)
        for stage in ("execution", "verification", "parity")
    }
except (KeyError, TypeError, ValueError) as exc:
    stage_receipt_map_error = str(exc)


def validate_committed_epoch_chain() -> dict[str, dict[str, object]]:
    if stage_receipt_map_error:
        raise ValueError(stage_receipt_map_error)
    if not workflow_release_identity:
        raise ValueError("SR6 workflow release identity is incomplete")
    execution_result = validate_workflow_stage_manifest(
        manifest_path=workflow_stage_manifest_path(repo_root, "sr6", "execution"),
        repo_root=repo_root,
        edition="sr6",
        stage="execution",
        expected_receipts=expected_stage_receipts["execution"],
        expected_release_identity=workflow_release_identity,
        expected_upstream_stage_manifests=[],
    )
    verification_result = validate_workflow_stage_manifest(
        manifest_path=workflow_stage_manifest_path(repo_root, "sr6", "verification"),
        repo_root=repo_root,
        edition="sr6",
        stage="verification",
        expected_receipts=expected_stage_receipts["verification"],
        expected_release_identity=workflow_release_identity,
        expected_upstream_stage_manifests=[execution_result["binding"]],
    )
    parity_result = validate_workflow_stage_manifest(
        manifest_path=workflow_stage_manifest_path(repo_root, "sr6", "parity"),
        repo_root=repo_root,
        edition="sr6",
        stage="parity",
        expected_receipts=expected_stage_receipts["parity"],
        expected_release_identity=workflow_release_identity,
        expected_upstream_stage_manifests=[
            execution_result["binding"],
            verification_result["binding"],
        ],
    )
    results = {
        "execution": execution_result,
        "verification": verification_result,
        "parity": parity_result,
    }
    identity_keys = (
        "producerRunId",
        "candidateSnapshotId",
        "workflowEpochId",
        "executionRunDigest",
        "candidateDigest",
        "releaseIdentity",
        "executionStartedAt",
        "executionCompletedAt",
    )
    execution_manifest = execution_result["manifest"]
    for stage in ("verification", "parity"):
        stage_manifest = results[stage]["manifest"]
        if any(
            stage_manifest.get(key) != execution_manifest.get(key)
            for key in identity_keys
        ):
            raise ValueError(
                f"SR6 {stage} manifest identity does not match the execution epoch"
            )
    return results


workflow_epoch_chain: dict[str, dict[str, object]] = {}
parity_stage_receipt_payloads: dict[str, dict[str, object]] = {}
parity_stage_manifest_binding: dict[str, object] = {}
parity_stage_epoch_commit_id = ""
epoch_producer_run_id = ""
candidate_snapshot_id = ""
execution_run_digest = ""
try:
    workflow_epoch_chain = validate_committed_epoch_chain()
    parity_stage_result = workflow_epoch_chain["parity"]
    parity_stage_manifest = parity_stage_result["manifest"]
    parity_stage_receipt_payloads = parity_stage_result["receiptPayloads"]
    parity_stage_manifest_binding = parity_stage_result["binding"]
    parity_stage_epoch_commit_id = str(
        parity_stage_manifest.get("epochCommitId") or ""
    )
    epoch_producer_run_id = str(parity_stage_manifest.get("producerRunId") or "")
    candidate_snapshot_id = str(
        parity_stage_manifest.get("candidateSnapshotId") or ""
    )
    execution_run_digest = str(
        parity_stage_manifest.get("executionRunDigest") or ""
    )
except (KeyError, OSError, TypeError, ValueError) as exc:
    append_reason(
        "SR6 committed workflow-family epoch chain is invalid: " + str(exc),
        parity_receipt_reasons,
        materialization_reasons,
    )

payload["producerRunId"] = epoch_producer_run_id
payload["candidateSnapshotId"] = candidate_snapshot_id
payload["workflowEpochId"] = candidate_snapshot_id
payload["executionRunDigest"] = execution_run_digest
payload["workflowFamilyParityEpochCommitId"] = parity_stage_epoch_commit_id
payload["evidence"]["producerRunId"] = epoch_producer_run_id
payload["evidence"]["candidateSnapshotId"] = candidate_snapshot_id
payload["evidence"]["workflowEpochId"] = candidate_snapshot_id
payload["evidence"]["executionRunDigest"] = execution_run_digest
payload["evidence"]["workflowFamilyParityEpochManifest"] = (
    parity_stage_manifest_binding
)
payload["evidence"]["workflowFamilyParityEpochCommitId"] = (
    parity_stage_epoch_commit_id
)
payload["evidence"]["workflowFamilyEpochCommitted"] = False

recursive_workflow_gate = (
    dict(ledger.get("recursiveWorkflowGate") or {})
    if isinstance(ledger.get("recursiveWorkflowGate"), dict)
    else {}
)
proof_areas = (
    dict(recursive_workflow_gate.get("proofAreas") or {})
    if isinstance(recursive_workflow_gate.get("proofAreas"), dict)
    else {}
)
required_recursive_gate_tests = [
    str(value).strip()
    for value in (recursive_workflow_gate.get("requiredTests") or [])
    if str(value).strip()
]
proof_area_tests = {
    area_id: [
        str(value).strip()
        for value in (
            (area_payload.get("requiredTests") or [])
            if isinstance(area_payload, dict)
            else []
        )
        if str(value).strip()
    ]
    for area_id, area_payload in proof_areas.items()
    if str(area_id).strip()
}
proof_area_summaries = {
    area_id: str(
        (area_payload.get("summary") if isinstance(area_payload, dict) else "") or ""
    ).strip()
    for area_id, area_payload in proof_areas.items()
    if str(area_id).strip()
}
return_surface_requirement = str(recursive_workflow_gate.get("returnSurfaceRequirement") or "").strip()
test_corpus = "\n".join(
    text
    for text, exists in [
        (dual_head_tests_text, dual_head_tests_exist),
        (compliance_tests_text, compliance_tests_exist),
        (ui_gate_tests_text, ui_gate_tests_exist),
        (workflow_gate_tests_text, workflow_gate_tests_exist),
    ]
    if exists
)

missing_family_ids = [family_id for family_id in required_family_ids if family_id not in families]
non_ready_family_ids = [
    family_id
    for family_id in required_family_ids
    if family_id in families and str(families[family_id].get("status") or "").strip().lower() != "ready"
]
missing_test_refs = {}
missing_parity_receipts = {}
failing_parity_receipts = {}
external_only_failing_parity_receipts = {}

for family_id in required_family_ids:
    family = families.get(family_id) or {}
    audit_tests = [str(value).strip() for value in (family.get("auditTests") or []) if str(value).strip()]
    if not audit_tests:
        missing_test_refs[family_id] = ["<missing auditTests>"]
    unresolved = [name for name in audit_tests if name not in test_corpus]
    if unresolved:
        missing_test_refs[family_id] = unresolved
    parity_receipts = [str(value).strip() for value in (family.get("parityReceipts") or []) if str(value).strip()]
    if not parity_receipts:
        missing_parity_receipts[family_id] = ["<missing parityReceipts>"]
        continue
    receipt_failures = []
    for receipt_ref in parity_receipts:
        receipt_ref = receipt_ref.replace("{familyId}", family_id)
        receipt_file = Path(receipt_ref)
        if not receipt_file.is_absolute():
            receipt_file = repo_root / receipt_file
        expected_receipt_file = (
            expected_stage_receipts.get("parity", {}).get(family_id)
        )
        if (
            expected_receipt_file is None
            or receipt_file.resolve(strict=False)
            != expected_receipt_file.resolve(strict=False)
        ):
            receipt_failures.append(
                f"{receipt_file} (not the canonical committed parity target)"
            )
            continue
        receipt_data = parity_stage_receipt_payloads.get(family_id)
        if not isinstance(receipt_data, dict):
            receipt_failures.append(
                f"{receipt_file} (not authorized by the committed parity manifest)"
            )
            continue
        receipt_status = str(receipt_data.get("status") or "").strip().lower()
        if receipt_status not in {"pass", "passed", "ready"}:
            receipt_evidence = (
                receipt_data.get("evidence")
                if isinstance(receipt_data.get("evidence"), dict)
                else {}
            )
            verification_failures = [
                str(value).strip().lower()
                for value in (receipt_evidence.get("verificationFailures") or [])
                if str(value).strip()
            ]
            external_only = bool(verification_failures) and all(
                "external_blocker=missing_api_surface_contract" in failure
                for failure in verification_failures
            )
            if external_only:
                external_only_failing_parity_receipts[family_id] = verification_failures
                receipt_failures.append(
                    f"{receipt_file} ({receipt_status or 'missing status'}; external_blocker=missing_api_surface_contract)"
                )
            else:
                receipt_failures.append(f"{receipt_file} ({receipt_status or 'missing status'})")
            continue
        evidence = dict(receipt_data.get("evidence") or {})
        receipt_edition = str(evidence.get("edition") or "").strip().lower()
        receipt_family = str(evidence.get("familyId") or "").strip()
        proof_kind = str(evidence.get("proofKind") or "").strip().lower()
        if receipt_edition != "sr6":
            receipt_failures.append(f"{receipt_file} (edition={receipt_edition or 'missing'})")
            continue
        if receipt_family != family_id:
            receipt_failures.append(f"{receipt_file} (familyId={receipt_family or 'missing'})")
            continue
        if proof_kind != "sr6_family_carry_forward":
            receipt_failures.append(f"{receipt_file} (proofKind={proof_kind or 'missing'})")
            continue
        if evidence.get("baselineReceipts") or evidence.get("sourceReceipts"):
            receipt_failures.append(f"{receipt_file} (uses generic release receipts instead of family carry-forward proof)")
    if receipt_failures:
        failing_parity_receipts[family_id] = receipt_failures

if missing_family_ids:
    append_reason(
        "SR6 workflow parity ledger is missing required families: " + ", ".join(missing_family_ids),
        workflow_family_reasons,
    )
if non_ready_family_ids:
    append_reason(
        "SR6 workflow parity ledger has unresolved families: "
        + ", ".join(f"{family_id}={families[family_id].get('status', 'missing')}" for family_id in non_ready_family_ids)
        , workflow_family_reasons
    )

sr4_receipt_channel_id = ""
sr4_receipt_channel_aliases: dict[str, str] = {}
sr4_receipt_release_version = ""
sr4_receipt_release_version_aliases: dict[str, str] = {}
sr4_receipt_generated_at_raw = ""
sr4_receipt_generated_at = None
if sr4_receipt_exists:
    if type(sr4_receipt.get("schemaVersion")) is not int or sr4_receipt.get("schemaVersion") != 1:
        append_reason(
            "SR4 desktop workflow parity receipt schemaVersion must equal integer 1.",
            sr4_baseline_reasons,
        )
    if sr4_receipt.get("contract_name") != "chummer6-ui.sr4_desktop_workflow_parity":
        append_reason(
            "SR4 desktop workflow parity receipt contract_name is invalid.",
            sr4_baseline_reasons,
        )
    if sr4_receipt.get("status") != "pass":
        append_reason("SR4 desktop workflow parity must pass before SR6 carry-forward parity can close.", sr4_baseline_reasons)
    sr4_receipt_channel_aliases = {
        key: str(sr4_receipt.get(key) or "").strip()
        for key in ("channelId", "channel")
        if key in sr4_receipt
    }
    if not sr4_receipt_channel_aliases:
        append_reason("SR4 desktop workflow parity receipt is missing channelId/channel.", sr4_baseline_reasons)
    elif any(not value for value in sr4_receipt_channel_aliases.values()):
        append_reason("SR4 desktop workflow parity receipt channelId/channel aliases must all be nonblank.", sr4_baseline_reasons)
    elif len({value.lower() for value in sr4_receipt_channel_aliases.values()}) != 1:
        append_reason("SR4 desktop workflow parity receipt has conflicting channelId/channel aliases.", sr4_baseline_reasons)
    else:
        sr4_receipt_channel_id = normalize(next(iter(sr4_receipt_channel_aliases.values())))
        if release_channel_channel_id and sr4_receipt_channel_id != release_channel_channel_id:
            append_reason("SR4 desktop workflow parity receipt channelId does not match release channel.", sr4_baseline_reasons)
    sr4_receipt_release_version_aliases = {
        key: str(sr4_receipt.get(key) or "").strip()
        for key in ("releaseVersion", "version")
        if key in sr4_receipt
    }
    if not sr4_receipt_release_version_aliases:
        append_reason(
            "SR4 desktop workflow parity receipt is missing releaseVersion/version.",
            sr4_baseline_reasons,
        )
    elif any(not value for value in sr4_receipt_release_version_aliases.values()):
        append_reason(
            "SR4 desktop workflow parity receipt releaseVersion/version aliases must all be nonblank.",
            sr4_baseline_reasons,
        )
    elif len(set(sr4_receipt_release_version_aliases.values())) != 1:
        append_reason(
            "SR4 desktop workflow parity receipt has conflicting releaseVersion/version aliases.",
            sr4_baseline_reasons,
        )
    else:
        sr4_receipt_release_version = next(
            iter(sr4_receipt_release_version_aliases.values())
        )
        if (
            release_channel_release_version
            and sr4_receipt_release_version != release_channel_release_version
        ):
            append_reason(
                "SR4 desktop workflow parity receipt releaseVersion does not match release channel.",
                sr4_baseline_reasons,
            )
    sr4_receipt_generated_at_aliases = {
        key: str(sr4_receipt.get(key) or "").strip()
        for key in ("generatedAt", "generated_at")
        if key in sr4_receipt
    }
    if not sr4_receipt_generated_at_aliases:
        append_reason("SR4 desktop workflow parity receipt is missing a valid generatedAt/generated_at timestamp.", sr4_baseline_reasons)
    elif any(not value for value in sr4_receipt_generated_at_aliases.values()):
        append_reason("SR4 desktop workflow parity receipt generatedAt/generated_at aliases must all be nonblank.", sr4_baseline_reasons)
    elif len(set(sr4_receipt_generated_at_aliases.values())) != 1:
        append_reason("SR4 desktop workflow parity receipt has conflicting generatedAt/generated_at aliases.", sr4_baseline_reasons)
    else:
        sr4_receipt_generated_at_raw = next(iter(sr4_receipt_generated_at_aliases.values()))
        sr4_receipt_generated_at = parse_iso(sr4_receipt_generated_at_raw)
        if sr4_receipt_generated_at is None:
            append_reason("SR4 desktop workflow parity receipt is missing a valid generatedAt/generated_at timestamp.", sr4_baseline_reasons)
        else:
            sr4_delta_seconds = (
                datetime.now(timezone.utc) - sr4_receipt_generated_at
            ).total_seconds()
            if sr4_delta_seconds > RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS:
                append_reason(
                    "SR4 desktop workflow parity receipt is stale.",
                    sr4_baseline_reasons,
                )
            if sr4_delta_seconds < -RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS:
                append_reason(
                    "SR4 desktop workflow parity receipt is too far in the future.",
                    sr4_baseline_reasons,
                )
            if release_channel_generated_at is not None and sr4_receipt_generated_at < release_channel_generated_at:
                append_reason("SR4 desktop workflow parity receipt predates the release channel generatedAt timestamp.", sr4_baseline_reasons)
if missing_test_refs:
    append_reason(
        "SR6 workflow parity ledger references missing executable tests: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_test_refs.items()))
        , test_reference_reasons
    )
missing_recursive_gate_tests = [
    test_name for test_name in required_recursive_gate_tests if test_name not in workflow_gate_tests_text
]
missing_recursive_gate_proof_areas = [
    area_id for area_id in required_recursive_gate_proof_area_ids if area_id not in proof_areas
]
missing_recursive_gate_proof_area_tests = {}
missing_recursive_gate_proof_area_summaries = []
proof_area_test_union = sorted({test_name for tests in proof_area_tests.values() for test_name in tests})
unmapped_recursive_gate_tests = [
    test_name for test_name in required_recursive_gate_tests if test_name not in proof_area_test_union
]
unexpected_proof_area_tests = [
    test_name for test_name in proof_area_test_union if test_name not in required_recursive_gate_tests
]
for area_id in required_recursive_gate_proof_area_ids:
    if area_id in missing_recursive_gate_proof_areas:
        continue
    area_tests = proof_area_tests.get(area_id) or []
    if not area_tests:
        missing_recursive_gate_proof_area_tests[area_id] = ["<missing requiredTests>"]
    else:
        unresolved = [test_name for test_name in area_tests if test_name not in workflow_gate_tests_text]
        if unresolved:
            missing_recursive_gate_proof_area_tests[area_id] = unresolved
    if not proof_area_summaries.get(area_id):
        missing_recursive_gate_proof_area_summaries.append(area_id)
if workflow_gate_exit != 0:
    append_reason(
        f"Workflow parity gate tests exited non-zero: {workflow_gate_exit}",
        workflow_gate_reasons,
    )
if not required_recursive_gate_tests:
    append_reason(
        "SR6 workflow parity ledger must declare recursive workflow gate tests.",
        workflow_gate_reasons,
    )
elif missing_recursive_gate_tests:
    append_reason(
        "SR6 workflow parity ledger recursive gate references missing workflow gate tests: "
        + ", ".join(missing_recursive_gate_tests),
        workflow_gate_reasons,
    )
if missing_recursive_gate_proof_areas:
    append_reason(
        "SR6 workflow parity ledger must declare recursive workflow proof areas: "
        + ", ".join(missing_recursive_gate_proof_areas),
        workflow_gate_reasons,
    )
if missing_recursive_gate_proof_area_tests:
    append_reason(
        "SR6 workflow parity ledger recursive proof areas reference missing workflow gate tests: "
        + ", ".join(
            f"{area_id}: {', '.join(test_names)}"
            for area_id, test_names in sorted(missing_recursive_gate_proof_area_tests.items())
        ),
        workflow_gate_reasons,
    )
if missing_recursive_gate_proof_area_summaries:
    append_reason(
        "SR6 workflow parity ledger recursive proof areas must include summaries: "
        + ", ".join(missing_recursive_gate_proof_area_summaries),
        workflow_gate_reasons,
    )
if unmapped_recursive_gate_tests:
    append_reason(
        "SR6 workflow parity ledger recursive gate tests are not mapped to proof areas: "
        + ", ".join(unmapped_recursive_gate_tests),
        workflow_gate_reasons,
    )
if unexpected_proof_area_tests:
    append_reason(
        "SR6 workflow parity ledger recursive proof areas reference tests outside requiredTests: "
        + ", ".join(unexpected_proof_area_tests),
        workflow_gate_reasons,
    )
if not return_surface_requirement:
    append_reason(
        "SR6 workflow parity ledger must document the returned-surface parity requirement for recursive workflows.",
        workflow_gate_reasons,
    )
if missing_parity_receipts:
    append_reason(
        "SR6 workflow parity ledger is missing edition-specific parity receipts: "
        + ", ".join(f"{family_id}: {', '.join(names)}" for family_id, names in sorted(missing_parity_receipts.items()))
        , parity_receipt_reasons
    )
if failing_parity_receipts:
    external_only_fail = (
        len(external_only_failing_parity_receipts) == len(failing_parity_receipts)
    )
    if external_only_fail:
        append_reason(
            "SR6 workflow parity receipts require a chummer-api host exposing /api/workspaces and /api/shell/bootstrap "
            "(external blocker: missing_api_surface_contract): "
            + ", ".join(
                f"{family_id}: {', '.join(names)}"
                for family_id, names in sorted(failing_parity_receipts.items())
            )
            , parity_receipt_reasons
        )
    else:
        append_reason(
            "SR6 workflow parity receipts are missing or not passing: "
            + ", ".join(
                f"{family_id}: {', '.join(names)}"
                for family_id, names in sorted(failing_parity_receipts.items())
            )
            , parity_receipt_reasons
        )
family_receipts_proven = not missing_parity_receipts and not failing_parity_receipts
if materializer_exit != 0:
    append_reason(
        f"SR6 family receipt materialization exited non-zero: {materializer_exit}",
        materialization_reasons,
    )
if verification_exit != 0:
    append_reason(
        f"SR6 verification receipt materialization exited non-zero: {verification_exit}",
        materialization_reasons,
    )
if execution_exit != 0:
    append_reason(
        f"SR6 execution receipt materialization exited non-zero: {execution_exit}",
        materialization_reasons,
    )

if sr4_receipt_binding:
    try:
        if binding_for_path(
            sr4_receipt_path, "SR4 workflow parity receipt"
        ) != sr4_receipt_binding:
            raise ValueError(
                "SR4 desktop workflow parity receipt changed before SR6 publication."
            )
    except ValueError as exc:
        append_reason(str(exc), source_artifact_reasons, sr4_baseline_reasons)

if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = (
        "SR6 desktop workflow carry-forward parity is explicitly proven across source artifacts, release-channel identity, "
        "SR4 baseline proof, workflow-family readiness, executable test references, recursive workflow gate execution "
        "for recursive menu workflows, legacy UI-control workflows, quick-action roots, and returned-surface parity, receipt proof, and materialization."
    )

payload["channelId"] = release_channel_channel_id
payload["channel"] = release_channel_channel_id
payload["releaseVersion"] = release_channel_release_version
payload["version"] = release_channel_release_version
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelReleaseVersion"] = release_channel_release_version
payload["evidence"]["releaseChannelReleaseVersionAliases"] = release_version_aliases
payload["evidence"]["releaseChannelGeneratedAt"] = release_channel_generated_at_raw
payload["evidence"]["releaseChannelAgeSeconds"] = release_channel_age_seconds
payload["evidence"]["releaseChannelFutureSkewSeconds"] = release_channel_future_skew_seconds
payload["evidence"]["requiredFamilyCount"] = len(required_family_ids)
payload["evidence"]["ledgerFamilyCount"] = len(families)
payload["evidence"]["missingFamilyIds"] = missing_family_ids
payload["evidence"]["nonReadyFamilyIds"] = non_ready_family_ids
payload["evidence"]["missingTestRefs"] = missing_test_refs
payload["evidence"]["recursiveWorkflowGateTests"] = required_recursive_gate_tests
payload["evidence"]["recursiveWorkflowGateTestCount"] = len(required_recursive_gate_tests)
payload["evidence"]["recursiveWorkflowGateProofAreas"] = required_recursive_gate_proof_area_ids
payload["evidence"]["recursiveWorkflowGateProofAreaCount"] = len(required_recursive_gate_proof_area_ids)
payload["evidence"]["recursiveWorkflowGateProofAreaTests"] = proof_area_tests
payload["evidence"]["recursiveWorkflowGateProofAreaSummaries"] = proof_area_summaries
payload["evidence"]["recursiveWorkflowGateMissingProofAreas"] = missing_recursive_gate_proof_areas
payload["evidence"]["recursiveWorkflowGateMissingProofAreaTests"] = missing_recursive_gate_proof_area_tests
payload["evidence"]["recursiveWorkflowGateUnmappedTests"] = unmapped_recursive_gate_tests
payload["evidence"]["recursiveWorkflowGateUnexpectedProofAreaTests"] = unexpected_proof_area_tests
payload["evidence"]["recursiveWorkflowGateReturnSurfaceRequirement"] = return_surface_requirement
payload["evidence"]["missingParityReceipts"] = missing_parity_receipts
payload["evidence"]["failingParityReceipts"] = failing_parity_receipts
payload["evidence"]["failingParityReceiptsExternalOnly"] = (
    len(external_only_failing_parity_receipts) == len(failing_parity_receipts)
    and bool(failing_parity_receipts)
)
payload["evidence"]["failingParityReceiptsExternal"] = external_only_failing_parity_receipts
payload["evidence"]["familyReceiptsProven"] = family_receipts_proven
payload["evidence"]["sourceArtifactChecks"] = {
    "ledger": ledger_exists,
    "sr4Receipt": sr4_receipt_exists,
    "dualHeadTests": dual_head_tests_exist,
    "complianceTests": compliance_tests_exist,
    "uiGateTests": ui_gate_tests_exist,
    "workflowGateTests": workflow_gate_tests_exist,
}
payload["evidence"]["sr4ReceiptChannelId"] = sr4_receipt_channel_id
payload["evidence"]["sr4ReceiptReleaseVersion"] = sr4_receipt_release_version
payload["evidence"]["sr4ReceiptReleaseVersionAliases"] = sr4_receipt_release_version_aliases
payload["evidence"]["sr4ReceiptGeneratedAt"] = sr4_receipt_generated_at_raw
payload["evidence"]["sr4ReceiptBinding"] = sr4_receipt_binding
payload["evidence"]["failureCount"] = len(payload["reasons"])

payload["sourceArtifactReview"] = {
    "status": "pass" if not source_artifact_reasons else "fail",
    "summary": (
        "SR6 ledger, SR4 baseline receipt, and executable test sources are present."
        if not source_artifact_reasons
        else "One or more SR6 ledger, SR4 baseline, or executable test sources are missing."
    ),
    "reasons": source_artifact_reasons,
    "checks": payload["evidence"]["sourceArtifactChecks"],
}
payload["releaseChannelReview"] = {
    "status": "pass" if not release_channel_reasons else "fail",
    "summary": (
        "SR6 workflow parity proof is aligned to a current release-channel identity."
        if not release_channel_reasons
        else "SR6 workflow parity proof is missing or drifting from release-channel identity."
    ),
    "reasons": release_channel_reasons,
    "path": str(release_channel_path),
    "source": release_channel_source,
    "channelId": release_channel_channel_id,
    "releaseVersion": release_channel_release_version,
    "generatedAt": release_channel_generated_at_raw,
    "ageSeconds": release_channel_age_seconds,
    "futureSkewSeconds": release_channel_future_skew_seconds,
    "maxAgeSeconds": RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS,
    "maxFutureSkewSeconds": RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS,
}
payload["sr4BaselineReview"] = {
    "status": "pass" if not sr4_baseline_reasons else "fail",
    "summary": (
        "SR4 baseline workflow parity proof is present and aligned for SR6 carry-forward closure."
        if not sr4_baseline_reasons
        else "SR4 baseline workflow parity proof is missing, stale, or misaligned for SR6 carry-forward closure."
    ),
    "reasons": sr4_baseline_reasons,
    "path": str(sr4_receipt_path),
    "channelId": sr4_receipt_channel_id,
    "releaseVersion": sr4_receipt_release_version,
    "generatedAt": sr4_receipt_generated_at_raw,
    "binding": sr4_receipt_binding,
}
payload["workflowFamilyReview"] = {
    "status": "pass" if not workflow_family_reasons else "fail",
    "summary": (
        "All required SR6 workflow families are present and ready."
        if not workflow_family_reasons
        else "One or more required SR6 workflow families are missing or non-ready."
    ),
    "reasons": workflow_family_reasons,
    "requiredFamilyCount": len(required_family_ids),
    "ledgerFamilyCount": len(families),
    "missingFamilyIds": missing_family_ids,
    "nonReadyFamilyIds": non_ready_family_ids,
}
payload["testReferenceReview"] = {
    "status": "pass" if not test_reference_reasons else "fail",
    "summary": (
        "SR6 workflow parity audit tests resolve to executable test sources."
        if not test_reference_reasons
        else "SR6 workflow parity ledger still references missing executable tests."
    ),
    "reasons": test_reference_reasons,
    "missingTestRefs": missing_test_refs,
}
payload["recursiveWorkflowGateReview"] = {
    "status": "pass" if not workflow_gate_reasons else "fail",
    "summary": (
        "Recursive workflow gate tests executed and the SR6 ledger keeps recursive menu workflows, legacy UI-control workflows, "
        "quick-action roots, and returned-surface parity explicit."
        if not workflow_gate_reasons
        else "Recursive workflow gate execution or the SR6 ledger recursive proof-area requirements are incomplete."
    ),
    "reasons": workflow_gate_reasons,
    "workflowGateExit": workflow_gate_exit,
    "requiredTests": required_recursive_gate_tests,
    "proofAreas": required_recursive_gate_proof_area_ids,
    "proofAreaTests": proof_area_tests,
    "proofAreaSummaries": proof_area_summaries,
    "returnSurfaceRequirement": return_surface_requirement,
}
payload["parityReceiptReview"] = {
    "status": "pass" if not parity_receipt_reasons else "fail",
    "summary": (
        "SR6 family-specific carry-forward receipts are present and passing."
        if not parity_receipt_reasons
        else "SR6 family-specific carry-forward receipts are missing, failing, or externally blocked."
    ),
    "reasons": parity_receipt_reasons,
    "missingParityReceipts": missing_parity_receipts,
    "failingParityReceipts": failing_parity_receipts,
    "failingParityReceiptsExternalOnly": payload["evidence"]["failingParityReceiptsExternalOnly"],
    "failingParityReceiptsExternal": external_only_failing_parity_receipts,
}
payload["materializationReview"] = {
    "status": "pass" if not materialization_reasons else "fail",
    "summary": (
        "SR6 family execution, verification, and receipt materializers exited within allowed bounds."
        if not materialization_reasons
        else "One or more SR6 family materializers exited unexpectedly."
    ),
    "reasons": materialization_reasons,
    "executionExit": execution_exit,
    "verificationExit": verification_exit,
    "materializerExit": materializer_exit,
}

try:
    final_epoch_chain = validate_committed_epoch_chain()
    if not workflow_epoch_chain:
        raise ValueError("the initial committed epoch chain was not accepted")
    for stage in ("execution", "verification", "parity"):
        if (
            final_epoch_chain[stage]["binding"]
            != workflow_epoch_chain[stage]["binding"]
        ):
            raise ValueError(
                f"the committed {stage} manifest changed before desktop receipt publication"
            )
    final_parity_manifest = final_epoch_chain["parity"]["manifest"]
    if (
        final_parity_manifest.get("epochCommitId")
        != parity_stage_epoch_commit_id
        or final_parity_manifest.get("producerRunId") != epoch_producer_run_id
        or final_parity_manifest.get("candidateSnapshotId")
        != candidate_snapshot_id
        or final_parity_manifest.get("executionRunDigest")
        != execution_run_digest
    ):
        raise ValueError(
            "the committed parity epoch identity changed before desktop receipt publication"
        )
    payload["evidence"]["workflowFamilyEpochCommitted"] = True
except (KeyError, OSError, TypeError, ValueError) as exc:
    append_reason(
        "SR6 committed workflow-family epoch chain changed before desktop receipt publication: "
        + str(exc),
        parity_receipt_reasons,
        materialization_reasons,
    )
    payload["status"] = "fail"
    payload["summary"] = (
        "SR6 desktop workflow carry-forward parity is not yet exhaustively proven."
    )
    payload["evidence"]["workflowFamilyEpochCommitted"] = False

payload["evidence"]["failureCount"] = len(payload["reasons"])
payload["parityReceiptReview"]["status"] = (
    "pass" if not parity_receipt_reasons else "fail"
)
payload["parityReceiptReview"]["summary"] = (
    "SR6 family-specific carry-forward receipts are present and passing."
    if not parity_receipt_reasons
    else "SR6 family-specific carry-forward receipts are missing, failing, or externally blocked."
)
payload["materializationReview"]["status"] = (
    "pass" if not materialization_reasons else "fail"
)
payload["materializationReview"]["summary"] = (
    "SR6 family execution, verification, and receipt materializers exited within allowed bounds."
    if not materialization_reasons
    else "One or more SR6 family materializers exited unexpectedly."
)

write_receipt_atomically(receipt_path, payload)
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[sr6-workflow-parity] PASS"
