#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json"
canonical_release_channel_path="/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"
linux_avalonia_gate_path="$repo_root/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
linux_blazor_gate_path="$repo_root/.codex-studio/published/UI_LINUX_BLAZOR_DESKTOP_EXIT_GATE.generated.json"
windows_gate_path_default="$repo_root/.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
visual_familiarity_gate_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
workflow_execution_gate_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$release_channel_path" "$linux_avalonia_gate_path" "$linux_blazor_gate_path" "$windows_gate_path_default" "$flagship_gate_path" "$visual_familiarity_gate_path" "$workflow_execution_gate_path" "$repo_root"
from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone


DESKTOP_PROOF_MAX_AGE_SECONDS = int(os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_AGE_SECONDS", "86400"))
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS", "86400")
)
from pathlib import Path
from typing import Any, Dict, List


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: str) -> bool:
    return value.strip().lower() in {"pass", "passed", "ready"}


def pick_status(payload: Dict[str, Any]) -> str:
    return str(payload.get("status") or "").strip().lower()


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def dedupe_preserve_order(values: List[str]) -> List[str]:
    seen: set[str] = set()
    deduped: List[str] = []
    for value in values:
        if value in seen:
            continue
        seen.add(value)
        deduped.append(value)
    return deduped


def parse_iso(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def payload_generated_at(payload: Dict[str, Any]) -> tuple[str, datetime | None]:
    for key in ("generated_at", "generatedAt"):
        if key in payload:
            raw = str(payload.get(key) or "").strip()
            return raw, parse_iso(raw)
    return "", None


def validate_receipt_freshness(label: str, payload: Dict[str, Any], evidence: Dict[str, Any], reasons: List[str]) -> None:
    generated_at_raw, generated_at = payload_generated_at(payload)
    evidence[f"{label}_generated_at"] = generated_at_raw
    if not generated_at_raw or generated_at is None:
        reasons.append(f"{label} is missing a valid generated_at timestamp.")
        return
    age_seconds = max(0, int((datetime.now(timezone.utc) - generated_at).total_seconds()))
    evidence[f"{label}_age_seconds"] = age_seconds
    if age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
        reasons.append(f"{label} is stale ({age_seconds}s old).")


def macos_rid_from_artifact(artifact: Dict[str, Any]) -> str:
    rid = normalize_token(artifact.get("rid"))
    if rid:
        return rid
    arch = normalize_token(artifact.get("arch"))
    if arch in {"arm64", "x64"}:
        return f"osx-{arch}"
    return ""


def is_desktop_install_media(platform: Any, kind: Any) -> bool:
    platform_token = normalize_token(platform)
    kind_token = normalize_token(kind)
    if platform_token == "macos":
        return kind_token in {"installer", "dmg", "pkg"}
    return kind_token == "installer"


def linux_gate_path_for_head(head: str, avalonia_path: Path, blazor_path: Path, receipt_root: Path) -> Path:
    if head == "avalonia":
        return avalonia_path
    if head == "blazor-desktop":
        return blazor_path
    return receipt_root / f"UI_LINUX_{head.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json"


def macos_gate_path_for_head(head: str, rid: str, receipt_root: Path) -> Path:
    return receipt_root / f"UI_MACOS_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json"


def windows_gate_path_for_head(
    head: str,
    rid: str,
    receipt_root: Path,
    default_gate_path: Path,
) -> Path:
    if head == "avalonia" and rid == "win-x64":
        return default_gate_path
    return receipt_root / f"UI_WINDOWS_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json"


def arch_from_rid(rid: str) -> str:
    normalized = normalize_token(rid)
    if normalized.endswith("x64"):
        return "x64"
    if normalized.endswith("arm64"):
        return "arm64"
    return ""


def path_within_root(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def validate_receipt_path_scope(path: Path, repo_root: Path, reasons: List[str], evidence: Dict[str, Any], label: str) -> None:
    in_scope = path_within_root(path, repo_root)
    evidence.setdefault("receipt_scope", {})[label] = {
        "path": str(path),
        "within_repo_root": in_scope,
        "repo_root": str(repo_root.resolve()),
    }
    if not in_scope:
        reasons.append(
            f"{label} receipt path is outside this repo root and cannot be used as authoritative local proof."
        )


def validate_flagship_head_proof(
    head: str,
    flagship_gate: Dict[str, Any],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    head_proofs = flagship_gate.get("headProofs") if isinstance(flagship_gate.get("headProofs"), dict) else {}
    proof = head_proofs.get(head) if isinstance(head_proofs.get(head), dict) else {}
    proof_status = normalize_token(proof.get("status"))
    evidence.setdefault("flagship_head_proofs", {})[head] = {
        "status": proof_status,
        "proof": proof,
    }
    if not status_ok(proof_status):
        reasons.append(f"Flagship UI proof is missing or not passing for promoted head '{head}'.")


def validate_linux_gate(
    head: str,
    gate_path: Path,
    gate_payload: Dict[str, Any],
    expected_artifact: Dict[str, Any] | None,
    repo_root: Path,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    gate_evidence: Dict[str, Any] = {
        "path": str(gate_path),
    }
    gate_status = pick_status(gate_payload)
    gate_evidence["status"] = gate_status
    validate_receipt_freshness(f"linux desktop exit gate proof for {head}", gate_payload, gate_evidence, reasons)

    gate_head = gate_payload.get("head") if isinstance(gate_payload.get("head"), dict) else {}
    gate_evidence["receipt_head"] = gate_head
    if normalize_token(gate_head.get("app_key")) != head:
        reasons.append(f"Linux desktop exit gate receipt head does not match promoted head '{head}'.")
    if normalize_token(gate_head.get("platform")) != "linux":
        reasons.append(f"Linux desktop exit gate receipt platform does not match promoted head '{head}'.")

    if not status_ok(gate_status):
        reasons.append(f"Linux desktop exit gate is missing or not passing for promoted head '{head}'.")

    startup = gate_payload.get("startup_smoke") if isinstance(gate_payload.get("startup_smoke"), dict) else {}
    primary = startup.get("primary") if isinstance(startup.get("primary"), dict) else {}
    fallback = startup.get("fallback") if isinstance(startup.get("fallback"), dict) else {}
    unit_tests = gate_payload.get("unit_tests") if isinstance(gate_payload.get("unit_tests"), dict) else {}

    primary_status = normalize_token(primary.get("status"))
    fallback_status = normalize_token(fallback.get("status"))
    unit_test_status = normalize_token(unit_tests.get("status"))

    gate_evidence["primary_smoke_status"] = primary_status
    gate_evidence["fallback_smoke_status"] = fallback_status
    gate_evidence["unit_test_status"] = unit_test_status
    gate_evidence["unit_test_summary"] = unit_tests.get("summary") if isinstance(unit_tests.get("summary"), dict) else {}

    if primary_status not in {"pass", "passed", "ready"}:
        reasons.append(f"Linux installer startup smoke is not passing for promoted head '{head}'.")
    if fallback_status not in {"pass", "passed", "ready"}:
        reasons.append(f"Linux archive startup smoke is not passing for promoted head '{head}'.")
    if unit_test_status not in {"pass", "passed", "ready"}:
        reasons.append(f"Linux desktop runtime unit tests are not passing for promoted head '{head}'.")

    primary_receipt = primary.get("receipt") if isinstance(primary.get("receipt"), dict) else {}
    primary_receipt_path_raw = str(primary.get("receipt_path") or "").strip()
    primary_receipt_path = Path(primary_receipt_path_raw) if primary_receipt_path_raw else None
    primary_receipt_file_exists = primary_receipt_path is not None and primary_receipt_path.is_file()
    primary_receipt_file = load_json(primary_receipt_path) if primary_receipt_file_exists and primary_receipt_path is not None else {}
    primary_receipt_for_validation = primary_receipt_file if primary_receipt_file else primary_receipt

    gate_evidence["primary_receipt_path"] = primary_receipt_path_raw
    gate_evidence["primary_receipt_file_exists"] = primary_receipt_file_exists
    if not primary_receipt_file_exists:
        reasons.append(f"Linux installer startup smoke receipt path is missing/unreadable for promoted head '{head}'.")
    elif primary_receipt_path is not None and not path_within_root(primary_receipt_path, repo_root):
        reasons.append(f"Linux installer startup smoke receipt path is outside this repo root for promoted head '{head}'.")

    gate_evidence["primary_receipt_artifact_digest"] = normalize_token(primary_receipt.get("artifactDigest"))
    gate_evidence["primary_receipt_ready_checkpoint"] = normalize_token(primary_receipt.get("readyCheckpoint"))
    recorded_at_raw = (
        str(primary_receipt_for_validation.get("completedAtUtc") or "").strip()
        or str(primary_receipt_for_validation.get("recordedAtUtc") or "").strip()
        or str(primary_receipt_for_validation.get("startedAtUtc") or "").strip()
    )
    gate_evidence["primary_receipt_head_id"] = normalize_token(primary_receipt_for_validation.get("headId"))
    gate_evidence["primary_receipt_platform"] = normalize_token(primary_receipt_for_validation.get("platform"))
    gate_evidence["primary_receipt_arch"] = normalize_token(primary_receipt_for_validation.get("arch"))
    gate_evidence["primary_receipt_artifact_digest"] = normalize_token(primary_receipt_for_validation.get("artifactDigest"))
    gate_evidence["primary_receipt_ready_checkpoint"] = normalize_token(primary_receipt_for_validation.get("readyCheckpoint"))
    gate_evidence["primary_receipt_recorded_at"] = recorded_at_raw
    recorded_at = parse_iso(recorded_at_raw)
    if not recorded_at_raw or recorded_at is None:
        reasons.append(f"Linux installer startup smoke receipt timestamp is missing/invalid for promoted head '{head}'.")
    else:
        age_seconds = max(0, int((datetime.now(timezone.utc) - recorded_at).total_seconds()))
        gate_evidence["primary_receipt_age_seconds"] = age_seconds
        if age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
            reasons.append(
                f"Linux installer startup smoke receipt is stale for promoted head '{head}' ({age_seconds}s old)."
            )
    if gate_evidence["primary_receipt_ready_checkpoint"] != "pre_ui_event_loop":
        reasons.append(f"Linux installer startup smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted head '{head}'.")
    if gate_evidence["primary_receipt_head_id"] != head:
        reasons.append(f"Linux installer startup smoke receipt headId does not match promoted head '{head}'.")
    if gate_evidence["primary_receipt_platform"] != "linux":
        reasons.append(f"Linux installer startup smoke receipt platform is not linux for promoted head '{head}'.")

    if expected_artifact is not None:
        expected_rid = normalize_token(expected_artifact.get("rid"))
        expected_sha = normalize_token(expected_artifact.get("sha256"))
        expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
        expected_arch = arch_from_rid(expected_rid)
        if expected_rid and normalize_token(gate_head.get("rid")) != expected_rid:
            reasons.append(f"Linux desktop exit gate receipt RID does not match promoted head '{head}' ({expected_rid}).")
        if expected_arch and gate_evidence["primary_receipt_arch"] != expected_arch:
            reasons.append(f"Linux installer startup smoke receipt arch does not match promoted RID for head '{head}'.")
        if expected_digest and gate_evidence["primary_receipt_artifact_digest"] != expected_digest:
            reasons.append(
                f"Linux installer startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes for head '{head}'."
            )

    for key, value in (
        ("install_launch_capture_path", str(primary_receipt.get("artifactInstallLaunchCapturePath") or "").strip()),
        ("install_wrapper_capture_path", str(primary_receipt.get("artifactInstallWrapperCapturePath") or "").strip()),
        ("install_desktop_entry_capture_path", str(primary_receipt.get("artifactInstallDesktopEntryCapturePath") or "").strip()),
        ("install_verification_path", str(primary_receipt.get("artifactInstallVerificationPath") or "").strip()),
    ):
        gate_evidence[key] = value
        if not value:
            reasons.append(f"Linux installer proof is missing {key} for promoted head '{head}'.")
        elif not os.path.exists(value):
            reasons.append(f"Linux installer proof path does not exist for promoted head '{head}': {value}")

    evidence.setdefault("linux_gates", {})[head] = gate_evidence


def validate_windows_gate(
    gate_label: str,
    gate_path: Path,
    gate_payload: Dict[str, Any],
    expected_artifact: Dict[str, Any],
    desktop_files_root: Path,
    repo_root: Path,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    gate_evidence: Dict[str, Any] = {
        "path": str(gate_path),
    }
    gate_status = pick_status(gate_payload)
    gate_evidence["status"] = gate_status
    validate_receipt_freshness("windows desktop exit gate proof", gate_payload, gate_evidence, reasons)

    gate_head = gate_payload.get("head") if isinstance(gate_payload.get("head"), dict) else {}
    gate_checks = gate_payload.get("checks") if isinstance(gate_payload.get("checks"), dict) else {}
    gate_reasons = [
        str(item).strip()
        for item in (gate_payload.get("reasons") or [])
        if str(item).strip()
    ]
    channel_artifact = (
        gate_checks.get("release_channel_windows_artifact")
        if isinstance(gate_checks.get("release_channel_windows_artifact"), dict)
        else {}
    )

    gate_evidence["receipt_head"] = gate_head
    gate_evidence["release_channel_windows_artifact"] = channel_artifact
    gate_evidence["windows_installer_path"] = str(gate_checks.get("windows_installer_path") or "").strip()
    gate_evidence["gate_reasons"] = gate_reasons
    gate_evidence["startup_smoke_receipt_path"] = str(gate_checks.get("startup_smoke_receipt_path") or "").strip()
    embedded_payload_marker_present = bool(gate_checks.get("embedded_payload_marker_present"))
    embedded_sample_marker_present = bool(gate_checks.get("embedded_sample_marker_present"))
    gate_evidence["embedded_payload_marker_present"] = embedded_payload_marker_present
    gate_evidence["embedded_sample_marker_present"] = embedded_sample_marker_present

    if normalize_token(gate_head.get("platform")) != "windows":
        reasons.append("Windows desktop exit gate receipt platform is not 'windows'.")
    if not status_ok(gate_status):
        reasons.append("Windows desktop exit gate is missing or not passing.")
    for gate_reason in gate_reasons:
        reasons.append(f"Windows gate reason: {gate_reason}")

    expected_head = normalize_token(expected_artifact.get("head"))
    expected_rid = normalize_token(expected_artifact.get("rid"))
    expected_tuple = (expected_head, expected_rid)
    gate_tuple = (
        normalize_token(gate_head.get("app_key")),
        normalize_token(gate_head.get("rid")),
    )
    if gate_tuple != expected_tuple:
        reasons.append(
            f"Windows desktop exit gate receipt head/RID does not match promoted release-channel Windows artifact tuple {gate_label}."
        )
        evidence.setdefault("windows_gates", {})[gate_label] = gate_evidence
        return

    expected_file_name = str(expected_artifact.get("fileName") or "").strip()
    expected_sha = normalize_token(expected_artifact.get("sha256"))
    expected_size = int(expected_artifact.get("sizeBytes") or 0)

    if normalize_token(channel_artifact.get("head")) != expected_head:
        reasons.append("Windows gate embedded release_channel_windows_artifact head does not match promoted release channel.")
    if normalize_token(channel_artifact.get("rid")) != expected_rid:
        reasons.append("Windows gate embedded release_channel_windows_artifact RID does not match promoted release channel.")
    if normalize_token(channel_artifact.get("platform")) != "windows":
        reasons.append("Windows gate embedded release_channel_windows_artifact platform is not 'windows'.")
    if str(channel_artifact.get("fileName") or "").strip() != expected_file_name:
        reasons.append("Windows gate embedded release_channel_windows_artifact fileName does not match promoted release channel.")
    if expected_sha and normalize_token(channel_artifact.get("sha256")) != expected_sha:
        reasons.append("Windows gate embedded release_channel_windows_artifact sha256 does not match promoted release channel.")
    if expected_size and int(channel_artifact.get("sizeBytes") or 0) != expected_size:
        reasons.append("Windows gate embedded release_channel_windows_artifact sizeBytes does not match promoted release channel.")

    installer_path = Path(str(gate_checks.get("windows_installer_path") or "").strip())
    if not installer_path.is_file():
        reasons.append("Windows desktop exit gate windows_installer_path does not exist.")
    else:
        gate_evidence["windows_installer_size_bytes"] = int(installer_path.stat().st_size)
        gate_evidence["windows_installer_sha256"] = hashlib.sha256(installer_path.read_bytes()).hexdigest().lower()
        if expected_size and gate_evidence["windows_installer_size_bytes"] != expected_size:
            reasons.append("Windows desktop exit gate installer size does not match promoted release-channel artifact bytes.")
        if expected_sha and normalize_token(gate_evidence["windows_installer_sha256"]) != expected_sha:
            reasons.append("Windows desktop exit gate installer sha256 does not match promoted release-channel artifact bytes.")

    shelf_path = desktop_files_root / expected_file_name
    gate_evidence["expected_windows_shelf_path"] = str(shelf_path)
    if shelf_path.is_file() and installer_path.is_file():
        shelf_sha = hashlib.sha256(shelf_path.read_bytes()).hexdigest().lower()
        gate_evidence["expected_windows_shelf_sha256"] = shelf_sha
        if shelf_sha != normalize_token(gate_evidence.get("windows_installer_sha256")):
            reasons.append("Windows desktop exit gate installer bytes do not match the local promoted desktop shelf artifact.")

    startup_smoke_receipt_path = (
        Path(gate_evidence["startup_smoke_receipt_path"]) if gate_evidence["startup_smoke_receipt_path"] else None
    )
    startup_smoke_receipt_exists = startup_smoke_receipt_path is not None and startup_smoke_receipt_path.is_file()
    startup_smoke_receipt_payload = (
        load_json(startup_smoke_receipt_path)
        if startup_smoke_receipt_exists and startup_smoke_receipt_path is not None
        else {}
    )
    if not startup_smoke_receipt_exists:
        reasons.append("Windows startup smoke receipt path is missing/unreadable for promoted installer bytes.")
    elif startup_smoke_receipt_path is not None and not path_within_root(startup_smoke_receipt_path, repo_root):
        reasons.append("Windows startup smoke receipt path is outside this repo root.")
    else:
        startup_smoke_status = normalize_token(
            startup_smoke_receipt_payload.get("status")
            or gate_checks.get("startup_smoke_status")
        )
        startup_smoke_ready_checkpoint = normalize_token(
            startup_smoke_receipt_payload.get("readyCheckpoint")
            or gate_checks.get("startup_smoke_ready_checkpoint")
        )
        startup_smoke_artifact_digest = normalize_token(
            startup_smoke_receipt_payload.get("artifactDigest")
            or gate_checks.get("startup_smoke_artifact_digest")
        )
        startup_smoke_head_id = normalize_token(
            startup_smoke_receipt_payload.get("headId")
            or gate_checks.get("startup_smoke_receipt_head_id")
        )
        startup_smoke_platform = normalize_token(
            startup_smoke_receipt_payload.get("platform")
            or gate_checks.get("startup_smoke_receipt_platform")
        )
        startup_smoke_arch = normalize_token(
            startup_smoke_receipt_payload.get("arch")
            or gate_checks.get("startup_smoke_receipt_arch")
        )
        startup_smoke_recorded_at_raw = str(
            startup_smoke_receipt_payload.get("completedAtUtc")
            or startup_smoke_receipt_payload.get("recordedAtUtc")
            or startup_smoke_receipt_payload.get("startedAtUtc")
            or gate_checks.get("startup_smoke_receipt_timestamp")
            or ""
        ).strip()
        startup_smoke_recorded_at = parse_iso(startup_smoke_recorded_at_raw)
        expected_startup_smoke_arch = arch_from_rid(expected_rid)
        expected_startup_smoke_digest = (
            f"sha256:{expected_sha}"
            if expected_sha
            else f"sha256:{normalize_token(gate_evidence.get('windows_installer_sha256'))}"
            if normalize_token(gate_evidence.get("windows_installer_sha256"))
            else ""
        )

        gate_evidence["startup_smoke_status"] = startup_smoke_status
        gate_evidence["startup_smoke_ready_checkpoint"] = startup_smoke_ready_checkpoint
        gate_evidence["startup_smoke_artifact_digest"] = startup_smoke_artifact_digest
        gate_evidence["startup_smoke_head_id"] = startup_smoke_head_id
        gate_evidence["startup_smoke_platform"] = startup_smoke_platform
        gate_evidence["startup_smoke_arch"] = startup_smoke_arch
        gate_evidence["startup_smoke_recorded_at"] = startup_smoke_recorded_at_raw

        if startup_smoke_status not in {"pass", "passed", "ready"}:
            reasons.append("Windows startup smoke receipt status is not passing for promoted installer bytes.")
        if startup_smoke_ready_checkpoint != "pre_ui_event_loop":
            reasons.append("Windows startup smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted installer bytes.")
        if expected_startup_smoke_digest and startup_smoke_artifact_digest != expected_startup_smoke_digest:
            reasons.append("Windows startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes.")
        if startup_smoke_head_id != expected_head:
            reasons.append("Windows startup smoke receipt headId does not match promoted release-channel head.")
        if startup_smoke_platform != "windows":
            reasons.append("Windows startup smoke receipt platform is not windows for promoted installer bytes.")
        if expected_startup_smoke_arch and startup_smoke_arch != expected_startup_smoke_arch:
            reasons.append("Windows startup smoke receipt arch does not match promoted release-channel RID.")
        if not startup_smoke_recorded_at_raw or startup_smoke_recorded_at is None:
            reasons.append("Windows startup smoke receipt timestamp is missing/invalid for promoted installer bytes.")
        else:
            startup_smoke_age_seconds = max(0, int((datetime.now(timezone.utc) - startup_smoke_recorded_at).total_seconds()))
            gate_evidence["startup_smoke_receipt_age_seconds"] = startup_smoke_age_seconds
            if startup_smoke_age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
                reasons.append(
                    f"Windows startup smoke receipt is stale for promoted installer bytes ({startup_smoke_age_seconds}s old)."
                )

    if not embedded_payload_marker_present:
        reasons.append(f"Windows installer receipt does not confirm embedded payload marker for promoted tuple {gate_label}.")
    if not embedded_sample_marker_present:
        reasons.append(f"Windows installer receipt does not confirm bundled demo sample marker for promoted tuple {gate_label}.")

    evidence.setdefault("windows_gates", {})[gate_label] = gate_evidence


def validate_macos_gate(
    head: str,
    rid: str,
    expected_artifact: Dict[str, Any],
    gate_path: Path,
    gate_payload: Dict[str, Any],
    desktop_files_root: Path,
    repo_root: Path,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    gate_evidence: Dict[str, Any] = {
        "path": str(gate_path),
    }
    gate_status = pick_status(gate_payload)
    gate_evidence["status"] = gate_status
    validate_receipt_freshness(f"macOS desktop exit gate proof for {head} ({rid})", gate_payload, gate_evidence, reasons)

    gate_head = gate_payload.get("head") if isinstance(gate_payload.get("head"), dict) else {}
    gate_checks = gate_payload.get("checks") if isinstance(gate_payload.get("checks"), dict) else {}
    gate_reasons = [
        str(item).strip()
        for item in (gate_payload.get("reasons") or [])
        if str(item).strip()
    ]
    channel_artifact = (
        gate_checks.get("release_channel_macos_artifact")
        if isinstance(gate_checks.get("release_channel_macos_artifact"), dict)
        else {}
    )
    gate_evidence["receipt_head"] = gate_head
    gate_evidence["gate_reasons"] = gate_reasons
    if normalize_token(gate_head.get("app_key")) != head:
        reasons.append(f"macOS desktop exit gate receipt head does not match promoted head '{head}'.")
    if normalize_token(gate_head.get("rid")) != rid:
        reasons.append(f"macOS desktop exit gate receipt RID does not match promoted head '{head}' ({rid}).")
    if normalize_token(gate_head.get("platform")) != "macos":
        reasons.append(f"macOS desktop exit gate receipt platform does not match promoted head '{head}'.")
    if not status_ok(gate_status):
        reasons.append(f"macOS desktop exit gate is missing or not passing for promoted head '{head}' ({rid}).")
    for gate_reason in gate_reasons:
        reasons.append(f"macOS gate reason ({head}/{rid}): {gate_reason}")

    startup = gate_payload.get("startup_smoke") if isinstance(gate_payload.get("startup_smoke"), dict) else {}
    artifact = gate_payload.get("artifact") if isinstance(gate_payload.get("artifact"), dict) else {}
    startup_receipt = startup.get("receipt") if isinstance(startup.get("receipt"), dict) else {}
    artifact_exists = bool(artifact.get("installer_exists"))
    expected_file_name = str(expected_artifact.get("fileName") or "").strip()
    expected_sha = normalize_token(expected_artifact.get("sha256"))
    expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
    expected_size = int(expected_artifact.get("sizeBytes") or 0)
    expected_arch = "arm64" if rid.endswith("arm64") else "x64" if rid.endswith("x64") else ""
    startup_receipt_path = Path(str(startup.get("receipt_path") or "").strip()) if startup.get("receipt_path") else None
    startup_receipt_exists = startup_receipt_path is not None and startup_receipt_path.is_file()
    startup_receipt_file = (
        load_json(startup_receipt_path)
        if startup_receipt_exists and startup_receipt_path is not None
        else {}
    )
    startup_receipt_for_validation = startup_receipt_file if startup_receipt_file else startup_receipt
    startup_smoke_status = normalize_token(
        startup_receipt_for_validation.get("status")
        or startup.get("status")
    )
    startup_smoke_ready_checkpoint = normalize_token(
        startup_receipt_for_validation.get("readyCheckpoint")
        or startup.get("ready_checkpoint")
    )
    startup_smoke_artifact_digest = normalize_token(
        startup_receipt_for_validation.get("artifactDigest")
        or startup.get("artifact_digest")
    )
    startup_smoke_head_id = normalize_token(startup_receipt_for_validation.get("headId"))
    startup_smoke_platform = normalize_token(startup_receipt_for_validation.get("platform"))
    startup_smoke_arch = normalize_token(startup_receipt_for_validation.get("arch"))
    startup_receipt_recorded_at_raw = str(
        startup_receipt_for_validation.get("completedAtUtc")
        or startup_receipt_for_validation.get("recordedAtUtc")
        or startup_receipt_for_validation.get("startedAtUtc")
        or startup.get("receipt_recorded_at")
        or ""
    ).strip()
    startup_receipt_recorded_at = parse_iso(startup_receipt_recorded_at_raw)

    gate_evidence["startup_smoke_status"] = startup_smoke_status
    gate_evidence["artifact"] = artifact
    gate_evidence["release_channel_macos_artifact"] = channel_artifact
    gate_evidence["startup_smoke_receipt_path"] = str(startup.get("receipt_path") or "").strip()
    gate_evidence["startup_smoke_receipt_file_exists"] = startup_receipt_exists
    gate_evidence["startup_smoke_ready_checkpoint"] = startup_smoke_ready_checkpoint
    gate_evidence["startup_smoke_artifact_digest"] = startup_smoke_artifact_digest
    gate_evidence["startup_smoke_receipt_head_id"] = startup_smoke_head_id
    gate_evidence["startup_smoke_receipt_platform"] = startup_smoke_platform
    gate_evidence["startup_smoke_receipt_arch"] = startup_smoke_arch
    gate_evidence["startup_smoke_receipt_recorded_at"] = startup_receipt_recorded_at_raw

    if startup_smoke_status not in {"pass", "passed", "ready"}:
        reasons.append(f"macOS startup smoke is not passing for promoted head '{head}' ({rid}).")
    if not artifact_exists:
        reasons.append(f"macOS installer artifact is missing for promoted head '{head}' ({rid}).")
    if normalize_token(channel_artifact.get("head")) != head:
        reasons.append("macOS gate embedded release_channel_macos_artifact head does not match promoted release channel.")
    if normalize_token(channel_artifact.get("rid")) != rid:
        reasons.append("macOS gate embedded release_channel_macos_artifact RID does not match promoted release channel.")
    if normalize_token(channel_artifact.get("platform")) != "macos":
        reasons.append("macOS gate embedded release_channel_macos_artifact platform is not macOS.")
    if expected_file_name and str(channel_artifact.get("fileName") or "").strip() != expected_file_name:
        reasons.append("macOS gate embedded release_channel_macos_artifact fileName does not match promoted release channel.")
    if expected_sha and normalize_token(channel_artifact.get("sha256")) != expected_sha:
        reasons.append("macOS gate embedded release_channel_macos_artifact sha256 does not match promoted release channel.")
    if expected_size and int(channel_artifact.get("sizeBytes") or 0) != expected_size:
        reasons.append("macOS gate embedded release_channel_macos_artifact sizeBytes does not match promoted release channel.")

    installer_path = Path(str(artifact.get("installer_path") or "").strip()) if artifact.get("installer_path") else None
    if installer_path is None or not installer_path.is_file():
        reasons.append(f"macOS gate installer path is missing or unreadable for promoted head '{head}' ({rid}).")
    else:
        installer_size = int(installer_path.stat().st_size)
        installer_sha = normalize_token(hashlib.sha256(installer_path.read_bytes()).hexdigest())
        gate_evidence["installer_size_bytes"] = installer_size
        gate_evidence["installer_sha256"] = installer_sha
        if expected_size and installer_size != expected_size:
            reasons.append("macOS desktop exit gate installer size does not match promoted release-channel artifact bytes.")
        if expected_sha and installer_sha != expected_sha:
            reasons.append("macOS desktop exit gate installer sha256 does not match promoted release-channel artifact bytes.")

        shelf_path = desktop_files_root / expected_file_name if expected_file_name else desktop_files_root
        gate_evidence["expected_macos_shelf_path"] = str(shelf_path)
        if expected_file_name and shelf_path.is_file():
            shelf_sha = normalize_token(hashlib.sha256(shelf_path.read_bytes()).hexdigest())
            gate_evidence["expected_macos_shelf_sha256"] = shelf_sha
            if shelf_sha != installer_sha:
                reasons.append("macOS desktop exit gate installer bytes do not match the local promoted desktop shelf artifact.")

    if not startup_receipt_exists:
        reasons.append(f"macOS startup smoke receipt path is missing or unreadable for promoted head '{head}' ({rid}).")
    elif startup_receipt_path is not None and not path_within_root(startup_receipt_path, repo_root):
        reasons.append(f"macOS startup smoke receipt path is outside this repo root for promoted head '{head}' ({rid}).")
    else:
        if startup_smoke_status not in {"pass", "passed", "ready"}:
            reasons.append(f"macOS startup smoke receipt status is not passing for promoted head '{head}' ({rid}).")
        if startup_smoke_ready_checkpoint != "pre_ui_event_loop":
            reasons.append(f"macOS startup smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted head '{head}' ({rid}).")
        if expected_digest and startup_smoke_artifact_digest != expected_digest:
            reasons.append(f"macOS startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes for head '{head}' ({rid}).")
        if startup_smoke_head_id != head:
            reasons.append(f"macOS startup smoke receipt headId does not match promoted head '{head}' ({rid}).")
        if startup_smoke_platform != "macos":
            reasons.append(f"macOS startup smoke receipt platform is not macOS for promoted head '{head}' ({rid}).")
        if expected_arch and startup_smoke_arch != expected_arch:
            reasons.append(f"macOS startup smoke receipt arch does not match promoted RID for head '{head}' ({rid}).")
        if not startup_receipt_recorded_at_raw or startup_receipt_recorded_at is None:
            reasons.append(f"macOS startup smoke receipt timestamp is missing/invalid for promoted head '{head}' ({rid}).")
        else:
            startup_age_seconds = max(0, int((datetime.now(timezone.utc) - startup_receipt_recorded_at).total_seconds()))
            gate_evidence["startup_smoke_receipt_age_seconds"] = startup_age_seconds
            if startup_age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
                reasons.append(
                    f"macOS startup smoke receipt is stale for promoted head '{head}' ({rid}) ({startup_age_seconds}s old)."
                )

    evidence.setdefault("macos_gates", {})[f"{head}:{rid}"] = gate_evidence


def validate_local_release_artifact_file(
    artifact: Dict[str, Any],
    desktop_files_root: Path,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    artifact_id = str(artifact.get("artifactId") or "").strip()
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        download_url = str(artifact.get("downloadUrl") or "").strip()
        file_name = Path(download_url).name if download_url else ""

    evidence_key = artifact_id or file_name or "unknown-artifact"
    local_path = desktop_files_root / file_name if file_name else desktop_files_root
    exists = bool(file_name) and local_path.is_file()

    artifact_evidence: Dict[str, Any] = {
        "artifact_id": artifact_id,
        "file_name": file_name,
        "path": str(local_path),
        "exists": exists,
    }

    expected_size = int(artifact.get("sizeBytes") or 0)
    expected_sha = normalize_token(artifact.get("sha256"))
    if exists:
        artifact_evidence["size_bytes"] = int(local_path.stat().st_size)
        artifact_evidence["sha256"] = normalize_token(hashlib.sha256(local_path.read_bytes()).hexdigest())
    else:
        artifact_evidence["size_bytes"] = 0
        artifact_evidence["sha256"] = ""

    artifact_evidence["expected_size_bytes"] = expected_size
    artifact_evidence["expected_sha256"] = expected_sha
    evidence.setdefault("release_artifacts_local", {})[evidence_key] = artifact_evidence

    if not file_name:
        reasons.append("Release channel desktop artifact is missing fileName/downloadUrl basename, so local proof cannot verify shipped bytes.")
        return

    if not exists:
        reasons.append(f"Promoted release-channel artifact is missing from local desktop downloads shelf: {file_name}.")
        return

    if expected_size and artifact_evidence["size_bytes"] != expected_size:
        reasons.append(f"Promoted release-channel artifact size does not match local bytes for {file_name}.")
    if expected_sha and artifact_evidence["sha256"] != expected_sha:
        reasons.append(f"Promoted release-channel artifact sha256 does not match local bytes for {file_name}.")


receipt_path, release_channel_path, linux_avalonia_gate_path, linux_blazor_gate_path, windows_gate_path_default, flagship_gate_path, visual_familiarity_gate_path, workflow_execution_gate_path, repo_root = [Path(v) for v in sys.argv[1:10]]

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "release_channel_path": str(release_channel_path),
    "linux_avalonia_gate_path": str(linux_avalonia_gate_path),
    "linux_blazor_gate_path": str(linux_blazor_gate_path),
    "windows_gate_path_default": str(windows_gate_path_default),
    "flagship_gate_path": str(flagship_gate_path),
    "visual_familiarity_gate_path": str(visual_familiarity_gate_path),
    "workflow_execution_gate_path": str(workflow_execution_gate_path),
    "repo_root": str(repo_root.resolve()),
}

release_channel = load_json(release_channel_path)
flagship_gate = load_json(flagship_gate_path)
visual_familiarity_gate = load_json(visual_familiarity_gate_path)
workflow_execution_gate = load_json(workflow_execution_gate_path)
validate_receipt_freshness("flagship UI release gate proof", flagship_gate, evidence, reasons)
validate_receipt_freshness("desktop visual familiarity gate proof", visual_familiarity_gate, evidence, reasons)
validate_receipt_freshness("desktop workflow execution gate proof", workflow_execution_gate, evidence, reasons)

flagship_status = pick_status(flagship_gate)
visual_familiarity_status = pick_status(visual_familiarity_gate)
workflow_execution_status = pick_status(workflow_execution_gate)

evidence["flagship_status"] = flagship_status
evidence["visual_familiarity_status"] = visual_familiarity_status
evidence["workflow_execution_status"] = workflow_execution_status

if not status_ok(flagship_status):
    reasons.append("Flagship UI release gate is missing or not passing.")
if not status_ok(visual_familiarity_status):
    reasons.append("Desktop visual familiarity exit gate is missing or not passing.")
if not status_ok(workflow_execution_status):
    reasons.append("Desktop workflow execution gate is missing or not passing.")
validate_receipt_path_scope(flagship_gate_path, repo_root, reasons, evidence, "flagship_gate")
validate_receipt_path_scope(visual_familiarity_gate_path, repo_root, reasons, evidence, "visual_familiarity_gate")
validate_receipt_path_scope(workflow_execution_gate_path, repo_root, reasons, evidence, "workflow_execution_gate")

visual_familiarity_evidence = (
    visual_familiarity_gate.get("evidence")
    if isinstance(visual_familiarity_gate.get("evidence"), dict)
    else {}
)
visual_screenshot_dir_raw = str(visual_familiarity_evidence.get("screenshot_dir") or "").strip()
visual_screenshot_dir = Path(visual_screenshot_dir_raw) if visual_screenshot_dir_raw else None
visual_required_screenshots = [
    str(item).strip()
    for item in (visual_familiarity_evidence.get("required_screenshots") or [])
    if str(item).strip()
]
evidence["visual_familiarity_screenshot_dir"] = visual_screenshot_dir_raw
evidence["visual_familiarity_required_screenshots"] = visual_required_screenshots
if visual_screenshot_dir is None:
    reasons.append("Desktop visual familiarity exit gate evidence is missing screenshot_dir.")
else:
    if not visual_screenshot_dir.is_dir():
        reasons.append("Desktop visual familiarity screenshot_dir does not exist on disk.")
    elif not path_within_root(visual_screenshot_dir, repo_root):
        reasons.append("Desktop visual familiarity screenshot_dir is outside this repo root.")
if not visual_required_screenshots:
    reasons.append("Desktop visual familiarity exit gate evidence is missing required_screenshots.")
else:
    missing_visual_screenshots = [
        name
        for name in visual_required_screenshots
        if visual_screenshot_dir is None or not (visual_screenshot_dir / name).is_file()
    ]
    evidence["visual_familiarity_missing_screenshots_now"] = missing_visual_screenshots
    if missing_visual_screenshots:
        reasons.append(
            "Desktop visual familiarity required screenshots are missing on disk: "
            + ", ".join(missing_visual_screenshots)
        )

artifacts = [
    item for item in (release_channel.get("artifacts") or [])
    if isinstance(item, dict)
]
release_channel_status = normalize_token(release_channel.get("status"))
release_channel_channel_id = str(release_channel.get("channelId") or "").strip()
release_channel_version = str(release_channel.get("version") or "").strip()

evidence["release_channel_status"] = release_channel_status
evidence["release_channel_channel_id"] = release_channel_channel_id
evidence["release_channel_version"] = release_channel_version

if not release_channel_channel_id:
    reasons.append("Release channel is missing channelId, so installer/update truth cannot be aligned by channel.")
if not release_channel_version:
    reasons.append("Release channel is missing version, so installer/update truth cannot be aligned by release head.")
if release_channel_status not in {"published", "ready", "pass", "passed"}:
    reasons.append("Release channel status is not in a publishable state for desktop executable proof.")

desktop_install_artifacts = [
    item for item in artifacts
    if normalize_token(item.get("platform")) in {"linux", "windows", "macos"}
    and is_desktop_install_media(item.get("platform"), item.get("kind"))
]
required_desktop_platforms = ("linux", "windows", "macos")
platform_artifact_counts = {
    platform: len(
        [
            item for item in desktop_install_artifacts
            if normalize_token(item.get("platform")) == platform
        ]
    )
    for platform in required_desktop_platforms
}
platform_heads_from_release_channel = {
    platform: sorted(
        {
            normalize_token(item.get("head"))
            for item in desktop_install_artifacts
            if normalize_token(item.get("platform")) == platform and normalize_token(item.get("head"))
        }
    )
    for platform in required_desktop_platforms
}
evidence["required_desktop_platforms"] = list(required_desktop_platforms)
evidence["platform_artifact_counts"] = platform_artifact_counts
evidence["platform_heads_from_release_channel"] = platform_heads_from_release_channel
desktop_files_root = repo_root / "Docker" / "Downloads" / "files"
evidence["desktop_files_root"] = str(desktop_files_root)
for required_platform in required_desktop_platforms:
    if platform_artifact_counts.get(required_platform, 0) < 1:
        reasons.append(
            f"Release channel does not publish desktop install media for required platform '{required_platform}'."
        )
for desktop_install_artifact in desktop_install_artifacts:
    validate_local_release_artifact_file(desktop_install_artifact, desktop_files_root, evidence, reasons)

expected_windows_artifacts = [
    item
    for item in desktop_install_artifacts
    if normalize_token(item.get("platform")) == "windows"
    and normalize_token(item.get("head"))
    and normalize_token(item.get("rid"))
]
evidence["windows_heads_expected"] = [
    {
        "head": normalize_token(item.get("head")),
        "rid": normalize_token(item.get("rid")),
        "fileName": str(item.get("fileName") or "").strip(),
    }
    for item in expected_windows_artifacts
]
if not expected_windows_artifacts and platform_artifact_counts.get("windows", 0) > 0:
    reasons.append("Release channel publishes Windows desktop media without explicit head/rid tuple metadata.")
for expected_windows_artifact in expected_windows_artifacts:
    expected_windows_head = normalize_token(expected_windows_artifact.get("head"))
    expected_windows_rid = normalize_token(expected_windows_artifact.get("rid"))
    gate_label = f"{expected_windows_head}:{expected_windows_rid}"
    gate_path = windows_gate_path_for_head(
        expected_windows_head,
        expected_windows_rid,
        receipt_path.parent,
        windows_gate_path_default,
    )
    validate_receipt_path_scope(gate_path, repo_root, reasons, evidence, f"windows_gate:{gate_label}")
    validate_windows_gate(
        gate_label,
        gate_path,
        load_json(gate_path),
        expected_windows_artifact,
        desktop_files_root,
        repo_root,
        evidence,
        reasons,
    )

windows_statuses = {
    label: normalize_token(
        gate_evidence.get("status")
        if isinstance(gate_evidence, dict)
        else ""
    )
    for label, gate_evidence in (
        evidence.get("windows_gates").items()
        if isinstance(evidence.get("windows_gates"), dict)
        else []
    )
}
evidence["windows_statuses"] = windows_statuses

expected_linux_heads = sorted(
    {
        normalize_token(item.get("head"))
        for item in desktop_install_artifacts
        if normalize_token(item.get("platform")) == "linux"
        and normalize_token(item.get("head"))
    }
)
evidence["linux_heads_expected"] = expected_linux_heads
expected_linux_artifacts_by_head = {
    normalize_token(item.get("head")): item
    for item in desktop_install_artifacts
    if normalize_token(item.get("platform")) == "linux"
    and normalize_token(item.get("head"))
    and normalize_token(item.get("rid"))
}
for expected_linux_head in expected_linux_heads:
    if expected_linux_head not in expected_linux_artifacts_by_head:
        reasons.append(
            f"Release channel publishes Linux desktop media for head '{expected_linux_head}' without explicit rid metadata."
        )

promoted_desktop_heads = sorted(
    {
        normalize_token(item.get("head"))
        for item in desktop_install_artifacts
        if normalize_token(item.get("platform")) in {"linux", "windows", "macos"}
        and normalize_token(item.get("head"))
    }
)
if not promoted_desktop_heads:
    reasons.append("Release channel does not publish any promoted desktop install media artifacts.")
evidence["promoted_desktop_heads"] = promoted_desktop_heads
for promoted_head in promoted_desktop_heads:
    validate_flagship_head_proof(promoted_head, flagship_gate, evidence, reasons)

for expected_linux_head in expected_linux_heads:
    gate_path = linux_gate_path_for_head(expected_linux_head, linux_avalonia_gate_path, linux_blazor_gate_path, receipt_path.parent)
    validate_receipt_path_scope(gate_path, repo_root, reasons, evidence, f"linux_gate:{expected_linux_head}")
    validate_linux_gate(
        expected_linux_head,
        gate_path,
        load_json(gate_path),
        expected_linux_artifacts_by_head.get(expected_linux_head),
        repo_root,
        evidence,
        reasons,
    )

expected_macos_artifacts = [
    item for item in desktop_install_artifacts
    if normalize_token(item.get("platform")) == "macos"
    and normalize_token(item.get("head"))
    and macos_rid_from_artifact(item)
]
evidence["macos_heads_expected"] = [
    {
        "head": normalize_token(item.get("head")),
        "rid": macos_rid_from_artifact(item),
        "fileName": str(item.get("fileName") or "").strip(),
    }
    for item in expected_macos_artifacts
]
for macos_artifact in expected_macos_artifacts:
    expected_head = normalize_token(macos_artifact.get("head"))
    expected_rid = macos_rid_from_artifact(macos_artifact)
    gate_path = macos_gate_path_for_head(expected_head, expected_rid, receipt_path.parent)
    validate_receipt_path_scope(gate_path, repo_root, reasons, evidence, f"macos_gate:{expected_head}:{expected_rid}")
    validate_macos_gate(
        expected_head,
        expected_rid,
        macos_artifact,
        gate_path,
        load_json(gate_path),
        desktop_files_root,
        repo_root,
        evidence,
        reasons,
    )

platform_tokens: List[str] = []
if expected_linux_heads:
    platform_tokens.append("Linux")
if expected_macos_artifacts:
    platform_tokens.append("macOS")
if platform_artifact_counts.get("windows", 0) > 0:
    platform_tokens.append("Windows")
platform_scope = ", ".join(platform_tokens) if platform_tokens else "none"

status = "pass" if not reasons else "fail"
reasons = dedupe_preserve_order(reasons)
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.desktop_executable_exit_gate",
    "status": status,
    "summary": (
        f"Desktop executable exit gate is proven by passing packaged-head receipts for promoted desktop platforms ({platform_scope}) and per-head flagship UI release proof."
        if status == "pass"
        else "Desktop executable exit gate is not fully proven."
    ),
    "reasons": reasons,
    "evidence": evidence,
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if status != "pass":
    print("[desktop-executable-exit-gate] FAIL", file=sys.stderr)
    print(f"[desktop-executable-exit-gate] receipt: {receipt_path}", file=sys.stderr)
    if reasons:
        for reason in reasons:
            print(f"[desktop-executable-exit-gate] reason: {reason}", file=sys.stderr)
    else:
        print("[desktop-executable-exit-gate] reason: unknown failure", file=sys.stderr)
    raise SystemExit(43)
PY

echo "[desktop-executable-exit-gate] PASS"
