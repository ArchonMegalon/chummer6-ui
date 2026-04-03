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
windows_gate_path="$repo_root/.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$release_channel_path" "$linux_avalonia_gate_path" "$linux_blazor_gate_path" "$windows_gate_path" "$flagship_gate_path" "$repo_root"
from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone


DESKTOP_PROOF_MAX_AGE_SECONDS = int(os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_AGE_SECONDS", "86400"))
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
    gate_path: Path,
    gate_payload: Dict[str, Any],
    expected_artifacts: List[Dict[str, Any]],
    desktop_files_root: Path,
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
    channel_artifact = (
        gate_checks.get("release_channel_windows_artifact")
        if isinstance(gate_checks.get("release_channel_windows_artifact"), dict)
        else {}
    )

    gate_evidence["receipt_head"] = gate_head
    gate_evidence["release_channel_windows_artifact"] = channel_artifact
    gate_evidence["windows_installer_path"] = str(gate_checks.get("windows_installer_path") or "").strip()

    if normalize_token(gate_head.get("platform")) != "windows":
        reasons.append("Windows desktop exit gate receipt platform is not 'windows'.")
    if not status_ok(gate_status):
        reasons.append("Windows desktop exit gate is missing or not passing.")

    expected_by_tuple = {
        (
            normalize_token(item.get("head")),
            normalize_token(item.get("rid")),
        ): item
        for item in expected_artifacts
    }
    expected_tuple = (
        normalize_token(gate_head.get("app_key")),
        normalize_token(gate_head.get("rid")),
    )
    matched_expected = expected_by_tuple.get(expected_tuple)
    if matched_expected is None:
        reasons.append(
            "Windows desktop exit gate receipt head/RID does not match any promoted release-channel Windows artifact tuple."
        )
        evidence["windows_gate"] = gate_evidence
        return

    expected_file_name = str(matched_expected.get("fileName") or "").strip()
    expected_sha = normalize_token(matched_expected.get("sha256"))
    expected_size = int(matched_expected.get("sizeBytes") or 0)
    expected_head = normalize_token(matched_expected.get("head"))
    expected_rid = normalize_token(matched_expected.get("rid"))

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

    evidence["windows_gate"] = gate_evidence


def validate_macos_gate(
    head: str,
    rid: str,
    gate_path: Path,
    gate_payload: Dict[str, Any],
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
    gate_evidence["receipt_head"] = gate_head
    if normalize_token(gate_head.get("app_key")) != head:
        reasons.append(f"macOS desktop exit gate receipt head does not match promoted head '{head}'.")
    if normalize_token(gate_head.get("rid")) != rid:
        reasons.append(f"macOS desktop exit gate receipt RID does not match promoted head '{head}' ({rid}).")
    if normalize_token(gate_head.get("platform")) != "macos":
        reasons.append(f"macOS desktop exit gate receipt platform does not match promoted head '{head}'.")
    if not status_ok(gate_status):
        reasons.append(f"macOS desktop exit gate is missing or not passing for promoted head '{head}' ({rid}).")

    startup = gate_payload.get("startup_smoke") if isinstance(gate_payload.get("startup_smoke"), dict) else {}
    artifact = gate_payload.get("artifact") if isinstance(gate_payload.get("artifact"), dict) else {}
    primary_status = normalize_token(startup.get("status"))
    artifact_exists = bool(artifact.get("installer_exists"))

    gate_evidence["startup_smoke_status"] = primary_status
    gate_evidence["artifact"] = artifact

    if primary_status not in {"pass", "passed", "ready"}:
        reasons.append(f"macOS startup smoke is not passing for promoted head '{head}' ({rid}).")
    if not artifact_exists:
        reasons.append(f"macOS installer artifact is missing for promoted head '{head}' ({rid}).")

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


receipt_path, release_channel_path, linux_avalonia_gate_path, linux_blazor_gate_path, windows_gate_path, flagship_gate_path, repo_root = [Path(v) for v in sys.argv[1:8]]

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "release_channel_path": str(release_channel_path),
    "linux_avalonia_gate_path": str(linux_avalonia_gate_path),
    "linux_blazor_gate_path": str(linux_blazor_gate_path),
    "windows_gate_path": str(windows_gate_path),
    "flagship_gate_path": str(flagship_gate_path),
    "repo_root": str(repo_root.resolve()),
}

release_channel = load_json(release_channel_path)
windows_gate = load_json(windows_gate_path)
flagship_gate = load_json(flagship_gate_path)
validate_receipt_freshness("flagship UI release gate proof", flagship_gate, evidence, reasons)

windows_status = pick_status(windows_gate)
flagship_status = pick_status(flagship_gate)

evidence["windows_status"] = windows_status
evidence["flagship_status"] = flagship_status

if not status_ok(windows_status):
    reasons.append("Windows desktop exit gate is missing or not passing.")
if not status_ok(flagship_status):
    reasons.append("Flagship UI release gate is missing or not passing.")
validate_receipt_path_scope(windows_gate_path, repo_root, reasons, evidence, "windows_gate")
validate_receipt_path_scope(flagship_gate_path, repo_root, reasons, evidence, "flagship_gate")

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
if len(expected_windows_artifacts) > 1:
    reasons.append(
        "Release channel currently promotes multiple Windows desktop installer tuples, but the executable gate supports one Windows receipt path."
    )
if expected_windows_artifacts:
    validate_windows_gate(windows_gate_path, windows_gate, expected_windows_artifacts, desktop_files_root, evidence, reasons)

expected_linux_heads = sorted(
    {
        normalize_token(item.get("head"))
        for item in desktop_install_artifacts
        if normalize_token(item.get("platform")) == "linux"
        and normalize_token(item.get("head"))
    }
)
evidence["linux_heads_expected"] = expected_linux_heads

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
    validate_linux_gate(expected_linux_head, gate_path, load_json(gate_path), evidence, reasons)

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
    validate_macos_gate(expected_head, expected_rid, gate_path, load_json(gate_path), evidence, reasons)

windows_checks = windows_gate.get("checks") if isinstance(windows_gate.get("checks"), dict) else {}
embedded_payload_marker_present = bool(windows_checks.get("embedded_payload_marker_present"))
embedded_sample_marker_present = bool(windows_checks.get("embedded_sample_marker_present"))
evidence["windows_embedded_payload_marker_present"] = embedded_payload_marker_present
evidence["windows_embedded_sample_marker_present"] = embedded_sample_marker_present
if not embedded_payload_marker_present:
    reasons.append("Windows installer receipt does not confirm embedded payload marker.")
if not embedded_sample_marker_present:
    reasons.append("Windows installer receipt does not confirm bundled demo sample marker.")

platform_tokens: List[str] = []
if expected_linux_heads:
    platform_tokens.append("Linux")
if expected_macos_artifacts:
    platform_tokens.append("macOS")
if platform_artifact_counts.get("windows", 0) > 0:
    platform_tokens.append("Windows")
platform_scope = ", ".join(platform_tokens) if platform_tokens else "none"

status = "pass" if not reasons else "fail"
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
