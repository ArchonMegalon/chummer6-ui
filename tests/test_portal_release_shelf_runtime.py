from __future__ import annotations

import hashlib
import json
import os
import shutil
import socket
import subprocess
import time
import urllib.request
from contextlib import contextmanager
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
PORTAL_PROJECT = REPO_ROOT / "Chummer.Portal" / "Chummer.Portal.csproj"
PORTAL_DOWNLOADS_DIR = REPO_ROOT / "Chummer.Portal" / "downloads"
PORTAL_RELEASES_FILE = PORTAL_DOWNLOADS_DIR / "releases.json"
REGISTRY_GLOBAL_FIXTURE_DIR = (
    REPO_ROOT / "tests" / "fixtures" / "registry-global-flagship-v2"
)
REGISTRY_8F02_COMMIT = "8f02ac8f3bfddb68690a547eb0696178d727fcef"
REGISTRY_FIXTURE_SHA256 = {
    "RELEASE_CHANNEL.generated.json": (
        "1749522a1b37fa023acde12072f5ca3fd03b1cb0836adac5dd58ba50f352d20d"
    ),
    "releases.json": (
        "9adbc1d62693833fb66fe1c4590ed997fd6d85ca9483df537c08b892165f964f"
    ),
}


def _resolve_registry_8f02_root() -> Path:
    configured = (
        os.environ.get("CHUMMER_REGISTRY_8F02_ROOT")
        or os.environ.get("CHUMMER_UI_TEST_REGISTRY_ROOT")
        or ""
    ).strip()
    candidates = [
        Path(configured) if configured else None,
        Path(
            "/docker/chummercomplete/.codex-worktrees/"
            "registry-promotion-integrated-20260725"
        ),
        REPO_ROOT.parent / "chummer-hub-registry",
        REPO_ROOT.parent.parent / "chummer-hub-registry",
    ]
    for candidate in candidates:
        if candidate is None:
            continue
        verifier = candidate / "scripts" / "verify_public_release_channel.py"
        if not verifier.is_file():
            continue
        completed = subprocess.run(
            ["git", "-C", str(candidate), "rev-parse", "HEAD"],
            check=True,
            capture_output=True,
            text=True,
        )
        if completed.stdout.strip() == REGISTRY_8F02_COMMIT:
            return candidate
    raise AssertionError(
        "Exact Registry 8f02 checkout is required; set CHUMMER_REGISTRY_8F02_ROOT."
    )


@pytest.fixture(scope="session")
def registry_verified_global_bundle(tmp_path_factory: pytest.TempPathFactory) -> Path:
    for file_name, expected_digest in REGISTRY_FIXTURE_SHA256.items():
        fixture_bytes = (REGISTRY_GLOBAL_FIXTURE_DIR / file_name).read_bytes()
        assert hashlib.sha256(fixture_bytes).hexdigest() == expected_digest

    bundle = tmp_path_factory.mktemp("registry-global-flagship") / "public-bundle"
    shutil.copytree(REGISTRY_GLOBAL_FIXTURE_DIR, bundle)
    verifier = (
        _resolve_registry_8f02_root()
        / "scripts"
        / "verify_public_release_channel.py"
    )
    for target in (
        bundle / "RELEASE_CHANNEL.generated.json",
        bundle / "releases.json",
        bundle,
    ):
        subprocess.run(
            [
                "python3",
                str(verifier),
                "--require-complete-desktop-coverage",
                str(target),
            ],
            check=True,
            capture_output=True,
            text=True,
        )
    return bundle


def _copy_registry_global_bundle(source: Path, destination: Path) -> Path:
    shutil.copytree(source, destination, dirs_exist_ok=True)
    return destination / "releases.json"


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as handle:
        handle.bind(("127.0.0.1", 0))
        return int(handle.getsockname()[1])


def _http_get(url: str) -> str:
    with urllib.request.urlopen(url, timeout=5) as response:
        return response.read().decode("utf-8")


class _NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # type: ignore[override]
        return None

    def http_error_301(self, req, fp, code, msg, headers):  # type: ignore[override]
        return fp

    http_error_302 = http_error_303 = http_error_307 = http_error_308 = http_error_301


def _http_request(
    url: str,
    *,
    method: str = "GET",
    headers: dict[str, str] | None = None,
    follow_redirects: bool = True,
) -> tuple[int, dict[str, str], bytes]:
    request = urllib.request.Request(url, method=method, headers=headers or {})
    opener = urllib.request.build_opener() if follow_redirects else urllib.request.build_opener(_NoRedirectHandler())
    with opener.open(request, timeout=5) as response:
        return int(response.status), dict(response.headers.items()), response.read()


@contextmanager
def _running_portal(
    releases_dir: Path = PORTAL_DOWNLOADS_DIR,
    releases_file: Path = PORTAL_RELEASES_FILE,
):
    port = _find_free_port()
    base_url = f"http://127.0.0.1:{port}"
    log_path = REPO_ROOT / ".tmp" / f"portal-runtime-test-{port}.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)

    env = os.environ.copy()
    env["ASPNETCORE_ENVIRONMENT"] = "Development"
    env["DOTNET_ENVIRONMENT"] = "Development"
    env["ASPNETCORE_URLS"] = base_url
    env["CHUMMER_PORTAL_RELEASES_DIR"] = str(releases_dir)
    env["CHUMMER_PORTAL_RELEASES_FILE"] = str(releases_file)
    env["CHUMMER_PORTAL_IMPLICIT_OWNER"] = "runtime-test@chummer.run"

    with log_path.open("w", encoding="utf-8") as log_file:
        process = subprocess.Popen(
            [
                "dotnet",
                "run",
                "--project",
                str(PORTAL_PROJECT),
                "--no-launch-profile",
                "-p:ChummerUseLocalCompatibilityTree=true",
                (
                    "-p:ChummerLocalContractsProject="
                    + str(
                        (
                            REPO_ROOT
                            / "chummer-core-engine"
                            / "Chummer.Contracts"
                            / "Chummer.Contracts.csproj"
                        ).resolve()
                    )
                ),
            ],
            cwd=REPO_ROOT,
            env=env,
            stdout=log_file,
            stderr=subprocess.STDOUT,
        )

    try:
        deadline = time.time() + 90
        last_error = ""
        ready = False
        while time.time() < deadline:
            if process.poll() is not None:
                break

            try:
                _http_get(f"{base_url}/downloads/")
                ready = True
                break
            except Exception as exc:  # pragma: no cover - only used on boot retry
                last_error = str(exc)
                time.sleep(0.5)

        if not ready:
            log_text = log_path.read_text(encoding="utf-8") if log_path.exists() else ""
            raise AssertionError(
                f"Portal did not become ready at {base_url}. Last error: {last_error}\n{log_text}"
            )

        yield base_url
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)


def test_portal_runtime_renders_release_shelf_help_and_status_from_local_manifest() -> None:
    manifest = json.loads(PORTAL_RELEASES_FILE.read_text(encoding="utf-8"))
    manifest_version = manifest["version"]
    manifest_downloads = manifest["downloads"]

    assert manifest_downloads, "Expected the checked-in local releases manifest to expose at least one download row."

    primary_download = manifest_downloads[0]

    with _running_portal() as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")
        help_html = _http_get(f"{base_url}/help")
        contact_html = _http_get(f"{base_url}/contact")
        releases_json = json.loads(_http_get(f"{base_url}/downloads/releases.json"))

    assert 'data-download-list="published-artifacts"' in downloads_html
    assert downloads_html.count('data-download-platform-card="') == 3
    assert 'data-download-platform-card="windows"' in downloads_html
    assert 'data-download-platform-card="linux"' in downloads_html
    assert 'data-download-platform-card="macos"' in downloads_html
    assert primary_download["fileName"] in downloads_html
    assert primary_download["url"] not in downloads_html
    assert f'data-download-dispatch-url="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    assert f'href="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    assert 'data-download-link-mode="local-dispatch"' in downloads_html
    assert "data-download-raw-url" not in downloads_html
    assert f'data-download-install-route="/downloads/install/{primary_download["artifactId"]}"' in downloads_html
    assert 'data-download-action="download-artifact"' in downloads_html
    assert 'data-download-security-state="digest_published"' in downloads_html
    assert 'data-download-journey="clean-install"' in downloads_html
    assert 'data-download-journey="existing-install-update"' in downloads_html
    assert "Open <strong>Update Status</strong> inside Chummer" in downloads_html
    assert "proof-required" not in downloads_html
    assert "artifact id pending" not in downloads_html
    assert "docker compose" not in downloads_html
    assert "promoted to Stable" not in downloads_html
    assert "MiB" in downloads_html
    assert 'data-download-manifest-link' in downloads_html

    assert manifest_version in status_html
    assert f"Platform coverage: {len(manifest_downloads)} of 3 desktop installers available." in status_html
    assert "data-portal-status-boundary=\"published-release-record\"" in status_html
    assert "Preview files are never counted as Stable downloads." in status_html

    assert 'data-portal-help-panel="handoff-guide"' in help_html
    assert 'aria-label="Help recovery actions"' in help_html
    assert 'data-portal-help-action="open-downloads"' in help_html
    assert 'data-portal-help-action="open-discord"' in help_html
    assert "/app?command=character_roster" in help_html

    assert 'data-portal-contact-action="open-discord"' in contact_html
    assert "The fastest human route is the Chummer Discord." in contact_html

    assert releases_json["version"] == manifest_version
    assert len(releases_json["downloads"]) == len(manifest_downloads)


def _download_row(
    platform: str,
    *,
    channel: str,
    version: str,
) -> dict[str, object]:
    platform_values = {
        "windows": ("win-x64", "x64", "exe"),
        "linux": ("linux-x64", "x64", "deb"),
        "macos": ("osx-arm64", "arm64", "dmg"),
    }
    rid, arch, file_format = platform_values[platform]
    artifact_id = f"avalonia-{rid}-installer"
    file_name = f"chummer-{artifact_id}.{file_format}"
    row: dict[str, object] = {
        "id": artifact_id,
        "artifactId": artifact_id,
        "head": "avalonia",
        "platform": platform,
        "platformId": f"{platform}-{arch}",
        "rid": rid,
        "arch": arch,
        "format": file_format,
        "kind": "installer",
        "flavor": "installer",
        "fileName": file_name,
        "url": f"https://chummer.run/downloads/files/{file_name}",
        "downloadUrl": f"https://chummer.run/downloads/files/{file_name}",
        "sha256": {"windows": "a", "linux": "b", "macos": "c"}[platform] * 64,
        "sizeBytes": {"windows": 2_900_000, "linux": 37_000_000, "macos": 48_000_000}[platform],
        "channel": channel,
        "channelId": channel,
        "version": version,
        "releaseVersion": version,
        "compatibilityState": "compatible",
        "installAccessClass": "open_public",
    }
    if platform == "windows":
        row["signingStatus"] = "passed"
    return row


def _write_release_manifest(
    root: Path,
    *,
    channel: str,
    version: str,
    platforms: tuple[str, ...],
    release_profile: str | None = None,
) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    downloads = [
        _download_row(platform, channel=channel, version=version)
        for platform in platforms
    ]
    payload: dict[str, object] = {
        "status": "published",
        "channel": channel,
        "channelId": channel,
        "rolloutState": channel,
        "version": version,
        "releaseVersion": version,
        "generatedAt": "2026-07-25T12:00:00Z",
        "generated_at": "2026-07-25T12:00:00Z",
        "publishedAt": "2026-07-25T12:00:00Z",
        "supportabilityState": "gold_supported",
        "downloads": downloads,
    }
    if release_profile:
        payload["releaseProfile"] = release_profile
    if release_profile == "global_flagship":
        raise AssertionError(
            "Global flagship tests must use the Registry 8f02 materialized fixture."
        )
    manifest_path = root / "releases.json"
    manifest_path.write_text(json.dumps(payload), encoding="utf-8")
    return manifest_path


def test_portal_runtime_renders_three_truthful_global_flagship_platform_cards(
    tmp_path: Path,
    registry_verified_global_bundle: Path,
) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _copy_registry_global_bundle(
        registry_verified_global_bundle,
        release_root,
    )
    release_version = json.loads(
        manifest_path.read_text(encoding="utf-8")
    )["releaseVersion"]

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")
        artifact_payload = json.loads(
            (release_root / "RELEASE_CHANNEL.generated.json").read_text(
                encoding="utf-8"
            )
        )
        manifest_path.write_text(json.dumps(artifact_payload), encoding="utf-8")
        artifacts_html = _http_get(f"{base_url}/downloads/")

    assert downloads_html.count('data-download-availability="available"') == 3
    assert artifacts_html.count('data-download-availability="available"') == 3
    assert 'data-download-security-state="digest_published"' in downloads_html
    assert 'data-download-security-state="package_verified"' in downloads_html
    assert 'data-download-security-state="signed_notarized"' in downloads_html
    assert "Signed installer" not in downloads_html
    assert "SHA-256 integrity published" in downloads_html
    assert "Native package and integrity verified" in downloads_html
    assert "Signed with Developer ID and notarized by Apple" in downloads_html
    assert "3 of 3 platforms available" in downloads_html
    assert f'data-portal-status-version="{release_version}"' in status_html
    assert "Platform coverage: 3 of 3 desktop installers available." in status_html


def test_portal_runtime_accepts_registry_generated_timestamp_alias_forms(
    tmp_path: Path,
    registry_verified_global_bundle: Path,
) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _copy_registry_global_bundle(
        registry_verified_global_bundle,
        release_root,
    )
    valid_payload = json.loads(manifest_path.read_text(encoding="utf-8"))

    with _running_portal(release_root, manifest_path) as base_url:
        timestamp_cases: dict[str, dict[str, object]] = {}

        generated_at_only = json.loads(json.dumps(valid_payload))
        generated_at_only.pop("generatedAt")
        timestamp_cases["generated_at_only"] = generated_at_only

        generated_at_camel_only = json.loads(json.dumps(valid_payload))
        generated_at_camel_only.pop("generated_at")
        timestamp_cases["generatedAt_only"] = generated_at_camel_only

        naive_utc = json.loads(json.dumps(valid_payload))
        naive_utc["generatedAt"] = "2026-07-25T15:51:39"
        naive_utc["generated_at"] = "2026-07-25T15:51:39"
        timestamp_cases["naive_treated_as_utc"] = naive_utc

        date_only_utc = json.loads(json.dumps(valid_payload))
        date_only_utc["generatedAt"] = "2026-07-25"
        date_only_utc["generated_at"] = "2026-07-25"
        timestamp_cases["date_only_treated_as_utc"] = date_only_utc

        for case_name, timestamp in (
            ("comma_fraction", "2026-07-25T15:51:39,123"),
            ("basic_date", "20260725"),
            ("week_date", "2026-W30-5"),
            ("week_date_default_monday", "2026-W30"),
            ("basic_time", "2026-07-25T155139"),
            ("fractional_hour", "2026-07-25T15.5"),
            ("fractional_minute", "2026-07-25T15:51.5"),
            ("arbitrary_separator", "2026-07-25X15:51:39Z"),
            ("astral_separator", "2026-07-25🐍15:51:39Z"),
            ("compact_offset", "2026-07-25T15:51:39+0000"),
            ("hour_offset", "2026-07-25T15:51:39+00"),
            ("fractional_offset_second", "2026-07-25T15:51:39+00:00:30.5"),
        ):
            payload = json.loads(json.dumps(valid_payload))
            payload["generatedAt"] = timestamp
            payload["generated_at"] = timestamp
            timestamp_cases[case_name] = payload

        for case_name, payload in timestamp_cases.items():
            manifest_path.write_text(json.dumps(payload), encoding="utf-8")
            downloads_html = _http_get(f"{base_url}/downloads/")
            assert (
                downloads_html.count('data-download-availability="available"') == 3
            ), case_name


def test_global_flagship_never_borrows_sibling_rows(
    tmp_path: Path,
    registry_verified_global_bundle: Path,
) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _copy_registry_global_bundle(
        registry_verified_global_bundle,
        release_root,
    )
    primary = json.loads(manifest_path.read_text(encoding="utf-8"))
    for row in primary["downloads"]:
        row["compatibilityState"] = "incompatible"
    manifest_path.write_text(json.dumps(primary), encoding="utf-8")

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")

    assert downloads_html.count('data-download-availability="available"') == 0
    assert 'data-release-state="unavailable"' in downloads_html
    assert "https://chummer.run/downloads/files/" not in downloads_html


def test_portal_runtime_never_presents_preview_rows_as_stable_downloads(tmp_path: Path) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _write_release_manifest(
        release_root,
        channel="preview",
        version="preview-20260725-120000",
        platforms=("windows", "linux", "macos"),
    )

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")

    assert 'data-release-state="unavailable"' in downloads_html
    assert downloads_html.count('data-download-availability="available"') == 0
    assert downloads_html.count('data-download-action="download-unavailable"') == 3
    assert "No Stable desktop release is published right now." in downloads_html
    assert "https://chummer.run/downloads/files/" not in downloads_html
    assert 'data-portal-status-release-status="Unavailable"' in status_html


def test_portal_runtime_fails_closed_for_malformed_release_manifest(tmp_path: Path) -> None:
    release_root = tmp_path / "downloads"
    release_root.mkdir(parents=True)
    manifest_path = release_root / "releases.json"
    manifest_path.write_text('{"status":"published","downloads":[', encoding="utf-8")

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")

    assert 'data-release-state="unavailable"' in downloads_html
    assert downloads_html.count('data-download-availability="available"') == 0
    assert downloads_html.count('data-download-action="download-unavailable"') == 3
    assert "Release information could not be loaded." in downloads_html
    assert 'data-portal-status-release-status="Unavailable"' in status_html


def test_portal_runtime_never_builds_a_hybrid_release_from_disagreeing_sibling(
    tmp_path: Path,
) -> None:
    release_root = tmp_path / "downloads"
    release_version = "run-20260725-125000"
    manifest_path = _write_release_manifest(
        release_root,
        channel="public_stable",
        version=release_version,
        platforms=("windows",),
    )
    sibling_path = release_root / "RELEASE_CHANNEL.generated.json"
    complete_payload = json.loads(manifest_path.read_text(encoding="utf-8"))

    with _running_portal(release_root, manifest_path) as base_url:
        matching_primary = json.loads(json.dumps(complete_payload))
        matching_primary["downloads"] = []
        manifest_path.write_text(json.dumps(matching_primary), encoding="utf-8")
        sibling_path.write_text(json.dumps(complete_payload), encoding="utf-8")
        matching_html = _http_get(f"{base_url}/downloads/")
        assert matching_html.count('data-download-availability="available"') == 1

        hybrid_cases: dict[str, tuple[dict[str, object], dict[str, object]]] = {}

        missing_release_version = json.loads(json.dumps(matching_primary))
        missing_release_version.pop("releaseVersion")
        hybrid_cases["missing_release_version"] = (
            missing_release_version,
            complete_payload,
        )

        missing_published_at = json.loads(json.dumps(matching_primary))
        missing_published_at.pop("publishedAt")
        hybrid_cases["missing_published_at"] = (
            missing_published_at,
            complete_payload,
        )

        missing_generated_at = json.loads(json.dumps(matching_primary))
        missing_generated_at.pop("generatedAt")
        missing_generated_at.pop("generated_at")
        hybrid_cases["missing_generated_at"] = (
            missing_generated_at,
            complete_payload,
        )

        generated_alias_drift = json.loads(json.dumps(matching_primary))
        generated_alias_drift["generated_at"] = "2026-07-25T12:00:01Z"
        hybrid_cases["generated_alias_drift"] = (
            generated_alias_drift,
            complete_payload,
        )

        different_version_sibling = json.loads(json.dumps(complete_payload))
        different_version_sibling["version"] = "run-20260725-125001"
        different_version_sibling["releaseVersion"] = "run-20260725-125001"
        for row in different_version_sibling["downloads"]:
            row["version"] = "run-20260725-125001"
            row["releaseVersion"] = "run-20260725-125001"
        hybrid_cases["different_version"] = (
            matching_primary,
            different_version_sibling,
        )

        different_date_sibling = json.loads(json.dumps(complete_payload))
        different_date_sibling["publishedAt"] = "2026-07-25T13:00:00Z"
        hybrid_cases["different_published_at"] = (
            matching_primary,
            different_date_sibling,
        )

        different_generated_at_sibling = json.loads(json.dumps(complete_payload))
        different_generated_at_sibling["generatedAt"] = "2026-07-25T12:00:01Z"
        different_generated_at_sibling["generated_at"] = "2026-07-25T12:00:01Z"
        hybrid_cases["different_generated_at"] = (
            matching_primary,
            different_generated_at_sibling,
        )

        global_profile_sibling = json.loads(json.dumps(complete_payload))
        global_profile_sibling["releaseProfile"] = "global_flagship"
        global_profile_sibling["schemaVersion"] = 2
        global_profile_sibling["contractVersion"] = 2
        hybrid_cases["different_release_profile"] = (
            matching_primary,
            global_profile_sibling,
        )

        for case_name, (primary_payload, sibling_payload) in hybrid_cases.items():
            manifest_path.write_text(json.dumps(primary_payload), encoding="utf-8")
            sibling_path.write_text(json.dumps(sibling_payload), encoding="utf-8")
            downloads_html = _http_get(f"{base_url}/downloads/")

            assert (
                downloads_html.count('data-download-availability="available"') == 0
            ), case_name
            assert (
                "https://chummer.run/downloads/files/" not in downloads_html
            ), case_name

        manifest_path.write_text(json.dumps(complete_payload), encoding="utf-8")
        sibling_path.write_text('{"status":"published","downloads":[', encoding="utf-8")
        malformed_sibling_html = _http_get(f"{base_url}/downloads/")
        assert (
            malformed_sibling_html.count('data-download-availability="available"') == 1
        )


def test_portal_runtime_withholds_global_macos_when_bound_evidence_is_invalid(
    tmp_path: Path,
    registry_verified_global_bundle: Path,
) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _copy_registry_global_bundle(
        registry_verified_global_bundle,
        release_root,
    )
    valid_payload = json.loads(manifest_path.read_text(encoding="utf-8"))

    with _running_portal(release_root, manifest_path) as base_url:
        for evidence_case in (
            "absent",
            "extra_top_level_key",
            "binding_contract",
            "source_contract",
            "source_extra_key",
            "source_digest",
            "candidate_bytes",
            "candidate_extra_key",
            "global_candidate_id",
            "global_generation_id",
            "global_release_version",
            "global_source_commit",
            "global_extra_key",
            "github_repository",
            "github_ref",
            "github_workflow",
            "github_actor_binding",
            "github_sha",
            "github_extra_key",
            "malformed_identity",
            "wrong_team_binding",
            "missing_certificate_hash",
            "missing_spki_hash",
            "signing_extra_key",
            "non_accepted_status",
            "malformed_submission_id",
            "notarization_extra_key",
            "missing_receipt_binding",
            "duplicate_receipt_path",
            "unsafe_receipt_path",
            "malformed_receipt_digest",
            "receipt_reference_extra_key",
        ):
            payload = json.loads(json.dumps(valid_payload))
            macos_row = next(
                row for row in payload["downloads"] if row["platform"] == "macos"
            )
            evidence = macos_row["macosFlagshipEvidence"]
            signing_identity = evidence["signingIdentity"]
            notarization = evidence["notarization"]
            receipt_bindings = evidence["receiptBindings"]
            macos_row["signingStatus"] = "passed"
            macos_row["notarizationStatus"] = "Accepted"

            if evidence_case == "absent":
                macos_row.pop("macosFlagshipEvidence")
            elif evidence_case == "extra_top_level_key":
                evidence["unexpectedAuthority"] = True
            elif evidence_case == "binding_contract":
                evidence["contractVersion"] = 2
            elif evidence_case == "source_contract":
                evidence["source"]["contractName"] = "unbound-source"
            elif evidence_case == "source_extra_key":
                evidence["source"]["unexpected"] = True
            elif evidence_case == "source_digest":
                evidence["source"]["sha256"] = "NOT-A-DIGEST"
            elif evidence_case == "candidate_bytes":
                evidence["candidate"]["sha256"] = "f" * 64
            elif evidence_case == "candidate_extra_key":
                evidence["candidate"]["unexpected"] = True
            elif evidence_case == "global_candidate_id":
                evidence["globalCandidateIdentity"]["candidateId"] = "other-candidate"
            elif evidence_case == "global_generation_id":
                evidence["globalCandidateIdentity"]["generationId"] = "bad/id"
            elif evidence_case == "global_release_version":
                evidence["globalCandidateIdentity"]["releaseVersion"] = "other-release"
            elif evidence_case == "global_source_commit":
                evidence["globalCandidateIdentity"]["sourceCommit"] = "f" * 40
            elif evidence_case == "global_extra_key":
                evidence["globalCandidateIdentity"]["unexpected"] = True
            elif evidence_case == "github_repository":
                evidence["github"]["repository"] = "example/fork"
            elif evidence_case == "github_ref":
                evidence["github"]["ref"] = "refs/heads/preview"
            elif evidence_case == "github_workflow":
                evidence["github"]["workflow"] = ".github/workflows/other.yml"
            elif evidence_case == "github_actor_binding":
                evidence["github"]["triggeringActor"] = "another-actor"
            elif evidence_case == "github_sha":
                evidence["github"]["sha"] = "f" * 40
            elif evidence_case == "github_extra_key":
                evidence["github"]["unexpected"] = True
            elif evidence_case == "malformed_identity":
                signing_identity["developerIdApplicationIdentity"] = "Developer ID Application: Chummer"
            elif evidence_case == "wrong_team_binding":
                signing_identity["teamId"] = "WRONGID123"
            elif evidence_case == "missing_certificate_hash":
                signing_identity.pop("certificateSha256")
            elif evidence_case == "missing_spki_hash":
                signing_identity.pop("certificateSpkiSha256")
            elif evidence_case == "signing_extra_key":
                signing_identity["unexpected"] = True
            elif evidence_case == "non_accepted_status":
                notarization["status"] = "Rejected"
            elif evidence_case == "malformed_submission_id":
                notarization["submissionId"] = "NOT-A-LOWERCASE-UUID"
            elif evidence_case == "notarization_extra_key":
                notarization["unexpected"] = True
            elif evidence_case == "missing_receipt_binding":
                receipt_bindings.pop("signingReceipt")
            elif evidence_case == "duplicate_receipt_path":
                receipt_bindings["signingReceipt"]["path"] = receipt_bindings[
                    "notaryResult"
                ]["path"]
            elif evidence_case == "unsafe_receipt_path":
                receipt_bindings["notaryResult"]["path"] = "../notary-result.json"
            elif evidence_case == "malformed_receipt_digest":
                receipt_bindings["notaryResult"]["sha256"] = "NOT-A-DIGEST"
            elif evidence_case == "receipt_reference_extra_key":
                receipt_bindings["notaryResult"]["unexpected"] = True

            manifest_path.write_text(json.dumps(payload), encoding="utf-8")
            downloads_html = _http_get(f"{base_url}/downloads/")

            assert (
                downloads_html.count('data-download-availability="available"') == 0
            ), evidence_case
            assert 'data-release-state="unavailable"' in downloads_html, evidence_case
            assert (
                'data-download-platform-card="macos" data-download-platform="macos" '
                'data-download-availability="unavailable"'
            ) in downloads_html, evidence_case
            assert (
                "Signed with Developer ID and notarized by Apple" not in downloads_html
            ), evidence_case


def test_portal_runtime_rejects_invalid_global_flagship_promotion_authority(
    tmp_path: Path,
    registry_verified_global_bundle: Path,
) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _copy_registry_global_bundle(
        registry_verified_global_bundle,
        release_root,
    )
    valid_payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    coverage_fields = (
        "requiredDesktopPlatforms",
        "requiredDesktopHeads",
        "promotedInstallerTuples",
        "promotedPlatformHeads",
        "requiredDesktopPlatformHeadRidTuples",
        "promotedPlatformHeadRidTuples",
        "missingRequiredPlatforms",
        "missingRequiredHeads",
        "missingRequiredPlatformHeadPairs",
        "missingRequiredPlatformHeadRidTuples",
        "externalProofRequests",
        "desktopRouteTruth",
        "complete",
    )

    with _running_portal(release_root, manifest_path) as base_url:
        for authority_case in (
            "schema_version",
            "channel_alias",
            "supportability",
            "desktop_coverage",
            *(f"coverage_missing:{field}" for field in coverage_fields),
            "coverage_extra_key",
            "coverage_promoted_tuple_binding",
            "coverage_promoted_platform_heads",
            "coverage_required_rid_tuples",
            "coverage_promoted_rid_tuples",
            "coverage_missing_array",
            "coverage_external_proof_request",
            "coverage_route_truth",
            "generated_aliases_missing",
            "generated_alias_drift",
            "generated_timestamp_invalid",
            "generated_timestamp_bad_month",
            "generated_timestamp_bad_time",
            "generated_timestamp_bad_offset",
            "artifact_id_missing",
            "artifact_head_casing",
            "artifact_url_missing",
            "artifact_id_alias_drift",
            "promotion_extra_key",
            "promotion_candidate",
            "promotion_release",
            "inventory_digest",
            "promotion_source",
            "promotion_reference",
            "assembly_repository",
            "assembly_ref",
            "assembly_workflow",
            "assembly_source_commit",
            "assembly_actor",
            "assembly_run_attempt",
        ):
            payload = json.loads(json.dumps(valid_payload))
            promotion = payload["channelPromotionAuthority"]
            assembly = promotion["assembly"]
            coverage = payload["desktopTupleCoverage"]

            if authority_case == "schema_version":
                payload["schemaVersion"] = 1
            elif authority_case == "channel_alias":
                payload["channelId"] = "preview"
            elif authority_case == "supportability":
                payload["supportabilityState"] = "best_effort"
            elif authority_case == "desktop_coverage":
                coverage["complete"] = False
            elif authority_case.startswith("coverage_missing:"):
                coverage.pop(authority_case.split(":", 1)[1])
            elif authority_case == "coverage_extra_key":
                coverage["unexpectedAuthority"] = True
            elif authority_case == "coverage_promoted_tuple_binding":
                coverage["promotedInstallerTuples"][0]["artifactId"] = "other-artifact"
            elif authority_case == "coverage_promoted_platform_heads":
                coverage["promotedPlatformHeads"]["linux"] = []
            elif authority_case == "coverage_required_rid_tuples":
                coverage["requiredDesktopPlatformHeadRidTuples"].pop()
            elif authority_case == "coverage_promoted_rid_tuples":
                coverage["promotedPlatformHeadRidTuples"].pop()
            elif authority_case == "coverage_missing_array":
                coverage["missingRequiredPlatforms"].append("macos")
            elif authority_case == "coverage_external_proof_request":
                coverage["externalProofRequests"].append({})
            elif authority_case == "coverage_route_truth":
                coverage["desktopRouteTruth"][0]["promotionState"] = "proof_required"
            elif authority_case == "generated_aliases_missing":
                payload.pop("generatedAt")
                payload.pop("generated_at")
            elif authority_case == "generated_alias_drift":
                payload["generated_at"] = "2026-07-25T15:51:40Z"
            elif authority_case == "generated_timestamp_invalid":
                payload["generatedAt"] = "not-a-timestamp"
                payload["generated_at"] = "not-a-timestamp"
            elif authority_case == "generated_timestamp_bad_month":
                payload["generatedAt"] = "2026-13-25T15:51:39Z"
                payload["generated_at"] = "2026-13-25T15:51:39Z"
            elif authority_case == "generated_timestamp_bad_time":
                payload["generatedAt"] = "2026-07-25T25:61:61Z"
                payload["generated_at"] = "2026-07-25T25:61:61Z"
            elif authority_case == "generated_timestamp_bad_offset":
                payload["generatedAt"] = "2026-07-25T15:51:39+24:00"
                payload["generated_at"] = "2026-07-25T15:51:39+24:00"
            elif authority_case == "artifact_id_missing":
                payload["downloads"][0].pop("artifactId")
            elif authority_case == "artifact_head_casing":
                payload["downloads"][0]["head"] = "Avalonia"
            elif authority_case == "artifact_url_missing":
                payload["downloads"][0].pop("url")
            elif authority_case == "artifact_id_alias_drift":
                payload["downloads"][0]["id"] = "different-artifact"
            elif authority_case == "promotion_extra_key":
                promotion["unexpectedAuthority"] = True
            elif authority_case == "promotion_candidate":
                promotion["candidateId"] = "different-candidate"
            elif authority_case == "promotion_release":
                promotion["releaseVersion"] = "different-release"
            elif authority_case == "inventory_digest":
                promotion["artifactInventorySha256"] = "f" * 64
            elif authority_case == "promotion_source":
                promotion["source"]["contractName"] = "unbound-source"
            elif authority_case == "promotion_reference":
                promotion["candidateManifest"]["path"] = "other-candidate.json"
            elif authority_case == "assembly_repository":
                assembly["repository"] = "example/fork"
            elif authority_case == "assembly_ref":
                assembly["ref"] = "refs/heads/preview"
            elif authority_case == "assembly_workflow":
                assembly["workflow"] = ".github/workflows/other.yml"
            elif authority_case == "assembly_source_commit":
                assembly["sha"] = "f" * 40
            elif authority_case == "assembly_actor":
                assembly["triggeringActor"] = "different-actor"
            elif authority_case == "assembly_run_attempt":
                assembly["runAttempt"] = 2

            manifest_path.write_text(json.dumps(payload), encoding="utf-8")
            downloads_html = _http_get(f"{base_url}/downloads/")

            assert (
                downloads_html.count('data-download-availability="available"') == 0
            ), authority_case
            assert 'data-release-state="unavailable"' in downloads_html, authority_case


def test_portal_status_reports_install_route_count_separately_from_downloads(
    tmp_path: Path,
) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _write_release_manifest(
        release_root,
        channel="public_stable",
        version="run-20260725-145000",
        platforms=("windows", "linux"),
    )
    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    payload["downloads"][0][
        "publicInstallRoute"
    ] = "/downloads/install/avalonia-win-x64-installer"
    manifest_path.write_text(json.dumps(payload), encoding="utf-8")

    with _running_portal(release_root, manifest_path) as base_url:
        status_html = _http_get(f"{base_url}/status")

    assert 'data-portal-status-artifact-count="2"' in status_html
    assert 'data-portal-status-install-route-count="1"' in status_html


def test_portal_runtime_home_links_to_truthful_contact_handoff() -> None:
    with _running_portal() as base_url:
        home_html = _http_get(f"{base_url}/")

    assert 'href="/contact"' in home_html
    assert 'data-portal-home-route="contact"' in home_html
    assert "Contact support" in home_html


def test_downloads_playwright_failure_removes_partial_pass_receipts(
    tmp_path: Path,
) -> None:
    receipt_dir = tmp_path / "downloads-polish-receipt"
    env = os.environ.copy()
    env["NODE_PATH"] = str(
        (REPO_ROOT / "chummer.run-services" / "node_modules").resolve()
    )
    env["CHUMMER_PORTAL_PLAYWRIGHT_SCOPE"] = "downloads"
    env["CHUMMER_PORTAL_DOWNLOADS_POLISH_RECEIPT_DIR"] = str(receipt_dir)
    env["CHUMMER_PORTAL_DOWNLOADS_POLISH_TEST_FAIL_AFTER_VIEWPORT"] = "desktop"

    with _running_portal() as base_url:
        env["CHUMMER_PORTAL_BASE_URL"] = base_url
        result = subprocess.run(
            ["node", str(REPO_ROOT / "scripts" / "e2e-portal-playwright.cjs")],
            cwd=REPO_ROOT,
            env=env,
            text=True,
            capture_output=True,
            timeout=120,
            check=False,
        )

    assert result.returncode != 0
    assert "Injected downloads polish failure after desktop evidence." in (
        result.stdout + result.stderr
    )
    assert not (
        receipt_dir / "DOWNLOADS_POLISH_JOURNEY.generated.json"
    ).exists()
    assert not (receipt_dir / "downloads-desktop.png").exists()
    assert not (receipt_dir / "downloads-mobile.png").exists()


def test_portal_runtime_keeps_open_public_installer_handoffs_on_the_self_hosted_edge() -> None:
    manifest = json.loads(PORTAL_RELEASES_FILE.read_text(encoding="utf-8"))
    primary_download = next(
        row for row in manifest["downloads"] if row.get("installAccessClass") == "open_public"
    )
    artifact_id = primary_download["artifactId"]
    expected_dispatch = f"/downloads/get/{artifact_id}"

    with _running_portal() as base_url:
        status, headers, _ = _http_request(
            f"{base_url}/downloads/install/{artifact_id}",
            follow_redirects=False,
        )
        get_status, get_headers, _ = _http_request(
            f"{base_url}{expected_dispatch}",
            headers={"Range": "bytes=0-0"},
            follow_redirects=False,
        )

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == expected_dispatch
    expected_local_file = PORTAL_DOWNLOADS_DIR / "files" / primary_download["fileName"]
    if expected_local_file.is_file():
        assert get_status in {200, 206}
        assert primary_download["fileName"] in get_headers.get("Content-Disposition", "")
    else:
        assert get_status in {301, 302, 303, 307, 308}
        assert get_headers.get("Location") == primary_download["url"]


def test_portal_runtime_redirects_public_app_route_to_hosted_blazor_app_and_preserves_query() -> None:
    with _running_portal() as base_url:
        status, headers, _ = _http_request(
            f"{base_url}/app?command=character_roster",
            follow_redirects=False,
        )
        slash_status, slash_headers, _ = _http_request(
            f"{base_url}/app/?command=new_character_origin",
            follow_redirects=False,
        )
        openapi = json.loads(_http_get(f"{base_url}/openapi/v1.json"))

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == "/blazor/app?command=character_roster"
    assert slash_status in {301, 302, 303, 307, 308}
    assert slash_headers.get("Location") == "/blazor/app?command=new_character_origin"
    assert isinstance(openapi.get("paths", {}).get("/app"), dict)
    assert isinstance(openapi.get("paths", {}).get("/blazor/app"), dict)


def test_portal_runtime_redirects_public_online_alias_to_hosted_blazor_app_and_preserves_query() -> None:
    with _running_portal() as base_url:
        status, headers, _ = _http_request(
            f"{base_url}/online?command=character_roster",
            follow_redirects=False,
        )
        slash_status, slash_headers, _ = _http_request(
            f"{base_url}/online/?command=new_character_origin",
            follow_redirects=False,
        )
        openapi = json.loads(_http_get(f"{base_url}/openapi/v1.json"))

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == "/blazor/app?command=character_roster"
    assert slash_status in {301, 302, 303, 307, 308}
    assert slash_headers.get("Location") == "/blazor/app?command=new_character_origin"
    assert isinstance(openapi.get("paths", {}).get("/online"), dict)
    assert isinstance(openapi.get("paths", {}).get("/blazor/app"), dict)
