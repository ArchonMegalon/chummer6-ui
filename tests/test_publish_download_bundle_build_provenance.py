from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import signal
import shutil
import subprocess
import sys
import time
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHER = REPO_ROOT / "scripts" / "publish-download-bundle.sh"
MANIFEST_GENERATOR = REPO_ROOT / "scripts" / "generate-releases-manifest.sh"
RELEASE_CANDIDATE_FS_HELPER = REPO_ROOT / "scripts" / "release_candidate_fs.py"


def write_executable(path: Path, body: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body, encoding="utf-8")
    path.chmod(0o755)


def test_manifest_generator_has_fail_closed_stage_only_secondary_sync_boundary() -> None:
    source = MANIFEST_GENERATOR.read_text(encoding="utf-8")
    assert 'MANIFEST_STAGE_ONLY="${CHUMMER_RELEASE_MANIFEST_STAGE_ONLY:-0}"' in source
    assert 'if ! to_bool "$MANIFEST_STAGE_ONLY"; then\n  sync_portal_outputs' in source
    assert "stage-only manifest generation skipped portal, run-services, presentation, and registry publication sync" in source


def make_publisher_fixture(tmp_path: Path, *, real_validator: bool = False) -> tuple[Path, Path]:
    repo = tmp_path / "publisher-repo"
    scripts = repo / "scripts"
    scripts.mkdir(parents=True)
    shutil.copy2(PUBLISHER, scripts / PUBLISHER.name)
    shutil.copy2(RELEASE_CANDIDATE_FS_HELPER, scripts / RELEASE_CANDIDATE_FS_HELPER.name)

    write_executable(
        scripts / "verify-windows-installer-payloads.py",
        "#!/usr/bin/env python3\nraise SystemExit(0)\n",
    )
    write_executable(
        scripts / "verify-releases-manifest.sh",
        """#!/usr/bin/env bash
if [[ -n "${VERIFY_CALL_LOG:-}" ]]; then
  printf '%s\\n' "$1" >>"$VERIFY_CALL_LOG"
fi
if [[ "${FIXTURE_REJECT_STAGED_CANDIDATE:-0}" == "1" ]]; then
  echo "fixture staged candidate rejected" >&2
  exit 1
fi
exit 0
""",
    )
    write_executable(
        scripts / "materialize-windows-desktop-exit-gate.sh",
        "#!/usr/bin/env bash\nexit 0\n",
    )
    write_executable(
        scripts / "verify-windows-bootstrap-startup-smoke.py",
        "#!/usr/bin/env python3\nraise SystemExit(0)\n",
    )
    write_executable(
        scripts / "materialize-downloads-publication-scope.py",
        """#!/usr/bin/env python3
import sys
from pathlib import Path

args = sys.argv[1:]
if "--output" in args:
    output = Path(args[args.index("--output") + 1])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(b"new-publication-scope")
""",
    )
    write_executable(
        scripts / "materialize_release_candidate_handoff.py",
        """#!/usr/bin/env python3
import sys
from pathlib import Path

root = Path(sys.argv[1])
(root / "RELEASE_BUILD_HANDOFF.generated.json").write_bytes(b"new-release-handoff-json")
(root / "RELEASE_BUILD_HANDOFF.generated.md").write_bytes(b"new-release-handoff-markdown")
(root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json").write_bytes(b"new-visual-handoff-json")
(root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md").write_bytes(b"new-visual-handoff-markdown")
""",
    )
    fixture_registry_root = tmp_path / "chummer-hub-registry"
    (fixture_registry_root / ".codex-studio").mkdir(parents=True, exist_ok=True)
    write_executable(
        scripts / "resolve-hub-registry-root.sh",
        f"#!/usr/bin/env bash\nprintf '%s\\n' {fixture_registry_root!s}\n",
    )
    write_executable(
        scripts / "generate-releases-manifest.sh",
        """#!/usr/bin/env bash
set -euo pipefail
: "${MANIFEST_PATH:?}"
: "${PORTAL_MANIFEST_PATH:?}"
: "${SOURCE_MANIFEST_PATH:?}"
if [[ -n "${GENERATOR_CALL_LOG:-}" ]]; then
  printf 'called\\n' >"$GENERATOR_CALL_LOG"
fi
mkdir -p "$(dirname "$MANIFEST_PATH")" "$(dirname "$PORTAL_MANIFEST_PATH")"
cp "$SOURCE_MANIFEST_PATH" "$MANIFEST_PATH"
canonical_source="${FIXTURE_GENERATOR_CANONICAL_SOURCE:-$SOURCE_MANIFEST_PATH}"
cp "$canonical_source" "$(dirname "$MANIFEST_PATH")/RELEASE_CHANNEL.generated.json"
mkdir -p "$(dirname "$MANIFEST_PATH")/files"
find "$DOWNLOADS_DIR" -maxdepth 1 -type f -exec cp {} "$(dirname "$MANIFEST_PATH")/files/" \\;
if [[ "${FIXTURE_MUTATE_CANDIDATE_ARTIFACT:-0}" == "1" ]]; then
  candidate_artifact="$(find "$(dirname "$MANIFEST_PATH")/files" -maxdepth 1 -type f | head -1)"
  printf 'drift' >>"$candidate_artifact"
fi
mkdir -p "$(dirname "$PROMOTION_EVIDENCE_PATH")" "$(dirname "$MANIFEST_PATH")/release-evidence/browser-lane"
printf 'new-public-promotion' >"$PROMOTION_EVIDENCE_PATH"
printf 'new-browser-lane' >"$(dirname "$MANIFEST_PATH")/release-evidence/browser-lane/proof.json"
printf 'new-quarantine-evidence' >"$QUARANTINE_PROMOTION_EVIDENCE_PATH"
if [[ "$(realpath -m "$PORTAL_MANIFEST_PATH")" != "$(realpath -m "$MANIFEST_PATH")" ]]; then
  cp "$SOURCE_MANIFEST_PATH" "$PORTAL_MANIFEST_PATH"
  cp "$SOURCE_MANIFEST_PATH" "$(dirname "$PORTAL_MANIFEST_PATH")/RELEASE_CHANNEL.generated.json"
fi
""",
    )

    validator = tmp_path / "chummer.run-services" / "scripts" / "release" / "verify_release_build_provenance_bundle.py"
    if real_validator:
        real_release_scripts = REPO_ROOT.parent / "chummer.run-services" / "scripts" / "release"
        validator.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(real_release_scripts / "verify_release_build_provenance_bundle.py", validator)
        shutil.copy2(real_release_scripts / "build_provenance_support.py", validator.parent / "build_provenance_support.py")
    else:
        write_executable(
            validator,
            """#!/usr/bin/env python3
import os
import sys
from pathlib import Path

root = Path(sys.argv[1])
call_log = os.environ.get("VALIDATOR_CALL_LOG", "")
if call_log:
    with Path(call_log).open("a", encoding="utf-8") as stream:
        stream.write(str(root) + "\\n")
receipt = root / "proof" / "build-provenance" / "v1" / "invocations" / "receipt.json"
expected = os.environ.get("VALIDATOR_EXPECTED_RECEIPT", "valid-receipt")
if not receipt.is_file() or receipt.is_symlink() or receipt.read_text(encoding="utf-8") != expected:
    print("fixture provenance mismatch", file=sys.stderr)
    raise SystemExit(1)
print("build_provenance_bundle=pass")
""",
        )
        (validator.parent / "build_provenance_support.py").write_text("# governed fixture support\n", encoding="utf-8")
    (tmp_path / "chummer.run-services" / "Chummer.Portal").mkdir(parents=True, exist_ok=True)
    return repo, validator


def write_bundle(bundle: Path, *, platform: str, proof_receipt: str | None = None) -> str:
    if platform == "macos":
        artifact_name = "chummer-avalonia-osx-arm64-installer.dmg"
        artifact_id = "avalonia-osx-arm64-installer"
        artifact_bytes = b"mac-installer-bytes"
    else:
        artifact_name = "chummer-avalonia-linux-x64-installer.deb"
        artifact_id = "avalonia-linux-x64-installer"
        artifact_bytes = b"linux-installer-bytes"

    files = bundle / "files"
    files.mkdir(parents=True)
    artifact = files / artifact_name
    artifact.write_bytes(artifact_bytes)
    digest = hashlib.sha256(artifact_bytes).hexdigest()
    row = {
        "artifactId": artifact_id,
        "head": "avalonia",
        "platform": platform,
        "rid": "osx-arm64" if platform == "macos" else "linux-x64",
        "kind": "installer",
        "fileName": artifact_name,
        "downloadUrl": f"/downloads/files/{artifact_name}",
        "sha256": digest,
        "sizeBytes": len(artifact_bytes),
    }
    payload = {
        "version": "fixture-v1",
        "channel": "preview",
        "publishedAt": "2026-07-13T12:00:00Z",
        "artifacts": [row],
        "downloads": [
            {
                "id": artifact_id,
                "head": "avalonia",
                "platform": f"Avalonia Desktop {platform} Installer",
                "platformId": "macos-arm64" if platform == "macos" else "linux-x64",
                "rid": None,
                "kind": "installer",
                "fileName": artifact_name,
                "url": f"/downloads/files/{artifact_name}",
                "sha256": digest,
                "sizeBytes": len(artifact_bytes),
            }
        ],
    }
    encoded = json.dumps(payload, indent=2) + "\n"
    (bundle / "releases.json").write_text(encoded, encoding="utf-8")
    (bundle / "RELEASE_CHANNEL.generated.json").write_text(encoded, encoding="utf-8")

    if proof_receipt is not None:
        proof = bundle / "proof" / "build-provenance" / "v1"
        (proof / "invocations").mkdir(parents=True)
        (proof / "sbom").mkdir(parents=True)
        (proof / "invocations" / "receipt.json").write_text(proof_receipt, encoding="utf-8")
        (proof / "sbom" / "desktop-avalonia.cdx.json").write_bytes(b"exact-sbom-bytes\n")
    return artifact_name


def write_real_provenance_bundle(tmp_path: Path, bundle: Path) -> str:
    upstream_test_path = REPO_ROOT.parent / "chummer.run-services" / "tests" / "test_mac_release_build_provenance.py"
    spec = importlib.util.spec_from_file_location("real_mac_provenance_fixture", upstream_test_path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    primary = tmp_path / "real-provenance-primary"
    project = module.make_primary_repo(primary)
    material_names = (
        "chummer-core-engine",
        "chummer.run-services",
        "chummer-ui-kit",
        "chummer-hub-registry",
        "chummer-media-factory",
        "chummer5a",
    )
    materials: dict[str, Path] = {}
    for name in material_names:
        material = tmp_path / "real-provenance-materials" / name
        module.init_repo(material)
        materials[name] = material

    artifact_name = "chummer-avalonia-osx-arm64-installer.dmg"
    artifact_id = "avalonia-osx-arm64-installer"
    artifact = bundle / "files" / artifact_name
    receipt = (
        bundle
        / "proof"
        / "build-provenance"
        / "v1"
        / "invocations"
        / "run-test.avalonia.osx-arm64.installer.json"
    )
    sbom = bundle / "proof" / "build-provenance" / "v1" / "sbom" / "desktop-avalonia.cdx.json"
    state = tmp_path / "real-provenance-state.json"
    begun = subprocess.run(
        module.common_begin_args(
            primary=primary,
            project=project,
            artifact=artifact,
            state=state,
            receipt=receipt,
            sbom=sbom,
            source_materials=materials,
        ),
        capture_output=True,
        text=True,
        check=False,
    )
    assert begun.returncode == 0, begun.stderr
    time.sleep(0.01)
    artifact.parent.mkdir(parents=True, exist_ok=True)
    artifact.write_bytes(b"real-governed-mac-installer-bytes")
    finalized = subprocess.run(
        module.finalize_args(state, receipt),
        capture_output=True,
        text=True,
        check=False,
    )
    assert finalized.returncode == 0, finalized.stderr

    digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
    row = {
        "artifactId": artifact_id,
        "head": "avalonia",
        "platform": "macos",
        "rid": "osx-arm64",
        "kind": "installer",
        "fileName": artifact_name,
        "downloadUrl": f"/downloads/files/{artifact_name}",
        "sha256": digest,
        "sizeBytes": artifact.stat().st_size,
    }
    payload = {
        "version": "fixture-v1",
        "channel": "preview",
        "publishedAt": "2026-07-13T12:00:00Z",
        "artifacts": [row],
        "downloads": [
            {
                "id": artifact_id,
                "platform": "macos",
                "url": f"/downloads/files/{artifact_name}",
                "sha256": digest,
                "sizeBytes": artifact.stat().st_size,
            }
        ],
    }
    encoded = json.dumps(payload, indent=2) + "\n"
    (bundle / "releases.json").write_text(encoded, encoding="utf-8")
    (bundle / "RELEASE_CHANNEL.generated.json").write_text(encoded, encoding="utf-8")
    return artifact_name


def seed_target(target: Path) -> None:
    (target / "files").mkdir(parents=True)
    (target / "files" / "existing.bin").write_bytes(b"existing-artifact")
    (target / "releases.json").write_bytes(b"existing-release-manifest")
    (target / "RELEASE_CHANNEL.generated.json").write_bytes(b"existing-canonical-manifest")
    (target / "proof" / "windows").mkdir(parents=True)
    (target / "proof" / "windows" / "receipt.json").write_bytes(b"windows-proof")
    (target / "proof" / "other-namespace").mkdir(parents=True)
    (target / "proof" / "other-namespace" / "receipt.bin").write_bytes(b"other-proof")
    (target / "proof" / "build-provenance" / "v1").mkdir(parents=True)
    (target / "proof" / "build-provenance" / "v1" / "stale.json").write_bytes(b"stale-proof")
    (target / "proof" / "build-provenance" / "v2").mkdir(parents=True)
    (target / "proof" / "build-provenance" / "v2" / "future.json").write_bytes(b"future-proof")
    (target / "RELEASE_BUILD_HANDOFF.generated.json").write_bytes(b"old-release-handoff-json")
    (target / "RELEASE_BUILD_HANDOFF.generated.md").write_bytes(b"old-release-handoff-markdown")
    (target / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json").write_bytes(b"old-visual-handoff-json")
    (target / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md").write_bytes(b"old-visual-handoff-markdown")
    (target / "QUARANTINED_INSTALLER_PROMOTION.generated.json").write_bytes(b"old-quarantine-evidence")
    (target / "PUBLICATION_SCOPE.generated.json").write_bytes(b"old-publication-scope")
    (target / "release-evidence" / "browser-lane").mkdir(parents=True)
    (target / "release-evidence" / "browser-lane" / "proof.json").write_bytes(b"old-browser-lane")
    (target / "release-evidence" / "public-promotion.json").write_bytes(b"old-public-promotion")


def write_complete_shelf_manifest(target: Path, platforms: tuple[str, ...]) -> None:
    platform_rows = {
        "linux": ("linux-x64", "deb"),
        "windows": ("win-x64", "exe"),
        "macos": ("osx-arm64", "dmg"),
    }
    artifacts = []
    promoted = []
    for platform in platforms:
        rid, extension = platform_rows[platform]
        artifact_id = f"avalonia-{rid}-installer"
        artifact_name = f"chummer-avalonia-{rid}-installer.{extension}"
        artifact_bytes = f"incumbent-{platform}-installer".encode()
        artifact_path = target / "files" / artifact_name
        artifact_path.parent.mkdir(parents=True, exist_ok=True)
        artifact_path.write_bytes(artifact_bytes)
        artifacts.append(
            {
                "artifactId": artifact_id,
                "head": "avalonia",
                "platform": platform,
                "rid": rid,
                "kind": "installer",
                "fileName": artifact_name,
                "downloadUrl": f"/downloads/files/{artifact_name}",
                "sha256": hashlib.sha256(artifact_bytes).hexdigest(),
                "sizeBytes": len(artifact_bytes),
            }
        )
        promoted.append(
            {
                "tupleId": f"avalonia:{platform}:{rid}",
                "head": "avalonia",
                "platform": platform,
                "rid": rid,
                "artifactId": artifact_id,
            }
        )
    payload = {
        "version": "incumbent-v1",
        "channel": "preview",
        "status": "published",
        "artifacts": artifacts,
        "desktopTupleCoverage": {
            "requiredDesktopPlatforms": list(platforms),
            "requiredDesktopHeads": ["avalonia"],
            "promotedInstallerTuples": promoted,
            "missingRequiredPlatforms": [],
            "missingRequiredHeads": [],
            "missingRequiredPlatformHeadPairs": [],
            "missingRequiredPlatformHeadRidTuples": [],
            "complete": True,
        },
    }
    (target / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps(payload, indent=2) + "\n",
        encoding="utf-8",
    )


def write_full_floor_bundle(bundle: Path) -> None:
    platform_rows = (
        ("linux", "linux-x64", "deb"),
        ("windows", "win-x64", "exe"),
        ("macos", "osx-arm64", "dmg"),
    )
    artifacts = []
    downloads = []
    promoted = []
    for platform, rid, extension in platform_rows:
        artifact_id = f"avalonia-{rid}-installer"
        artifact_name = f"chummer-avalonia-{rid}-installer.{extension}"
        artifact_bytes = f"candidate-{platform}-installer".encode()
        artifact_path = bundle / "files" / artifact_name
        artifact_path.parent.mkdir(parents=True, exist_ok=True)
        artifact_path.write_bytes(artifact_bytes)
        digest = hashlib.sha256(artifact_bytes).hexdigest()
        row = {
            "artifactId": artifact_id,
            "head": "avalonia",
            "platform": platform,
            "rid": rid,
            "kind": "installer",
            "fileName": artifact_name,
            "downloadUrl": f"/downloads/files/{artifact_name}",
            "sha256": digest,
            "sizeBytes": len(artifact_bytes),
        }
        artifacts.append(row)
        downloads.append(
            {
                "id": artifact_id,
                "head": "avalonia",
                "platform": platform,
                "rid": rid,
                "url": f"/downloads/files/{artifact_name}",
                "sha256": digest,
                "sizeBytes": len(artifact_bytes),
            }
        )
        promoted.append(
            {
                "tupleId": f"avalonia:{platform}:{rid}",
                "head": "avalonia",
                "platform": platform,
                "rid": rid,
                "artifactId": artifact_id,
            }
        )
    payload = {
        "version": "fixture-v1",
        "channel": "preview",
        "publishedAt": "2026-07-13T12:00:00Z",
        "artifacts": artifacts,
        "downloads": downloads,
        "desktopTupleCoverage": {
            "requiredDesktopPlatforms": ["linux", "windows", "macos"],
            "requiredDesktopHeads": ["avalonia"],
            "promotedInstallerTuples": promoted,
            "missingRequiredPlatforms": [],
            "missingRequiredHeads": [],
            "missingRequiredPlatformHeadPairs": [],
            "missingRequiredPlatformHeadRidTuples": [],
            "complete": True,
        },
    }
    encoded = json.dumps(payload, indent=2) + "\n"
    (bundle / "releases.json").write_text(encoded, encoding="utf-8")
    (bundle / "RELEASE_CHANNEL.generated.json").write_text(encoded, encoding="utf-8")
    proof = bundle / "proof" / "build-provenance" / "v1"
    (proof / "invocations").mkdir(parents=True)
    (proof / "sbom").mkdir(parents=True)
    (proof / "invocations" / "receipt.json").write_text("valid-receipt", encoding="utf-8")
    (proof / "sbom" / "desktop-avalonia.cdx.json").write_bytes(b"exact-sbom-bytes\n")


def write_startup_smoke_receipts(bundle: Path) -> None:
    manifest = json.loads((bundle / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
    startup = bundle / "startup-smoke"
    startup.mkdir(parents=True, exist_ok=True)
    recorded_at = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    operating_systems = {
        "linux": "Fixture Linux",
        "windows": "Fixture Windows",
        "macos": "Fixture macOS",
    }
    for artifact in manifest["artifacts"]:
        rid = artifact["rid"]
        platform = artifact["platform"]
        receipt = {
            "status": "pass",
            "readyCheckpoint": "pre_ui_event_loop",
            "headId": artifact["head"],
            "platform": platform,
            "arch": rid.partition("-")[2],
            "rid": rid,
            "hostClass": rid,
            "operatingSystem": operating_systems[platform],
            "artifactDigest": f"sha256:{artifact['sha256']}",
            "artifactPath": str(bundle / "files" / artifact["fileName"]),
            "artifactFileName": artifact["fileName"],
            "recordedAtUtc": recorded_at,
        }
        path = startup / f"startup-smoke-{artifact['head']}-{rid}.receipt.json"
        path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


def tree_bytes(root: Path) -> dict[str, tuple[str, bytes | str | None]]:
    snapshot: dict[str, tuple[str, bytes | str | None]] = {}
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        if path.is_symlink():
            snapshot[relative] = ("symlink", os.readlink(path))
        elif path.is_dir():
            snapshot[relative] = ("directory", None)
        elif path.is_file():
            snapshot[relative] = ("file", path.read_bytes())
        else:
            snapshot[relative] = ("special", None)
    return snapshot


def run_publisher(
    repo: Path,
    validator: Path,
    bundle: Path,
    deploy: Path,
    mirror: Path,
    tmp_path: Path,
    *,
    expected_receipt: str = "valid-receipt",
    extra_env: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    env = os.environ.copy()
    env.update(
        {
            "CHUMMER_RELEASE_BUILD_PROVENANCE_VALIDATOR": str(validator),
            "CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS": ",".join(
                (
                    str(mirror),
                    str(tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"),
                    str(tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"),
                )
            ),
            "CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS": "true",
            "PORTAL_MANIFEST_PATH": str(deploy / "releases.json"),
            "PORTAL_DOWNLOADS_DIR": str(deploy),
            "RELEASE_VERSION": "fixture-v1",
            "RELEASE_CHANNEL": "preview",
            "RELEASE_PUBLISHED_AT": "2026-07-13T12:00:00Z",
            "CHUMMER_FORCE_NIGHTLY_PUBLISH": "1",
            "GENERATOR_CALL_LOG": str(tmp_path / "generator-called"),
            "VALIDATOR_CALL_LOG": str(tmp_path / "validator-called"),
            "VALIDATOR_EXPECTED_RECEIPT": expected_receipt,
        }
    )
    env.update(extra_env or {})
    return subprocess.run(
        ["bash", str(repo / "scripts" / PUBLISHER.name), str(bundle), str(deploy)],
        cwd=repo,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )


def assert_proof_exact(source_v1: Path, target_v1: Path) -> None:
    assert tree_bytes(target_v1) == tree_bytes(source_v1)


def test_valid_mac_provenance_replaces_only_v1_with_exact_bytes_in_deploy_and_mirror(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    artifact_name = write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    seed_target(deploy)
    seed_target(mirror)
    seed_target(inherent_mirror)
    seed_target(inherent_registry)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode == 0, result.stderr
    source_v1 = bundle / "proof" / "build-provenance" / "v1"
    assert_proof_exact(source_v1, deploy / "proof" / "build-provenance" / "v1")
    assert_proof_exact(source_v1, mirror / "proof" / "build-provenance" / "v1")
    assert_proof_exact(source_v1, inherent_mirror / "proof" / "build-provenance" / "v1")
    assert_proof_exact(source_v1, inherent_registry / "proof" / "build-provenance" / "v1")
    assert not (deploy / "proof" / "build-provenance" / "v1" / "stale.json").exists()
    assert not (mirror / "proof" / "build-provenance" / "v1" / "stale.json").exists()
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        assert (target / "proof" / "windows" / "receipt.json").read_bytes() == b"windows-proof"
        assert (target / "proof" / "other-namespace" / "receipt.bin").read_bytes() == b"other-proof"
        assert (target / "proof" / "build-provenance" / "v2" / "future.json").read_bytes() == b"future-proof"
        assert (target / "files" / artifact_name).read_bytes() == b"mac-installer-bytes"
        assert (target / "RELEASE_BUILD_HANDOFF.generated.json").read_bytes() == b"new-release-handoff-json"
        assert (target / "RELEASE_BUILD_HANDOFF.generated.md").read_bytes() == b"new-release-handoff-markdown"
        assert (target / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json").read_bytes() == b"new-visual-handoff-json"
        assert (target / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md").read_bytes() == b"new-visual-handoff-markdown"
        assert (target / "QUARANTINED_INSTALLER_PROMOTION.generated.json").read_bytes() == b"new-quarantine-evidence"
        assert (target / "PUBLICATION_SCOPE.generated.json").read_bytes() == b"new-publication-scope"
        assert (target / "release-evidence" / "browser-lane" / "proof.json").read_bytes() == b"new-browser-lane"
        assert (target / "release-evidence" / "public-promotion.json").read_bytes() == b"new-public-promotion"
    validator_calls = (tmp_path / "validator-called").read_text(encoding="utf-8").splitlines()
    assert any(call.endswith("/release-candidate") for call in validator_calls)
    assert any(".release-stage-" in call for call in validator_calls)


def test_partial_candidate_cannot_replace_complete_cross_platform_shelf(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        seed_target(target)
    write_complete_shelf_manifest(deploy, ("linux", "windows"))
    before = {target: tree_bytes(target) for target in (deploy, mirror, inherent_mirror, inherent_registry)}

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert "candidate would erase installer tuples from complete shelf" in result.stderr
    for target, expected in before.items():
        assert tree_bytes(target) == expected


def test_staged_registry_verification_fails_before_any_shelf_mutation(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        seed_target(target)
    before = {target: tree_bytes(target) for target in (deploy, mirror, inherent_mirror, inherent_registry)}

    result = run_publisher(
        repo,
        validator,
        bundle,
        deploy,
        mirror,
        tmp_path,
        extra_env={"FIXTURE_REJECT_STAGED_CANDIDATE": "1"},
    )

    assert result.returncode != 0
    assert "fixture staged candidate rejected" in result.stderr
    for target, expected in before.items():
        assert tree_bytes(target) == expected


def test_candidate_rejects_non_basename_manifest_file_names_before_shelf_mutation(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    for manifest_name, rows_key in (
        ("RELEASE_CHANNEL.generated.json", "artifacts"),
        ("releases.json", "downloads"),
    ):
        manifest_path = bundle / manifest_name
        payload = json.loads(manifest_path.read_text(encoding="utf-8"))
        payload[rows_key][0]["fileName"] = "../files/chummer-avalonia-osx-arm64-installer.dmg"
        manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    seed_target(deploy)
    seed_target(mirror)
    before = {target: tree_bytes(target) for target in (deploy, mirror)}

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert "fileName must be a base name" in result.stderr
    for target, expected in before.items():
        assert tree_bytes(target) == expected


@pytest.mark.parametrize(
    "managed_path_kind",
    ["manifest", "files_tree", "release_evidence_tree", "files_child"],
)
def test_non_mac_publication_rejects_unsafe_managed_target_paths_before_mutation(
    tmp_path: Path,
    managed_path_kind: str,
) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    write_bundle(bundle, platform="linux")
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        seed_target(target)
    external = tmp_path / "external-managed-target"
    external.mkdir()
    (external / "sentinel").write_bytes(b"external-sentinel")
    if managed_path_kind == "manifest":
        path = deploy / "RELEASE_CHANNEL.generated.json"
        path.unlink()
        path.symlink_to(external / "sentinel")
    elif managed_path_kind == "files_tree":
        path = deploy / "files"
        shutil.rmtree(path)
        path.symlink_to(external, target_is_directory=True)
    elif managed_path_kind == "release_evidence_tree":
        path = deploy / "release-evidence"
        shutil.rmtree(path)
        path.symlink_to(external, target_is_directory=True)
    else:
        path = deploy / "files" / "managed-installer.deb"
        path.symlink_to(external / "sentinel")
    before = {
        target: tree_bytes(target)
        for target in (deploy, mirror, inherent_mirror, inherent_registry)
    }
    external_before = tree_bytes(external)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert "Managed release target preflight failed" in result.stderr
    assert not (tmp_path / "generator-called").exists()
    for target, expected in before.items():
        assert tree_bytes(target) == expected
    assert tree_bytes(external) == external_before


def test_coherent_full_floor_candidate_can_heal_divergent_complete_shelves(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    write_full_floor_bundle(bundle)
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        seed_target(target)
    write_complete_shelf_manifest(deploy, ("linux", "windows"))
    write_complete_shelf_manifest(mirror, ("macos",))

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode == 0, result.stderr
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        payload = json.loads((target / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
        assert payload["version"] == "fixture-v1"
        assert {
            row["platform"]
            for row in payload["artifacts"]
            if row.get("kind") == "installer"
        } == {"linux", "windows", "macos"}


def test_public_stable_candidate_requires_the_full_canonical_platform_floor(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    for manifest_name in ("releases.json", "RELEASE_CHANNEL.generated.json"):
        manifest_path = bundle / manifest_name
        payload = json.loads(manifest_path.read_text(encoding="utf-8"))
        payload["channel"] = "public_stable"
        payload["desktopTupleCoverage"] = {
            "requiredDesktopPlatforms": ["macos"],
            "requiredDesktopHeads": ["avalonia"],
            "promotedInstallerTuples": [
                {
                    "tupleId": "avalonia:macos:osx-arm64",
                    "head": "avalonia",
                    "platform": "macos",
                    "rid": "osx-arm64",
                    "artifactId": "avalonia-osx-arm64-installer",
                }
            ],
            "missingRequiredPlatforms": [],
            "missingRequiredHeads": [],
            "missingRequiredPlatformHeadPairs": [],
            "missingRequiredPlatformHeadRidTuples": [],
            "complete": True,
        }
        manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    seed_target(deploy)
    seed_target(mirror)
    blockers = tmp_path / "RELEASE_BLOCKERS.generated.json"
    blockers.write_text(
        json.dumps(
            {
                "generated_at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                "blockers": [
                    {
                        "blocker_id": "release_posture:non_flagship_channel",
                    }
                ],
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    before = {target: tree_bytes(target) for target in (deploy, mirror)}

    result = run_publisher(
        repo,
        validator,
        bundle,
        deploy,
        mirror,
        tmp_path,
        extra_env={
            "RELEASE_CHANNEL": "public_stable",
            "CHUMMER_ROOT_RELEASE_BLOCKERS_PATH": str(blockers),
        },
    )

    assert result.returncode != 0
    assert "requiredDesktopPlatforms must equal the canonical platform floor" in result.stderr
    for target, expected in before.items():
        assert tree_bytes(target) == expected


def test_real_governed_validator_accepts_exact_transactional_candidate(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path, real_validator=True)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    artifact_name = write_real_provenance_bundle(tmp_path, bundle)
    seed_target(deploy)
    seed_target(mirror)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode == 0, result.stderr
    assert (deploy / "files" / artifact_name).read_bytes() == b"real-governed-mac-installer-bytes"
    assert_proof_exact(
        bundle / "proof" / "build-provenance" / "v1",
        deploy / "proof" / "build-provenance" / "v1",
    )


def test_real_governed_validator_rejects_tampered_artifact_before_generator_or_cutover(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path, real_validator=True)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    artifact_name = write_real_provenance_bundle(tmp_path, bundle)
    seed_target(deploy)
    seed_target(mirror)
    (bundle / "files" / artifact_name).write_bytes(b"tampered-after-attestation")
    deploy_before = tree_bytes(deploy)
    mirror_before = tree_bytes(mirror)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert "build_provenance_bundle_failure" in result.stderr
    assert tree_bytes(deploy) == deploy_before
    assert tree_bytes(mirror) == mirror_before
    assert not (tmp_path / "generator-called").exists()


@pytest.mark.parametrize("failure_kind", ["missing", "mismatch", "symlink"])
def test_invalid_mac_provenance_fails_before_any_shelf_mutation(tmp_path: Path, failure_kind: str) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    proof_receipt = None if failure_kind == "missing" else "wrong-receipt"
    write_bundle(bundle, platform="macos", proof_receipt=proof_receipt)
    if failure_kind == "symlink":
        receipt = bundle / "proof" / "build-provenance" / "v1" / "invocations" / "receipt.json"
        receipt.unlink()
        external = tmp_path / "external-receipt.json"
        external.write_text("valid-receipt", encoding="utf-8")
        receipt.symlink_to(external)
    seed_target(deploy)
    seed_target(mirror)
    deploy_before = tree_bytes(deploy)
    mirror_before = tree_bytes(mirror)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert tree_bytes(deploy) == deploy_before
    assert tree_bytes(mirror) == mirror_before
    assert not (tmp_path / "generator-called").exists()
    if failure_kind in {"missing", "symlink"}:
        assert not (tmp_path / "validator-called").exists()


@pytest.mark.parametrize(
    "symlink_kind",
    ["bundle_root", "bundle_ancestor", "deploy_root", "deploy_ancestor", "mirror_root", "mirror_ancestor"],
)
def test_symlinked_source_or_target_root_fails_before_any_shelf_mutation(tmp_path: Path, symlink_kind: str) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    real_bundle = tmp_path / "real-bundle"
    bundle = real_bundle
    real_deploy = tmp_path / "real-deploy"
    deploy = real_deploy
    real_mirror = tmp_path / "real-mirror"
    mirror = real_mirror
    write_bundle(real_bundle, platform="macos", proof_receipt="valid-receipt")
    seed_target(real_deploy)
    seed_target(real_mirror)
    if symlink_kind == "bundle_root":
        bundle = tmp_path / "bundle-link"
        bundle.symlink_to(real_bundle, target_is_directory=True)
    elif symlink_kind == "bundle_ancestor":
        ancestor = tmp_path / "bundle-parent-link"
        ancestor.symlink_to(tmp_path, target_is_directory=True)
        bundle = ancestor / real_bundle.name
    elif symlink_kind == "deploy_root":
        deploy = tmp_path / "deploy-link"
        deploy.symlink_to(real_deploy, target_is_directory=True)
    elif symlink_kind == "deploy_ancestor":
        ancestor = tmp_path / "deploy-parent-link"
        ancestor.symlink_to(tmp_path, target_is_directory=True)
        deploy = ancestor / real_deploy.name
    elif symlink_kind == "mirror_root":
        mirror = tmp_path / "mirror-link"
        mirror.symlink_to(real_mirror, target_is_directory=True)
    else:
        ancestor = tmp_path / "mirror-parent-link"
        ancestor.symlink_to(tmp_path, target_is_directory=True)
        mirror = ancestor / real_mirror.name
    deploy_before = tree_bytes(real_deploy)
    mirror_before = tree_bytes(real_mirror)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert tree_bytes(real_deploy) == deploy_before
    assert tree_bytes(real_mirror) == mirror_before
    assert not (tmp_path / "generator-called").exists()


def test_configured_validator_must_match_governed_validator_bytes(tmp_path: Path) -> None:
    repo, governed_validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    write_bundle(bundle, platform="macos", proof_receipt="definitely-invalid")
    seed_target(deploy)
    seed_target(mirror)
    bypass = tmp_path / "accept-any-provenance.py"
    write_executable(bypass, "#!/usr/bin/env python3\nraise SystemExit(0)\n")
    (bypass.parent / "build_provenance_support.py").write_text("# bypass support\n", encoding="utf-8")
    deploy_before = tree_bytes(deploy)
    mirror_before = tree_bytes(mirror)

    result = run_publisher(repo, bypass, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert "does not match the governed portable validator bytes" in result.stderr
    assert tree_bytes(deploy) == deploy_before
    assert tree_bytes(mirror) == mirror_before
    assert not (tmp_path / "generator-called").exists()
    assert governed_validator.is_file()


@pytest.mark.parametrize("drift_kind", ["manifest", "artifact"])
def test_exact_staged_candidate_drift_fails_before_shelf_mutation(tmp_path: Path, drift_kind: str) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    seed_target(deploy)
    seed_target(mirror)
    deploy_before = tree_bytes(deploy)
    mirror_before = tree_bytes(mirror)
    extra_env: dict[str, str]
    if drift_kind == "manifest":
        compatibility = json.loads((bundle / "releases.json").read_text(encoding="utf-8"))
        compatibility["downloads"][0]["sha256"] = "0" * 64
        (bundle / "releases.json").write_text(json.dumps(compatibility, indent=2) + "\n", encoding="utf-8")
        extra_env = {"FIXTURE_GENERATOR_CANONICAL_SOURCE": str(bundle / "RELEASE_CHANNEL.generated.json")}
    else:
        extra_env = {"FIXTURE_MUTATE_CANDIDATE_ARTIFACT": "1"}

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path, extra_env=extra_env)

    assert result.returncode != 0
    assert tree_bytes(deploy) == deploy_before
    assert tree_bytes(mirror) == mirror_before
    assert (tmp_path / "generator-called").is_file()
    assert "candidate manifest disagreement" in result.stderr.lower() or "build_provenance_bundle_failure" in result.stderr


def test_transaction_failure_rolls_back_every_already_cut_over_target(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        seed_target(target)
    before = {target: tree_bytes(target) for target in (deploy, mirror, inherent_mirror, inherent_registry)}

    result = run_publisher(
        repo,
        validator,
        bundle,
        deploy,
        mirror,
        tmp_path,
        extra_env={"CHUMMER_RELEASE_TRANSACTION_FAULT_AFTER_COMMITS": "1"},
    )

    assert result.returncode != 0
    assert "rolled back" in result.stderr.lower()
    for target, snapshot in before.items():
        assert tree_bytes(target) == snapshot
    assert not list(tmp_path.rglob(".*.release-stage-*"))
    assert not list(tmp_path.rglob(".*.release-backup-*"))
    assert not list(tmp_path.rglob(".*.release-failed-*"))


def test_windows_only_and_generic_stage_only_modes_fail_closed_before_work(tmp_path: Path) -> None:
    repo, _ = make_publisher_fixture(tmp_path)
    output = tmp_path / "generic-candidate"

    result = subprocess.run(
        ["bash", str(repo / "scripts" / PUBLISHER.name)],
        cwd=repo,
        env={
            **os.environ,
            "CHUMMER_WINDOWS_ONLY_PUBLICATION_STAGE_ROOT": str(tmp_path / "windows-stage"),
            "CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY": "1",
            "CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR": str(output),
        },
        capture_output=True,
        text=True,
        check=False,
    )

    assert result.returncode != 0
    assert "cannot be combined with the generic release-candidate stage-only lane" in result.stderr
    assert not output.exists()
    assert not (tmp_path / "generator-called").exists()


@pytest.mark.parametrize("stage_only", [False, True])
def test_filesystem_publisher_refuses_unverifiable_external_claim_before_shelf_mutation(
    tmp_path: Path,
    stage_only: bool,
) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    write_bundle(bundle, platform="linux")
    targets = (deploy, mirror, inherent_mirror, inherent_registry)
    for target in targets:
        seed_target(target)
    before = {target: tree_bytes(target) for target in targets}
    output = tmp_path / "candidate-output"
    extra_env = {
        "CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED": "true",
        "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL": (
            "https://example.invalid/downloads/RELEASE_CHANNEL.generated.json"
        ),
    }
    if stage_only:
        extra_env.update(
            {
                "CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY": "1",
                "CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR": str(output),
            }
        )

    result = run_publisher(
        repo,
        validator,
        bundle,
        deploy,
        mirror,
        tmp_path,
        extra_env=extra_env,
    )

    assert result.returncode != 0
    assert "cannot verify or claim external publication" in result.stderr
    assert not output.exists()
    assert not (tmp_path / "generator-called").exists()
    for target, snapshot in before.items():
        assert tree_bytes(target) == snapshot


def write_minimal_transaction_candidate(root: Path) -> None:
    (root / "files").mkdir(parents=True)
    (root / "files" / "candidate.bin").write_bytes(b"candidate-bytes")
    (root / "releases.json").write_text('{"version":"candidate"}\n', encoding="utf-8")
    (root / "RELEASE_CHANNEL.generated.json").write_text(
        '{"version":"candidate"}\n',
        encoding="utf-8",
    )


def run_transaction_helper(
    candidate: Path,
    targets: tuple[Path, ...],
    *,
    extra_env: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            sys.executable,
            str(RELEASE_CANDIDATE_FS_HELPER),
            "transaction",
            str(candidate),
            "-",
            *(str(target) for target in targets),
        ],
        cwd=REPO_ROOT,
        env={**os.environ, **(extra_env or {})},
        capture_output=True,
        text=True,
        check=False,
    )


def test_durable_transaction_recovers_target_missing_after_uncatchable_exit(
    tmp_path: Path,
) -> None:
    candidate = tmp_path / "candidate"
    targets = (tmp_path / "target-a", tmp_path / "target-b")
    write_minimal_transaction_candidate(candidate)
    for index, target in enumerate(targets):
        target.mkdir()
        (target / "incumbent.txt").write_text(f"incumbent-{index}", encoding="utf-8")
    before = {target: tree_bytes(target) for target in targets}

    interrupted = run_transaction_helper(
        candidate,
        targets,
        extra_env={"CHUMMER_RELEASE_TRANSACTION_HARD_EXIT_PHASE": "after-backup"},
    )

    assert interrupted.returncode == 92
    assert list(tmp_path.rglob(".*.release-transaction-*.json"))
    shutil.rmtree(candidate / "files")

    recovered = run_transaction_helper(candidate, targets)

    assert recovered.returncode != 0
    assert "recovered_release_candidate_transaction=" in recovered.stderr
    assert "rolled_back" in recovered.stderr
    for target, snapshot in before.items():
        assert tree_bytes(target) == snapshot
    assert not list(tmp_path.rglob(".*.release-stage-*"))
    assert not list(tmp_path.rglob(".*.release-backup-*"))
    assert not list(tmp_path.rglob(".*.release-transaction-*.json"))


def test_durable_commit_marker_recovers_forward_and_cleans_backups(tmp_path: Path) -> None:
    candidate = tmp_path / "candidate"
    targets = (tmp_path / "target-a", tmp_path / "target-b")
    write_minimal_transaction_candidate(candidate)
    for index, target in enumerate(targets):
        target.mkdir()
        (target / "incumbent.txt").write_text(f"incumbent-{index}", encoding="utf-8")

    interrupted = run_transaction_helper(
        candidate,
        targets,
        extra_env={
            "CHUMMER_RELEASE_TRANSACTION_HARD_EXIT_PHASE": "after-commit-marker",
        },
    )

    assert interrupted.returncode == 94
    assert list(tmp_path.rglob(".*.release-backup-*"))
    assert list(tmp_path.rglob(".*.release-transaction-*.json"))
    shutil.rmtree(candidate / "files")

    recovered = run_transaction_helper(candidate, targets)

    assert recovered.returncode != 0
    assert "recovered_release_candidate_transaction=" in recovered.stderr
    assert ":committed" in recovered.stderr
    for target in targets:
        assert (target / "files" / "candidate.bin").read_bytes() == b"candidate-bytes"
        assert json.loads((target / "releases.json").read_text(encoding="utf-8")) == {
            "version": "candidate"
        }
    assert not list(tmp_path.rglob(".*.release-stage-*"))
    assert not list(tmp_path.rglob(".*.release-backup-*"))
    assert not list(tmp_path.rglob(".*.release-transaction-*.json"))


def test_parent_ancestor_swap_cannot_redirect_descriptor_anchored_cutover(
    tmp_path: Path,
) -> None:
    candidate = tmp_path / "candidate"
    write_minimal_transaction_candidate(candidate)
    trusted_ancestor = tmp_path / "trusted-ancestor"
    trusted_target = trusted_ancestor / "shelf-parent" / "target"
    trusted_target.mkdir(parents=True)
    (trusted_target / "incumbent.txt").write_text("trusted", encoding="utf-8")
    external_ancestor = tmp_path / "external-ancestor"
    external_target = external_ancestor / "shelf-parent" / "target"
    external_target.mkdir(parents=True)
    (external_target / "incumbent.txt").write_text("external", encoding="utf-8")
    trusted_before = tree_bytes(trusted_target)
    external_before = tree_bytes(external_target)

    process = subprocess.Popen(
        [
            sys.executable,
            str(RELEASE_CANDIDATE_FS_HELPER),
            "transaction",
            str(candidate),
            "-",
            str(trusted_target),
        ],
        cwd=REPO_ROOT,
        env={
            **os.environ,
            "CHUMMER_RELEASE_TRANSACTION_TEST_PAUSE_BEFORE_PARENT_OPEN": "1",
        },
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        if process.poll() is not None:
            break
        status = Path(f"/proc/{process.pid}/status").read_text(encoding="utf-8")
        if "\nState:\tT" in status:
            break
        time.sleep(0.01)
    else:
        process.kill()
        pytest.fail("transaction helper did not reach the parent-open race barrier")
    assert process.poll() is None

    original_ancestor = tmp_path / "trusted-ancestor-original"
    trusted_ancestor.rename(original_ancestor)
    trusted_ancestor.symlink_to(external_ancestor, target_is_directory=True)
    process.send_signal(signal.SIGCONT)
    _, stderr = process.communicate(timeout=10)

    assert process.returncode != 0
    assert "publication target parent" in stderr
    assert tree_bytes(original_ancestor / "shelf-parent" / "target") == trusted_before
    assert tree_bytes(external_target) == external_before
    assert not list(tmp_path.rglob(".*.release-stage-*"))
    assert not list(tmp_path.rglob(".*.release-backup-*"))
    assert not list(tmp_path.rglob(".*.release-transaction-*.json"))


def test_unvalidated_journal_cannot_authorize_missing_parent_creation(
    tmp_path: Path,
) -> None:
    candidate = tmp_path / "candidate"
    write_minimal_transaction_candidate(candidate)
    existing_target = tmp_path / "existing-parent" / "target-a"
    existing_target.mkdir(parents=True)
    missing_target = tmp_path / "missing-parent" / "target-b"
    transaction_id = "a" * 32
    journal = existing_target.parent / (
        f".{existing_target.name}.release-transaction-{transaction_id}.json"
    )
    journal.write_text("{}\n", encoding="utf-8")

    result = run_transaction_helper(
        candidate,
        (existing_target, missing_target),
    )

    assert result.returncode != 0
    assert "transaction journal schema is unsupported" in result.stderr
    assert not missing_target.parent.exists()
    assert journal.read_text(encoding="utf-8") == "{}\n"


def test_transaction_cleans_stage_when_candidate_application_fails(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    write_bundle(bundle, platform="macos", proof_receipt="valid-receipt")
    seed_target(deploy)
    seed_target(mirror)
    external = tmp_path / "external-handoff.md"
    external.write_bytes(b"external-handoff")
    managed_handoff = deploy / "RELEASE_BUILD_HANDOFF.generated.md"
    managed_handoff.unlink()
    managed_handoff.symlink_to(external)
    deploy_before = tree_bytes(deploy)
    mirror_before = tree_bytes(mirror)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode != 0
    assert "Managed release target preflight failed: managed file is symlinked" in result.stderr
    assert tree_bytes(deploy) == deploy_before
    assert tree_bytes(mirror) == mirror_before
    assert external.read_bytes() == b"external-handoff"
    assert not list(tmp_path.rglob(".*.release-stage-*"))
    assert not list(tmp_path.rglob(".*.release-backup-*"))
    assert not list(tmp_path.rglob(".*.release-failed-*"))


def test_non_mac_shelf_removes_stale_v1_but_preserves_other_proof_namespaces(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    artifact_name = write_bundle(bundle, platform="linux")
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        seed_target(target)

    result = run_publisher(repo, validator, bundle, deploy, mirror, tmp_path)

    assert result.returncode == 0, result.stderr
    assert not (tmp_path / "validator-called").exists()
    for target in (deploy, mirror, inherent_mirror, inherent_registry):
        assert not (target / "proof" / "build-provenance" / "v1").exists()
        assert (target / "proof" / "windows" / "receipt.json").read_bytes() == b"windows-proof"
        assert (target / "proof" / "other-namespace" / "receipt.bin").read_bytes() == b"other-proof"
        assert (target / "proof" / "build-provenance" / "v2" / "future.json").read_bytes() == b"future-proof"
        assert (target / "files" / artifact_name).read_bytes() == b"linux-installer-bytes"


def test_stage_only_persists_fully_validated_candidate_without_target_mutation(tmp_path: Path) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    portal = tmp_path / "portal"
    mirror = tmp_path / "mirror"
    inherent_mirror = tmp_path / "chummer.run-services" / "Chummer.Portal" / "downloads"
    inherent_registry = tmp_path / "chummer-hub-registry" / ".codex-studio" / "published"
    output = tmp_path / "sealed-candidate"
    write_full_floor_bundle(bundle)
    write_startup_smoke_receipts(bundle)
    for target in (deploy, portal, mirror, inherent_mirror, inherent_registry):
        seed_target(target)
    bundle_before = tree_bytes(bundle)
    target_before = {
        target: tree_bytes(target)
        for target in (deploy, portal, mirror, inherent_mirror, inherent_registry)
    }

    result = run_publisher(
        repo,
        validator,
        bundle,
        deploy,
        mirror,
        tmp_path,
        extra_env={
            "CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY": "1",
            "CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR": str(output),
            "PORTAL_MANIFEST_PATH": str(portal / "releases.json"),
            "PORTAL_DOWNLOADS_DIR": str(portal),
            "VERIFY_CALL_LOG": str(tmp_path / "verify-called"),
        },
    )

    assert result.returncode == 0, result.stderr
    assert f"release_candidate_stage_only_path={output}" in result.stdout
    assert output.is_dir() and not output.is_symlink()
    assert tree_bytes(bundle) == bundle_before
    for target, expected in target_before.items():
        assert tree_bytes(target) == expected
    assert tree_bytes(output / "files") == tree_bytes(bundle / "files")
    assert_proof_exact(
        bundle / "proof" / "build-provenance" / "v1",
        output / "proof" / "build-provenance" / "v1",
    )
    compatibility = json.loads((output / "releases.json").read_text(encoding="utf-8"))
    canonical = json.loads((output / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8"))
    assert (canonical["version"], canonical["channel"]) == (
        compatibility["version"],
        compatibility["channel"],
    )
    assert {row["artifactId"] for row in canonical["artifacts"]} == {
        row["id"] for row in compatibility["downloads"]
    }
    assert len(list((output / "startup-smoke").glob("startup-smoke-*.receipt.json"))) == 3
    for path in output.rglob("*"):
        if path.is_file() and path.suffix in {".json", ".md", ".log", ".txt"}:
            assert b".candidate-build." not in path.read_bytes()
    verify_calls = (tmp_path / "verify-called").read_text(encoding="utf-8").splitlines()
    assert len(verify_calls) >= 3
    assert all(str(target) not in call for target in target_before for call in verify_calls)
    assert not list(tmp_path.glob(".sealed-candidate.candidate-build.*"))


@pytest.mark.parametrize(
    "failure_kind",
    ["existing_output", "symlink_output", "nested_deploy", "nested_portal", "candidate_rejected"],
)
def test_stage_only_failure_never_leaves_partial_output_or_mutates_targets(
    tmp_path: Path,
    failure_kind: str,
) -> None:
    repo, validator = make_publisher_fixture(tmp_path)
    bundle = tmp_path / "bundle"
    deploy = tmp_path / "deploy"
    portal = tmp_path / "portal"
    mirror = tmp_path / "mirror"
    output = tmp_path / "sealed-candidate"
    write_full_floor_bundle(bundle)
    write_startup_smoke_receipts(bundle)
    for target in (deploy, portal, mirror):
        seed_target(target)
    target_before = {target: tree_bytes(target) for target in (deploy, portal, mirror)}
    external = tmp_path / "external-output"
    if failure_kind == "nested_deploy":
        output = deploy / "sealed-candidate"
    elif failure_kind == "nested_portal":
        output = portal / "sealed-candidate"
    if failure_kind == "existing_output":
        output.mkdir()
        (output / "sentinel").write_bytes(b"existing-output")
    elif failure_kind == "symlink_output":
        external.mkdir()
        (external / "sentinel").write_bytes(b"external-output")
        output.symlink_to(external, target_is_directory=True)

    extra_env = {
        "CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY": "1",
        "CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR": str(output),
        "PORTAL_MANIFEST_PATH": str(portal / "releases.json"),
        "PORTAL_DOWNLOADS_DIR": str(portal),
    }
    if failure_kind == "candidate_rejected":
        extra_env["FIXTURE_REJECT_STAGED_CANDIDATE"] = "1"

    result = run_publisher(
        repo,
        validator,
        bundle,
        deploy,
        mirror,
        tmp_path,
        extra_env=extra_env,
    )

    assert result.returncode != 0
    for target, expected in target_before.items():
        assert tree_bytes(target) == expected
    if failure_kind == "existing_output":
        assert (output / "sentinel").read_bytes() == b"existing-output"
        assert not (tmp_path / "generator-called").exists()
    elif failure_kind == "symlink_output":
        assert output.is_symlink()
        assert (external / "sentinel").read_bytes() == b"external-output"
        assert not (tmp_path / "generator-called").exists()
    elif failure_kind == "candidate_rejected":
        assert not output.exists() and not output.is_symlink()
        assert (tmp_path / "generator-called").is_file()
    else:
        assert not output.exists() and not output.is_symlink()
        assert not (tmp_path / "generator-called").exists()
    assert not list(output.parent.glob(f".{output.name}.candidate-build.*"))
