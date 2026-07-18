#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any


PASSING_STATUSES = {"pass", "passed", "ready"}
WINDOWS_INSTALL_KINDS = {"installer", "msix"}
WINDOWS_VISUAL_HANDOFF_CONTRACT = "chummer6-ui.windows_installer_visual_proof_handoff"
WINDOWS_EXIT_GATE_CONTRACT = "chummer6-ui.windows_desktop_exit_gate"


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_object(path: Path, label: str, errors: list[str]) -> dict[str, Any]:
    if not path.is_file():
        errors.append(f"{label} is missing: {path}")
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        errors.append(f"{label} is unreadable: {path} ({exc})")
        return {}
    if not isinstance(payload, dict):
        errors.append(f"{label} must contain a JSON object: {path}")
        return {}
    return payload


def release_artifacts(payload: dict[str, Any]) -> list[dict[str, Any]]:
    rows = payload.get("artifacts") or []
    if not isinstance(rows, list):
        return []
    return [row for row in rows if isinstance(row, dict)]


def download_rows(payload: dict[str, Any]) -> list[dict[str, Any]]:
    rows = payload.get("downloads") or payload.get("artifacts") or []
    if not isinstance(rows, list):
        return []
    return [row for row in rows if isinstance(row, dict)]


def artifact_id(row: dict[str, Any]) -> str:
    return str(row.get("artifactId") or row.get("id") or "").strip()


def artifact_digest(row: dict[str, Any]) -> str:
    return normalize(row.get("sha256")).removeprefix("sha256:")


def artifact_file_name(row: dict[str, Any]) -> str:
    return str(row.get("fileName") or "").strip()


def is_windows_install_artifact(row: dict[str, Any]) -> bool:
    return (
        normalize(row.get("platform")) == "windows"
        and normalize(row.get("kind")) in WINDOWS_INSTALL_KINDS
        and normalize(row.get("rid")).startswith("win-")
        and bool(artifact_file_name(row))
    )


def manifest_version(payload: dict[str, Any]) -> str:
    return str(payload.get("version") or payload.get("releaseVersion") or "").strip()


def manifest_channel(payload: dict[str, Any]) -> str:
    return normalize(payload.get("channelId") or payload.get("channel"))


def proof_routes(payload: dict[str, Any]) -> set[str]:
    proof = payload.get("releaseProof")
    if not isinstance(proof, dict):
        return set()
    routes = proof.get("proofRoutes") or proof.get("proof_routes") or []
    if not isinstance(routes, list):
        return set()
    return {str(route).strip() for route in routes if str(route).strip()}


def find_matching_download(
    rows: list[dict[str, Any]],
    artifact: dict[str, Any],
) -> dict[str, Any] | None:
    expected_id = normalize(artifact_id(artifact))
    expected_name = artifact_file_name(artifact)
    matches = [
        row
        for row in rows
        if (
            (expected_id and normalize(artifact_id(row)) == expected_id)
            or (expected_name and artifact_file_name(row) == expected_name)
        )
    ]
    return matches[0] if len(matches) == 1 else None


def find_signing_artifact(receipt: dict[str, Any], file_name: str) -> dict[str, Any] | None:
    rows = receipt.get("artifacts") or []
    if not isinstance(rows, list):
        return None
    matches = [
        row
        for row in rows
        if isinstance(row, dict) and artifact_file_name(row) == file_name
    ]
    return matches[0] if len(matches) == 1 else None


def check_equal(
    errors: list[str],
    actual: Any,
    expected: Any,
    message: str,
) -> None:
    if normalize(actual) != normalize(expected):
        errors.append(message)


def nested_object(payload: dict[str, Any], *keys: str) -> dict[str, Any]:
    current: Any = payload
    for key in keys:
        if not isinstance(current, dict):
            return {}
        current = current.get(key)
    return current if isinstance(current, dict) else {}


def normalized_string_list(value: Any) -> list[str] | None:
    if not isinstance(value, list):
        return None
    if any(not isinstance(item, str) or not item.strip() for item in value):
        return None
    return [item.strip() for item in value]


def check_proof_only_manifest_posture(
    manifest: dict[str, Any],
    errors: list[str],
) -> None:
    if manifest_channel(manifest) != "preview":
        errors.append("proof-only Windows visual handoff is allowed only for channel preview")

    public_release_channel = nested_object(manifest, "publicTrustMetrics", "releaseChannel")
    registry_coverage = nested_object(manifest, "registryBoundaryCoverage")
    registry_release_channel = nested_object(registry_coverage, "releaseChannel")
    if normalize(public_release_channel.get("channelId") or public_release_channel.get("channel")) != "preview":
        errors.append("proof-only Windows visual handoff requires public trust channel=preview")
    if normalize(registry_coverage.get("channelId") or registry_coverage.get("channel")) != "preview":
        errors.append("proof-only Windows visual handoff requires registry channel=preview")
    for actual, label in (
        (manifest.get("supportabilityState"), "top-level supportabilityState"),
        (public_release_channel.get("supportabilityState"), "publicTrustMetrics release supportabilityState"),
        (registry_release_channel.get("supportabilityState"), "registry release supportabilityState"),
    ):
        if normalize(actual) != "review_required":
            errors.append(f"proof-only Windows visual handoff requires {label}=review_required")
    if normalize(registry_release_channel.get("publicTrustPosture")) != "blocked":
        errors.append("proof-only Windows visual handoff requires registry publicTrustPosture=blocked")


def check_visual_proof_handoff(
    *,
    artifact: dict[str, Any],
    manifest: dict[str, Any],
    startup_path: Path,
    startup: dict[str, Any],
    exit_gate: dict[str, Any],
    handoff: dict[str, Any],
    errors: list[str],
    caveats: list[str],
) -> None:
    artifact_identifier = artifact_id(artifact)
    file_name = artifact_file_name(artifact)
    digest = artifact_digest(artifact)
    version = manifest_version(manifest)
    prefix = f"{artifact_identifier or file_name}:"

    check_equal(
        errors,
        handoff.get("contract_name") or handoff.get("contractName"),
        WINDOWS_VISUAL_HANDOFF_CONTRACT,
        f"{prefix} Windows visual handoff contract mismatch",
    )
    for field, expected in (
        ("handoff_only", True),
        ("stable_release_unchanged", True),
        ("requires_separate_publish_lane", True),
    ):
        if handoff.get(field) is not expected:
            errors.append(f"{prefix} Windows visual handoff {field} must be true")
    check_equal(
        errors,
        handoff.get("handoff_scope"),
        "staged_nightly_windows_visual_proof",
        f"{prefix} Windows visual handoff scope mismatch",
    )
    check_equal(
        errors,
        handoff.get("status"),
        "ready_for_windows_host",
        f"{prefix} Windows visual handoff is not ready_for_windows_host",
    )
    if handoff.get("only_blocker_is_visual_proof") is not True:
        errors.append(f"{prefix} Windows visual handoff does not attest visual proof as the only blocker")
    if handoff.get("blockers") != []:
        errors.append(f"{prefix} Windows visual handoff contains other blockers")

    handoff_release = nested_object(handoff, "release")
    check_equal(
        errors,
        handoff_release.get("channel_id"),
        "preview",
        f"{prefix} Windows visual handoff channel is not preview",
    )
    for field in ("version", "release_version"):
        check_equal(
            errors,
            handoff_release.get(field),
            version,
            f"{prefix} Windows visual handoff release {field} mismatch",
        )

    handoff_installer = nested_object(handoff, "windows_installer")
    check_equal(
        errors,
        handoff_installer.get("artifact_id"),
        artifact_identifier,
        f"{prefix} Windows visual handoff artifact id mismatch",
    )
    check_equal(
        errors,
        handoff_installer.get("file_name"),
        file_name,
        f"{prefix} Windows visual handoff installer file mismatch",
    )
    if normalize(handoff_installer.get("sha256")).removeprefix("sha256:") != digest:
        errors.append(f"{prefix} Windows visual handoff installer digest mismatch")

    handoff_startup = nested_object(handoff, "startup_smoke")
    if normalize(handoff_startup.get("status")) not in PASSING_STATUSES:
        errors.append(f"{prefix} Windows visual handoff startup-smoke status is not passing")
    for field in ("version", "release_version"):
        check_equal(
            errors,
            handoff_startup.get(field),
            version,
            f"{prefix} Windows visual handoff startup-smoke {field} mismatch",
        )
    check_equal(
        errors,
        handoff_startup.get("artifact_file_name"),
        file_name,
        f"{prefix} Windows visual handoff startup-smoke artifact file mismatch",
    )
    if normalize(handoff_startup.get("artifact_digest")).removeprefix("sha256:") != digest:
        errors.append(f"{prefix} Windows visual handoff startup-smoke artifact digest mismatch")
    for field in (
        "matches_release_version",
        "matches_artifact_file_name",
        "matches_artifact_digest",
    ):
        if handoff_startup.get(field) is not True:
            errors.append(f"{prefix} Windows visual handoff startup-smoke {field} must be true")

    reported_startup_path = str(handoff.get("startup_smoke_path") or "").strip()
    expected_receipt_name = startup_path.name
    if not reported_startup_path or Path(reported_startup_path).name != expected_receipt_name:
        errors.append(f"{prefix} Windows visual handoff startup-smoke path does not name the staged receipt")
    check_equal(
        errors,
        handoff_startup.get("receipt_file_name"),
        expected_receipt_name,
        f"{prefix} Windows visual handoff startup-smoke receipt file mismatch",
    )
    if startup_path.is_file():
        if normalize(handoff_startup.get("receipt_sha256")).removeprefix("sha256:") != sha256_file(startup_path):
            errors.append(f"{prefix} Windows visual handoff startup-smoke receipt digest mismatch")

    # Rebind handoff assertions to the independently loaded staged receipt.
    if normalize(handoff_startup.get("status")) != normalize(startup.get("status")):
        errors.append(f"{prefix} Windows visual handoff startup-smoke status disagrees with staged receipt")
    if normalize(handoff_startup.get("artifact_digest")).removeprefix("sha256:") != normalize(
        startup.get("artifactDigest")
    ).removeprefix("sha256:"):
        errors.append(f"{prefix} Windows visual handoff startup-smoke digest disagrees with staged receipt")

    check_equal(
        errors,
        exit_gate.get("contract_name") or exit_gate.get("contractName"),
        WINDOWS_EXIT_GATE_CONTRACT,
        f"{prefix} Windows exit-gate contract mismatch for proof-only handoff",
    )
    if normalize(exit_gate.get("status")) in PASSING_STATUSES:
        errors.append(f"{prefix} proof-only visual handoff cannot replace an already-passing Windows exit gate")
    if normalize(exit_gate.get("blockingMode") or exit_gate.get("blocking_mode")) != "external_only":
        errors.append(f"{prefix} Windows exit gate is not blocked only on external host proof")

    gate_reasons = normalized_string_list(exit_gate.get("reasons"))
    handoff_reasons = normalized_string_list(handoff.get("windows_gate_reasons"))
    if not gate_reasons:
        errors.append(f"{prefix} Windows exit gate has no visual-proof blocker reason")
    elif any(not reason.lower().startswith("windows installer visual proof ") for reason in gate_reasons):
        errors.append(f"{prefix} Windows exit gate contains a non-visual-proof blocker")
    if handoff_reasons is None or handoff_reasons != gate_reasons:
        errors.append(f"{prefix} Windows visual handoff reasons do not match the staged exit gate")
    check_equal(
        errors,
        handoff.get("windows_gate_status"),
        exit_gate.get("status"),
        f"{prefix} Windows visual handoff status does not match the staged exit gate",
    )

    caveats.append(f"{artifact_identifier}: native Windows installer visual proof is outstanding")


def check_artifact(
    *,
    artifact: dict[str, Any],
    manifest: dict[str, Any],
    downloads: dict[str, Any],
    signing_dir: Path,
    startup_dir: Path,
    exit_gate: dict[str, Any],
    visual_handoff: dict[str, Any],
    files_dir: Path,
    require_authenticode: bool,
    require_native_windows: bool,
    allow_proof_only_visual_handoff: bool,
    errors: list[str],
    caveats: list[str],
) -> dict[str, Any]:
    artifact_identifier = artifact_id(artifact)
    file_name = artifact_file_name(artifact)
    head = normalize(artifact.get("head"))
    rid = normalize(artifact.get("rid"))
    version = manifest_version(manifest)
    channel = manifest_channel(manifest)
    digest = artifact_digest(artifact)
    prefix = f"{artifact_identifier or file_name}:"

    if not artifact_identifier:
        errors.append(f"{prefix} artifactId is missing")
    if not head:
        errors.append(f"{prefix} head is missing")
    if not rid:
        errors.append(f"{prefix} rid is missing")
    if len(digest) != 64 or any(character not in "0123456789abcdef" for character in digest):
        errors.append(f"{prefix} manifest sha256 is missing or invalid")
    expected_size = int(artifact.get("sizeBytes") or 0)
    if expected_size <= 0:
        errors.append(f"{prefix} manifest sizeBytes is missing or invalid")

    artifact_path = files_dir / file_name
    if not artifact_path.is_file():
        errors.append(f"{prefix} installer bytes are missing: {artifact_path}")
    else:
        if digest and sha256_file(artifact_path) != digest:
            errors.append(f"{prefix} manifest sha256 does not match installer bytes")
        if expected_size and artifact_path.stat().st_size != expected_size:
            errors.append(f"{prefix} manifest sizeBytes does not match installer bytes")

    matching_download = find_matching_download(download_rows(downloads), artifact)
    if matching_download is None:
        errors.append(f"{prefix} downloads manifest does not contain exactly one matching row")
    else:
        if artifact_digest(matching_download) != digest:
            errors.append(f"{prefix} downloads manifest sha256 does not match canonical manifest")
        if int(matching_download.get("sizeBytes") or 0) != expected_size:
            errors.append(f"{prefix} downloads manifest sizeBytes does not match canonical manifest")
        download_version = str(
            matching_download.get("releaseVersion")
            or matching_download.get("version")
            or manifest_version(downloads)
            or ""
        ).strip()
        if download_version and download_version != version:
            errors.append(f"{prefix} downloads manifest version does not match canonical manifest")

    required_route = f"/downloads/install/{artifact_identifier}"
    if required_route not in proof_routes(manifest):
        errors.append(f"{prefix} release proof is missing {required_route}")

    signing_path = signing_dir / f"signing-{head}-{rid}.receipt.json"
    signing = load_object(signing_path, f"{prefix} signing receipt", errors)
    signing_status = normalize(signing.get("signingStatus"))
    if signing:
        check_equal(errors, signing.get("platform"), "windows", f"{prefix} signing platform mismatch")
        check_equal(errors, signing.get("app"), head, f"{prefix} signing head mismatch")
        check_equal(errors, signing.get("rid"), rid, f"{prefix} signing rid mismatch")
        check_equal(
            errors,
            signing.get("releaseVersion"),
            version,
            f"{prefix} signing version mismatch",
        )
        check_equal(
            errors,
            signing.get("releaseChannel"),
            channel,
            f"{prefix} signing channel mismatch",
        )
        signing_artifact = find_signing_artifact(signing, file_name)
        if signing_artifact is None:
            errors.append(f"{prefix} signing receipt has no unique artifact row")
        else:
            if artifact_digest(signing_artifact) != digest:
                errors.append(f"{prefix} signing receipt digest mismatch")
            if normalize(signing_artifact.get("signingStatus")) != signing_status:
                errors.append(f"{prefix} signing artifact status disagrees with receipt")

    if require_authenticode:
        if signing_status != "pass":
            errors.append(f"{prefix} Authenticode signing receipt is not passing")
    elif signing_status == "pass":
        pass
    elif signing_status == "skipped_preview" and channel == "preview":
        caveats.append(f"{artifact_identifier}: unsigned preview artifact")
    else:
        errors.append(f"{prefix} signing status is not allowed for channel {channel or '<missing>'}")

    startup_path = startup_dir / f"startup-smoke-{head}-{rid}.receipt.json"
    startup = load_object(startup_path, f"{prefix} startup-smoke receipt", errors)
    execution_environment = normalize(startup.get("executionEnvironment"))
    if startup:
        if normalize(startup.get("status")) not in PASSING_STATUSES:
            errors.append(f"{prefix} startup-smoke status is not passing")
        check_equal(
            errors,
            startup.get("readyCheckpoint"),
            "pre_ui_event_loop",
            f"{prefix} startup-smoke checkpoint mismatch",
        )
        check_equal(errors, startup.get("headId"), head, f"{prefix} startup-smoke head mismatch")
        check_equal(errors, startup.get("rid"), rid, f"{prefix} startup-smoke rid mismatch")
        check_equal(errors, startup.get("platform"), "windows", f"{prefix} startup-smoke platform mismatch")
        check_equal(
            errors,
            startup.get("releaseVersion") or startup.get("version"),
            version,
            f"{prefix} startup-smoke version mismatch",
        )
        check_equal(
            errors,
            startup.get("channelId") or startup.get("channel"),
            channel,
            f"{prefix} startup-smoke channel mismatch",
        )
        check_equal(
            errors,
            startup.get("artifactDigest"),
            f"sha256:{digest}",
            f"{prefix} startup-smoke artifact digest mismatch",
        )
        if allow_proof_only_visual_handoff:
            check_equal(
                errors,
                startup.get("artifactFileName"),
                file_name,
                f"{prefix} startup-smoke artifact file mismatch",
            )

    if require_native_windows:
        if execution_environment != "native_windows":
            errors.append(f"{prefix} startup-smoke did not prove executionEnvironment=native_windows")
    elif execution_environment != "native_windows":
        caveats.append(f"{artifact_identifier}: native Windows execution proof is outstanding")

    gate_status = normalize(exit_gate.get("status"))
    check_equal(
        errors,
        exit_gate.get("contract_name") or exit_gate.get("contractName"),
        WINDOWS_EXIT_GATE_CONTRACT,
        f"{prefix} Windows desktop exit gate contract mismatch",
    )
    check_equal(
        errors,
        exit_gate.get("channelId"),
        channel,
        f"{prefix} Windows desktop exit gate channel mismatch",
    )
    check_equal(
        errors,
        exit_gate.get("releaseVersion"),
        version,
        f"{prefix} Windows desktop exit gate version mismatch",
    )
    gate_head = exit_gate.get("head") if isinstance(exit_gate.get("head"), dict) else {}
    check_equal(errors, gate_head.get("app_key"), head, f"{prefix} Windows desktop exit gate head mismatch")
    check_equal(errors, gate_head.get("rid"), rid, f"{prefix} Windows desktop exit gate rid mismatch")
    checks = exit_gate.get("checks") if isinstance(exit_gate.get("checks"), dict) else {}
    for field, label in (
        ("installer_sha256", "installer"),
        ("startup_smoke_artifact_digest", "startup-smoke"),
    ):
        value = normalize(checks.get(field)).removeprefix("sha256:")
        if value != digest:
            errors.append(f"{prefix} Windows exit-gate {label} digest mismatch")

    if allow_proof_only_visual_handoff:
        check_visual_proof_handoff(
            artifact=artifact,
            manifest=manifest,
            startup_path=startup_path,
            startup=startup,
            exit_gate=exit_gate,
            handoff=visual_handoff,
            errors=errors,
            caveats=caveats,
        )
    else:
        if gate_status not in PASSING_STATUSES:
            errors.append(f"{prefix} Windows desktop exit gate is not passing")
        if normalize(exit_gate.get("blockingMode")) != "none" or normalize(
            exit_gate.get("blocking_mode")
        ) != "none":
            errors.append(f"{prefix} Windows desktop exit gate remains blocked")
        if exit_gate.get("reasons") != []:
            errors.append(f"{prefix} Windows desktop exit gate still reports reasons")
        visual_digest = normalize(
            checks.get("windows_installer_visual_effective_artifact_digest")
            or checks.get("windows_installer_visual_proof_artifact_digest")
        ).removeprefix("sha256:")
        if visual_digest != digest:
            errors.append(f"{prefix} Windows exit-gate visual proof digest mismatch")
        if checks.get("windows_installer_visual_proof_skipped") is True:
            errors.append(f"{prefix} Windows visual proof was skipped")

    return {
        "artifactId": artifact_identifier,
        "fileName": file_name,
        "head": head,
        "rid": rid,
        "sha256": digest,
        "signingStatus": signing_status,
        "executionEnvironment": execution_environment or "unclassified",
        "proofOnlyVisualHandoff": allow_proof_only_visual_handoff,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Bind all Windows release evidence to the same manifest version and installer bytes."
    )
    parser.add_argument("--release-channel", required=True, type=Path)
    parser.add_argument("--downloads-manifest", required=True, type=Path)
    parser.add_argument("--files-dir", required=True, type=Path)
    parser.add_argument("--signing-dir", required=True, type=Path)
    parser.add_argument("--startup-smoke-dir", required=True, type=Path)
    parser.add_argument(
        "--windows-exit-gate",
        required=True,
        type=Path,
        action="append",
        help="Head-specific Windows exit gate; repeat once for every Windows install artifact.",
    )
    parser.add_argument("--windows-visual-proof-handoff", type=Path)
    parser.add_argument(
        "--allow-proof-only-visual-handoff",
        action="store_true",
        help=(
            "Explicitly allow a preview/review_required/blocked proof-only result when the "
            "staged Windows exit gate is blocked solely on native visual capture."
        ),
    )
    parser.add_argument("--require-authenticode", action="store_true")
    parser.add_argument("--require-native-windows", action="store_true")
    parser.add_argument("--output", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    errors: list[str] = []
    caveats: list[str] = []
    manifest = load_object(args.release_channel, "release-channel manifest", errors)
    downloads = load_object(args.downloads_manifest, "downloads manifest", errors)
    exit_gates_by_tuple: dict[tuple[str, str], dict[str, Any]] = {}
    for exit_gate_path in args.windows_exit_gate:
        exit_gate = load_object(exit_gate_path, "Windows desktop exit gate", errors)
        gate_head = exit_gate.get("head") if isinstance(exit_gate.get("head"), dict) else {}
        gate_key = (normalize(gate_head.get("app_key")), normalize(gate_head.get("rid")))
        if not all(gate_key):
            errors.append(f"Windows desktop exit gate has no head/rid identity: {exit_gate_path}")
            continue
        if gate_key in exit_gates_by_tuple:
            errors.append(f"Windows desktop exit gate tuple is duplicated: {':'.join(gate_key)}")
            continue
        exit_gates_by_tuple[gate_key] = exit_gate
    visual_handoff: dict[str, Any] = {}
    if args.allow_proof_only_visual_handoff:
        if args.windows_visual_proof_handoff is None:
            errors.append(
                "--allow-proof-only-visual-handoff requires --windows-visual-proof-handoff"
            )
        else:
            visual_handoff = load_object(
                args.windows_visual_proof_handoff,
                "Windows installer visual-proof handoff",
                errors,
            )
        check_proof_only_manifest_posture(manifest, errors)
    elif args.windows_visual_proof_handoff is not None:
        errors.append(
            "--windows-visual-proof-handoff requires explicit --allow-proof-only-visual-handoff"
        )
    version = manifest_version(manifest)
    channel = manifest_channel(manifest)
    if not version:
        errors.append("release-channel manifest version is missing")
    if not channel:
        errors.append("release-channel manifest channel is missing")

    windows_artifacts = [row for row in release_artifacts(manifest) if is_windows_install_artifact(row)]
    if not windows_artifacts:
        errors.append("release-channel manifest contains no Windows install artifact")
    if args.allow_proof_only_visual_handoff and len(windows_artifacts) != 1:
        errors.append(
            "proof-only Windows visual handoff requires exactly one Windows install artifact"
        )

    checked: list[dict[str, Any]] = []
    for artifact in windows_artifacts:
        artifact_key = (normalize(artifact.get("head")), normalize(artifact.get("rid")))
        exit_gate = exit_gates_by_tuple.get(artifact_key)
        if exit_gate is None:
            errors.append(f"{artifact_id(artifact)}: matching Windows desktop exit gate is missing")
            exit_gate = {}
        checked.append(
            check_artifact(
                artifact=artifact,
                manifest=manifest,
                downloads=downloads,
                signing_dir=args.signing_dir,
                startup_dir=args.startup_smoke_dir,
                exit_gate=exit_gate,
                visual_handoff=visual_handoff,
                files_dir=args.files_dir,
                require_authenticode=args.require_authenticode,
                require_native_windows=args.require_native_windows,
                allow_proof_only_visual_handoff=args.allow_proof_only_visual_handoff,
                errors=errors,
                caveats=caveats,
            )
        )
    expected_gate_keys = {
        (normalize(artifact.get("head")), normalize(artifact.get("rid")))
        for artifact in windows_artifacts
    }
    extra_gate_keys = sorted(set(exit_gates_by_tuple) - expected_gate_keys)
    if extra_gate_keys:
        errors.append(
            "Windows desktop exit-gate set contains non-release tuples: "
            + ", ".join(":".join(key) for key in extra_gate_keys)
        )

    launch_ready = not errors and not caveats
    payload = {
        "contractName": "chummer.windows_release_evidence.v1",
        "status": "fail" if errors else ("pass" if launch_ready else "proof_only"),
        "verdict": (
            "WINDOWS_RELEASE_EVIDENCE_INVALID"
            if errors
            else ("WINDOWS_FLAGSHIP_READY" if launch_ready else "WINDOWS_PROOF_PREVIEW_READY")
        ),
        "version": version,
        "channel": channel,
        "launchReady": launch_ready,
        "supportabilityFloor": "review_required" if errors or caveats else "preview_supported",
        "requireAuthenticode": args.require_authenticode,
        "requireNativeWindows": args.require_native_windows,
        "allowProofOnlyVisualHandoff": args.allow_proof_only_visual_handoff,
        "proofOnlyVisualHandoffPath": (
            str(args.windows_visual_proof_handoff)
            if args.windows_visual_proof_handoff is not None
            else ""
        ),
        "checkedArtifacts": checked,
        "caveats": caveats,
        "errors": errors,
    }
    rendered = json.dumps(payload, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8")
    if errors:
        print(rendered, file=sys.stderr, end="")
        return 1
    print(rendered, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
