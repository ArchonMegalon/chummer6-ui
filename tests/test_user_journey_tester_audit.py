from __future__ import annotations

import ast
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
from datetime import UTC, datetime, timedelta
from pathlib import Path
from typing import Callable

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "ai" / "milestones" / "user-journey-tester-audit.sh"
LINUX_GATE_SCRIPT = REPO_ROOT / "scripts" / "materialize-linux-desktop-exit-gate.sh"
CAPTURE_SCRIPT = REPO_ROOT / "scripts" / "ai" / "milestones" / "capture-user-journey-tester-trace.sh"
PROMOTION_SCRIPT = REPO_ROOT / "scripts" / "ai" / "milestones" / "promote-user-journey-tester-proof.sh"
BUNDLE_SCRIPT = REPO_ROOT / "scripts" / "ai" / "milestones" / "user_journey_evidence_bundle.py"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
WORKFLOW_ASSERTIONS = {
    "master_index_search_focus_stability": {
        "focus_preserved_after_typing": True,
        "search_text_accumulates_keyboard_input": True,
    },
    "file_new_character_visible_workspace": {
        "new_character_action_opened_visible_workspace": True,
        "visible_workspace_nonblank": True,
        "starter_attributes_match_seeded_workspace": True,
        "section_preview_omits_review_copy": True,
    },
    "minimal_character_build_save_reload": {
        "character_created_saved_reloaded": True,
        "reload_preserved_character_identity": True,
    },
    "major_navigation_sanity": {
        "primary_navigation_clicks_change_visible_content": True,
        "no_unhandled_errors": True,
    },
    "validation_or_export_smoke": {
        "validation_or_export_action_completed": True,
        "result_visible_or_file_created": True,
    },
}


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.write_text(json.dumps(payload), encoding="utf-8")


def run_audit(
    trace_timestamp: str | None,
    *,
    max_trace_age_hours: str = "24",
    refresh_trace_from_flagship_gate: str | None = None,
    omit_flagship_derived_assertions: bool = False,
    bind_release_candidate: bool = False,
    include_screenshot_dir_env: bool = True,
    fixture_mutator: Callable[[dict[str, Path]], None] | None = None,
    post_audit: Callable[[dict[str, Path]], None] | None = None,
) -> tuple[subprocess.CompletedProcess[str], dict[str, object], bytes, bytes]:
    state_root = REPO_ROOT / ".state"
    state_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="user-journey-audit-test-", dir=state_root) as temp_name:
        root = Path(temp_name)
        screenshots = root / "screenshots"
        screenshots.mkdir()
        workflows: list[dict[str, object]] = []
        for workflow_index, (workflow_id, assertions) in enumerate(WORKFLOW_ASSERTIONS.items()):
            names: list[str] = []
            for frame_index in range(2):
                name = f"{workflow_id}-{frame_index}.png"
                marker = bytes([workflow_index * 2 + frame_index + 1])
                screenshot_bytes = (
                    PNG_SIGNATURE
                    + b"\x00\x00\x00\rIHDR"
                    + (16 + workflow_index).to_bytes(4, "big")
                    + (16 + frame_index).to_bytes(4, "big")
                    + b"\x08\x06\x00\x00\x00"
                    + b"\x00\x00\x00\x00"
                    + marker * 2048
                )
                (screenshots / name).write_bytes(screenshot_bytes)
                names.append(name)
            workflows.append(
                {
                    "id": workflow_id,
                    "status": "pass",
                    "assertions": dict(assertions),
                    "screenshots": names,
                    "screenshot_sha256": {
                        name: "sha256:" + hashlib.sha256((screenshots / name).read_bytes()).hexdigest()
                        for name in names
                    },
                    "interaction_notes": [f"routed workflow {workflow_id}"],
                }
            )

        release_version = "run-test-stable"
        release_channel = "public_stable"
        try:
            parsed_trace_timestamp = datetime.fromisoformat(
                (trace_timestamp or "").replace("Z", "+00:00")
            )
            if parsed_trace_timestamp.tzinfo is None:
                raise ValueError("naive timestamp")
            completed_at = parsed_trace_timestamp.astimezone(UTC)
        except ValueError:
            completed_at = datetime.now(UTC).replace(microsecond=0)
        completed_at_text = timestamp(completed_at)
        candidate_published_at = timestamp(completed_at - timedelta(hours=1))
        gate_generated_at = timestamp(completed_at + timedelta(minutes=1))

        trace_path = root / "trace.json"
        linux_gate_path = root / "linux-gate.json"
        flagship_gate_path = root / "flagship-gate.json"
        receipt_path = root / "receipt.json"
        release_candidate_path = root / "release-candidate.json"
        candidate_files_root = root / "files"
        candidate_files_root.mkdir()
        run_root = root / "run"
        dist_dir = run_root / "dist"
        source_receipt_dir = run_root / "startup-smoke"
        dist_dir.mkdir(parents=True)
        source_receipt_dir.mkdir(parents=True)
        mouse_screenshot_dir = source_receipt_dir / "mouse-first-screenshots"
        mouse_screenshot_dir.mkdir()
        artifact_file_name = "chummer-avalonia-linux-x64-installer.deb"
        artifact_bytes = (b"chummer6-test-installer\n" * 128) + b"candidate"
        artifact_sha256 = hashlib.sha256(artifact_bytes).hexdigest()
        artifact_digest = f"sha256:{artifact_sha256}"
        candidate_artifact_path = candidate_files_root / artifact_file_name
        tested_installer_path = dist_dir / artifact_file_name
        candidate_artifact_path.write_bytes(artifact_bytes)
        tested_installer_path.write_bytes(artifact_bytes)

        mouse_screenshots: list[Path] = []
        for frame_index in range(5):
            frame_path = mouse_screenshot_dir / f"frame-{frame_index}.png"
            frame_path.write_bytes(
                PNG_SIGNATURE
                + b"\x00\x00\x00\rIHDR"
                + (32 + frame_index).to_bytes(4, "big")
                + (24 + frame_index).to_bytes(4, "big")
                + b"\x08\x06\x00\x00\x00"
                + b"\x00\x00\x00\x00"
                + bytes([64 + frame_index]) * 2048
            )
            mouse_screenshots.append(frame_path)
        mouse_trace_path = source_receipt_dir / "mouse-first.trace.json"
        write_json(
            mouse_trace_path,
            {
                "contract_name": "chummer6-ui.mouse_first_journey_trace",
                "status": "pass",
                "observedInputEvents": list(range(8)),
            },
        )

        source_mouse_receipt_path = source_receipt_dir / "mouse-first.receipt.json"
        source_mouse_receipt: dict[str, object] = {
            "status": "pass",
            "journeyMode": "mouse_first_live_binary",
            "headId": "avalonia",
            "version": release_version,
            "releaseVersion": release_version,
            "channelId": release_channel,
            "platform": "linux",
            "arch": "x64",
            "rid": "linux-x64",
            "artifactDigest": artifact_digest,
            "artifactDigestSource": "environment",
            "completedAtUtc": completed_at_text,
            "hasSavedWorkspace": True,
            "pointerActionCount": 7,
            "textEntryActionCount": 2,
            "directTextMutationCount": 0,
            "usedForcedComboDropdownOpen": False,
            "usedComboSelectionFallback": False,
            "observedInputEvents": list(range(8)),
            "screenshotPaths": [str(path) for path in mouse_screenshots],
            "tracePath": str(mouse_trace_path),
        }
        write_json(source_mouse_receipt_path, source_mouse_receipt)
        source_mouse_receipt_sha256 = hashlib.sha256(source_mouse_receipt_path.read_bytes()).hexdigest()

        trace: dict[str, object] = {
            "contract_name": "chummer6-ui.user_journey_tester_trace",
            "status": "pass",
            "tester_shard_id": "tester-shard",
            "fix_shard_id": "fixer-shard",
            "used_internal_apis": False,
            "linux_binary_under_test": True,
            "open_blocking_findings": [],
            "release_version": release_version,
            "release_channel": release_channel,
            "artifact_digest": artifact_digest,
            "artifact_digest_source": "environment",
            "source_mouse_receipt_name": source_mouse_receipt_path.name,
            "source_mouse_receipt_path": str(source_mouse_receipt_path),
            "source_mouse_receipt_sha256": f"sha256:{source_mouse_receipt_sha256}",
            "workflows": workflows,
        }
        if trace_timestamp is not None:
            trace["generated_at_utc"] = trace_timestamp

        if omit_flagship_derived_assertions:
            file_new_workflow = next(
                row for row in workflows if row["id"] == "file_new_character_visible_workspace"
            )
            assertions = file_new_workflow["assertions"]
            assert isinstance(assertions, dict)
            assertions.pop("starter_attributes_match_seeded_workspace")
            assertions.pop("section_preview_omits_review_copy")

        write_json(trace_path, trace)
        trace_bytes_before = trace_path.read_bytes()

        candidate_artifact: dict[str, object] = {
            "artifactId": "avalonia-linux-x64-installer",
            "head": "avalonia",
            "platform": "linux",
            "rid": "linux-x64",
            "arch": "x64",
            "kind": "installer",
            "fileName": artifact_file_name,
            "sha256": artifact_sha256,
            "sizeBytes": len(artifact_bytes),
            "version": release_version,
            "releaseVersion": release_version,
            "channel": release_channel,
            "channelId": release_channel,
        }
        write_json(
            release_candidate_path,
            {
                "contract_name": "Chummer.Hub.Registry.Contracts",
                "contractName": "Chummer.Hub.Registry.Contracts",
                "schemaVersion": 1,
                "status": "published",
                "version": release_version,
                "releaseVersion": release_version,
                "channel": release_channel,
                "channelId": release_channel,
                "publishedAt": candidate_published_at,
                "generated_at": candidate_published_at,
                "generatedAt": candidate_published_at,
                "rolloutState": "public_stable",
                "supportabilityState": "gold_supported",
                "artifacts": [candidate_artifact],
                "artifactPublicationBindings": [
                    {
                        "artifactId": "avalonia-linux-x64-installer",
                        "head": "avalonia",
                        "platform": "linux",
                        "rid": "linux-x64",
                        "arch": "x64",
                        "kind": "installer",
                        "tupleId": "avalonia:linux:linux-x64",
                        "releaseVersion": release_version,
                        "channelId": release_channel,
                        "publicationState": "published",
                    }
                ],
            },
        )
        write_json(
            linux_gate_path,
            {
                "contract_name": "chummer6-ui.linux_desktop_exit_gate",
                "status": "passed",
                "generated_at": gate_generated_at,
                "releaseVersion": release_version,
                "channelId": release_channel,
                "head": {
                    "app_key": "avalonia",
                    "platform": "linux",
                    "rid": "linux-x64",
                    "version": release_version,
                    "channel": release_channel,
                },
                "build": {
                    "dist_dir": str(dist_dir),
                    "installer_path": str(tested_installer_path),
                    "installer_sha256": artifact_sha256,
                    "installer_bytes": len(artifact_bytes),
                },
                "checks": {
                    "release_channel_linux_artifact": candidate_artifact,
                },
                "release_channel": {
                    "path": str(release_candidate_path),
                    "local_desktop_files_root": str(candidate_files_root),
                    "use_promoted_installer": True,
                    "installer_smoke_artifact_path": str(tested_installer_path),
                    "promoted_installer_path": str(candidate_artifact_path),
                    "mouse_first_journey_receipt_path": str(source_mouse_receipt_path),
                },
                "mouse_first_journey": {
                    "primary": {
                        "status": "passed",
                        "receipt_path": str(source_mouse_receipt_path),
                        "receipt": source_mouse_receipt,
                    }
                },
            },
        )
        write_json(
            flagship_gate_path,
            {
                "status": "pass",
                "interactionProof": {
                    "runtimeBackedNewCharacterFileWorkflow": "pass",
                },
                "headProofs": {
                    "avalonia": {
                        "requiredRuntimeBackedTests": [
                            "Runtime_backed_new_character_starter_attributes_match_seeded_workspace_and_omit_review_copy"
                        ],
                    },
                },
            },
        )
        if fixture_mutator is not None:
            fixture_mutator(
                {
                    "root": root,
                    "trace": trace_path,
                    "linux_gate": linux_gate_path,
                    "release_candidate": release_candidate_path,
                    "candidate_artifact": candidate_artifact_path,
                    "tested_installer": tested_installer_path,
                    "source_receipt": source_mouse_receipt_path,
                    "screenshots": screenshots,
                    "mouse_screenshots": mouse_screenshot_dir,
                    "mouse_trace": mouse_trace_path,
                    "flagship_gate": flagship_gate_path,
                    "audit": receipt_path,
                }
            )
        env = {
            **os.environ,
            "CHUMMER_USER_JOURNEY_TESTER_AUDIT_PATH": str(receipt_path),
            "CHUMMER_USER_JOURNEY_TESTER_TRACE_PATH": str(trace_path),
            "CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH": str(linux_gate_path),
            "CHUMMER_USER_JOURNEY_TESTER_FLAGSHIP_GATE_PATH": str(flagship_gate_path),
            "CHUMMER_USER_JOURNEY_TESTER_MAX_TRACE_AGE_HOURS": max_trace_age_hours,
        }
        if include_screenshot_dir_env:
            env["CHUMMER_USER_JOURNEY_TESTER_SCREENSHOT_DIR"] = str(screenshots)
        else:
            env.pop("CHUMMER_USER_JOURNEY_TESTER_SCREENSHOT_DIR", None)
        env.pop("CHUMMER_USER_JOURNEY_TESTER_REFRESH_TRACE_FROM_FLAGSHIP_GATE", None)
        if refresh_trace_from_flagship_gate is not None:
            env["CHUMMER_USER_JOURNEY_TESTER_REFRESH_TRACE_FROM_FLAGSHIP_GATE"] = (
                refresh_trace_from_flagship_gate
            )
        if bind_release_candidate:
            env["CHUMMER_USER_JOURNEY_TESTER_RELEASE_CANDIDATE_PATH"] = str(
                release_candidate_path
            )
        result = subprocess.run(
            ["bash", str(SCRIPT)],
            cwd=REPO_ROOT,
            env=env,
            text=True,
            capture_output=True,
            check=False,
        )
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        trace_bytes_after = trace_path.read_bytes()
        if post_audit is not None:
            post_audit(
                {
                    "root": root,
                    "trace": trace_path,
                    "linux_gate": linux_gate_path,
                    "flagship_gate": flagship_gate_path,
                    "audit": receipt_path,
                    "release_candidate": release_candidate_path,
                    "candidate_artifact": candidate_artifact_path,
                    "tested_installer": tested_installer_path,
                    "source_receipt": source_mouse_receipt_path,
                    "screenshots": screenshots,
                    "mouse_screenshots": mouse_screenshot_dir,
                    "mouse_trace": mouse_trace_path,
                }
            )
        return result, receipt, trace_bytes_before, trace_bytes_after


def timestamp(value: datetime) -> str:
    return value.replace(microsecond=0).isoformat().replace("+00:00", "Z")


def promotion_stable_bytes() -> Callable[[Path, str, int], bytes]:
    script = PROMOTION_SCRIPT.read_text(encoding="utf-8")
    python_source = script.split("<<'PY'\n", 1)[1].split("\nPY\n)", 1)[0]
    parsed = ast.parse(python_source, filename=str(PROMOTION_SCRIPT))
    helper_nodes = [
        node
        for node in parsed.body
        if isinstance(node, (ast.Import, ast.ImportFrom, ast.FunctionDef))
    ]
    namespace: dict[str, object] = {}
    exec(
        compile(
            ast.fix_missing_locations(ast.Module(body=helper_nodes, type_ignores=[])),
            str(PROMOTION_SCRIPT),
            "exec",
        ),
        namespace,
    )
    stable_reader = namespace["stable_bytes"]
    assert callable(stable_reader)
    return stable_reader  # type: ignore[return-value]


def promote_passing_fixture(published_root: Path) -> subprocess.CompletedProcess[str]:
    promotion_results: list[subprocess.CompletedProcess[str]] = []

    def promote(paths: dict[str, Path]) -> None:
        capture_root = paths["root"] / "passing-capture"
        capture_root.mkdir()
        for source_key, destination_name in (
            ("trace", "USER_JOURNEY_TESTER_TRACE.generated.json"),
            ("linux_gate", "UI_LINUX_DESKTOP_EXIT_GATE.generated.json"),
            ("flagship_gate", "UI_FLAGSHIP_RELEASE_GATE.generated.json"),
            ("audit", "USER_JOURNEY_TESTER_AUDIT.generated.json"),
        ):
            shutil.copyfile(paths[source_key], capture_root / destination_name)
        env = {
            key: value
            for key, value in os.environ.items()
            if not key.startswith("CHUMMER_USER_JOURNEY_TESTER_")
        }
        env["CHUMMER_USER_JOURNEY_TESTER_PUBLISHED_ROOT"] = str(published_root)
        promotion_results.append(
            subprocess.run(
                [
                    "bash",
                    str(PROMOTION_SCRIPT),
                    str(capture_root),
                    str(paths["release_candidate"]),
                ],
                cwd=REPO_ROOT,
                env=env,
                text=True,
                capture_output=True,
                check=False,
            )
        )

    staged_result, staged_receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        bind_release_candidate=True,
        post_audit=promote,
    )
    assert staged_result.returncode == 0, staged_result.stderr
    assert staged_receipt["status"] == "pass"
    assert len(promotion_results) == 1
    return promotion_results[0]


def audit_promoted_bundle(
    published_root: Path,
    receipt_path: Path,
) -> tuple[subprocess.CompletedProcess[str], dict[str, object]]:
    env = {
        key: value
        for key, value in os.environ.items()
        if not key.startswith("CHUMMER_USER_JOURNEY_TESTER_")
    }
    env.update(
        {
            "CHUMMER_USER_JOURNEY_TESTER_AUDIT_PATH": str(receipt_path),
            "CHUMMER_USER_JOURNEY_TESTER_BUNDLE_POINTER_PATH": str(
                published_root / "USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json"
            ),
            "CHUMMER_USER_JOURNEY_TESTER_MAX_TRACE_AGE_HOURS": "24",
        }
    )
    result = subprocess.run(
        ["bash", str(SCRIPT)],
        cwd=REPO_ROOT,
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )
    return result, json.loads(receipt_path.read_text(encoding="utf-8"))


def test_current_trace_passes_and_is_digest_bound() -> None:
    result, receipt, trace_before, trace_after = run_audit(timestamp(datetime.now(UTC)))

    assert result.returncode == 0, result.stderr
    assert receipt["status"] == "pass"
    assert len(receipt["evidence"]["trace_sha256"]) == 64
    assert receipt["evidence"]["trace_max_age_hours"] == 24
    assert receipt["trace_mutation_requested"] is False
    assert receipt["trace_mutation_performed"] is False
    assert receipt["evidence"]["trace_mutation_request_value"] == "0"
    assert receipt["evidence"]["trace_bytes_unchanged_during_audit"] is True
    assert trace_after == trace_before


def test_linux_gate_routes_opt_in_trace_only_to_installer_journey() -> None:
    script = LINUX_GATE_SCRIPT.read_text(encoding="utf-8")

    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_USER_JOURNEY_TRACE_OUTPUT" in script
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_USER_JOURNEY_TESTER_SHARD_ID" in script
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_USER_JOURNEY_FIX_SHARD_ID" in script
    assert "Linux user-journey tester and fixer shard IDs must be distinct." in script
    assert 'CHUMMER_DESKTOP_USER_JOURNEY_TRACE_OUTPUT=""' in script
    assert script.count(
        'CHUMMER_DESKTOP_USER_JOURNEY_TRACE_OUTPUT="$USER_JOURNEY_TRACE_OUTPUT"'
    ) == 2


def test_capture_wrapper_is_staged_promoted_candidate_workflow() -> None:
    script = CAPTURE_SCRIPT.read_text(encoding="utf-8")
    result = subprocess.run(
        ["bash", str(CAPTURE_SCRIPT)],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 2
    assert "Usage:" in result.stderr
    assert 'CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER:-1}"' in script
    assert 'CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY:-1}"' in script
    assert 'CHUMMER_USER_JOURNEY_TESTER_EVIDENCE_ROOT="$capture_root"' in script
    assert "Build and promote a candidate containing the live producer" in script
    assert "CHUMMER_USER_JOURNEY_TESTER_RELEASE_CANDIDATE_PATH" in script


def test_promotion_wrapper_requires_a_passing_byte_bound_staged_audit() -> None:
    script = PROMOTION_SCRIPT.read_text(encoding="utf-8")
    result = subprocess.run(
        ["bash", str(PROMOTION_SCRIPT)],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 2
    assert "Usage:" in result.stderr
    assert 'evidence.get("release_candidate_binding_status") != "pass"' in script
    assert "The staged trace bytes no longer match the passing owning audit." in script
    assert "CHUMMER_USER_JOURNEY_TESTER_BUNDLE_POINTER_PATH" in script
    assert "user_journey_evidence_bundle.py" in script
    assert "FILE_ATTRIBUTE_REPARSE_POINT" in script


def test_promotion_stable_reader_uses_regular_descriptor_anchored_ancestors(
    tmp_path: Path,
) -> None:
    evidence_dir = tmp_path / "capture" / "proof"
    evidence_dir.mkdir(parents=True)
    evidence_path = evidence_dir / "trace.json"
    evidence_path.write_bytes(b'{"status":"pass"}')

    stable_reader = promotion_stable_bytes()

    assert stable_reader(evidence_path, "staged trace", 1024) == evidence_path.read_bytes()


def test_promotion_stable_reader_rejects_symlinked_ancestor(tmp_path: Path) -> None:
    evidence_dir = tmp_path / "real-capture"
    evidence_dir.mkdir()
    evidence_path = evidence_dir / "trace.json"
    evidence_path.write_bytes(b'{"status":"pass"}')
    linked_capture = tmp_path / "linked-capture"
    try:
        linked_capture.symlink_to(evidence_dir, target_is_directory=True)
    except OSError as exc:
        pytest.skip(f"symlinks unavailable: {exc}")

    stable_reader = promotion_stable_bytes()

    with pytest.raises(SystemExit, match="symbolic-link or reparse-point"):
        stable_reader(linked_capture / evidence_path.name, "staged trace", 1024)


def test_promoted_bundle_survives_capture_and_candidate_source_deletion(tmp_path: Path) -> None:
    published_root = tmp_path / "published"
    promotion = promote_passing_fixture(published_root)

    assert promotion.returncode == 0, promotion.stderr
    canonical_audit = json.loads(
        (published_root / "USER_JOURNEY_TESTER_AUDIT.generated.json").read_text(
            encoding="utf-8"
        )
    )
    assert canonical_audit["status"] == "pass"
    assert canonical_audit["evidence"]["bundle_verification_status"] == "pass"

    result, receipt = audit_promoted_bundle(
        published_root,
        tmp_path / "independent-rerun.json",
    )

    assert result.returncode == 0, result.stderr
    assert receipt["status"] == "pass"
    evidence = receipt["evidence"]
    assert evidence["bundle_verification_status"] == "pass"
    assert evidence["bundle_entry_count"] >= 24
    bundle_root = Path(evidence["bundle_manifest_path"]).parent
    for path_key in (
        "trace_path",
        "linux_gate_path",
        "flagship_gate_path",
        "release_candidate_path",
        "release_candidate_file_path",
        "tested_installer_resolved_path",
        "source_mouse_receipt_resolved_path",
    ):
        Path(evidence[path_key]).relative_to(bundle_root)


@pytest.mark.parametrize(
    "tampered_role",
    [
        "trace",
        "linux_gate",
        "flagship_gate",
        "staged_audit",
        "source_receipt",
        "release_candidate",
        "candidate_artifact",
        "tested_installer",
        "workflow_screenshot",
        "mouse_screenshot",
        "mouse_trace",
    ],
)
def test_promoted_bundle_tamper_fails_closed_for_every_evidence_role(
    tmp_path: Path,
    tampered_role: str,
) -> None:
    published_root = tmp_path / "published"
    promotion = promote_passing_fixture(published_root)
    assert promotion.returncode == 0, promotion.stderr
    pointer = json.loads(
        (published_root / "USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json").read_text(
            encoding="utf-8"
        )
    )
    manifest_path = published_root / pointer["manifest_path"]
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    entry = next(row for row in manifest["entries"] if row["role"] == tampered_role)
    evidence_path = manifest_path.parent / entry["path"]
    evidence_path.write_bytes(evidence_path.read_bytes() + b"tamper")

    result, receipt = audit_promoted_bundle(
        published_root,
        tmp_path / f"tamper-{tampered_role}.json",
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert receipt["evidence"]["bundle_verification_status"] == "fail"
    assert any(
        "bundle verification failed" in reason
        for reason in receipt["reasons"]
    )


@pytest.mark.parametrize("metadata_target", ["pointer", "manifest"])
def test_promoted_bundle_metadata_tamper_fails_closed(
    tmp_path: Path,
    metadata_target: str,
) -> None:
    published_root = tmp_path / "published"
    promotion = promote_passing_fixture(published_root)
    assert promotion.returncode == 0, promotion.stderr
    pointer_path = published_root / "USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json"
    pointer = json.loads(pointer_path.read_text(encoding="utf-8"))
    if metadata_target == "pointer":
        pointer["bundle_id"] = "0" * 64
        write_json(pointer_path, pointer)
    else:
        manifest_path = published_root / pointer["manifest_path"]
        manifest_path.write_bytes(manifest_path.read_bytes() + b" ")

    result, receipt = audit_promoted_bundle(
        published_root,
        tmp_path / f"tamper-{metadata_target}.json",
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert receipt["evidence"]["bundle_verification_status"] == "fail"
    assert any("bundle verification failed" in reason for reason in receipt["reasons"])


def test_current_trace_can_bind_exact_stable_release_candidate_bytes() -> None:
    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        bind_release_candidate=True,
    )

    assert result.returncode == 0, result.stderr
    evidence = receipt["evidence"]
    assert evidence["release_candidate_version"] == "run-test-stable"
    assert evidence["release_candidate_channel"] == "public_stable"
    assert evidence["release_candidate_status"] == "published"
    assert evidence["release_candidate_rollout_state"] == "public_stable"
    assert evidence["release_candidate_supportability_state"] == "gold_supported"
    assert evidence["release_candidate_binding_status"] == "pass"
    assert len(evidence["release_candidate_sha256"]) == 64


def test_locally_rebuilt_installer_cannot_masquerade_as_promoted_candidate() -> None:
    def mutate(paths: dict[str, Path]) -> None:
        paths["tested_installer"].write_bytes(b"locally rebuilt, not promoted")

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert receipt["evidence"]["release_candidate_binding_status"] == "fail"
    assert any(
        reason.startswith("candidate_artifact_digest_mismatch:")
        for reason in receipt["reasons"]
    )


def test_explicit_source_candidate_binds_exact_tested_bytes_without_claiming_promotion() -> None:
    def mutate(paths: dict[str, Path]) -> None:
        gate = json.loads(paths["linux_gate"].read_text(encoding="utf-8"))
        gate["release_channel"]["use_promoted_installer"] = False
        gate["release_channel"]["promoted_installer_path"] = ""
        write_json(paths["linux_gate"], gate)
        paths["candidate_artifact"].write_bytes(b"older promoted candidate")

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode == 0, result.stderr
    assert receipt["status"] == "pass"
    assert receipt["evidence"]["artifact_binding_mode"] == "source"
    assert receipt["evidence"]["release_candidate_binding_status"] == "source"
    assert "candidate_manifest" not in receipt["evidence"]["candidate_digest_bindings"]
    assert "candidate_file" not in receipt["evidence"]["candidate_digest_bindings"]


def test_source_mouse_receipt_tamper_breaks_trace_and_embedded_bindings() -> None:
    def mutate(paths: dict[str, Path]) -> None:
        source_receipt = json.loads(paths["source_receipt"].read_text(encoding="utf-8"))
        source_receipt["channelId"] = "tampered-channel"
        write_json(paths["source_receipt"], source_receipt)

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("source_mouse_receipt_sha256" in reason for reason in receipt["reasons"])
    assert any("embedded mouse-first receipt" in reason for reason in receipt["reasons"])


def test_screenshot_byte_tamper_breaks_declared_digest_binding() -> None:
    def mutate(paths: dict[str, Path]) -> None:
        screenshot = next(paths["screenshots"].glob("*.png"))
        screenshot.write_bytes(screenshot.read_bytes() + b"tamper")

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("screenshot SHA-256 binding does not match" in reason for reason in receipt["reasons"])


@pytest.mark.parametrize("asset_kind", ["screenshot", "trace"])
def test_mouse_first_named_evidence_is_opened_and_tamper_detected(asset_kind: str) -> None:
    def mutate(paths: dict[str, Path]) -> None:
        if asset_kind == "screenshot":
            asset = next(paths["mouse_screenshots"].glob("*.png"))
            asset.write_bytes(b"not-a-png")
        else:
            asset = paths["mouse_trace"]
            asset.write_bytes(asset.read_bytes() + b"tamper")

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert receipt["evidence"]["mouse_first_evidence_binding_status"] == "fail"


@pytest.mark.parametrize("unsafe_template", ["./{}", "nested/../{}"])
def test_explicit_dot_segments_in_screenshot_paths_fail_closed(
    unsafe_template: str,
) -> None:
    def mutate(paths: dict[str, Path]) -> None:
        trace = json.loads(paths["trace"].read_text(encoding="utf-8"))
        workflow = trace["workflows"][0]
        original = workflow["screenshots"][0]
        unsafe = unsafe_template.format(original)
        if unsafe.startswith("nested/"):
            (paths["screenshots"] / "nested").mkdir()
        workflow["screenshots"][0] = unsafe
        declared_hashes = workflow["screenshot_sha256"]
        declared_hashes[unsafe] = declared_hashes.pop(original)
        write_json(paths["trace"], trace)

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("without dot or dotdot segments" in reason for reason in receipt["reasons"])


def test_missing_screenshot_root_serializes_as_empty_text_not_none() -> None:
    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        include_screenshot_dir_env=False,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert receipt["evidence"]["screenshot_dir"] == ""
    assert any("screenshot directory must be explicit" in reason for reason in receipt["reasons"])


def test_symlinked_trace_is_rejected_as_unsafe_evidence() -> None:
    def mutate(paths: dict[str, Path]) -> None:
        target = paths["root"] / "trace-target.json"
        target.write_bytes(paths["trace"].read_bytes())
        paths["trace"].unlink()
        paths["trace"].symlink_to(target)

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("stable regular non-symlink" in reason for reason in receipt["reasons"])


def test_oversized_trace_is_rejected_before_json_parsing() -> None:
    def mutate(paths: dict[str, Path]) -> None:
        paths["trace"].write_bytes(b"{" + b" " * (1024 * 1024) + b"}")

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("byte safety limit" in reason for reason in receipt["reasons"])


def test_missing_trace_timestamp_fails_closed() -> None:
    result, receipt, _, _ = run_audit(None)

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("offset-aware generated_at_utc" in reason for reason in receipt["reasons"])


@pytest.mark.parametrize(
    ("alias_mode", "expected_reason"),
    [
        ("generated_at", "offset-aware generated_at_utc"),
        ("generatedAt", "offset-aware generated_at_utc"),
        ("evidence", "offset-aware generated_at_utc"),
        ("conflict", "conflicts with canonical generated_at_utc"),
    ],
)
def test_trace_timestamp_aliases_cannot_replace_or_conflict_with_canonical_field(
    alias_mode: str,
    expected_reason: str,
) -> None:
    def mutate(paths: dict[str, Path]) -> None:
        trace = json.loads(paths["trace"].read_text(encoding="utf-8"))
        canonical = trace["generated_at_utc"]
        if alias_mode == "evidence":
            trace.pop("generated_at_utc")
            trace["evidence"] = {"generated_at_utc": canonical}
        elif alias_mode == "conflict":
            trace["generated_at"] = "2000-01-01T00:00:00Z"
        else:
            trace.pop("generated_at_utc")
            trace[alias_mode] = canonical
        write_json(paths["trace"], trace)

    result, receipt, _, _ = run_audit(
        timestamp(datetime.now(UTC)),
        fixture_mutator=mutate,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any(expected_reason in reason for reason in receipt["reasons"])


def test_stale_trace_timestamp_cannot_be_freshness_laundered() -> None:
    stale = datetime.now(UTC) - timedelta(hours=25)
    result, receipt, _, _ = run_audit(timestamp(stale))

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("trace is stale" in reason for reason in receipt["reasons"])


def test_invalid_trace_freshness_policy_fails_and_rewrites_receipt() -> None:
    result, receipt, _, _ = run_audit(timestamp(datetime.now(UTC)), max_trace_age_hours="invalid")

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert any("must be a positive integer" in reason for reason in receipt["reasons"])


def test_refresh_request_cannot_mutate_or_launder_missing_trace_assertions() -> None:
    result, receipt, trace_before, trace_after = run_audit(
        timestamp(datetime.now(UTC)),
        refresh_trace_from_flagship_gate="1",
        omit_flagship_derived_assertions=True,
    )

    assert result.returncode != 0
    assert receipt["status"] == "fail"
    assert receipt["trace_mutation_requested"] is True
    assert receipt["trace_mutation_performed"] is False
    assert receipt["evidence"]["trace_mutation_request_value"] == "1"
    assert receipt["evidence"]["trace_mutation_allowed"] is False
    assert receipt["evidence"]["trace_bytes_unchanged_during_audit"] is True
    assert receipt["evidence"]["missing_assertion_workflows"][
        "file_new_character_visible_workspace"
    ] == [
        "starter_attributes_match_seeded_workspace",
        "section_preview_omits_review_copy",
    ]
    assert any("trace mutation request is prohibited" in reason for reason in receipt["reasons"])
    assert any("missing required user-observable assertion" in reason for reason in receipt["reasons"])
    assert trace_after == trace_before
