#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


def now_iso() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def normalize(value: object) -> str:
    return str(value or "").strip()


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise SystemExit(f"expected JSON object in {path}")
    return payload


def collect_receipts(startup_smoke_dir: Path) -> dict[str, dict[str, Any]]:
    receipts: dict[str, dict[str, Any]] = {}
    for path in sorted(startup_smoke_dir.glob("startup-smoke-*.receipt.json")):
        payload = load_json(path)
        tuple_key = f"{normalize(payload.get('headId'))}:{normalize(payload.get('platform'))}:{normalize(payload.get('rid'))}"
        receipts[tuple_key] = {
            "path": str(path),
            "status": normalize(payload.get("status")),
            "skip_reason": normalize(payload.get("skipReason")),
            "artifact_digest": normalize(payload.get("artifactDigest")),
            "ready_checkpoint": normalize(payload.get("readyCheckpoint")),
            "host_class": normalize(payload.get("hostClass")),
        }
    return receipts


def maybe_materialize_windows_exit_gate(stage_dir: Path) -> dict[str, Any]:
    manifest_path = stage_dir / "RELEASE_CHANNEL.generated.json"
    files_dir = stage_dir / "files"
    output_path = stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    configured_script = normalize(os.environ.get("CHUMMER_WINDOWS_EXIT_GATE_SCRIPT_PATH"))
    script_path = Path(configured_script) if configured_script else Path(__file__).with_name("materialize-windows-desktop-exit-gate.sh")

    payload: dict[str, Any] = {
        "script_path": str(script_path),
        "json_path": str(output_path),
        "status": "",
        "summary": "",
        "blocking_mode": "",
        "stdout": "",
        "stderr": "",
        "return_code": None,
    }

    if not manifest_path.is_file() or not files_dir.is_dir():
        payload["status"] = "unavailable"
        return payload

    if not script_path.is_file():
        if output_path.is_file():
            existing = load_json(output_path)
            payload["status"] = normalize(existing.get("status")) or "existing_only"
            payload["summary"] = normalize(existing.get("summary"))
            payload["blocking_mode"] = normalize(existing.get("blockingMode") or existing.get("blocking_mode"))
            return payload
        payload["status"] = "missing_script"
        return payload

    env = dict(os.environ)
    env.update(
        {
            "CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH": str(manifest_path),
            "CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT": str(files_dir),
            "CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH": str(output_path),
            "CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH": str(stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"),
        }
    )
    completed = subprocess.run(
        ["bash", str(script_path)],
        text=True,
        capture_output=True,
        check=False,
        env=env,
    )
    payload["stdout"] = completed.stdout.strip()
    payload["stderr"] = completed.stderr.strip()
    payload["return_code"] = completed.returncode

    if output_path.is_file():
        refreshed = load_json(output_path)
        payload["status"] = normalize(refreshed.get("status")) or ("pass" if completed.returncode == 0 else "failed")
        payload["summary"] = normalize(refreshed.get("summary"))
        payload["blocking_mode"] = normalize(refreshed.get("blockingMode") or refreshed.get("blocking_mode"))
    else:
        payload["status"] = "missing_output" if completed.returncode == 0 else "error"

    return payload


def maybe_materialize_windows_visual_proof_handoff(stage_dir: Path) -> dict[str, Any]:
    manifest_path = stage_dir / "RELEASE_CHANNEL.generated.json"
    windows_gate_path = stage_dir / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_path = stage_dir / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json"
    if not manifest_path.is_file() or not windows_gate_path.is_file() or not startup_smoke_path.is_file():
        return {}

    visual_proof_path = stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
    json_output = stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    md_output = stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"
    script_path = Path(__file__).with_name("materialize_windows_visual_proof_handoff.py")

    completed = subprocess.run(
        [
            sys.executable,
            str(script_path),
            "--manifest",
            str(manifest_path),
            "--windows-gate",
            str(windows_gate_path),
            "--startup-smoke",
            str(startup_smoke_path),
            "--visual-proof",
            str(visual_proof_path),
            "--json-output",
            str(json_output),
            "--md-output",
            str(md_output),
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    payload: dict[str, Any] = {
        "status": "error" if completed.returncode else "",
        "json_path": str(json_output),
        "md_path": str(md_output),
        "visual_proof_path": str(visual_proof_path),
        "command": " ".join(
            [
                sys.executable,
                str(script_path),
                "--manifest",
                str(manifest_path),
                "--windows-gate",
                str(windows_gate_path),
                "--startup-smoke",
                str(startup_smoke_path),
                "--visual-proof",
                str(visual_proof_path),
                "--json-output",
                str(json_output),
                "--md-output",
                str(md_output),
            ]
        ),
        "stdout": completed.stdout.strip(),
        "stderr": completed.stderr.strip(),
    }

    if json_output.is_file():
        child = load_json(json_output)
        payload.update(
            {
                "status": normalize(child.get("status")),
                "summary": normalize(child.get("summary")),
                "only_blocker_is_visual_proof": bool(child.get("only_blocker_is_visual_proof")),
                "blockers": child.get("blockers") or [],
                "next_actions": child.get("next_actions") or [],
            }
        )
    elif completed.returncode == 0:
        payload["status"] = "missing_output"

    return payload


def build_payload(stage_dir: Path) -> dict[str, Any]:
    manifest_path = stage_dir / "RELEASE_CHANNEL.generated.json"
    manifest = load_json(manifest_path)
    windows_exit_gate_refresh = maybe_materialize_windows_exit_gate(stage_dir)
    coverage = manifest.get("desktopTupleCoverage") or {}
    receipts = collect_receipts(stage_dir / "startup-smoke")
    release_version = normalize(manifest.get("version"))
    release_channel = normalize(manifest.get("channelId") or manifest.get("channel"))

    artifacts = []
    artifact_keys: set[str] = set()
    for row in manifest.get("artifacts") or []:
        artifact_id = normalize(row.get("artifactId"))
        file_name = normalize(row.get("fileName"))
        platform = normalize(row.get("platform"))
        rid = normalize(row.get("rid"))
        head = normalize(row.get("head"))
        artifacts.append(
            {
                "artifact_id": artifact_id,
                "file_name": file_name,
                "platform": platform,
                "rid": rid,
                "version": normalize(row.get("version")),
            }
        )
        artifact_keys.add(f"{head}:{platform}:{rid}:{file_name}")

    blockers: list[str] = []
    next_actions: list[str] = []
    missing_platforms = coverage.get("missingRequiredPlatforms") or []
    if "windows" in missing_platforms:
        blockers.append("Windows installer tuple is not promotable from this host because startup-smoke did not pass.")
        next_actions.append("Run Windows installer startup-smoke on a Windows-capable host for the staged installer bytes.")
    if "macos" in missing_platforms:
        blockers.append("macOS tuple is missing entirely from the candidate bundle.")
        next_actions.append("Build the macOS DMG, capture fresh startup-smoke, and restage the bundle.")
    if "linux" in missing_platforms:
        blockers.append("Linux tuple is missing or not promotable.")
        next_actions.append("Rebuild Linux installer bytes and rerun Linux startup-smoke.")

    for tuple_key, receipt in sorted(receipts.items()):
        head, platform, rid = tuple_key.split(":", 2)
        if platform != "windows":
            continue
        if normalize(receipt.get("status")) not in {"pass", "passed", "ready"}:
            continue
        receipt_payload = load_json(Path(receipt["path"]))
        receipt_version = normalize(receipt_payload.get("releaseVersion") or receipt_payload.get("version"))
        receipt_channel = normalize(receipt_payload.get("channelId") or receipt_payload.get("channel"))
        if release_version and receipt_version and receipt_version != release_version:
            continue
        if release_channel and receipt_channel and receipt_channel != release_channel:
            continue
        file_name = normalize(receipt_payload.get("artifactFileName") or receipt_payload.get("fileName"))
        if not file_name:
            blockers.append(f"Windows startup-smoke receipt {Path(receipt['path']).name} is missing its installer file name.")
            continue
        file_path = stage_dir / "files" / file_name
        if not file_path.is_file():
            blockers.append(
                f"Windows startup-smoke passed for {file_name}, but the staged installer bytes are missing from {stage_dir / 'files'}."
            )
            next_actions.append("Restage the matching Windows installer bytes before publication.")
            continue
        artifact_key = f"{head}:{platform}:{rid}:{file_name}"
        if artifact_key not in artifact_keys:
            blockers.append(
                f"Windows startup-smoke passed for {file_name}, but RELEASE_CHANNEL.generated.json does not expose a matching Windows artifact row."
            )
            next_actions.append("Regenerate the release manifest after restoring the Windows installer and payload metadata.")

    gate_refresh_status = normalize(windows_exit_gate_refresh.get("status"))
    if gate_refresh_status in {"missing_script", "missing_output", "error", "unavailable"}:
        blockers.append("Stage-local Windows exit-gate refresh did not produce a usable gate receipt.")
        next_actions.append(
            "Fix the stage-local Windows exit-gate refresh path before promotion: "
            f"{windows_exit_gate_refresh.get('json_path') or windows_exit_gate_refresh.get('script_path')}"
        )

    windows_visual_proof_handoff = maybe_materialize_windows_visual_proof_handoff(stage_dir)
    if windows_visual_proof_handoff:
        status = normalize(windows_visual_proof_handoff.get("status"))
        if status == "ready_for_windows_host":
            blockers.append("Windows visual proof is still outstanding for the staged installer bytes.")
            next_actions.append(
                "Use the Windows visual-proof handoff packet to capture progress and completion screenshots for the staged installer bytes: "
                f"{windows_visual_proof_handoff.get('json_path')}"
            )
        elif status in {"needs_review", "error", "missing_output"}:
            blockers.append(
                "Windows visual-proof handoff is not ready; inspect the staged handoff packet before asking a Windows operator to continue."
            )
            next_actions.append(
                "Inspect the Windows visual-proof handoff packet and fix the staged shelf mismatch before promotion: "
                f"{windows_visual_proof_handoff.get('json_path') or windows_visual_proof_handoff.get('command')}"
            )

    next_actions.append("Publish the verified bundle with CHUMMER_RELEASE_UPLOAD_TOKEN once all required platform tuples are promotable.")

    return {
        "contract_name": "chummer.release_build_handoff",
        "generated_at": now_iso(),
        "stage_dir": str(stage_dir),
        "channel": normalize(manifest.get("channelId")),
        "version": normalize(manifest.get("version")),
        "artifact_count": len(artifacts),
        "artifacts": artifacts,
        "promoted_tuples": coverage.get("promotedPlatformHeadRidTuples") or [],
        "missing_required_platforms": missing_platforms,
        "missing_required_heads": coverage.get("missingRequiredHeads") or [],
        "startup_smoke_receipts": receipts,
        "windows_exit_gate_refresh": windows_exit_gate_refresh,
        "windows_visual_proof_handoff": windows_visual_proof_handoff,
        "blockers": blockers,
        "next_actions": next_actions,
        "promotion_ready": not missing_platforms and not blockers,
    }


def render_markdown(payload: dict[str, Any]) -> str:
    blocker_lines = [f"- {item}" for item in payload["blockers"]] if payload["blockers"] else ["- none"]
    windows_exit_gate_refresh = payload.get("windows_exit_gate_refresh") if isinstance(payload.get("windows_exit_gate_refresh"), dict) else {}
    windows_visual_proof_handoff = payload.get("windows_visual_proof_handoff") if isinstance(payload.get("windows_visual_proof_handoff"), dict) else {}
    windows_exit_gate_lines = ["- none"]
    if windows_exit_gate_refresh:
        windows_exit_gate_lines = [
            f"- Status: `{normalize(windows_exit_gate_refresh.get('status'))}`",
            f"- JSON: `{normalize(windows_exit_gate_refresh.get('json_path'))}`",
            f"- Script: `{normalize(windows_exit_gate_refresh.get('script_path'))}`",
            f"- Blocking mode: `{normalize(windows_exit_gate_refresh.get('blocking_mode'))}`",
        ]
        if normalize(windows_exit_gate_refresh.get("summary")):
            windows_exit_gate_lines.append(f"- Summary: {normalize(windows_exit_gate_refresh.get('summary'))}")
    windows_visual_proof_lines = ["- none"]
    if windows_visual_proof_handoff:
        windows_visual_proof_lines = [
            f"- Status: `{normalize(windows_visual_proof_handoff.get('status'))}`",
            f"- JSON: `{normalize(windows_visual_proof_handoff.get('json_path'))}`",
            f"- Markdown: `{normalize(windows_visual_proof_handoff.get('md_path'))}`",
            f"- Visual proof receipt target: `{normalize(windows_visual_proof_handoff.get('visual_proof_path'))}`",
        ]
        if normalize(windows_visual_proof_handoff.get("summary")):
            windows_visual_proof_lines.append(f"- Summary: {normalize(windows_visual_proof_handoff.get('summary'))}")

    lines = [
        "# Release Build Handoff",
        "",
        f"Generated: {payload['generated_at']}",
        "",
        f"- Stage dir: `{payload['stage_dir']}`",
        f"- Channel: `{payload['channel']}`",
        f"- Version: `{payload['version']}`",
        f"- Artifact count: `{payload['artifact_count']}`",
        f"- Promotion ready: `{payload['promotion_ready']}`",
        "",
        "## Artifacts",
        "",
        *[
            f"- `{row['artifact_id']}` -> `{row['file_name']}` ({row['platform']} / {row['rid']})"
            for row in payload["artifacts"]
        ],
        "",
        "## Startup Smoke",
        "",
        *[
            f"- `{tuple_key}`: `{row['status']}`"
            + (f" - {row['skip_reason']}" if row["skip_reason"] else "")
            for tuple_key, row in sorted(payload["startup_smoke_receipts"].items())
        ],
        "",
        "## Windows Exit Gate Refresh",
        "",
        *windows_exit_gate_lines,
        "",
        "## Windows Visual Proof Handoff",
        "",
        *windows_visual_proof_lines,
        "",
        "## Remaining Blockers",
        "",
        *blocker_lines,
        "",
        "## Next Actions",
        "",
        *[f"- {item}" for item in payload["next_actions"]],
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("stage_dir", type=Path)
    parser.add_argument("--json-output", type=Path)
    parser.add_argument("--md-output", type=Path)
    args = parser.parse_args()

    payload = build_payload(args.stage_dir)
    json_output = args.json_output or (args.stage_dir / "RELEASE_BUILD_HANDOFF.generated.json")
    md_output = args.md_output or (args.stage_dir / "RELEASE_BUILD_HANDOFF.generated.md")
    json_output.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    md_output.write_text(render_markdown(payload), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
