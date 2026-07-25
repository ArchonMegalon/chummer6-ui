from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import sys
import zipfile
from datetime import UTC, datetime, timedelta
from email.message import Message
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "desktop_native_lifecycle_evidence.py"
SPEC = importlib.util.spec_from_file_location("desktop_native_lifecycle_evidence", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
LINUX_EXPORT_WORKFLOW = (
    REPO_ROOT / ".github/workflows/linux-native-candidate-export.yml"
)
LINUX_LIFECYCLE_WORKFLOW = (
    REPO_ROOT / ".github/workflows/linux-native-lifecycle-evidence.yml"
)
LINUX_LIFECYCLE_RUNNER = (
    REPO_ROOT / "scripts/run-linux-native-lifecycle-e2e.sh"
)


def canonical(value: object) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def timestamp(offset: int = 0) -> str:
    return (
        datetime.now(UTC).replace(microsecond=0) + timedelta(seconds=offset)
    ).isoformat().replace("+00:00", "Z")


def n_minus_one_binding() -> dict[str, object]:
    generation = "g-20260720T120000Z-previous"
    name = "chummer-avalonia-linux-x64-installer.deb"
    return {
        "artifactFileName": name,
        "artifactSha256": "1" * 64,
        "artifactSizeBytes": 1024,
        "artifactUrl": f"https://chummer.run/downloads/generations/{generation}/files/{name}",
        "contractName": MODULE.N_MINUS_ONE_CONTRACT,
        "contractVersion": 1,
        "generationId": generation,
        "manifestSha256": "2" * 64,
        "manifestUrl": (
            f"https://chummer.run/downloads/generations/{generation}/"
            "RELEASE_CHANNEL.generated.json"
        ),
        "platform": "linux",
        "releasedAt": timestamp(-3600),
        "rid": "linux-x64",
        "version": "run-20260720-120000",
    }


def windows_n_minus_one_binding() -> dict[str, object]:
    binding = n_minus_one_binding()
    generation = str(binding["generationId"])
    installer = "chummer-avalonia-win-x64-installer.exe"
    payload = "chummer-avalonia-win-x64-payload.zip"
    binding.update(
        {
            "artifactFileName": installer,
            "artifactUrl": (
                f"https://chummer.run/downloads/g/{generation}/files/{installer}"
            ),
            "platform": "windows",
            "rid": "win-x64",
            "payloadFileName": payload,
            "payloadSha256": "3" * 64,
            "payloadSizeBytes": 2048,
            "payloadUrl": (
                f"https://chummer.run/downloads/g/{generation}/files/{payload}"
            ),
        }
    )
    return binding


def release_channel_manifest(
    binding: dict[str, object],
) -> dict[str, object]:
    artifact_id = MODULE.LIVE_PREDECESSOR_ARTIFACT_IDS[
        str(binding["platform"])
    ]
    artifact = {
        "artifactId": artifact_id,
        "downloadUrl": str(binding["artifactUrl"]).removeprefix(
            "https://chummer.run"
        ),
        "fileName": binding["artifactFileName"],
        "id": artifact_id,
        "platform": binding["platform"],
        "releaseVersion": binding["version"],
        "rid": binding["rid"],
        "sha256": binding["artifactSha256"],
        "sizeBytes": binding["artifactSizeBytes"],
        "version": binding["version"],
    }
    if binding["platform"] == "windows":
        artifact.update(
            {
                "executionEnvironment": "native_windows",
                "nativeHostEvidence": {
                    "contractName": "chummer6-ui.native_windows_host_evidence",
                    "hostPlatform": "windows",
                    "isNativeWindows": True,
                    "status": "verified",
                },
                "payloadDownloadUrl": (
                    "/downloads/g/"
                    f"{binding['generationId']}/install/"
                    f"{artifact_id}/payload"
                ),
                "payloadFileName": binding["payloadFileName"],
                "payloadSha256": binding["payloadSha256"],
                "payloadSizeBytes": binding["payloadSizeBytes"],
                "verificationScope": "native_windows_startup",
            }
        )
    return {
        "artifacts": [artifact],
        "contractName": "Chummer.Hub.Registry.Contracts",
        "generationId": binding["generationId"],
        "publishedAt": binding["releasedAt"],
        "releaseVersion": binding["version"],
        "schemaVersion": 1,
        "status": "published",
        "version": binding["version"],
    }


def test_windows_relay_authority_binds_canonical_n_minus_one_and_signer_pins() -> None:
    raw = canonical(windows_n_minus_one_binding())
    live_raw = json.dumps(release_channel_manifest(windows_n_minus_one_binding()))
    result = MODULE.validate_windows_relay_authority(
        raw, live_raw, "8" * 64, "9" * 64
    )
    assert result == {
        "artifactSha256": "1" * 64,
        "certificateSha256": "8" * 64,
        "generationId": "g-20260720T120000Z-previous",
        "liveReleaseChannelSha256": hashlib.sha256(
            live_raw.encode()
        ).hexdigest(),
        "manifestSha256": "2" * 64,
        "payloadSha256": "3" * 64,
        "selectedTupleSha256": result["selectedTupleSha256"],
        "sha256": hashlib.sha256(raw.encode()).hexdigest(),
        "spkiSha256": "9" * 64,
        "version": "run-20260720-120000",
    }


@pytest.mark.parametrize(
    ("mutation", "expected"),
    [
        (lambda value: value.pop("payloadUrl"), "missing keys"),
        (lambda value: value.update(extra="rejected"), "extra keys"),
        (lambda value: value.update(platform="linux"), "platform tuple"),
        (lambda value: value.update(rid="linux-x64"), "platform tuple"),
    ],
)
def test_windows_relay_authority_rejects_structural_drift(
    mutation, expected: str
) -> None:
    binding = windows_n_minus_one_binding()
    live_raw = json.dumps(release_channel_manifest(binding))
    mutation(binding)
    with pytest.raises(MODULE.ContractError, match=expected):
        MODULE.validate_windows_relay_authority(
            canonical(binding), live_raw, "8" * 64, "9" * 64
        )


def test_windows_relay_authority_rejects_noncanonical_or_mutated_bytes() -> None:
    binding = windows_n_minus_one_binding()
    canonical_raw = canonical(binding)
    live_raw = json.dumps(release_channel_manifest(binding))
    with pytest.raises(MODULE.ContractError, match="canonical JSON"):
        MODULE.validate_windows_relay_authority(
            json.dumps(binding, indent=2), live_raw, "8" * 64, "9" * 64
        )
    with pytest.raises(MODULE.ContractError, match="expected SHA-256"):
        MODULE.validate_windows_relay_authority(
            canonical_raw,
            live_raw,
            "8" * 64,
            "9" * 64,
            expected_sha256="a" * 64,
        )


@pytest.mark.parametrize("certificate,spki", [("", "9" * 64), ("8" * 64, "A" * 64)])
def test_windows_relay_authority_requires_exact_signer_pins(
    certificate: str, spki: str
) -> None:
    with pytest.raises(MODULE.ContractError, match="signer"):
        binding = windows_n_minus_one_binding()
        MODULE.validate_windows_relay_authority(
            canonical(binding),
            json.dumps(release_channel_manifest(binding)),
            certificate,
            spki,
        )


@pytest.mark.parametrize(
    ("platform", "rid", "file_name"),
    [
        ("linux", "linux-x64", "chummer-avalonia-linux-x64-installer.deb"),
        ("macos", "osx-arm64", "chummer-avalonia-osx-arm64-installer.dmg"),
    ],
)
def test_live_predecessor_validator_is_platform_generic(
    platform: str,
    rid: str,
    file_name: str,
) -> None:
    binding = n_minus_one_binding()
    binding.update(
        {
            "artifactFileName": file_name,
            "artifactUrl": (
                "https://chummer.run/downloads/g/"
                f"{binding['generationId']}/files/{file_name}"
            ),
            "platform": platform,
            "rid": rid,
        }
    )
    binding_raw = canonical(binding)
    live_raw = json.dumps(release_channel_manifest(binding), indent=2)
    first = MODULE.validate_live_predecessor_authority(
        binding_raw,
        live_raw,
        platform,
        rid,
    )
    second = MODULE.validate_live_predecessor_authority(
        binding_raw,
        live_raw,
        platform,
        rid,
        expected_n_minus_one_sha256=first["nMinusOneReleaseSha256"],
        expected_live_release_channel_sha256=first[
            "liveReleaseChannelSha256"
        ],
        expected_selected_tuple_sha256=first["selectedTupleSha256"],
    )
    assert second == first
    assert first["liveReleaseChannelSha256"] == hashlib.sha256(
        live_raw.encode()
    ).hexdigest()


@pytest.mark.parametrize(
    ("mutation", "expected"),
    [
        (
            lambda manifest: manifest.update(generationId="g-substituted"),
            "generation is invalid",
        ),
        (
            lambda manifest: manifest["artifacts"][0].update(sha256="0" * 64),
            "artifact sha256 differs",
        ),
        (
            lambda manifest: manifest["artifacts"][0].update(
                payloadSha256="0" * 64
            ),
            "payloadSha256 differs",
        ),
        (
            lambda manifest: manifest["artifacts"][0].update(
                downloadUrl="/downloads/g/g-substituted/files/installer.exe"
            ),
            "artifact URL",
        ),
        (
            lambda manifest: manifest["artifacts"][0].update(
                executionEnvironment="wine_compatibility",
                nativeHostEvidence={
                    "contractName": "chummer6-ui.native_windows_host_evidence",
                    "hostPlatform": "linux",
                    "isNativeWindows": False,
                    "status": "not_native",
                },
                verificationScope="windows_compatibility_startup",
            ),
            "lacks verified native-host flagship evidence",
        ),
        (
            lambda manifest: manifest.update(artifacts=[]),
            "does not select one exact flagship artifact",
        ),
        (
            lambda manifest: manifest.pop("generationId"),
            "generation is invalid",
        ),
    ],
)
def test_live_predecessor_rejects_public_root_selection_drift(
    mutation,
    expected: str,
) -> None:
    binding = windows_n_minus_one_binding()
    manifest = release_channel_manifest(binding)
    mutation(manifest)
    with pytest.raises(MODULE.ContractError, match=expected):
        MODULE.validate_live_predecessor_authority(
            canonical(binding),
            json.dumps(manifest),
            "windows",
            "win-x64",
        )


def test_live_predecessor_expected_hashes_bind_exact_raw_bytes() -> None:
    binding = windows_n_minus_one_binding()
    binding_raw = canonical(binding)
    live_raw = json.dumps(release_channel_manifest(binding))
    validated = MODULE.validate_live_predecessor_authority(
        binding_raw,
        live_raw,
        "windows",
        "win-x64",
    )
    with pytest.raises(MODULE.ContractError, match="live release-channel"):
        MODULE.validate_live_predecessor_authority(
            binding_raw,
            live_raw + "\n",
            "windows",
            "win-x64",
            expected_live_release_channel_sha256=validated[
                "liveReleaseChannelSha256"
            ],
        )
    with pytest.raises(MODULE.ContractError, match="selected tuple"):
        MODULE.validate_live_predecessor_authority(
            binding_raw,
            live_raw,
            "windows",
            "win-x64",
            expected_selected_tuple_sha256="f" * 64,
        )


class FakeLiveResponse:
    def __init__(
        self,
        data: bytes,
        *,
        status: int = 200,
        url: str = MODULE.LIVE_RELEASE_CHANNEL_URL,
        content_encoding: str | None = None,
    ) -> None:
        self.status = status
        self._url = url
        self._stream = io.BytesIO(data)
        self.headers = Message()
        self.headers["Content-Length"] = str(len(data))
        if content_encoding is not None:
            self.headers["Content-Encoding"] = content_encoding

    def __enter__(self) -> "FakeLiveResponse":
        return self

    def __exit__(self, *_: object) -> None:
        return None

    def geturl(self) -> str:
        return self._url

    def read(self, size: int = -1) -> bytes:
        return self._stream.read(size)


class FakeLiveOpener:
    def __init__(self, response: FakeLiveResponse) -> None:
        self.response = response
        self.request = None

    def open(self, request, timeout: int):
        self.request = request
        assert timeout == 60
        return self.response


def test_live_predecessor_fetch_is_pinned_uncached_and_byte_stable(
    tmp_path: Path,
) -> None:
    binding = n_minus_one_binding()
    raw = json.dumps(release_channel_manifest(binding))
    opener = FakeLiveOpener(FakeLiveResponse(raw.encode("utf-8")))
    output = tmp_path / "live-root.json"
    result = MODULE.fetch_live_predecessor_authority(
        canonical(binding),
        raw,
        "linux",
        "linux-x64",
        output_live_release_channel=output,
        opener=opener,
    )
    assert output.read_bytes() == raw.encode("utf-8")
    assert result["liveReleaseChannelSha256"] == sha256(output)
    assert opener.request.full_url == MODULE.LIVE_RELEASE_CHANNEL_URL
    assert opener.request.get_header("Accept-encoding") == "identity"
    assert opener.request.get_header("Cache-control") == (
        "no-cache, no-store, max-age=0"
    )
    assert opener.request.get_header("Pragma") == "no-cache"


@pytest.mark.parametrize(
    ("response", "expected"),
    [
        (FakeLiveResponse(b"{}", status=503), "non-2xx"),
        (
            FakeLiveResponse(b"{}", url="https://chummer.run/redirected.json"),
            "redirect",
        ),
        (FakeLiveResponse(b"{}", content_encoding="gzip"), "encoded bytes"),
        (
            FakeLiveResponse(
                b"x" * (MODULE.MAX_LIVE_RELEASE_CHANNEL_BYTES + 1)
            ),
            "fixed bound",
        ),
    ],
)
def test_live_root_fetch_rejects_transport_drift(
    response: FakeLiveResponse,
    expected: str,
) -> None:
    with pytest.raises(MODULE.ContractError, match=expected):
        MODULE.fetch_live_release_channel_bytes(
            opener=FakeLiveOpener(response)
        )


@pytest.mark.parametrize(
    ("declared_length", "duplicate", "expected"),
    [
        ("1", False, "differ from Content-Length"),
        ("2", True, "duplicate Content-Length"),
    ],
)
def test_live_root_fetch_rejects_content_length_drift(
    declared_length: str,
    duplicate: bool,
    expected: str,
) -> None:
    response = FakeLiveResponse(b"{}")
    if duplicate:
        response.headers.add_header("Content-Length", declared_length)
    else:
        response.headers.replace_header("Content-Length", declared_length)

    with pytest.raises(MODULE.ContractError, match=expected):
        MODULE.fetch_live_release_channel_bytes(
            opener=FakeLiveOpener(response)
        )


@pytest.mark.parametrize(
    ("mutate", "expected"),
    [
        (
            lambda raw: raw.replace(
                '"status": "published"',
                '"status": "published", "status": "published"',
            ),
            "duplicate key",
        ),
        (
            lambda raw: raw.replace('"schemaVersion": 1', '"schemaVersion": NaN'),
            "non-finite",
        ),
        (
            lambda raw: raw.replace(
                '"generationId":', '"unexpectedGenerationId":'
            ),
            "generation is invalid",
        ),
    ],
)
def test_fetched_live_root_rejects_json_and_shape_drift(
    mutate,
    expected: str,
) -> None:
    binding = n_minus_one_binding()
    raw = mutate(json.dumps(release_channel_manifest(binding)))
    with pytest.raises(MODULE.ContractError, match=expected):
        MODULE.fetch_live_predecessor_authority(
            canonical(binding),
            raw,
            "linux",
            "linux-x64",
            opener=FakeLiveOpener(
                FakeLiveResponse(raw.encode("utf-8"))
            ),
        )


def test_fetched_live_root_rejects_cross_boundary_byte_change() -> None:
    binding = n_minus_one_binding()
    raw = json.dumps(release_channel_manifest(binding))
    with pytest.raises(MODULE.ContractError, match="changed across"):
        MODULE.fetch_live_predecessor_authority(
            canonical(binding),
            raw + "\n",
            "linux",
            "linux-x64",
            opener=FakeLiveOpener(
                FakeLiveResponse(raw.encode("utf-8"))
            ),
        )


def test_linux_boundaries_all_refetch_and_thread_live_predecessor() -> None:
    export = LINUX_EXPORT_WORKFLOW.read_text(encoding="utf-8")
    lifecycle = LINUX_LIFECYCLE_WORKFLOW.read_text(encoding="utf-8")
    runner = LINUX_LIFECYCLE_RUNNER.read_text(encoding="utf-8")

    assert "live_release_channel_json:" in export
    assert export.count("fetch-live-predecessor-authority") == 3
    assert "livePredecessorAuthority" in export
    assert '"contractVersion": 2' in export
    assert "live_release_channel_json: process.env.LIVE_RELEASE_CHANNEL_JSON" in export
    assert "live_release_channel_json:" in lifecycle
    assert lifecycle.count("fetch-live-predecessor-authority") == 1
    assert "--expected-selected-tuple-sha256" in lifecycle
    assert "materialize-linux-desktop-exit-gate.sh" in lifecycle
    assert (
        "UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
        in lifecycle
    )
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY=1" in lifecycle
    assert "CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER=1" in lifecycle
    assert 'gate_evidence_root="$EVIDENCE_ROOT/canonical-linux-exit-gate"' in lifecycle
    assert "canonical Linux exit-gate evidence contains a link" in lifecycle
    assert '"chummer6-ui.linux_desktop_exit_gate"' in lifecycle
    assert "dotnet-version: 10.0.103" in lifecycle
    assert runner.count("fetch-live-predecessor-authority") == 1
    assert "--output-live-release-channel" in runner
    assert '"contractVersion": 2' in runner
    for digest in (
        "n_minus_one_release_sha256",
        "live_release_channel_sha256",
        "selected_tuple_sha256",
    ):
        assert digest in export
        assert digest in lifecycle
    for field in (
        "nMinusOneReleaseSha256",
        "liveReleaseChannelSha256",
        "selectedTupleSha256",
    ):
        assert field in runner


def write_n_minus_one_manifest(
    path: Path, binding: dict[str, object]
) -> dict[str, object]:
    manifest = release_channel_manifest(binding)
    path.write_text(json.dumps(manifest) + "\n")
    binding["manifestSha256"] = sha256(path)
    return manifest


def candidate_binding(candidate_path: Path) -> dict[str, object]:
    previous = n_minus_one_binding()
    predecessor = MODULE.validate_live_predecessor_authority(
        canonical(previous),
        json.dumps(release_channel_manifest(previous)),
        "linux",
        "linux-x64",
    )
    return {
        "artifactFileName": candidate_path.name,
        "artifactMemberPath": f"files/{candidate_path.name}",
        "artifactSha256": sha256(candidate_path),
        "artifactSizeBytes": candidate_path.stat().st_size,
        "contractName": MODULE.CANDIDATE_CONTRACT,
        "contractVersion": 2,
        "livePredecessorAuthority": {
            "liveReleaseChannelSha256": predecessor[
                "liveReleaseChannelSha256"
            ],
            "nMinusOneReleaseSha256": predecessor[
                "nMinusOneReleaseSha256"
            ],
            "selectedTupleSha256": predecessor["selectedTupleSha256"],
        },
        "platform": "linux",
        "producedAt": timestamp(-60),
        "producer": {
            "actor": "github-actions[bot]",
            "artifactId": "1234",
            "artifactName": (
                "global-flagship-candidate-payload-"
                "candidate-20260725-1234-1"
            ),
            "artifactZipSha256": "3" * 64,
            "ref": "refs/heads/main",
            "repository": "ArchonMegalon/chummer6-ui",
            "runAttempt": "1",
            "runId": "1234",
            "sha": "4" * 40,
            "workflow": MODULE.LINUX_CANDIDATE_PRODUCER_WORKFLOW,
        },
        "rid": "linux-x64",
        "version": "run-20260725-120000",
    }


def write_passing_json(
    path: Path,
    marker: str,
    *,
    version: str,
    artifact_sha256: str,
    platform: str = "linux",
    rid: str = "linux-x64",
) -> dict[str, object]:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload: dict[str, object] = {
        "arch": "x64",
        "artifactDigest": f"sha256:{artifact_sha256}",
        "artifactDigestSource": "environment",
        "headId": "avalonia",
        "hostClass": f"github-actions-{platform}-x64",
        "marker": marker,
        "platform": platform,
        "releaseVersion": version,
        "rid": rid,
        "status": "pass",
        "version": version,
    }
    if "startup" in marker:
        payload["readyCheckpoint"] = "pre_ui_event_loop"
    else:
        payload.update(
            {
                "error": None,
                "journeyMode": "mouse_first_live_binary",
                "pointerActionCount": 1,
                "steps": ["opened-and-saved-character"],
            }
        )
    path.write_text(json.dumps(payload) + "\n")
    return {
        "path": path.name,
        "role": marker,
        "sha256": sha256(path),
        "sizeBytes": path.stat().st_size,
    }


def passing_receipt(root: Path) -> tuple[Path, dict[str, object]]:
    previous = n_minus_one_binding()
    manifest_path = root / "n-minus-one-release-manifest.json"
    write_n_minus_one_manifest(manifest_path, previous)
    manifest_binding = {
        "path": manifest_path.name,
        "role": "n-minus-one-release-manifest",
        "sha256": previous["manifestSha256"],
        "sizeBytes": manifest_path.stat().st_size,
    }
    live_release_raw = json.dumps(release_channel_manifest(previous))
    live_release_path = root / "live-release-channel-root.json"
    live_release_path.write_text(live_release_raw)
    live_release_binding = {
        "path": live_release_path.name,
        "role": "live-release-channel-root",
        "sha256": sha256(live_release_path),
        "sizeBytes": live_release_path.stat().st_size,
    }
    live_predecessor = MODULE.validate_live_predecessor_authority(
        canonical(previous),
        live_release_raw,
        "linux",
        "linux-x64",
    )
    candidate_version = "run-20260725-120000"
    candidate_sha256 = "b" * 64
    roles = (
        "candidate-core-mouse-first",
        "candidate-core-startup",
        "n-minus-one-core-mouse-first",
        "n-minus-one-core-startup",
    )
    bindings = {
        role: write_passing_json(
            root / f"{role}.json",
            role,
            version=(
                candidate_version
                if role.startswith("candidate-")
                else str(previous["version"])
            ),
            artifact_sha256=(
                candidate_sha256
                if role.startswith("candidate-")
                else str(previous["artifactSha256"])
            ),
        )
        for role in roles
    }
    started = datetime.now(UTC).replace(microsecond=0) - timedelta(minutes=2)
    phases = []
    details = {
        "artifact_authentication": {
            "candidateDigestVerified": True,
            "liveReleaseRootVerified": True,
            "nMinusOneDigestVerified": True,
            "nativePackageAuthorityVerified": True,
        },
        "clean_install_n_minus_one": {"installed": True, "launcherPresent": True},
        "core_workflow_n_minus_one": {
            "mouseFirstJourneyPassed": True,
            "startupSmokePassed": True,
        },
        "update_to_candidate": {
            "candidateBytesInstalled": True,
            "installedVersionChanged": True,
            "statePreserved": True,
        },
        "core_workflow_candidate": {
            "mouseFirstJourneyPassed": True,
            "startupSmokePassed": True,
        },
        "normal_uninstall": {
            "launcherAbsent": True,
            "packageAbsent": True,
            "uninstallerInvoked": True,
        },
    }
    for index, name in enumerate(MODULE.PHASES):
        phase_start = started + timedelta(seconds=index * 10)
        phases.append(
            {
                "completedAt": (phase_start + timedelta(seconds=5))
                .isoformat()
                .replace("+00:00", "Z"),
                "details": details[name],
                "name": name,
                "startedAt": phase_start.isoformat().replace("+00:00", "Z"),
                "status": "passed",
            }
        )
    sentinel = "a" * 64
    receipt = {
        "candidate": {
            "artifactFileName": "chummer-avalonia-linux-x64-installer.deb",
            "sha256": candidate_sha256,
            "sizeBytes": 2048,
            "sourceCommit": "c" * 40,
            "version": candidate_version,
        },
        "contractName": MODULE.RECEIPT_CONTRACT,
        "contractVersion": 2,
        "coreWorkflow": {
            "candidate": {
                "mouseFirstReceipt": bindings["candidate-core-mouse-first"],
                "startupReceipt": bindings["candidate-core-startup"],
            },
            "nMinusOne": {
                "mouseFirstReceipt": bindings["n-minus-one-core-mouse-first"],
                "startupReceipt": bindings["n-minus-one-core-startup"],
            },
        },
        "evidenceFiles": sorted(
            [*bindings.values(), live_release_binding, manifest_binding],
            key=lambda row: str(row["path"]),
        ),
        "generatedAt": timestamp(),
        "livePredecessorAuthority": {
            "liveReleaseChannel": live_release_binding,
            "liveReleaseChannelSha256": live_predecessor[
                "liveReleaseChannelSha256"
            ],
            "nMinusOneReleaseSha256": live_predecessor[
                "nMinusOneReleaseSha256"
            ],
            "selectedTupleSha256": live_predecessor[
                "selectedTupleSha256"
            ],
            "url": MODULE.LIVE_RELEASE_CHANNEL_URL,
        },
        "nMinusOne": {
            "artifactFileName": previous["artifactFileName"],
            "artifactUrl": previous["artifactUrl"],
            "generationId": previous["generationId"],
            "manifestSha256": previous["manifestSha256"],
            "manifestUrl": previous["manifestUrl"],
            "releasedAt": previous["releasedAt"],
            "sha256": previous["artifactSha256"],
            "sizeBytes": previous["artifactSizeBytes"],
            "version": previous["version"],
        },
        "nativeRunner": {
            "architecture": "x64",
            "environment": "native",
            "kernel": "Linux",
            "runnerName": "GitHub-Actions",
            "runnerOs": "Linux",
            "source": {
                "actor": "github-actions[bot]",
                "ref": "refs/heads/main",
                "repository": "ArchonMegalon/chummer6-ui",
                "rerunPolicy": "same-actor-only",
                "runAttempt": "1",
                "runId": "5678",
                "sha": "c" * 40,
                "triggeringActor": "github-actions[bot]",
                "workflow": ".github/workflows/linux-native-lifecycle-evidence.yml",
            },
        },
        "packageAuthority": {
            "candidate": {
                "architecture": "amd64",
                "packageName": "chummer6-avalonia",
                "packageVersion": "2.0.0",
            },
            "manifestSha256": previous["manifestSha256"],
            "manifestReceipt": manifest_binding,
            "mode": "debian-package-metadata-and-immutable-manifest",
            "nMinusOne": {
                "architecture": "amd64",
                "packageName": "chummer6-avalonia",
                "packageVersion": "1.0.0",
            },
        },
        "phases": phases,
        "platform": "linux",
        "rid": "linux-x64",
        "statePreservation": {
            "preservedAfterUninstall": True,
            "preservedAfterUpdate": True,
            "sentinelSha256AfterUninstall": sentinel,
            "sentinelSha256AfterUpdate": sentinel,
            "sentinelSha256BeforeUpdate": sentinel,
        },
        "status": "passed",
        "uninstall": {
            "installRootRemoved": True,
            "launchersRemoved": True,
            "mode": "dpkg-purge",
            "statusAfter": "not-installed",
        },
    }
    receipt_path = root / "lifecycle-receipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
    return receipt_path, receipt


def passing_windows_receipt(root: Path) -> tuple[Path, dict[str, object]]:
    receipt_path, receipt = passing_receipt(root)
    receipt["contractVersion"] = 2
    generation = receipt["nMinusOne"]["generationId"]
    receipt["platform"] = "windows"
    receipt["rid"] = "win-x64"
    receipt["nativeRunner"]["runnerOs"] = "Windows"
    receipt["nativeRunner"]["kernel"] = "Microsoft-Windows-NT-10.0"
    receipt["nativeRunner"]["source"]["workflow"] = (
        ".github/workflows/windows-native-evidence-capture.yml"
    )
    for binding in receipt["evidenceFiles"]:
        if "-core-" not in str(binding["role"]):
            continue
        core_path = root / str(binding["path"])
        core_payload = json.loads(core_path.read_text())
        core_payload["hostClass"] = "github-actions-windows-x64"
        core_payload["platform"] = "windows"
        core_payload["rid"] = "win-x64"
        core_path.write_text(json.dumps(core_payload) + "\n")
        binding["sha256"] = sha256(core_path)
        binding["sizeBytes"] = core_path.stat().st_size
    receipt["candidate"].update(
        {
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "payload": {
                "fileName": "chummer-avalonia-win-x64-payload.zip",
                "sha256": "6" * 64,
                "sizeBytes": 4096,
            },
        }
    )
    receipt["nMinusOne"].update(
        {
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactUrl": (
                f"https://chummer.run/downloads/generations/{generation}/files/"
                "chummer-avalonia-win-x64-installer.exe"
            ),
            "payload": {
                "fileName": "chummer-avalonia-win-x64-payload.zip",
                "sha256": "7" * 64,
                "sizeBytes": 3072,
                "url": (
                    f"https://chummer.run/downloads/generations/{generation}/files/"
                    "chummer-avalonia-win-x64-payload.zip"
                ),
            },
        }
    )
    manifest_binding = next(
        row
        for row in receipt["evidenceFiles"]
        if row["role"] == "n-minus-one-release-manifest"
    )
    windows_previous = MODULE.receipt_n_minus_one_binding(
        receipt["nMinusOne"], "windows", "win-x64"
    )
    write_n_minus_one_manifest(root / manifest_binding["path"], windows_previous)
    receipt["nMinusOne"]["manifestSha256"] = windows_previous["manifestSha256"]
    manifest_binding["sha256"] = windows_previous["manifestSha256"]
    manifest_binding["sizeBytes"] = (root / manifest_binding["path"]).stat().st_size
    live_binding = receipt["livePredecessorAuthority"][
        "liveReleaseChannel"
    ]
    live_path = root / str(live_binding["path"])
    live_raw = json.dumps(release_channel_manifest(windows_previous))
    live_path.write_text(live_raw)
    live_predecessor = MODULE.validate_live_predecessor_authority(
        canonical(windows_previous),
        live_raw,
        "windows",
        "win-x64",
    )
    live_binding["sha256"] = sha256(live_path)
    live_binding["sizeBytes"] = live_path.stat().st_size
    receipt["livePredecessorAuthority"].update(
        {
            "liveReleaseChannelSha256": live_predecessor[
                "liveReleaseChannelSha256"
            ],
            "nMinusOneReleaseSha256": live_predecessor[
                "nMinusOneReleaseSha256"
            ],
            "selectedTupleSha256": live_predecessor[
                "selectedTupleSha256"
            ],
        }
    )
    cert = "8" * 64
    spki = "9" * 64
    source = receipt["nativeRunner"]["source"]

    def auth_file(name: str, artifact: dict[str, object], role: str) -> dict[str, object]:
        path = root / name
        path.write_text(
            json.dumps(
                {
                    "artifact": {
                        "fileName": artifact["artifactFileName"],
                        "sha256": artifact["sha256"],
                        "sizeBytes": artifact["sizeBytes"],
                    },
                    "contractName": "chummer6-ui.windows-authenticode-verification",
                    "contractVersion": 1,
                    "policy": {
                        "signerCertificateSha256": cert,
                        "signerSpkiSha256": spki,
                    },
                    "signer": {
                        "certificateSha256": cert,
                        "spkiSha256": spki,
                    },
                    "source": source,
                    "status": "verified",
                }
            )
            + "\n"
        )
        return {
            "path": name,
            "role": role,
            "sha256": sha256(path),
            "sizeBytes": path.stat().st_size,
        }

    candidate_auth = auth_file(
        "candidate-authenticode.json",
        receipt["candidate"],
        "candidate-authenticode",
    )
    old_auth = auth_file(
        "n-minus-one-authenticode.json",
        receipt["nMinusOne"],
        "n-minus-one-authenticode",
    )
    signing_path = root / "candidate-v2-signing-receipt.json"
    signer = {
        "certificateSha256": cert,
        "spkiSha256": spki,
    }
    signing_path.write_text(
        json.dumps(
            {
                "artifactSignatures": [
                    {
                        "artifactFileName": receipt["candidate"]["artifactFileName"],
                        "artifactSha256": receipt["candidate"]["sha256"],
                        "cryptographicVerification": "passed",
                        "digestAlgorithm": "sha256",
                        "signer": signer,
                        "signerChain": {"trusted": True},
                        "timestamp": {
                            "chain": {"trusted": True},
                            "digestAlgorithm": "sha256",
                            "format": "rfc3161",
                            "status": "verified",
                        },
                        "verifier": {
                            "jsignOutputTrusted": False,
                            "providerIndependent": True,
                        },
                    }
                ],
                "artifacts": [
                    {
                        "fileName": receipt["candidate"]["artifactFileName"],
                        "kind": "installer",
                        "sha256": receipt["candidate"]["sha256"],
                        "signingStatus": "pass",
                    }
                ],
                "contractName": "chummer6-ui.desktop_artifact_signing",
                "contractVersion": 2,
                "digestAlgorithm": "sha256",
                "platform": "windows",
                "releaseVersion": receipt["candidate"]["version"],
                "rid": "win-x64",
                "signer": signer,
                "signingBackend": "digicert_keylocker_linux_jsign",
                "signingStatus": "pass",
                "timestamp": {
                    "digestAlgorithm": "sha256",
                    "protocol": "rfc3161",
                    "status": "verified",
                },
            }
        )
        + "\n"
    )
    signing = {
        "path": signing_path.name,
        "role": "candidate-v2-signing-receipt",
        "sha256": sha256(signing_path),
        "sizeBytes": signing_path.stat().st_size,
    }
    receipt["packageAuthority"] = {
        "candidate": {
            "authenticodeReceipt": candidate_auth,
            "signingReceipt": signing,
        },
        "expectedSignerCertificateSha256": cert,
        "expectedSignerSpkiSha256": spki,
        "manifestReceipt": manifest_binding,
        "mode": "authenticode",
        "nMinusOne": {"authenticodeReceipt": old_auth},
    }
    receipt["evidenceFiles"].extend([candidate_auth, old_auth, signing])
    receipt["evidenceFiles"].sort(key=lambda row: str(row["path"]))
    receipt["uninstall"]["mode"] = "registered-cached-uninstaller"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
    return receipt_path, receipt


def test_validates_exact_n_minus_one_and_candidate_bindings(tmp_path: Path) -> None:
    previous = n_minus_one_binding()
    assert MODULE.validate_n_minus_one(
        canonical(previous), "linux", "linux-x64"
    )["generationId"] == previous["generationId"]

    candidate_root = tmp_path / "candidate"
    candidate = candidate_root / "files" / "chummer-avalonia-linux-x64-installer.deb"
    candidate.parent.mkdir(parents=True)
    candidate.write_bytes(b"candidate-package-bytes")
    binding = candidate_binding(candidate)
    validated = MODULE.validate_candidate(
        canonical(binding), "linux", "linux-x64", candidate_root
    )
    assert validated["resolvedPath"] == str(candidate)


def test_linux_candidate_rejects_ungoverned_producer_workflow(
    tmp_path: Path,
) -> None:
    candidate = tmp_path / "chummer-avalonia-linux-x64-installer.deb"
    candidate.write_bytes(b"candidate-package-bytes")
    binding = candidate_binding(candidate)
    binding["producer"]["workflow"] = ".github/workflows/human-dispatch.yml"
    with pytest.raises(MODULE.ContractError, match="governed export lane"):
        MODULE.validate_candidate(canonical(binding), "linux", "linux-x64")


def test_windows_n_minus_one_requires_immutable_payload_binding() -> None:
    previous = n_minus_one_binding()
    previous.update(
        {
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactUrl": (
                "https://chummer.run/downloads/generations/"
                f"{previous['generationId']}/files/chummer-avalonia-win-x64-installer.exe"
            ),
            "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
            "payloadSha256": "5" * 64,
            "payloadSizeBytes": 2048,
            "payloadUrl": (
                "https://chummer.run/downloads/generations/"
                f"{previous['generationId']}/files/chummer-avalonia-win-x64-payload.zip"
            ),
            "platform": "windows",
            "rid": "win-x64",
        }
    )
    validated = MODULE.validate_n_minus_one(
        canonical(previous), "windows", "win-x64"
    )
    assert validated["payloadSha256"] == "5" * 64
    del previous["payloadUrl"]
    with pytest.raises(MODULE.ContractError, match="missing keys"):
        MODULE.validate_n_minus_one(canonical(previous), "windows", "win-x64")


def test_downloaded_n_minus_one_manifest_binds_exact_artifact_and_payload(
    tmp_path: Path,
) -> None:
    previous = n_minus_one_binding()
    previous.update(
        {
            "artifactFileName": "chummer-avalonia-win-x64-installer.exe",
            "artifactUrl": (
                "https://chummer.run/downloads/generations/"
                f"{previous['generationId']}/files/chummer-avalonia-win-x64-installer.exe"
            ),
            "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
            "payloadSha256": "5" * 64,
            "payloadSizeBytes": 2048,
            "payloadUrl": (
                "https://chummer.run/downloads/generations/"
                f"{previous['generationId']}/files/chummer-avalonia-win-x64-payload.zip"
            ),
            "platform": "windows",
            "rid": "win-x64",
        }
    )
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    manifest = write_n_minus_one_manifest(manifest_path, previous)
    result = MODULE.validate_downloaded_n_minus_one_manifest(
        manifest_path,
        canonical(previous),
        "windows",
        "win-x64",
    )
    assert result["artifactSha256"] == previous["artifactSha256"]

    manifest["artifacts"][0]["payloadSha256"] = "0" * 64
    manifest_path.write_text(json.dumps(manifest) + "\n")
    previous["manifestSha256"] = sha256(manifest_path)
    with pytest.raises(MODULE.ContractError, match="payloadSha256 differs"):
        MODULE.validate_downloaded_n_minus_one_manifest(
            manifest_path,
            canonical(previous),
            "windows",
            "win-x64",
        )


def test_downloaded_n_minus_one_manifest_rejects_unrelated_generation_manifest(
    tmp_path: Path,
) -> None:
    previous = n_minus_one_binding()
    manifest_path = tmp_path / "RELEASE_CHANNEL.generated.json"
    manifest = write_n_minus_one_manifest(manifest_path, previous)
    manifest["generationId"] = "g-20260719T120000Z-unrelated"
    manifest_path.write_text(json.dumps(manifest) + "\n")
    previous["manifestSha256"] = sha256(manifest_path)
    with pytest.raises(MODULE.ContractError, match="generation"):
        MODULE.validate_downloaded_n_minus_one_manifest(
            manifest_path,
            canonical(previous),
            "linux",
            "linux-x64",
        )


def test_materializes_only_bound_candidate_member(tmp_path: Path) -> None:
    package_bytes = b"candidate-package-bytes"
    archive = tmp_path / "candidate.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as handle:
        handle.writestr("files/", b"")
        handle.writestr(
            "files/chummer-avalonia-linux-x64-installer.deb", package_bytes
        )
        handle.writestr("unrelated.txt", b"not extracted")
    package = tmp_path / "package.deb"
    package.write_bytes(package_bytes)
    binding = candidate_binding(package)
    binding["artifactFileName"] = "chummer-avalonia-linux-x64-installer.deb"
    binding["artifactMemberPath"] = (
        "files/chummer-avalonia-linux-x64-installer.deb"
    )
    binding["producer"]["artifactZipSha256"] = sha256(archive)
    output = tmp_path / "held"
    validated = MODULE.materialize_candidate(
        archive, canonical(binding), "linux", "linux-x64", output
    )
    assert Path(validated["resolvedPath"]).read_bytes() == package_bytes
    assert not (output / "unrelated.txt").exists()


def test_bindings_reject_noncanonical_json_and_mutable_url() -> None:
    previous = n_minus_one_binding()
    with pytest.raises(MODULE.ContractError, match="canonical"):
        MODULE.validate_n_minus_one(json.dumps(previous), "linux", "linux-x64")
    previous["artifactUrl"] = "https://chummer.run/downloads/latest/package.deb"
    with pytest.raises(MODULE.ContractError, match="generation"):
        MODULE.validate_n_minus_one(canonical(previous), "linux", "linux-x64")


def test_candidate_rejects_symlink_and_byte_drift(tmp_path: Path) -> None:
    root = tmp_path / "candidate"
    actual = tmp_path / "actual.deb"
    actual.write_bytes(b"candidate")
    linked = root / "files" / "candidate.deb"
    linked.parent.mkdir(parents=True)
    linked.symlink_to(actual)
    binding = candidate_binding(actual)
    binding["artifactFileName"] = linked.name
    binding["artifactMemberPath"] = "files/candidate.deb"
    with pytest.raises(MODULE.ContractError, match="symlink"):
        MODULE.validate_candidate(canonical(binding), "linux", "linux-x64", root)

    linked.unlink()
    linked.write_bytes(b"different")
    with pytest.raises(MODULE.ContractError, match="differ"):
        MODULE.validate_candidate(canonical(binding), "linux", "linux-x64", root)


def test_passing_native_lifecycle_receipt_is_fully_revalidated(tmp_path: Path) -> None:
    receipt_path, _ = passing_receipt(tmp_path)
    result = MODULE.validate_receipt(receipt_path, tmp_path)
    assert result["platform"] == "linux"
    assert result["rid"] == "linux-x64"
    assert result["receiptSha256"] == sha256(receipt_path)


def test_receipt_rejects_different_triggering_actor(tmp_path: Path) -> None:
    receipt_path, receipt = passing_receipt(tmp_path)
    receipt["nativeRunner"]["source"]["triggeringActor"] = "human-operator"
    receipt_path.write_text(json.dumps(receipt) + "\n")

    with pytest.raises(MODULE.ContractError, match="same-actor-only"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_receipt_rejects_passing_core_receipt_for_different_artifact(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_receipt(tmp_path)
    binding = receipt["coreWorkflow"]["candidate"]["startupReceipt"]
    core_path = tmp_path / binding["path"]
    core_payload = json.loads(core_path.read_text())
    core_payload["artifactDigest"] = f"sha256:{'0' * 64}"
    core_path.write_text(json.dumps(core_payload) + "\n")
    binding["sha256"] = sha256(core_path)
    binding["sizeBytes"] = core_path.stat().st_size
    receipt_path.write_text(json.dumps(receipt) + "\n")
    with pytest.raises(MODULE.ContractError, match="exact native release artifact"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_windows_receipt_binds_authenticode_pins_and_v2_signing_receipt(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_windows_receipt(tmp_path)
    result = MODULE.validate_receipt(receipt_path, tmp_path)
    assert result["platform"] == "windows"
    adapter_path = tmp_path / "windows-native-e2e-adapter.json"
    MODULE.emit_flagship_adapter(
        receipt_path=receipt_path,
        evidence_root=tmp_path,
        candidate_root=tmp_path,
        evidence_path=receipt_path.name,
        output_path=adapter_path,
        candidate_id="candidate-20260725",
        generation_id="generation-20260725",
        artifact_id="avalonia-win-x64-installer",
        source_commit=receipt["candidate"]["sourceCommit"],
    )
    adapter = json.loads(adapter_path.read_text())
    assert adapter["contractName"] == (
        "chummer6-ui.flagship-native-e2e.windows.v2"
    )
    assert adapter["contractVersion"] == 2
    assert adapter["livePredecessorAuthority"] == {
        key: receipt["livePredecessorAuthority"][key]
        for key in (
            "liveReleaseChannelSha256",
            "nMinusOneReleaseSha256",
            "selectedTupleSha256",
            "url",
        )
    }
    receipt["packageAuthority"]["expectedSignerSpkiSha256"] = "0" * 64
    receipt_path.write_text(json.dumps(receipt))
    with pytest.raises(MODULE.ContractError, match="signer pins"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_windows_receipt_rejects_nonpassing_or_misbinding_v2_signing_receipt(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_windows_receipt(tmp_path)
    signing = receipt["packageAuthority"]["candidate"]["signingReceipt"]
    signing_path = tmp_path / signing["path"]
    payload = json.loads(signing_path.read_text())
    payload["signingStatus"] = "passed"
    signing_path.write_text(json.dumps(payload) + "\n")
    signing["sha256"] = sha256(signing_path)
    signing["sizeBytes"] = signing_path.stat().st_size
    receipt_path.write_text(json.dumps(receipt) + "\n")
    with pytest.raises(MODULE.ContractError, match="signingStatus"):
        MODULE.validate_receipt(receipt_path, tmp_path)

    payload["signingStatus"] = "pass"
    payload["artifactSignatures"][0]["signer"]["spkiSha256"] = "0" * 64
    signing_path.write_text(json.dumps(payload) + "\n")
    signing["sha256"] = sha256(signing_path)
    signing["sizeBytes"] = signing_path.stat().st_size
    receipt_path.write_text(json.dumps(receipt) + "\n")
    with pytest.raises(MODULE.ContractError, match="signature evidence"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_windows_receipt_requires_versioned_live_root_custody(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_windows_receipt(tmp_path)
    receipt["contractVersion"] = 1
    receipt_path.write_text(json.dumps(receipt) + "\n")
    with pytest.raises(MODULE.ContractError, match="contract or status"):
        MODULE.validate_receipt(receipt_path, tmp_path)

    receipt["contractVersion"] = 2
    receipt["phases"][0]["details"]["liveReleaseRootVerified"] = False
    receipt_path.write_text(json.dumps(receipt) + "\n")
    with pytest.raises(MODULE.ContractError, match="liveReleaseRootVerified"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_emits_exact_global_flagship_adapter_bound_to_rich_receipt(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_receipt(tmp_path)
    output = tmp_path / "linux-native-e2e-adapter.json"
    result = MODULE.emit_flagship_adapter(
        receipt_path=receipt_path,
        evidence_root=tmp_path,
        candidate_root=tmp_path,
        evidence_path=receipt_path.name,
        output_path=output,
        candidate_id="candidate-20260725",
        generation_id="generation-20260725",
        artifact_id="avalonia-linux-x64-installer",
        source_commit=receipt["candidate"]["sourceCommit"],
    )
    adapter = json.loads(output.read_text())
    assert set(adapter) == {
        "artifact",
        "candidate",
        "checks",
        "contractName",
        "contractVersion",
        "generatedAt",
        "livePredecessorAuthority",
        "platform",
        "rid",
        "runner",
        "status",
    }
    assert adapter["contractName"] == "chummer6-ui.flagship-native-e2e.linux.v2"
    assert adapter["contractVersion"] == 2
    assert adapter["candidate"]["releaseVersion"] == receipt["candidate"]["version"]
    assert adapter["candidate"]["previousReleaseVersion"] == receipt["nMinusOne"]["version"]
    assert adapter["artifact"] == {
        "artifactId": "avalonia-linux-x64-installer",
        "fileName": receipt["candidate"]["artifactFileName"],
        "sha256": receipt["candidate"]["sha256"],
        "sizeBytes": receipt["candidate"]["sizeBytes"],
    }
    assert adapter["runner"]["rerunPolicy"] == "same-actor-only"
    assert adapter["runner"]["triggeringActor"] == "github-actions[bot]"
    assert adapter["livePredecessorAuthority"] == {
        key: receipt["livePredecessorAuthority"][key]
        for key in (
            "liveReleaseChannelSha256",
            "nMinusOneReleaseSha256",
            "selectedTupleSha256",
            "url",
        )
    }
    evidence_rows = [
        adapter["checks"]["cleanInstall"]["evidence"],
        adapter["checks"]["coreWorkflow"]["evidence"],
        adapter["checks"]["nMinusOneUpdate"]["evidence"],
    ]
    assert evidence_rows == [
        {
            "path": receipt_path.name,
            "sha256": sha256(receipt_path),
            "sizeBytes": receipt_path.stat().st_size,
        }
    ] * 3
    assert result["adapterSha256"] == sha256(output)


def test_flagship_adapter_rejects_unbound_global_identity_or_evidence_path(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_receipt(tmp_path)
    kwargs = {
        "receipt_path": receipt_path,
        "evidence_root": tmp_path,
        "candidate_root": tmp_path,
        "evidence_path": receipt_path.name,
        "output_path": tmp_path / "adapter.json",
        "candidate_id": "candidate-20260725",
        "generation_id": "generation-20260725",
        "artifact_id": "avalonia-linux-x64-installer",
        "source_commit": "0" * 40,
    }
    with pytest.raises(MODULE.ContractError, match="sourceCommit differs"):
        MODULE.emit_flagship_adapter(**kwargs)
    kwargs["source_commit"] = receipt["candidate"]["sourceCommit"]
    kwargs["evidence_path"] = "different.json"
    with pytest.raises(MODULE.ContractError, match="does not resolve"):
        MODULE.emit_flagship_adapter(**kwargs)


def test_receipt_rejects_compatibility_runner_and_missing_update_proof(
    tmp_path: Path,
) -> None:
    receipt_path, receipt = passing_receipt(tmp_path)
    receipt["nativeRunner"]["environment"] = "container"
    receipt_path.write_text(json.dumps(receipt))
    with pytest.raises(MODULE.ContractError, match="matching native runner"):
        MODULE.validate_receipt(receipt_path, tmp_path)

    receipt["nativeRunner"]["environment"] = "native"
    receipt["phases"][3]["details"]["statePreserved"] = False
    receipt_path.write_text(json.dumps(receipt))
    with pytest.raises(MODULE.ContractError, match="statePreserved"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_receipt_rejects_evidence_mutation(tmp_path: Path) -> None:
    receipt_path, receipt = passing_receipt(tmp_path)
    evidence_path = tmp_path / receipt["evidenceFiles"][0]["path"]
    evidence_path.write_text('{"status":"passed","mutated":true}\n')
    with pytest.raises(MODULE.ContractError, match="differ"):
        MODULE.validate_receipt(receipt_path, tmp_path)


def test_installer_unattended_mode_uses_full_lifecycle_path() -> None:
    source = (
        REPO_ROOT / "Chummer.Desktop.Installer" / "Program.cs"
    ).read_text(encoding="utf-8")
    assert 'private const string UnattendedSwitch = "--unattended";' in source
    assert "return InstallUnattended(" in source
    assert "CompleteInstall(metadata, payloadPathOverride, payloadDownload, claimCode, progress: null);" in source
    assert "return Uninstall(metadata, unattended);" in source
    assert "if (!unattended)" in source


def test_native_workflows_fail_closed_and_run_real_lifecycles() -> None:
    windows_workflow = (
        REPO_ROOT / ".github" / "workflows" / "windows-native-evidence-capture.yml"
    ).read_text(encoding="utf-8")
    linux_workflow = (
        REPO_ROOT / ".github" / "workflows" / "linux-native-lifecycle-evidence.yml"
    ).read_text(encoding="utf-8")
    windows_runner = (
        REPO_ROOT / "scripts" / "run-windows-native-lifecycle-e2e.ps1"
    ).read_text(encoding="utf-8")
    linux_runner = (
        REPO_ROOT / "scripts" / "run-linux-native-lifecycle-e2e.sh"
    ).read_text(encoding="utf-8")

    assert "n_minus_one_release_json:" in windows_workflow
    assert "run-windows-native-lifecycle-e2e.ps1" in windows_workflow
    assert windows_workflow.index("run-windows-native-lifecycle-e2e.ps1") < (
        windows_workflow.index("capture_windows_installer_visual.ps1")
    )
    assert "ExpectedSignerCertificateSha256" in windows_workflow
    assert "ExpectedSignerSpkiSha256" in windows_workflow
    assert "ExpectedNMinusOneReleaseSha256" in windows_workflow
    assert "ExpectedLiveReleaseChannelSha256" in windows_workflow
    assert "ExpectedSelectedTupleSha256" in windows_workflow
    assert "github.triggering_actor" in windows_workflow
    assert "same-actor reruns" in windows_workflow
    assert "continue-on-error:" not in windows_workflow

    assert "runs-on: ubuntu-latest" in linux_workflow
    assert "candidate_binding_json:" in linux_workflow
    assert "materialize-candidate" in linux_workflow
    assert "run-linux-native-lifecycle-e2e.sh" in linux_workflow
    assert "github.triggering_actor" in linux_workflow
    assert "same-actor reruns" in linux_workflow
    assert "continue-on-error:" not in linux_workflow
    assert "!Number.isFinite(createdAt)" in linux_workflow
    assert "createdAt > now + 5 * 60 * 1000" in linux_workflow

    assert "$env:RUNNER_OS -cne 'Windows'" in windows_runner
    assert windows_runner.count("$authenticodeScript") >= 3
    assert "Resolve-CachedUninstaller" in windows_runner
    assert "@('--uninstall', '--unattended')" in windows_runner
    assert "validate-n-minus-one-manifest" in windows_runner
    assert windows_runner.count("fetch-live-predecessor-authority") == 1
    assert "liveReleaseRootVerified = $true" in windows_runner
    assert "livePredecessorAuthority = [ordered]@{" in windows_runner
    assert "contractVersion = 2" in windows_runner
    assert "sourceCommit = $SourceSha" in windows_runner

    assert '[[ "${RUNNER_OS:-}" != "Linux"' in linux_runner
    assert 'apt-get install -y "$OLD_PACKAGE"' in linux_runner
    assert 'apt-get install -y "$CANDIDATE"' in linux_runner
    assert 'apt-get remove --purge -y "$PACKAGE_NAME"' in linux_runner
    assert "validate-n-minus-one-manifest" in linux_runner
    assert '"sourceCommit": os.environ["LIFECYCLE_SOURCE_SHA"]' in linux_runner
    assert "dpkg_rootless" not in linux_runner


def test_linux_candidate_relay_is_fixed_and_bot_only() -> None:
    producer_workflow = (
        REPO_ROOT / ".github" / "workflows" / "linux-native-candidate-export.yml"
    ).read_text(encoding="utf-8")
    lifecycle_workflow = (
        REPO_ROOT / ".github" / "workflows" / "linux-native-lifecycle-evidence.yml"
    ).read_text(encoding="utf-8")
    dispatch_contract = producer_workflow.split("permissions: {}", maxsplit=1)[0]

    assert "candidate_binding_json:" not in dispatch_contract
    assert "source_actor:" not in dispatch_contract
    assert "target_workflow:" not in dispatch_contract
    assert "target_ref:" not in dispatch_contract
    assert "workflow_id: 'linux-native-lifecycle-evidence.yml'" in producer_workflow
    assert "ref: 'main'" in producer_workflow
    assert "github-token: ${{ github.token }}" in producer_workflow
    assert "actions: write" in producer_workflow
    assert "artifact.digest" in producer_workflow
    assert "listWorkflowRunArtifacts" in producer_workflow
    assert "actor: run.data.actor.login" in producer_workflow
    assert "candidate_binding_json: candidateBindingJson" in producer_workflow
    assert "findmnt -n -o OPTIONS --target" in producer_workflow
    assert "*,ro,*) ;;" in producer_workflow
    assert "continue-on-error:" not in producer_workflow

    assert (
        "process.env.GITHUB_ACTOR !== 'github-actions[bot]'"
        in lifecycle_workflow
    )
    assert (
        "producer.workflow\n"
        "                !== '.github/workflows/linux-native-candidate-export.yml'"
        in lifecycle_workflow
    )
    assert "workflow_id: ${{" not in producer_workflow
