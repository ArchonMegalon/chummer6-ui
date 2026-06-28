#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).absolute().parents[1]
DEFAULT_MANIFEST = REPO_ROOT / ".tmp" / "verify-release-channel" / "RELEASE_CHANNEL.generated.json"
DEFAULT_WINDOWS_GATE = REPO_ROOT / ".codex-studio" / "published" / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
DEFAULT_STARTUP_SMOKE = REPO_ROOT / "Docker" / "Downloads" / "startup-smoke" / "startup-smoke-avalonia-win-x64.receipt.json"
DEFAULT_CAPTURE_SCRIPT = REPO_ROOT / "scripts" / "capture-windows-installer-visual-proof.ps1"
DEFAULT_VISUAL_PROOF = REPO_ROOT / ".codex-studio" / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"
DEFAULT_JSON_OUTPUT = REPO_ROOT / ".codex-studio" / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
DEFAULT_MD_OUTPUT = REPO_ROOT / ".codex-studio" / "published" / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"


def now_iso() -> str:
    return datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"expected JSON object in {path}")
    return payload


def normalize(value: Any) -> str:
    return str(value or "").strip()


def normalize_sha256(value: Any) -> str:
    normalized = normalize(value).lower()
    if not normalized:
        return ""
    if normalized.startswith("sha256:"):
        return normalized
    if len(normalized) == 64:
        return f"sha256:{normalized}"
    return normalized


def unique_paths(paths: list[Path]) -> list[Path]:
    seen: set[str] = set()
    unique: list[Path] = []
    for path in paths:
        token = str(path.resolve(strict=False))
        if token in seen:
            continue
        seen.add(token)
        unique.append(path)
    return unique


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Materialize the exact Windows installer visual-proof handoff for the current Avalonia release candidate."
    )
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--windows-gate", type=Path, default=DEFAULT_WINDOWS_GATE)
    parser.add_argument("--startup-smoke", type=Path, default=DEFAULT_STARTUP_SMOKE)
    parser.add_argument("--capture-script", type=Path, default=DEFAULT_CAPTURE_SCRIPT)
    parser.add_argument("--visual-proof", type=Path, default=DEFAULT_VISUAL_PROOF)
    parser.add_argument("--json-output", type=Path, default=DEFAULT_JSON_OUTPUT)
    parser.add_argument("--md-output", type=Path, default=DEFAULT_MD_OUTPUT)
    return parser.parse_args()


def find_windows_artifact(manifest: dict[str, Any]) -> dict[str, Any] | None:
    for row in manifest.get("artifacts") or []:
        if not isinstance(row, dict):
            continue
        if normalize(row.get("artifactId")) == "avalonia-win-x64-installer":
            return row
    for row in manifest.get("artifacts") or []:
        if not isinstance(row, dict):
            continue
        head = normalize(row.get("head")).lower()
        platform = normalize(row.get("platform")).lower()
        rid = normalize(row.get("rid")).lower()
        kind = normalize(row.get("kind")).lower()
        if head == "avalonia" and platform == "windows" and rid == "win-x64" and kind == "installer":
            return row
    return None


def parse_path(value: Any) -> Path | None:
    raw = normalize(value)
    return Path(raw) if raw else None


def candidate_files_roots(repo_root: Path, manifest_path: Path, windows_gate: dict[str, Any]) -> list[Path]:
    checks = windows_gate.get("checks") if isinstance(windows_gate.get("checks"), dict) else {}
    roots: list[Path] = []

    manifest_files_root = manifest_path.parent / "files"
    roots.append(manifest_files_root)

    primary_shelf_root = parse_path(checks.get("windows_installer_primary_shelf_root"))
    if primary_shelf_root is not None:
        roots.append(primary_shelf_root)

    installer_path = parse_path(checks.get("windows_installer_path"))
    if installer_path is not None:
        roots.append(installer_path.parent)

    payload_path = parse_path(checks.get("bootstrap_payload_path"))
    if payload_path is not None:
        roots.append(payload_path.parent)

    roots.extend(
        [
            repo_root / "Docker" / "Downloads" / "files",
            repo_root / "files",
            repo_root.parent / "chummer.run-services" / "Chummer.Portal" / "downloads" / "files",
        ]
    )
    return unique_paths(roots)


def find_local_candidates(
    repo_root: Path,
    manifest_path: Path,
    windows_gate: dict[str, Any],
    installer_name: str,
    payload_name: str,
) -> dict[str, list[str]]:
    checks = windows_gate.get("checks") if isinstance(windows_gate.get("checks"), dict) else {}
    files_roots = candidate_files_roots(repo_root, manifest_path, windows_gate)

    installer_candidates = []
    payload_candidates = []

    installer_path = parse_path(checks.get("windows_installer_path"))
    if installer_path is not None:
        installer_candidates.append(installer_path)

    bootstrap_payload_path = parse_path(checks.get("bootstrap_payload_path"))
    if bootstrap_payload_path is not None:
        payload_candidates.append(bootstrap_payload_path)

    installer_candidates.extend(root / installer_name for root in files_roots)
    payload_candidates.extend(root / payload_name for root in files_roots)

    installer_candidates = unique_paths(installer_candidates)
    payload_candidates = unique_paths(payload_candidates)

    return {
        "files_root_candidates": [str(path) for path in files_roots],
        "installer_candidate_paths": [str(path) for path in installer_candidates],
        "payload_candidate_paths": [str(path) for path in payload_candidates],
        "installer_existing_paths": [str(path) for path in installer_candidates if path.is_file()],
        "payload_existing_paths": [str(path) for path in payload_candidates if path.is_file()],
    }


def resolve_startup_smoke_path(windows_gate: dict[str, Any], requested_path: Path) -> tuple[Path | None, list[str]]:
    checks = windows_gate.get("checks") if isinstance(windows_gate.get("checks"), dict) else {}
    raw_candidates: list[str] = []
    preferred = normalize(checks.get("startup_smoke_receipt_path"))
    if preferred:
        raw_candidates.append(preferred)
    requested = normalize(requested_path)
    if requested:
        raw_candidates.append(requested)
    for candidate in checks.get("startup_smoke_receipt_candidates") or []:
        normalized = normalize(candidate)
        if normalized:
            raw_candidates.append(normalized)

    seen: set[str] = set()
    ordered_candidates: list[str] = []
    for candidate in raw_candidates:
        if candidate in seen:
            continue
        seen.add(candidate)
        ordered_candidates.append(candidate)

    for candidate in ordered_candidates:
        path = Path(candidate)
        if path.is_file():
            return path, ordered_candidates
    return None, ordered_candidates


def build_payload(
    *,
    repo_root: Path,
    manifest_path: Path,
    windows_gate_path: Path,
    startup_smoke_path: Path,
    capture_script_path: Path,
    visual_proof_path: Path,
) -> dict[str, Any]:
    manifest = load_json(manifest_path)
    windows_gate = load_json(windows_gate_path)
    gate_checks = windows_gate.get("checks") if isinstance(windows_gate.get("checks"), dict) else {}

    windows_artifact = find_windows_artifact(manifest)
    blockers: list[str] = []
    if windows_artifact is None:
        blockers.append("Release manifest does not expose avalonia-win-x64-installer.")

    installer_name = normalize((windows_artifact or {}).get("fileName"))
    payload_name = normalize((windows_artifact or {}).get("payloadFileName"))
    local_candidates = (
        find_local_candidates(repo_root, manifest_path, windows_gate, installer_name, payload_name)
        if installer_name and payload_name
        else {
            "files_root_candidates": [],
            "installer_candidate_paths": [],
            "payload_candidate_paths": [],
            "installer_existing_paths": [],
            "payload_existing_paths": [],
        }
    )

    gate_reasons = [normalize(item) for item in (windows_gate.get("reasons") or []) if normalize(item)]
    only_visual_blocker = bool(gate_reasons) and all("visual proof" in item.lower() for item in gate_reasons)
    resolved_startup_smoke_path, startup_smoke_candidate_paths = resolve_startup_smoke_path(windows_gate, startup_smoke_path)
    startup_smoke = load_json(resolved_startup_smoke_path) if resolved_startup_smoke_path is not None else {}
    if resolved_startup_smoke_path is None:
        blockers.append("Startup smoke receipt could not be resolved from the Windows gate or the requested path.")

    release_version = normalize((windows_artifact or {}).get("releaseVersion") or manifest.get("releaseVersion") or manifest.get("version"))
    installer_sha256 = normalize_sha256((windows_artifact or {}).get("sha256"))
    startup_smoke_status = normalize(startup_smoke.get("status"))
    startup_smoke_version = normalize(startup_smoke.get("version"))
    startup_smoke_release_version = normalize(startup_smoke.get("releaseVersion"))
    startup_smoke_artifact_file_name = normalize(startup_smoke.get("artifactFileName"))
    startup_smoke_artifact_digest = normalize_sha256(startup_smoke.get("artifactDigest"))

    startup_smoke_status_ok = startup_smoke_status.lower() == "pass"
    startup_smoke_version_matches_release = bool(release_version) and startup_smoke_version == release_version
    startup_smoke_release_matches_release = bool(release_version) and startup_smoke_release_version == release_version
    startup_smoke_release_ok = startup_smoke_version_matches_release and startup_smoke_release_matches_release
    startup_smoke_artifact_matches_installer = bool(installer_name) and startup_smoke_artifact_file_name == installer_name
    startup_smoke_digest_matches_installer = bool(installer_sha256) and startup_smoke_artifact_digest == installer_sha256

    if resolved_startup_smoke_path is not None and not startup_smoke_status_ok:
        blockers.append("Startup smoke receipt is present but not passing for the current Windows installer candidate.")
    if resolved_startup_smoke_path is not None and not startup_smoke_release_ok:
        blockers.append("Startup smoke receipt version does not match the current Windows release candidate.")
    if resolved_startup_smoke_path is not None and not startup_smoke_artifact_matches_installer:
        blockers.append("Startup smoke receipt artifact file does not match the current Windows installer.")
    if resolved_startup_smoke_path is not None and not startup_smoke_digest_matches_installer:
        blockers.append("Startup smoke receipt artifact digest does not match the current Windows installer digest.")

    startup_smoke_progress_log_path = parse_path(gate_checks.get("startup_smoke_progress_log_path"))
    startup_smoke_progress_log_exists = bool(
        startup_smoke_progress_log_path is not None and startup_smoke_progress_log_path.is_file()
    )

    screenshot_dir = visual_proof_path.parent / "windows-installer-visual-proof"
    progress_path = screenshot_dir / "windows-installer-progress.png"
    completion_path = screenshot_dir / "windows-installer-completion.png"
    current_visual_proof = load_json(visual_proof_path) if visual_proof_path.is_file() else {}
    current_visual_proof_status = normalize(current_visual_proof.get("status"))
    current_visual_proof_version = normalize(current_visual_proof.get("version") or current_visual_proof.get("releaseVersion"))
    current_visual_proof_digest = normalize_sha256(
        current_visual_proof.get("artifactDigest")
        or current_visual_proof.get("installerDigest")
        or current_visual_proof.get("installerSha256")
    )
    current_visual_proof_matches_release = bool(release_version) and current_visual_proof_version == release_version
    current_visual_proof_matches_installer_digest = bool(installer_sha256) and current_visual_proof_digest == installer_sha256
    current_visual_proof_stale = bool(current_visual_proof) and (
        not current_visual_proof_matches_release or not current_visual_proof_matches_installer_digest
    )

    next_actions = [
        "On a real Windows host, open the repo checkout that contains the capture script and run "
        f"`powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\capture-windows-installer-visual-proof.ps1 "
        f"-ReleaseChannelPath \"{manifest_path}\" -OutputPath \"{visual_proof_path}\"`.",
        f"Confirm `{progress_path.name}` and `{completion_path.name}` are written under `{screenshot_dir}`.",
        f"Confirm `{visual_proof_path.name}` is written under `{visual_proof_path.parent}`.",
        "Rerun the Windows exit gate against the same shelf: "
        f"`CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH=\"{manifest_path}\" "
        f"CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT=\"{manifest_path.parent / 'files'}\" "
        f"CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH=\"{visual_proof_path}\" "
        f"bash {repo_root / 'scripts' / 'materialize-windows-desktop-exit-gate.sh'}`.",
    ]
    if current_visual_proof_stale:
        next_actions.insert(
            0,
            f"Overwrite the stale Windows visual-proof receipt at `{visual_proof_path}`; its recorded release or installer digest no longer matches the staged candidate.",
        )
    elif current_visual_proof:
        next_actions.insert(
            0,
            f"Refresh the existing Windows visual-proof receipt at `{visual_proof_path}` against the staged candidate before promotion.",
        )
    ready_for_windows_host = only_visual_blocker and not blockers

    return {
        "contract_name": "chummer6-ui.windows_installer_visual_proof_handoff",
        "generated_at": now_iso(),
        "repo_root": str(repo_root),
        "release_channel_manifest_path": str(manifest_path),
        "release_shelf_root": str(manifest_path.parent),
        "windows_gate_path": str(windows_gate_path),
        "startup_smoke_requested_path": str(startup_smoke_path),
        "startup_smoke_path": str(resolved_startup_smoke_path or startup_smoke_path),
        "startup_smoke_candidate_paths": startup_smoke_candidate_paths,
        "startup_smoke_progress_log_path": str(startup_smoke_progress_log_path) if startup_smoke_progress_log_path else "",
        "startup_smoke_progress_log_exists": startup_smoke_progress_log_exists,
        "capture_script_path": str(capture_script_path),
        "visual_proof_receipt_path": str(visual_proof_path),
        "current_visual_proof_exists": bool(current_visual_proof),
        "status": "ready_for_windows_host" if ready_for_windows_host else "needs_review",
        "summary": normalize(windows_gate.get("summary")),
        "windows_gate_status": normalize(windows_gate.get("status")),
        "windows_gate_reasons": gate_reasons,
        "only_blocker_is_visual_proof": only_visual_blocker,
        "release": {
            "channel_id": normalize(manifest.get("channelId") or manifest.get("channel")),
            "version": normalize(manifest.get("version")),
            "release_version": release_version,
        },
        "windows_installer": {
            "artifact_id": normalize((windows_artifact or {}).get("artifactId")),
            "file_name": installer_name,
            "download_url": normalize((windows_artifact or {}).get("downloadUrl")),
            "sha256": installer_sha256,
            "payload_file_name": payload_name,
            "payload_download_url": normalize((windows_artifact or {}).get("payloadDownloadUrl")),
            "local_candidates": local_candidates,
        },
        "startup_smoke": {
            "status": startup_smoke_status,
            "version": startup_smoke_version,
            "release_version": startup_smoke_release_version,
            "artifact_file_name": startup_smoke_artifact_file_name,
            "artifact_digest": startup_smoke_artifact_digest,
            "host_class": normalize(startup_smoke.get("hostClass")),
            "matches_release_version": startup_smoke_release_ok,
            "matches_artifact_file_name": startup_smoke_artifact_matches_installer,
            "matches_artifact_digest": startup_smoke_digest_matches_installer,
            "progress_log_path": str(startup_smoke_progress_log_path) if startup_smoke_progress_log_path else "",
            "progress_log_exists": startup_smoke_progress_log_exists,
        },
        "current_visual_proof": {
            "status": current_visual_proof_status,
            "version": current_visual_proof_version,
            "artifact_digest": current_visual_proof_digest,
            "matches_release_version": current_visual_proof_matches_release,
            "matches_installer_digest": current_visual_proof_matches_installer_digest,
            "stale": current_visual_proof_stale,
        },
        "required_screenshots": [
            {
                "role": "progress",
                "file_name": progress_path.name,
                "path": str(progress_path),
            },
            {
                "role": "completion",
                "file_name": completion_path.name,
                "path": str(completion_path),
            },
        ],
        "blockers": blockers,
        "next_actions": next_actions,
    }


def render_markdown(payload: dict[str, Any]) -> str:
    artifact = payload["windows_installer"]
    startup_smoke = payload["startup_smoke"]
    current_visual_proof = payload["current_visual_proof"]
    screenshot_lines = [
        f"- `{item['role']}`: `{item['file_name']}` -> `{item['path']}`"
        for item in payload["required_screenshots"]
    ]
    local_installer_lines = artifact["local_candidates"]["installer_existing_paths"]
    local_payload_lines = artifact["local_candidates"]["payload_existing_paths"]
    rendered_installer_lines = (
        [f"- `{item}`" for item in local_installer_lines]
        if local_installer_lines
        else ["- none found"]
    )
    rendered_payload_lines = (
        [f"- `{item}`" for item in local_payload_lines]
        if local_payload_lines
        else ["- none found"]
    )
    blocker_lines = [f"- {item}" for item in payload["blockers"]] if payload["blockers"] else ["- none"]
    reason_lines = [f"- {item}" for item in payload["windows_gate_reasons"]] if payload["windows_gate_reasons"] else ["- none"]
    next_action_lines = [f"- {item}" for item in payload["next_actions"]]

    lines = [
        "# Windows Visual Proof Handoff",
        "",
        f"Generated: {payload['generated_at']}",
        "",
        f"- Status: `{payload['status']}`",
        f"- Gate summary: {payload['summary']}",
        f"- Only blocker is visual proof: `{payload['only_blocker_is_visual_proof']}`",
        f"- Channel: `{payload['release']['channel_id']}`",
        f"- Version: `{payload['release']['version']}`",
        f"- Shelf root: `{payload['release_shelf_root']}`",
        "",
        "## Installer",
        "",
        f"- Artifact: `{artifact['artifact_id']}`",
        f"- File: `{artifact['file_name']}`",
        f"- URL: `{artifact['download_url']}`",
        f"- SHA-256: `{artifact['sha256']}`",
        f"- Payload: `{artifact['payload_file_name']}`",
        f"- Payload URL: `{artifact['payload_download_url']}`",
        "",
        "### Local installer bytes found",
        "",
        *rendered_installer_lines,
        "",
        "### Local payload bytes found",
        "",
        *rendered_payload_lines,
        "",
        "## Startup smoke already present",
        "",
        f"- Status: `{startup_smoke['status']}`",
        f"- Version: `{startup_smoke['version']}`",
        f"- Release version: `{startup_smoke['release_version']}`",
        f"- Receipt: `{payload['startup_smoke_path']}`",
        f"- Host class: `{startup_smoke['host_class']}`",
        f"- Matches release candidate: `{startup_smoke['matches_release_version']}`",
        f"- Matches installer file: `{startup_smoke['matches_artifact_file_name']}`",
        f"- Matches installer digest: `{startup_smoke['matches_artifact_digest']}`",
        f"- Progress log: `{startup_smoke['progress_log_path']}`",
        f"- Progress log present: `{startup_smoke['progress_log_exists']}`",
        "",
        "## Current visual proof state",
        "",
        f"- Exists: `{payload['current_visual_proof_exists']}`",
        f"- Status: `{current_visual_proof['status']}`",
        f"- Version: `{current_visual_proof['version']}`",
        f"- Digest: `{current_visual_proof['artifact_digest']}`",
        f"- Matches release candidate: `{current_visual_proof['matches_release_version']}`",
        f"- Matches installer digest: `{current_visual_proof['matches_installer_digest']}`",
        f"- Stale: `{current_visual_proof['stale']}`",
        "",
        "## Required screenshots",
        "",
        *screenshot_lines,
        "",
        "## Gate reasons",
        "",
        *reason_lines,
        "",
        "## Blockers",
        "",
        *blocker_lines,
        "",
        "## Next actions",
        "",
        *next_action_lines,
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    args = parse_args()
    payload = build_payload(
        repo_root=REPO_ROOT,
        manifest_path=args.manifest,
        windows_gate_path=args.windows_gate,
        startup_smoke_path=args.startup_smoke,
        capture_script_path=args.capture_script,
        visual_proof_path=args.visual_proof,
    )
    args.json_output.parent.mkdir(parents=True, exist_ok=True)
    args.md_output.parent.mkdir(parents=True, exist_ok=True)
    args.json_output.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    args.md_output.write_text(render_markdown(payload), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
