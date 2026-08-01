from __future__ import annotations

import hashlib
import json
import os
import stat
import uuid
import xml.etree.ElementTree as ET
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any


MAX_TRX_BYTES = 32 * 1024 * 1024
MAX_REGULAR_INPUT_BYTES = 64 * 1024 * 1024
MAX_BUILD_OUTPUT_FILES = 20000
MAX_BUILD_OUTPUT_BYTES = 2 * 1024 * 1024 * 1024
VOLATILE_ROOT_BUILD_OUTPUT_FILE_NAMES = frozenset(
    {
        ".msCoverageExtensionSourceRootsMapping_Chummer.Tests",
        ".msCoverageSourceRootsMapping_Chummer.Tests",
        "CoverletSourceRootsMapping_Chummer.Tests",
    }
)
WORKFLOW_STAGE_MAX_AGE_SECONDS = 86400
WORKFLOW_STAGE_MAX_FUTURE_SKEW_SECONDS = 300
WORKFLOW_EXECUTION_MAX_DURATION_SECONDS = 21600
TRX_TIMESTAMP_TOLERANCE_SECONDS = 5
TRX_NAMESPACE_URI = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
TRX_NAMESPACE = {"t": TRX_NAMESPACE_URI}
ZERO_COUNTER_NAMES = (
    "failed",
    "error",
    "timeout",
    "aborted",
    "inconclusive",
    "notExecuted",
    "notRunnable",
    "disconnected",
    "warning",
)

_DUAL_HEAD_TESTS = {
    "Avalonia_and_Blazor_all_workspace_section_actions_render_matching_sections",
    "Avalonia_and_Blazor_attributes_and_skills_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_character_settings_save_updates_shared_state",
    "Avalonia_and_Blazor_combat_and_cyberware_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_cyberware_workspace_preserves_modular_legacy_fixture_details",
    "Avalonia_and_Blazor_dialog_and_import_commands_expose_matching_dialog_contracts",
    "Avalonia_and_Blazor_dialog_workflow_keeps_shell_regions_in_parity",
    "Avalonia_and_Blazor_download_export_and_print_commands_prepare_matching_receipts",
    "Avalonia_and_Blazor_gear_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_magic_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_metadata_save_roundtrip_match",
    "Avalonia_and_Blazor_support_family_workspace_actions_render_matching_sections",
    "Avalonia_and_Blazor_tab_selection_loads_same_workspace_section",
    "Avalonia_and_Blazor_two_workspace_import_switch_save_flow_matches",
    "Avalonia_and_Blazor_workspace_action_summary_matches",
}
_AVALONIA_GATE_TESTS = {
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
    "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Menu_click_surfaces_visible_command_choices_in_shell",
    "Runtime_loaded_runner_quick_action_workflows_materialize_dialog_contracts_and_continuations_across_sr4_sr5_and_sr6",
    "Runtime_loaded_runner_tabpanel_covers_legacy_tabs_actions_and_backed_quick_actions_across_sr4_sr5_and_sr6",
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
}
_WORKFLOW_GATE_TESTS = {
    "Legacy_ui_controls_are_exhaustively_classified",
    "Legacy_ui_controls_keep_recursive_parity",
    "Menu_dialog_workflows_are_exhaustively_classified",
    "Menu_dialog_workflows_keep_recursive_parity",
    "Quick_action_roots_are_exhaustively_classified",
    "Runtime_backed_new_character_character_settings_materialize_house_rule_and_build_method_defaults",
    "Runtime_backed_new_character_conditional_workflow_matrix_materializes_priority_and_karma_branches_across_sr4_sr5_and_sr6",
}
CANONICAL_TEST_CLASS_BY_NAME = {
    **{
        name: "Chummer.Tests.Presentation.DualHeadAcceptanceTests"
        for name in _DUAL_HEAD_TESTS
    },
    **{
        name: "Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests"
        for name in _AVALONIA_GATE_TESTS
    },
    **{
        name: "Chummer.Tests.Presentation.WorkflowParityGateTests"
        for name in _WORKFLOW_GATE_TESTS
    },
}
# Port 8088 is the published local-stack ingress.  Workflow-family proof must
# own the process it probes, so use a dedicated loopback port instead of
# colliding with an already-running self-hosted Chummer API container.
CANONICAL_API_BASE_URL = "http://127.0.0.1:18088"
CANONICAL_API_PROBE_PATHS = (
    "/api/workspaces?maxCount=1",
    "/api/shell/bootstrap",
)
WORKFLOW_STAGE_MANIFEST_CONTRACTS = {
    "execution": "chummer6-ui.workflow_family_execution_epoch_manifest",
    "verification": "chummer6-ui.workflow_family_verification_epoch_manifest",
    "parity": "chummer6-ui.workflow_family_parity_epoch_manifest",
}
WORKFLOW_STAGE_MANIFEST_KEYS = {
    "schemaVersion",
    "publicationId",
    "generatedAt",
    "contract_name",
    "commitState",
    "status",
    "edition",
    "stage",
    "producerRunId",
    "candidateSnapshotId",
    "workflowEpochId",
    "executionRunDigest",
    "executionStartedAt",
    "executionCompletedAt",
    "candidateDigest",
    "releaseIdentity",
    "receiptCount",
    "receiptSetDigest",
    "receipts",
    "upstreamStageManifests",
    "epochCommitId",
}


def validate_api_probe_contract(
    api_probe: Any,
    dotnet_host_path: Path,
    api_project_path: Path,
) -> None:
    if not isinstance(api_probe, dict):
        raise ValueError("API runtime proof must be an object")
    expected_command = [
        str(dotnet_host_path),
        "run",
        "--project",
        str(api_project_path),
        "--configuration",
        "Release",
        "--no-launch-profile",
        "--no-restore",
        "--urls",
        CANONICAL_API_BASE_URL,
    ]
    if api_probe.get("baseUrl") != CANONICAL_API_BASE_URL:
        raise ValueError("API runtime proof does not use the canonical loopback URL")
    if api_probe.get("autostarted") is not True:
        raise ValueError("API runtime proof was not started by the canonical producer")
    if api_probe.get("autostartCommand") != expected_command:
        raise ValueError("API runtime proof command is not canonical")
    if api_probe.get("processAliveAtProof") is not True:
        raise ValueError("API runtime process was not alive when proof was captured")
    if api_probe.get("warmed") is not True:
        raise ValueError("API runtime surface was not confirmed by a warmed probe")
    process_id = api_probe.get("autostartPid")
    if type(process_id) is not int or process_id <= 0:
        raise ValueError("API runtime proof carries an invalid process ID")
    results = api_probe.get("results")
    if not isinstance(results, list) or len(results) != len(CANONICAL_API_PROBE_PATHS):
        raise ValueError("API runtime proof has an incomplete probe inventory")
    observed_paths: list[str] = []
    for result in results:
        if not isinstance(result, dict):
            raise ValueError("API runtime probe result must be an object")
        path = result.get("path")
        status_code = result.get("statusCode")
        if (
            not isinstance(path, str)
            or result.get("ok") is not True
            or type(status_code) is not int
            or status_code not in {200, 401, 403, 405}
            or result.get("error") not in {"", None}
        ):
            raise ValueError("API runtime probe result is not a clean canonical route proof")
        observed_paths.append(path)
    if tuple(observed_paths) != CANONICAL_API_PROBE_PATHS:
        raise ValueError("API runtime probe paths are not the canonical ordered inventory")


def _read_regular_bytes(
    path: Path,
    label: str,
    max_bytes: int,
    *,
    require_nonempty: bool = False,
) -> bytes:
    if path.is_symlink():
        raise ValueError(f"{label} must not be a symlink: {path}")
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0),
        )
    except OSError as exc:
        raise ValueError(f"{label} is missing or unreadable: {path}: {exc}") from exc
    try:
        before = os.fstat(descriptor)
        if not stat.S_ISREG(before.st_mode):
            raise ValueError(f"{label} is not a regular file: {path}")
        if (require_nonempty and before.st_size <= 0) or before.st_size > max_bytes:
            raise ValueError(
                f"{label} size is outside the permitted range: {before.st_size}"
            )
        chunks: list[bytes] = []
        total = 0
        while True:
            chunk = os.read(descriptor, min(1024 * 1024, max_bytes + 1 - total))
            if not chunk:
                break
            chunks.append(chunk)
            total += len(chunk)
            if total > max_bytes:
                raise ValueError(f"{label} exceeds {max_bytes} bytes")
        after = os.fstat(descriptor)
    finally:
        os.close(descriptor)
    if (
        before.st_dev != after.st_dev
        or before.st_ino != after.st_ino
        or before.st_mode != after.st_mode
        or before.st_size != after.st_size
        or before.st_mtime_ns != after.st_mtime_ns
        or total != after.st_size
    ):
        raise ValueError(f"{label} changed while being read: {path}")
    return b"".join(chunks)


def _binding(path: Path, raw: bytes) -> dict[str, Any]:
    return {
        "path": str(path.resolve()),
        "sha256": hashlib.sha256(raw).hexdigest(),
        "sizeBytes": len(raw),
    }


def file_binding(path: Path, label: str) -> dict[str, Any]:
    return _binding(path, _read_regular_bytes(path, label, MAX_REGULAR_INPUT_BYTES))


def _canonical_digest(value: Any) -> str:
    return hashlib.sha256(
        json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()


def _is_sha256(value: Any) -> bool:
    return (
        isinstance(value, str)
        and len(value) == 64
        and all(character in "0123456789abcdef" for character in value)
    )


def _parse_offset_timestamp(value: Any, label: str) -> datetime:
    if not isinstance(value, str) or not value or value != value.strip():
        raise ValueError(f"{label} must be a canonical nonblank timestamp")
    normalized = value[:-1] + "+00:00" if value.endswith("Z") else value
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError as exc:
        raise ValueError(f"{label} is not an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ValueError(f"{label} must include an explicit UTC offset")
    return parsed


def _canonical_uuid(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise ValueError(f"{label} must be a nonblank UUID")
    try:
        parsed = uuid.UUID(value)
    except ValueError as exc:
        raise ValueError(f"{label} must be a UUID") from exc
    if parsed.int == 0:
        raise ValueError(f"{label} must not be the zero UUID")
    return str(parsed)


def _canonical_utc_timestamp(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def workflow_stage_manifest_path(repo_root: Path, edition: str, stage: str) -> Path:
    if edition not in {"sr4", "sr6"}:
        raise ValueError(f"unsupported workflow manifest edition: {edition}")
    if stage not in WORKFLOW_STAGE_MANIFEST_CONTRACTS:
        raise ValueError(f"unsupported workflow manifest stage: {stage}")
    return (
        repo_root
        / ".codex-studio/published/workflow-family-parity/epochs"
        / edition
        / f"{stage}.generated.json"
    )


def workflow_stage_receipt_record(path: Path, payload: dict[str, Any]) -> dict[str, Any]:
    evidence = payload.get("evidence")
    if not isinstance(evidence, dict):
        raise ValueError(f"workflow stage receipt evidence must be an object: {path}")
    family_id = evidence.get("familyId")
    if not isinstance(family_id, str) or not family_id or family_id != family_id.strip():
        raise ValueError(f"workflow stage receipt familyId is invalid: {path}")
    binding = file_binding(path, "workflow stage receipt")
    return {
        "familyId": family_id,
        "path": binding["path"],
        "sha256": binding["sha256"],
        "sizeBytes": binding["sizeBytes"],
        "contractName": payload.get("contract_name"),
        "status": payload.get("status"),
    }


def execution_run_digest_for(
    *,
    edition: str,
    producer_run_id: str,
    execution_started_at: str,
    execution_completed_at: str,
    release_identity: dict[str, Any],
    candidate_digest: str,
    test_execution_records: dict[str, dict[str, Any]],
    api_probe: dict[str, Any],
) -> str:
    normalized_tests = {
        test_name: {
            key: record.get(key)
            for key in (
                "testName",
                "testMethodClassName",
                "testId",
                "trxRunId",
                "executionId",
                "attemptStartedAt",
                "attemptCompletedAt",
                "trxStartedAt",
                "trxCompletedAt",
                "resultStartedAt",
                "resultCompletedAt",
                "exitCode",
                "attemptCount",
                "outcomes",
                "resultCount",
                "unexpectedTestNames",
                "summaryOutcome",
                "counters",
                "summaryValid",
                "trx",
            )
        }
        for test_name, record in sorted(test_execution_records.items())
        if isinstance(record, dict)
    }
    return _canonical_digest(
        {
            "schemaVersion": 1,
            "edition": edition,
            "producerRunId": producer_run_id,
            "executionStartedAt": execution_started_at,
            "executionCompletedAt": execution_completed_at,
            "releaseIdentity": release_identity,
            "candidateDigest": candidate_digest,
            "testExecutions": normalized_tests,
            "apiProbe": api_probe,
        }
    )


def build_workflow_stage_manifest(
    *,
    edition: str,
    stage: str,
    status: str,
    generated_at: str,
    producer_run_id: str,
    candidate_snapshot_id: str,
    execution_run_digest: str,
    execution_started_at: str,
    execution_completed_at: str,
    candidate_digest: str,
    release_identity: dict[str, Any],
    receipt_records: list[dict[str, Any]],
    upstream_stage_manifests: list[dict[str, Any]],
) -> dict[str, Any]:
    if status not in {"pass", "fail"}:
        raise ValueError("workflow stage manifest status must be pass or fail")
    ordered_receipts = sorted(receipt_records, key=lambda item: str(item.get("familyId")))
    family_ids = [record.get("familyId") for record in ordered_receipts]
    if (
        not ordered_receipts
        or any(not isinstance(family_id, str) or not family_id for family_id in family_ids)
        or len(family_ids) != len(set(family_ids))
    ):
        raise ValueError("workflow stage manifest receipt inventory is empty or duplicated")
    receipt_set_digest = _canonical_digest(ordered_receipts)
    publication_id = str(uuid.uuid4())
    core = {
        "schemaVersion": 1,
        "publicationId": publication_id,
        "generatedAt": generated_at,
        "contract_name": WORKFLOW_STAGE_MANIFEST_CONTRACTS[stage],
        "commitState": "committed",
        "status": status,
        "edition": edition,
        "stage": stage,
        "producerRunId": producer_run_id,
        "candidateSnapshotId": candidate_snapshot_id,
        "workflowEpochId": candidate_snapshot_id,
        "executionRunDigest": execution_run_digest,
        "executionStartedAt": execution_started_at,
        "executionCompletedAt": execution_completed_at,
        "candidateDigest": candidate_digest,
        "releaseIdentity": release_identity,
        "receiptCount": len(ordered_receipts),
        "receiptSetDigest": receipt_set_digest,
        "receipts": ordered_receipts,
        "upstreamStageManifests": upstream_stage_manifests,
    }
    return {**core, "epochCommitId": _canonical_digest(core)}


def validate_workflow_stage_manifest(
    *,
    manifest_path: Path,
    repo_root: Path,
    edition: str,
    stage: str,
    expected_receipts: dict[str, Path],
    expected_release_identity: dict[str, Any],
    expected_upstream_stage_manifests: list[dict[str, Any]],
    require_pass: bool = True,
) -> dict[str, Any]:
    expected_manifest_path = workflow_stage_manifest_path(repo_root, edition, stage)
    if manifest_path.resolve(strict=False) != expected_manifest_path.resolve(strict=False):
        raise ValueError("workflow stage manifest path is not canonical")
    raw = _read_regular_bytes(
        manifest_path, "workflow stage manifest", MAX_REGULAR_INPUT_BYTES, require_nonempty=True
    )
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError("workflow stage manifest is not valid JSON") from exc
    if not isinstance(payload, dict):
        raise ValueError("workflow stage manifest root must be an object")
    if set(payload) != WORKFLOW_STAGE_MANIFEST_KEYS:
        raise ValueError("workflow stage manifest top-level shape is invalid")
    if type(payload.get("schemaVersion")) is not int or payload.get("schemaVersion") != 1:
        raise ValueError("workflow stage manifest schemaVersion must equal integer 1")
    if payload.get("contract_name") != WORKFLOW_STAGE_MANIFEST_CONTRACTS.get(stage):
        raise ValueError("workflow stage manifest contract is invalid")
    if payload.get("commitState") != "committed":
        raise ValueError("workflow stage manifest is not committed")
    if payload.get("status") not in {"pass", "fail"}:
        raise ValueError("workflow stage manifest status is invalid")
    if require_pass and payload.get("status") != "pass":
        raise ValueError("workflow stage manifest is not passing")
    if payload.get("edition") != edition or payload.get("stage") != stage:
        raise ValueError("workflow stage manifest edition/stage identity is invalid")
    publication_id = payload.get("publicationId")
    try:
        publication_id_is_valid = (
            isinstance(publication_id, str)
            and str(uuid.UUID(publication_id)) == publication_id
        )
    except (ValueError, AttributeError):
        publication_id_is_valid = False
    if not publication_id_is_valid:
        raise ValueError("workflow stage manifest publicationId must be a canonical UUID")
    generated_at = _parse_offset_timestamp(
        payload.get("generatedAt"), "workflow stage manifest generatedAt"
    )
    execution_started_at = _parse_offset_timestamp(
        payload.get("executionStartedAt"), "workflow stage executionStartedAt"
    )
    execution_completed_at = _parse_offset_timestamp(
        payload.get("executionCompletedAt"), "workflow stage executionCompletedAt"
    )
    if not execution_started_at <= execution_completed_at <= generated_at:
        raise ValueError("workflow stage manifest execution timestamps are out of order")
    execution_duration_seconds = (
        execution_completed_at - execution_started_at
    ).total_seconds()
    if execution_duration_seconds > WORKFLOW_EXECUTION_MAX_DURATION_SECONDS:
        raise ValueError("workflow stage manifest execution duration exceeds the fixed limit")
    now = datetime.now(timezone.utc)
    age_seconds = (now - generated_at).total_seconds()
    if age_seconds > WORKFLOW_STAGE_MAX_AGE_SECONDS:
        raise ValueError("workflow stage manifest is stale")
    if age_seconds < -WORKFLOW_STAGE_MAX_FUTURE_SKEW_SECONDS:
        raise ValueError("workflow stage manifest is too far in the future")
    producer_run_id = payload.get("producerRunId")
    if require_pass:
        try:
            producer_run_id_is_valid = (
                isinstance(producer_run_id, str)
                and str(uuid.UUID(producer_run_id)) == producer_run_id
            )
        except (ValueError, AttributeError):
            producer_run_id_is_valid = False
        if not producer_run_id_is_valid:
            raise ValueError("workflow stage manifest producerRunId must be a canonical UUID")
    candidate_snapshot_id = payload.get("candidateSnapshotId")
    execution_run_digest = payload.get("executionRunDigest")
    candidate_digest = payload.get("candidateDigest")
    invalid_epoch_identity = (
        payload.get("workflowEpochId") != candidate_snapshot_id
        or (
            require_pass
            and (
                not _is_sha256(candidate_snapshot_id)
                or not _is_sha256(execution_run_digest)
                or not _is_sha256(candidate_digest)
            )
        )
        or (
            not require_pass
            and any(
                value not in {"", None} and not _is_sha256(value)
                for value in (
                    candidate_snapshot_id,
                    execution_run_digest,
                    candidate_digest,
                )
            )
        )
    )
    if invalid_epoch_identity:
        raise ValueError("workflow stage manifest epoch/candidate identity is invalid")
    if payload.get("releaseIdentity") != expected_release_identity:
        raise ValueError("workflow stage manifest releaseIdentity is invalid")
    expected_upstream_count = {"execution": 0, "verification": 1, "parity": 2}[stage]
    if (
        require_pass
        and len(expected_upstream_stage_manifests) != expected_upstream_count
    ) or (
        not require_pass
        and len(expected_upstream_stage_manifests) not in {0, expected_upstream_count}
    ):
        raise ValueError("workflow stage manifest expected upstream chain is incomplete")
    if any(
        not isinstance(binding, dict)
        or set(binding) != {"path", "sha256", "sizeBytes"}
        or not isinstance(binding.get("path"), str)
        or not _is_sha256(binding.get("sha256"))
        or type(binding.get("sizeBytes")) is not int
        or binding.get("sizeBytes") <= 0
        for binding in expected_upstream_stage_manifests
    ):
        raise ValueError("workflow stage manifest expected upstream binding is invalid")
    if payload.get("upstreamStageManifests") != expected_upstream_stage_manifests:
        raise ValueError("workflow stage manifest upstream manifest chain is invalid")
    records = payload.get("receipts")
    if not isinstance(records, list) or payload.get("receiptCount") != len(records):
        raise ValueError("workflow stage manifest receiptCount is invalid")
    if [record.get("familyId") for record in records if isinstance(record, dict)] != sorted(
        expected_receipts
    ):
        raise ValueError("workflow stage manifest receipts are not in canonical family order")
    if payload.get("receiptSetDigest") != _canonical_digest(records):
        raise ValueError("workflow stage manifest receiptSetDigest is invalid")
    expected_core = {key: value for key, value in payload.items() if key != "epochCommitId"}
    if payload.get("epochCommitId") != _canonical_digest(expected_core):
        raise ValueError("workflow stage manifest epochCommitId is invalid")
    expected_family_ids = set(expected_receipts)
    observed_family_ids: set[str] = set()
    receipt_payloads: dict[str, dict[str, Any]] = {}
    expected_contract = f"chummer6-ui.{edition}_workflow_family_{stage}_receipt"
    for record in records:
        if not isinstance(record, dict) or set(record) != {
            "familyId",
            "path",
            "sha256",
            "sizeBytes",
            "contractName",
            "status",
        }:
            raise ValueError("workflow stage manifest receipt record shape is invalid")
        family_id = record.get("familyId")
        if not isinstance(family_id, str) or family_id not in expected_receipts:
            raise ValueError("workflow stage manifest contains an unexpected family receipt")
        if family_id in observed_family_ids:
            raise ValueError("workflow stage manifest contains a duplicate family receipt")
        observed_family_ids.add(family_id)
        receipt_path = expected_receipts[family_id]
        receipt_raw = _read_regular_bytes(
            receipt_path, "workflow stage family receipt", MAX_REGULAR_INPUT_BYTES, require_nonempty=True
        )
        current_binding = _binding(receipt_path, receipt_raw)
        if {
            "path": record.get("path"),
            "sha256": record.get("sha256"),
            "sizeBytes": record.get("sizeBytes"),
        } != current_binding:
            raise ValueError("workflow stage manifest does not bind current receipt bytes")
        try:
            receipt_payload = json.loads(receipt_raw.decode("utf-8-sig"))
        except (UnicodeError, json.JSONDecodeError) as exc:
            raise ValueError("workflow stage family receipt is not valid JSON") from exc
        if not isinstance(receipt_payload, dict):
            raise ValueError("workflow stage family receipt root must be an object")
        evidence = receipt_payload.get("evidence")
        if not isinstance(evidence, dict):
            raise ValueError("workflow stage family receipt evidence must be an object")
        if (
            record.get("contractName") != expected_contract
            or receipt_payload.get("contract_name") != expected_contract
            or record.get("status") != receipt_payload.get("status")
            or (require_pass and receipt_payload.get("status") != "pass")
            or receipt_payload.get("producerRunId") != producer_run_id
            or receipt_payload.get("candidateSnapshotId")
            != payload.get("candidateSnapshotId")
            or receipt_payload.get("workflowEpochId")
            != payload.get("candidateSnapshotId")
            or receipt_payload.get("executionRunDigest")
            != payload.get("executionRunDigest")
            or evidence.get("edition") != edition
            or evidence.get("familyId") != family_id
            or evidence.get("producerRunId") != producer_run_id
            or evidence.get("candidateSnapshotId")
            != payload.get("candidateSnapshotId")
            or evidence.get("workflowEpochId")
            != payload.get("candidateSnapshotId")
            or evidence.get("executionRunDigest")
            != payload.get("executionRunDigest")
            or evidence.get("candidateDigest") != payload.get("candidateDigest")
            or evidence.get("releaseIdentity") != expected_release_identity
        ):
            raise ValueError("workflow stage family receipt identity does not match its manifest")
        receipt_payloads[family_id] = receipt_payload
    if observed_family_ids != expected_family_ids:
        raise ValueError("workflow stage manifest does not cover the exact family inventory")
    expected_manifest_status = (
        "pass"
        if receipt_payloads
        and all(item.get("status") == "pass" for item in receipt_payloads.values())
        else "fail"
    )
    if payload.get("status") != expected_manifest_status:
        raise ValueError("workflow stage manifest status does not match its receipt set")
    if stage == "execution" and require_pass:
        merged_test_records: dict[str, dict[str, Any]] = {}
        expected_test_names: set[str] = set()
        api_probe: dict[str, Any] | None = None
        for receipt_payload in receipt_payloads.values():
            evidence = receipt_payload["evidence"]
            observed_probe = evidence.get("apiProbe")
            if not isinstance(observed_probe, dict):
                raise ValueError("execution manifest family receipt lacks API runtime proof")
            if api_probe is None:
                api_probe = observed_probe
            elif observed_probe != api_probe:
                raise ValueError("execution manifest family receipts disagree on API runtime proof")
            records_by_test = evidence.get("testExecutions")
            if not isinstance(records_by_test, dict):
                raise ValueError("execution manifest family receipt lacks testExecutions")
            audit_tests = evidence.get("auditTests")
            if (
                not isinstance(audit_tests, list)
                or any(
                    not isinstance(test_name, str)
                    or not test_name
                    or test_name != test_name.strip()
                    for test_name in audit_tests
                )
                or len(audit_tests) != len(set(audit_tests))
                or set(records_by_test) != set(audit_tests)
            ):
                raise ValueError(
                    "execution manifest family receipt test inventory does not match auditTests"
                )
            expected_test_names.update(audit_tests)
            for test_name, test_record in records_by_test.items():
                if not isinstance(test_record, dict):
                    raise ValueError("execution manifest test record must be an object")
                previous = merged_test_records.get(test_name)
                if previous is not None and previous != test_record:
                    raise ValueError("execution manifest family receipts disagree on a test record")
                merged_test_records[test_name] = test_record
        if set(merged_test_records) != expected_test_names:
            raise ValueError("execution manifest test union is incomplete")
        trx_run_ids = [record.get("trxRunId") for record in merged_test_records.values()]
        execution_ids = [record.get("executionId") for record in merged_test_records.values()]
        if (
            any(not isinstance(value, str) or not value for value in trx_run_ids)
            or len(trx_run_ids) != len(set(trx_run_ids))
        ):
            raise ValueError("execution manifest TRX run IDs are missing or reused")
        if (
            any(not isinstance(value, str) or not value for value in execution_ids)
            or len(execution_ids) != len(set(execution_ids))
        ):
            raise ValueError("execution manifest execution IDs are missing or reused")
        expected_execution_run_digest = execution_run_digest_for(
            edition=edition,
            producer_run_id=str(producer_run_id),
            execution_started_at=str(payload.get("executionStartedAt")),
            execution_completed_at=str(payload.get("executionCompletedAt")),
            release_identity=expected_release_identity,
            candidate_digest=str(payload.get("candidateDigest")),
            test_execution_records=merged_test_records,
            api_probe=api_probe or {},
        )
        if expected_execution_run_digest != payload.get("executionRunDigest"):
            raise ValueError("execution stage manifest does not match the bound test epoch")
    return {
        "manifest": payload,
        "binding": _binding(manifest_path, raw),
        "receiptPayloads": receipt_payloads,
    }


def build_desktop_execution_epoch(
    *,
    release_identity: dict[str, Any],
    candidate_snapshot_id: str,
    stage_manifests: dict[str, dict[str, dict[str, Any]]],
    stage_bindings: dict[str, dict[str, dict[str, Any]]],
    reference_time: datetime | None = None,
) -> dict[str, Any]:
    expected_editions = {"sr4", "sr6"}
    expected_stages = {"execution", "verification", "parity"}
    if set(stage_manifests) != expected_editions or set(stage_bindings) != expected_editions:
        raise ValueError("desktop execution epoch requires exact SR4/SR6 manifest sets")
    if not _is_sha256(candidate_snapshot_id):
        raise ValueError("desktop execution epoch candidateSnapshotId is invalid")
    if not isinstance(release_identity, dict):
        raise ValueError("desktop execution epoch releaseIdentity must be an object")
    release_subject = {
        key: release_identity.get(key)
        for key in (
            "channelId",
            "releaseVersion",
            "generatedAt",
            "sha256",
            "sizeBytes",
        )
    }
    if (
        not all(release_subject.get(key) for key in ("channelId", "releaseVersion", "generatedAt"))
        or not _is_sha256(release_subject.get("sha256"))
        or type(release_subject.get("sizeBytes")) is not int
        or release_subject["sizeBytes"] <= 0
    ):
        raise ValueError("desktop execution epoch release subject is invalid")
    now = reference_time or datetime.now(timezone.utc)
    if now.tzinfo is None or now.utcoffset() is None:
        raise ValueError("desktop execution epoch reference time must include an offset")

    runs: list[dict[str, Any]] = []
    parsed_bounds: dict[str, tuple[datetime, datetime]] = {}
    for edition in ("sr4", "sr6"):
        manifests = stage_manifests[edition]
        bindings = stage_bindings[edition]
        if set(manifests) != expected_stages or set(bindings) != expected_stages:
            raise ValueError(f"{edition} execution epoch stage inventory is incomplete")
        execution_manifest = manifests["execution"]
        identity = {
            key: execution_manifest.get(key)
            for key in (
                "producerRunId",
                "candidateSnapshotId",
                "workflowEpochId",
                "executionRunDigest",
                "executionStartedAt",
                "executionCompletedAt",
                "candidateDigest",
            )
        }
        producer_run_id = identity["producerRunId"]
        try:
            producer_run_id_is_valid = (
                isinstance(producer_run_id, str)
                and str(uuid.UUID(producer_run_id)) == producer_run_id
            )
        except (ValueError, AttributeError):
            producer_run_id_is_valid = False
        if not producer_run_id_is_valid:
            raise ValueError(f"{edition} execution epoch producerRunId is invalid")
        if (
            identity["candidateSnapshotId"] != candidate_snapshot_id
            or identity["workflowEpochId"] != candidate_snapshot_id
            or not _is_sha256(identity["executionRunDigest"])
            or not _is_sha256(identity["candidateDigest"])
        ):
            raise ValueError(f"{edition} execution epoch identity is invalid")
        started_at = _parse_offset_timestamp(
            identity["executionStartedAt"], f"{edition} execution epoch startedAt"
        )
        completed_at = _parse_offset_timestamp(
            identity["executionCompletedAt"], f"{edition} execution epoch completedAt"
        )
        if started_at > completed_at:
            raise ValueError(f"{edition} execution epoch bounds are inverted")
        if (completed_at - started_at).total_seconds() > WORKFLOW_EXECUTION_MAX_DURATION_SECONDS:
            raise ValueError(f"{edition} execution epoch duration exceeds the fixed limit")
        completion_age_seconds = (now - completed_at).total_seconds()
        if completion_age_seconds > WORKFLOW_STAGE_MAX_AGE_SECONDS:
            raise ValueError(f"{edition} execution epoch completion is stale")
        if completion_age_seconds < -WORKFLOW_STAGE_MAX_FUTURE_SKEW_SECONDS:
            raise ValueError(f"{edition} execution epoch completion is too far in the future")

        stage_records: list[dict[str, Any]] = []
        for stage in ("execution", "verification", "parity"):
            manifest = manifests[stage]
            binding = bindings[stage]
            if not isinstance(manifest, dict) or manifest.get("stage") != stage:
                raise ValueError(f"{edition}:{stage} execution epoch manifest is invalid")
            if manifest.get("edition") != edition or manifest.get("status") != "pass":
                raise ValueError(f"{edition}:{stage} execution epoch manifest is not passing")
            if manifest.get("releaseIdentity") != release_identity:
                raise ValueError(f"{edition}:{stage} execution epoch release identity differs")
            if any(manifest.get(key) != value for key, value in identity.items()):
                raise ValueError(f"{edition} execution epoch stage identities differ")
            generated_at = _parse_offset_timestamp(
                manifest.get("generatedAt"), f"{edition}:{stage} manifest generatedAt"
            )
            if generated_at < completed_at:
                raise ValueError(f"{edition}:{stage} manifest predates execution completion")
            manifest_age_seconds = (now - generated_at).total_seconds()
            if manifest_age_seconds > WORKFLOW_STAGE_MAX_AGE_SECONDS:
                raise ValueError(f"{edition}:{stage} manifest is stale")
            if manifest_age_seconds < -WORKFLOW_STAGE_MAX_FUTURE_SKEW_SECONDS:
                raise ValueError(f"{edition}:{stage} manifest is too far in the future")
            if (
                not isinstance(binding, dict)
                or set(binding) != {"path", "sha256", "sizeBytes"}
                or not _is_sha256(binding.get("sha256"))
                or type(binding.get("sizeBytes")) is not int
                or binding.get("sizeBytes") <= 0
            ):
                raise ValueError(f"{edition}:{stage} manifest binding is invalid")
            epoch_commit_id = manifest.get("epochCommitId")
            if not _is_sha256(epoch_commit_id):
                raise ValueError(f"{edition}:{stage} epochCommitId is invalid")
            stage_records.append(
                {
                    "stage": stage,
                    "epochCommitId": epoch_commit_id,
                    "manifest": {
                        "sha256": binding["sha256"],
                        "sizeBytes": binding["sizeBytes"],
                    },
                }
            )
        parsed_bounds[edition] = (started_at, completed_at)
        runs.append(
            {
                "edition": edition,
                "producerRunId": producer_run_id,
                "candidateDigest": identity["candidateDigest"],
                "executionRunDigest": identity["executionRunDigest"],
                "executionStartedAt": identity["executionStartedAt"],
                "executionCompletedAt": identity["executionCompletedAt"],
                "stages": stage_records,
            }
        )

    if len({run["producerRunId"] for run in runs}) != 2:
        raise ValueError("desktop execution epoch reuses a producerRunId")
    if len({run["executionRunDigest"] for run in runs}) != 2:
        raise ValueError("desktop execution epoch reuses an executionRunDigest")
    if parsed_bounds["sr4"][1] > parsed_bounds["sr6"][0]:
        raise ValueError("desktop execution epoch SR4/SR6 runs overlap or are out of order")
    span_seconds = int(
        (parsed_bounds["sr6"][1] - parsed_bounds["sr4"][0]).total_seconds()
    )
    if span_seconds > WORKFLOW_EXECUTION_MAX_DURATION_SECONDS:
        raise ValueError("desktop execution epoch span exceeds the fixed limit")
    core = {
        "schemaVersion": 1,
        "releaseSubject": release_subject,
        "candidateSnapshotId": candidate_snapshot_id,
        "runs": runs,
    }
    return {
        "executionEpochId": _canonical_digest(core),
        "executionEpoch": core,
        "executionEpochSpanSeconds": span_seconds,
        "executionEpochMaxSpanSeconds": WORKFLOW_EXECUTION_MAX_DURATION_SECONDS,
    }


def validate_workflow_edition_epoch_chain(
    *,
    repo_root: Path,
    edition: str,
    expected_receipts_by_stage: dict[str, dict[str, Path]],
    expected_release_identity: dict[str, Any],
    require_pass: bool = True,
) -> dict[str, dict[str, Any]]:
    expected_stages = {"execution", "verification", "parity"}
    if set(expected_receipts_by_stage) != expected_stages:
        raise ValueError("workflow edition epoch requires all three stage inventories")
    results: dict[str, dict[str, Any]] = {}
    upstream_stage_manifests: list[dict[str, Any]] = []
    for stage in ("execution", "verification", "parity"):
        result = validate_workflow_stage_manifest(
            manifest_path=workflow_stage_manifest_path(repo_root, edition, stage),
            repo_root=repo_root,
            edition=edition,
            stage=stage,
            expected_receipts=expected_receipts_by_stage[stage],
            expected_release_identity=expected_release_identity,
            expected_upstream_stage_manifests=list(upstream_stage_manifests),
            require_pass=require_pass,
        )
        results[stage] = result
        upstream_stage_manifests.append(result["binding"])
    return results


def snapshot_output_tree(root: Path, label: str) -> list[dict[str, Any]]:
    try:
        root_metadata = os.lstat(root)
    except OSError as exc:
        raise ValueError(f"{label} is missing or unreadable: {root}: {exc}") from exc
    if not stat.S_ISDIR(root_metadata.st_mode):
        raise ValueError(f"{label} must be a real directory, not a symlink or special file: {root}")

    bindings: list[dict[str, Any]] = []
    pending = [root]
    total_bytes = 0
    while pending:
        directory = pending.pop()
        try:
            entries = sorted(os.scandir(directory), key=lambda item: item.name)
        except OSError as exc:
            raise ValueError(f"{label} cannot be enumerated safely: {directory}: {exc}") from exc
        for entry in entries:
            path = Path(entry.path)
            try:
                metadata = entry.stat(follow_symlinks=False)
            except OSError as exc:
                raise ValueError(f"{label} entry cannot be inspected: {path}: {exc}") from exc
            if stat.S_ISDIR(metadata.st_mode):
                pending.append(path)
                continue
            if not stat.S_ISREG(metadata.st_mode):
                raise ValueError(f"{label} contains a symlink or special file: {path}")
            if path.parent == root and path.name in VOLATILE_ROOT_BUILD_OUTPUT_FILE_NAMES:
                continue
            binding = file_binding(path, label)
            binding["relativePath"] = path.relative_to(root).as_posix()
            bindings.append(binding)
            total_bytes += int(binding["sizeBytes"])
            if len(bindings) > MAX_BUILD_OUTPUT_FILES:
                raise ValueError(f"{label} exceeds {MAX_BUILD_OUTPUT_FILES} files")
            if total_bytes > MAX_BUILD_OUTPUT_BYTES:
                raise ValueError(f"{label} exceeds {MAX_BUILD_OUTPUT_BYTES} total bytes")
    if not bindings:
        raise ValueError(f"{label} contains no regular files: {root}")
    return sorted(bindings, key=lambda item: str(item["relativePath"]))


def validate_trx_contract(
    path: Path,
    expected_test_name: str,
    expected_binding: Any,
    expected_run_root: Path,
    expected_attempt_started_at: Any,
    expected_attempt_completed_at: Any,
) -> dict[str, Any]:
    expected_class_name = CANONICAL_TEST_CLASS_BY_NAME.get(expected_test_name)
    if not expected_class_name:
        raise ValueError(f"TRX test is outside the reviewed canonical inventory: {expected_test_name}")
    if not isinstance(expected_binding, dict) or set(expected_binding) != {
        "path",
        "sha256",
        "sizeBytes",
    }:
        raise ValueError(f"TRX binding has an invalid shape for {expected_test_name}")

    run_root = expected_run_root.resolve()
    resolved_path = path.resolve()
    try:
        resolved_path.relative_to(run_root)
    except ValueError as exc:
        raise ValueError(
            f"TRX path escapes the bound producer run root for {expected_test_name}: {path}"
        ) from exc
    if resolved_path.parent != run_root:
        raise ValueError(f"TRX path must be a direct child of the producer run root: {path}")
    if expected_binding.get("path") != str(resolved_path):
        raise ValueError(f"TRX binding path mismatch for {expected_test_name}")

    raw = _read_regular_bytes(
        path,
        f"TRX for {expected_test_name}",
        MAX_TRX_BYTES,
        require_nonempty=True,
    )
    if _binding(path, raw) != expected_binding:
        raise ValueError(f"TRX byte binding mismatch for {expected_test_name}")
    try:
        root = ET.fromstring(raw)
    except ET.ParseError as exc:
        raise ValueError(f"TRX is malformed for {expected_test_name}: {exc}") from exc

    if root.tag != f"{{{TRX_NAMESPACE_URI}}}TestRun":
        raise ValueError(f"TRX root must be the canonical namespaced TestRun for {expected_test_name}")
    trx_run_id = _canonical_uuid(
        root.attrib.get("id"), f"TRX TestRun.id for {expected_test_name}"
    )
    times_nodes = root.findall("t:Times", TRX_NAMESPACE)
    if len(times_nodes) != 1:
        raise ValueError(f"TRX must contain exactly one Times node for {expected_test_name}")
    trx_started_at = _parse_offset_timestamp(
        times_nodes[0].attrib.get("start"), f"TRX Times.start for {expected_test_name}"
    )
    trx_completed_at = _parse_offset_timestamp(
        times_nodes[0].attrib.get("finish"), f"TRX Times.finish for {expected_test_name}"
    )
    attempt_started_at = _parse_offset_timestamp(
        expected_attempt_started_at,
        f"test attempt startedAt for {expected_test_name}",
    )
    attempt_completed_at = _parse_offset_timestamp(
        expected_attempt_completed_at,
        f"test attempt completedAt for {expected_test_name}",
    )
    if attempt_started_at > attempt_completed_at:
        raise ValueError(f"test attempt bounds are inverted for {expected_test_name}")

    results = root.findall(".//t:UnitTestResult", TRX_NAMESPACE)
    if len(results) != 1:
        raise ValueError(f"TRX must contain exactly one UnitTestResult for {expected_test_name}")
    result = results[0]
    test_id = _canonical_uuid(
        result.attrib.get("testId"), f"TRX result testId for {expected_test_name}"
    )
    execution_id = _canonical_uuid(
        result.attrib.get("executionId"),
        f"TRX result executionId for {expected_test_name}",
    )
    result_started_at = _parse_offset_timestamp(
        result.attrib.get("startTime"),
        f"TRX result startTime for {expected_test_name}",
    )
    result_completed_at = _parse_offset_timestamp(
        result.attrib.get("endTime"),
        f"TRX result endTime for {expected_test_name}",
    )
    observed_result_name = str(result.attrib.get("testName") or "").strip()
    allowed_result_names = {
        expected_test_name,
        f"{expected_class_name}.{expected_test_name}",
    }
    if observed_result_name not in allowed_result_names:
        raise ValueError(f"TRX result identity mismatch for {expected_test_name}")
    if str(result.attrib.get("outcome") or "").strip() != "Passed":
        raise ValueError(f"TRX outcome must equal Passed for {expected_test_name}")

    definitions = root.findall(".//t:UnitTest", TRX_NAMESPACE)
    if len(definitions) != 1:
        raise ValueError(f"TRX must bind one UnitTest definition for {expected_test_name}")
    definition_test_id = _canonical_uuid(
        definitions[0].attrib.get("id"),
        f"TRX UnitTest.id for {expected_test_name}",
    )
    if definition_test_id != test_id:
        raise ValueError(f"TRX UnitTest definition is not linked for {expected_test_name}")
    definition_executions = definitions[0].findall("t:Execution", TRX_NAMESPACE)
    if len(definition_executions) != 1:
        raise ValueError(f"TRX UnitTest must bind one Execution for {expected_test_name}")
    definition_execution_id = _canonical_uuid(
        definition_executions[0].attrib.get("id"),
        f"TRX UnitTest Execution.id for {expected_test_name}",
    )
    if definition_execution_id != execution_id:
        raise ValueError(f"TRX UnitTest execution is not linked for {expected_test_name}")
    method = definitions[0].find("t:TestMethod", TRX_NAMESPACE)
    if method is None:
        raise ValueError(f"TRX is missing TestMethod metadata for {expected_test_name}")
    if (
        str(method.attrib.get("name") or "").strip() != expected_test_name
        or str(method.attrib.get("className") or "").strip() != expected_class_name
    ):
        raise ValueError(f"TRX TestMethod identity mismatch for {expected_test_name}")

    test_entries = root.findall(".//t:TestEntry", TRX_NAMESPACE)
    if len(test_entries) != 1:
        raise ValueError(f"TRX must contain exactly one TestEntry for {expected_test_name}")
    entry_test_id = _canonical_uuid(
        test_entries[0].attrib.get("testId"),
        f"TRX TestEntry.testId for {expected_test_name}",
    )
    entry_execution_id = _canonical_uuid(
        test_entries[0].attrib.get("executionId"),
        f"TRX TestEntry.executionId for {expected_test_name}",
    )
    if entry_test_id != test_id or entry_execution_id != execution_id:
        raise ValueError(f"TRX TestEntry is not linked for {expected_test_name}")

    summaries = root.findall(".//t:ResultSummary", TRX_NAMESPACE)
    if len(summaries) != 1 or summaries[0].attrib.get("outcome") != "Completed":
        raise ValueError(f"TRX summary must be exactly Completed for {expected_test_name}")
    counters_nodes = summaries[0].findall("t:Counters", TRX_NAMESPACE)
    if len(counters_nodes) != 1:
        raise ValueError(f"TRX must contain exactly one Counters node for {expected_test_name}")
    counters = dict(counters_nodes[0].attrib)
    required_counter_names = {"total", "executed", "passed", *ZERO_COUNTER_NAMES}
    if not required_counter_names.issubset(counters):
        raise ValueError(f"TRX counters are incomplete for {expected_test_name}")
    try:
        normalized_counters = {
            name: int(counters[name]) for name in required_counter_names
        }
    except ValueError as exc:
        raise ValueError(f"TRX counters are not integers for {expected_test_name}") from exc
    if (
        normalized_counters["total"] != 1
        or normalized_counters["executed"] != 1
        or normalized_counters["passed"] != 1
        or any(normalized_counters[name] != 0 for name in ZERO_COUNTER_NAMES)
    ):
        raise ValueError(f"TRX counters do not prove one clean pass for {expected_test_name}")

    ordered_timestamps = (
        attempt_started_at,
        trx_started_at,
        result_started_at,
        result_completed_at,
        trx_completed_at,
        attempt_completed_at,
    )
    tolerance = timedelta(seconds=TRX_TIMESTAMP_TOLERANCE_SECONDS)
    if any(
        earlier > later + tolerance
        for earlier, later in zip(ordered_timestamps, ordered_timestamps[1:])
    ):
        raise ValueError(f"TRX and attempt timestamps are out of order for {expected_test_name}")
    if (
        attempt_completed_at - attempt_started_at
    ).total_seconds() > WORKFLOW_EXECUTION_MAX_DURATION_SECONDS:
        raise ValueError(f"TRX attempt duration exceeds the fixed limit for {expected_test_name}")

    return {
        "testName": expected_test_name,
        "className": expected_class_name,
        "testId": test_id,
        "trxRunId": trx_run_id,
        "executionId": execution_id,
        "attemptStartedAt": _canonical_utc_timestamp(attempt_started_at),
        "attemptCompletedAt": _canonical_utc_timestamp(attempt_completed_at),
        "trxStartedAt": _canonical_utc_timestamp(trx_started_at),
        "trxCompletedAt": _canonical_utc_timestamp(trx_completed_at),
        "resultStartedAt": _canonical_utc_timestamp(result_started_at),
        "resultCompletedAt": _canonical_utc_timestamp(result_completed_at),
        "outcome": "Passed",
        "summaryOutcome": "Completed",
        "counters": {name: str(value) for name, value in normalized_counters.items()},
        "trx": expected_binding,
    }


def validate_trx_record_contract(
    record: Any,
    validated_trx: dict[str, Any],
) -> None:
    if not isinstance(record, dict):
        raise ValueError("TRX execution record must be an object")
    expected_claims = {
        "testName": validated_trx["testName"],
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
        "exitCode": 0,
        "attemptCount": 1,
        "outcomes": [validated_trx["outcome"]],
        "resultCount": 1,
        "unexpectedTestNames": [],
        "summaryOutcome": validated_trx["summaryOutcome"],
        "counters": validated_trx["counters"],
        "summaryValid": True,
        "trx": validated_trx["trx"],
    }
    if any(record.get(key) != value for key, value in expected_claims.items()):
        raise ValueError("TRX execution record claims do not match the validated TRX")
