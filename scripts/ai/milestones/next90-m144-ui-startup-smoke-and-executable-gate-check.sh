#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

release_channel_path="${CHUMMER_NEXT90_M144_RELEASE_CHANNEL_PATH:-$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json}"
receipt_path="${CHUMMER_NEXT90_M144_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json}"
downloads_root="${CHUMMER_NEXT90_M144_DOWNLOADS_ROOT:-$repo_root/Docker/Downloads/files}"
startup_smoke_dir="${CHUMMER_NEXT90_M144_STARTUP_SMOKE_DIR:-$repo_root/Docker/Downloads/startup-smoke}"
windows_gate_path="${CHUMMER_NEXT90_M144_WINDOWS_GATE_PATH:-$repo_root/.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json}"
linux_gate_path="${CHUMMER_NEXT90_M144_LINUX_GATE_PATH:-$repo_root/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json}"
macos_gate_path="${CHUMMER_NEXT90_M144_MACOS_GATE_PATH:-$repo_root/.codex-studio/published/UI_MACOS_AVALONIA_OSX_ARM64_DESKTOP_EXIT_GATE.generated.json}"
aggregate_gate_path="${CHUMMER_NEXT90_M144_AGGREGATE_GATE_PATH:-$repo_root/.codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json}"

mkdir -p "$(dirname "$receipt_path")"

python3 - "$release_channel_path" "$receipt_path" "$downloads_root" "$startup_smoke_dir" "$windows_gate_path" "$linux_gate_path" "$macos_gate_path" "$aggregate_gate_path" <<'PY'
from __future__ import annotations

import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PACKAGE_ID = "next90-m144-ui-capture-fresh-windows-linux-and-macos-startup-smoke-and-executable-gate-p"
TITLE = "Capture fresh Windows, Linux, and macOS startup-smoke and executable-gate proof for the currently promoted flagship bytes."
TASK = TITLE
FRONTIER_ID = 1884153291
WORK_TASK_ID = "144.1"
MILESTONE_ID = 144
WAVE = "W22P"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Desktop.Runtime",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "capture_fresh_windows_linux_and_macos_startup_smoke_and:ui",
]
PASS_STATUSES = {"pass", "passed", "ready"}

release_channel_path = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
downloads_root = Path(sys.argv[3])
startup_smoke_dir = Path(sys.argv[4])
windows_gate_path = Path(sys.argv[5])
linux_gate_path = Path(sys.argv[6])
macos_gate_path = Path(sys.argv[7])
aggregate_gate_path = Path(sys.argv[8])


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def load_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


def release_kind_matches(platform: str, kind: str) -> bool:
    normalized_platform = normalize(platform)
    normalized_kind = normalize(kind)
    if normalized_platform == "windows":
        return normalized_kind in {"installer", "msix"}
    if normalized_platform == "linux":
        return normalized_kind == "installer"
    if normalized_platform == "macos":
        return normalized_kind in {"installer", "dmg", "pkg"}
    return False


def local_artifact_candidates(head: str, rid: str, platform: str) -> list[Path]:
    candidates: list[Path] = []
    if normalize(platform) == "windows":
        candidates.extend(
            [
                downloads_root / f"chummer-{head}-{rid}-installer.exe",
                downloads_root / f"chummer-{head}-{rid}.exe",
                downloads_root / f"chummer-{head}-{rid}.zip",
            ]
        )
    elif normalize(platform) == "linux":
        candidates.extend(
            [
                downloads_root / f"chummer-{head}-{rid}-installer.deb",
                downloads_root / f"chummer-{head}-{rid}.tar.gz",
            ]
        )
    elif normalize(platform) == "macos":
        candidates.extend(
            [
                downloads_root / f"chummer-{head}-{rid}-installer.dmg",
                downloads_root / f"chummer-{head}-{rid}.dmg",
                downloads_root / f"chummer-{head}-{rid}-installer.pkg",
                downloads_root / f"chummer-{head}-{rid}.pkg",
                downloads_root / f"chummer-{head}-{rid}.tar.gz",
            ]
        )
    return candidates


def first_existing(paths: list[Path]) -> Path | None:
    for path in paths:
        if path.is_file():
            return path
    return None


release_channel = load_json(release_channel_path)
release_version = str(release_channel.get("version") or "").strip()
release_channel_id = str(release_channel.get("channelId") or release_channel.get("channel") or "").strip().lower()
artifacts = [row for row in release_channel.get("artifacts", []) if isinstance(row, dict)]

windows_promoted = [
    row
    for row in artifacts
    if normalize(row.get("platform")) == "windows"
    and release_kind_matches("windows", str(row.get("kind") or ""))
    and normalize(row.get("head"))
    and normalize(row.get("rid"))
]
windows_promoted.sort(key=lambda row: (0 if normalize(row.get("head")) == "avalonia" else 1, normalize(row.get("rid"))))
flagship_head = normalize(windows_promoted[0].get("head")) if windows_promoted else "avalonia"

platform_specs = [
    {
        "platform": "windows",
        "rid": "win-x64",
        "gate_path": windows_gate_path,
    },
    {
        "platform": "linux",
        "rid": "linux-x64",
        "gate_path": linux_gate_path,
    },
    {
        "platform": "macos",
        "rid": "osx-arm64",
        "gate_path": macos_gate_path,
    },
]

proof_rows: list[dict[str, Any]] = []
blocking_findings: list[str] = []

for spec in platform_specs:
    platform = spec["platform"]
    rid = spec["rid"]
    tuple_id = f"{flagship_head}:{rid}:{platform}"
    artifact_row = next(
        (
            row
            for row in artifacts
            if normalize(row.get("head")) == flagship_head
            and normalize(row.get("platform")) == platform
            and normalize(row.get("rid")) == rid
            and release_kind_matches(platform, str(row.get("kind") or ""))
        ),
        None,
    )
    artifact_path = None
    artifact_digest = ""
    artifact_candidates = local_artifact_candidates(flagship_head, rid, platform)
    if artifact_row:
        file_name = str(artifact_row.get("fileName") or "").strip()
        if file_name:
            candidate = downloads_root / file_name
            if candidate.is_file():
                artifact_path = candidate
    if artifact_path is None:
        artifact_path = first_existing(artifact_candidates)
    if artifact_path is not None:
        artifact_digest = f"sha256:{sha256_file(artifact_path)}"

    receipt_file = startup_smoke_dir / f"startup-smoke-{flagship_head}-{rid}.receipt.json"
    receipt_payload = load_json(receipt_file)
    receipt_status = normalize(receipt_payload.get("status"))
    receipt_version = str(receipt_payload.get("version") or receipt_payload.get("releaseVersion") or "").strip()
    receipt_channel = normalize(receipt_payload.get("channelId") or receipt_payload.get("channel"))
    receipt_digest = normalize(receipt_payload.get("artifactDigest"))
    receipt_ready_checkpoint = str(receipt_payload.get("readyCheckpoint") or "").strip()

    gate_path = spec["gate_path"]
    gate_payload = load_json(gate_path)
    gate_status = normalize(gate_payload.get("status"))
    gate_version = str(gate_payload.get("releaseVersion") or gate_payload.get("release_channel", {}).get("version") or "").strip()
    gate_channel = normalize(gate_payload.get("channelId") or gate_payload.get("release_channel", {}).get("channelId"))

    row_blockers: list[str] = []
    if artifact_row is None:
        row_blockers.append(f"{tuple_id} is missing a promoted {platform} installer/media row in RELEASE_CHANNEL.generated.json.")
    if artifact_path is None:
        row_blockers.append(f"{tuple_id} is missing a local artifact under Docker/Downloads/files.")
    if not receipt_payload:
        row_blockers.append(f"{tuple_id} is missing startup smoke receipt {receipt_file}.")
    else:
        if receipt_status not in PASS_STATUSES:
            row_blockers.append(f"{tuple_id} startup smoke status is {receipt_status or 'missing'} instead of pass.")
        if normalize(receipt_payload.get("headId")) != flagship_head:
            row_blockers.append(f"{tuple_id} startup smoke headId drifted from {flagship_head}.")
        if normalize(receipt_payload.get("rid")) != rid:
            row_blockers.append(f"{tuple_id} startup smoke rid drifted from {rid}.")
        if normalize(receipt_payload.get("platform")) != platform:
            row_blockers.append(f"{tuple_id} startup smoke platform drifted from {platform}.")
        if release_version and receipt_version != release_version:
            row_blockers.append(
                f"{tuple_id} startup smoke version {receipt_version or 'missing'} does not match release channel version {release_version}."
            )
        if release_channel_id and receipt_channel != release_channel_id:
            row_blockers.append(
                f"{tuple_id} startup smoke channel {receipt_channel or 'missing'} does not match release channel {release_channel_id}."
            )
        if receipt_ready_checkpoint != "pre_ui_event_loop":
            row_blockers.append(
                f"{tuple_id} startup smoke ready checkpoint {receipt_ready_checkpoint or 'missing'} does not match pre_ui_event_loop."
            )
        if artifact_digest and receipt_digest and receipt_digest != normalize(artifact_digest):
            row_blockers.append(f"{tuple_id} startup smoke artifact digest does not match the local artifact bytes.")
    if not gate_payload:
        row_blockers.append(f"{tuple_id} is missing executable gate receipt {gate_path}.")
    else:
        if gate_status not in PASS_STATUSES:
            row_blockers.append(f"{tuple_id} executable gate status is {gate_status or 'missing'} instead of pass.")
        if release_version and gate_version and gate_version != release_version:
            row_blockers.append(
                f"{tuple_id} executable gate version {gate_version} does not match release channel version {release_version}."
            )
        if release_channel_id and gate_channel and gate_channel != release_channel_id:
            row_blockers.append(
                f"{tuple_id} executable gate channel {gate_channel} does not match release channel {release_channel_id}."
            )

    proof_rows.append(
        {
            "tupleId": tuple_id,
            "head": flagship_head,
            "platform": platform,
            "rid": rid,
            "releaseChannelArtifactId": artifact_row.get("artifactId") if artifact_row else None,
            "releaseChannelArtifactPresent": artifact_row is not None,
            "localArtifactPath": str(artifact_path) if artifact_path is not None else "",
            "localArtifactPresent": artifact_path is not None,
            "localArtifactDigest": artifact_digest,
            "startupSmokeReceiptPath": str(receipt_file),
            "startupSmokeReceiptPresent": bool(receipt_payload),
            "startupSmokeStatus": receipt_status,
            "startupSmokeVersion": receipt_version,
            "startupSmokeVersionMatchesReleaseChannel": not release_version or receipt_version == release_version,
            "startupSmokeChannelId": receipt_channel,
            "startupSmokeChannelMatchesReleaseChannel": not release_channel_id or receipt_channel == release_channel_id,
            "startupSmokeReadyCheckpoint": receipt_ready_checkpoint,
            "startupSmokeReadyCheckpointMatches": receipt_ready_checkpoint == "pre_ui_event_loop",
            "startupSmokeArtifactDigest": receipt_payload.get("artifactDigest"),
            "startupSmokeArtifactDigestMatchesLocalArtifact": not artifact_digest or not receipt_digest or receipt_digest == normalize(artifact_digest),
            "executableGatePath": str(gate_path),
            "executableGatePresent": bool(gate_payload),
            "executableGateStatus": gate_status,
            "executableGateVersion": gate_version,
            "executableGateVersionMatchesReleaseChannel": not release_version or not gate_version or gate_version == release_version,
            "executableGateChannelId": gate_channel,
            "executableGateChannelMatchesReleaseChannel": not release_channel_id or not gate_channel or gate_channel == release_channel_id,
            "blockingFindings": row_blockers,
        }
    )
    blocking_findings.extend(row_blockers)

aggregate_gate_payload = load_json(aggregate_gate_path)
aggregate_gate_status = normalize(aggregate_gate_payload.get("status"))
aggregate_gate_blocking_count = aggregate_gate_payload.get("localBlockingFindingsCount")
if not aggregate_gate_payload:
    blocking_findings.append(f"Aggregate executable gate receipt is missing at {aggregate_gate_path}.")
elif aggregate_gate_status not in PASS_STATUSES:
    blocking_findings.append(f"Aggregate executable gate status is {aggregate_gate_status or 'missing'} instead of pass.")
elif aggregate_gate_blocking_count not in {0, None}:
    blocking_findings.append(
        f"Aggregate executable gate localBlockingFindingsCount is {aggregate_gate_blocking_count} instead of 0."
    )

required_release_tuples = [
    f"{flagship_head}:linux-x64:linux",
    f"{flagship_head}:osx-arm64:macos",
    f"{flagship_head}:win-x64:windows",
]
reported_required_tuples = sorted(
    {
        normalize(item)
        for item in (release_channel.get("desktopTupleCoverage", {}) or {}).get("requiredDesktopPlatformHeadRidTuples", [])
        if str(item or "").strip()
    }
)
if sorted(required_release_tuples) != reported_required_tuples:
    blocking_findings.append(
        "Release channel requiredDesktopPlatformHeadRidTuples does not cover the expected flagship Windows/Linux/macOS tuple set."
    )

status = "pass" if not blocking_findings else "fail"
summary = (
    "Cross-platform startup-smoke and executable-gate proof is current for the promoted flagship bytes."
    if status == "pass"
    else "Cross-platform startup-smoke and executable-gate proof is still blocked for the promoted flagship bytes."
)

payload = {
    "contract_name": "chummer6-ui.next90_m144_startup_smoke_and_executable_gate",
    "generated_at": now_iso(),
    "status": status,
    "summary": summary,
    "title": TITLE,
    "task": TASK,
    "packageId": PACKAGE_ID,
    "frontierId": FRONTIER_ID,
    "workTaskId": WORK_TASK_ID,
    "milestoneId": MILESTONE_ID,
    "wave": WAVE,
    "allowedPaths": EXPECTED_ALLOWED_PATHS,
    "ownedSurfaces": EXPECTED_SURFACES,
    "releaseChannel": {
        "path": str(release_channel_path),
        "present": bool(release_channel),
        "channelId": release_channel_id,
        "version": release_version,
        "requiredDesktopPlatformHeadRidTuples": reported_required_tuples,
    },
    "aggregateExecutableGate": {
        "path": str(aggregate_gate_path),
        "present": bool(aggregate_gate_payload),
        "status": aggregate_gate_status,
        "localBlockingFindingsCount": aggregate_gate_blocking_count,
    },
    "crossPlatformTupleGoals": required_release_tuples,
    "proofs": proof_rows,
    "blockingFindings": blocking_findings,
    "blocking_findings": blocking_findings,
    "reasons": blocking_findings,
    "blockingFindingsCount": len(blocking_findings),
    "blocking_findings_count": len(blocking_findings),
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
