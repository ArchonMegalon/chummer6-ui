from __future__ import annotations

import base64
import copy
import hashlib
import importlib.util
import json
import os
from datetime import UTC, datetime, timedelta
from pathlib import Path

import pytest

from preview_supply_chain_fixtures import write_valid_supply_chain


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_PATH = REPO_ROOT / "scripts" / "preview_supply_chain.py"
SPEC = importlib.util.spec_from_file_location("preview_supply_chain", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
SUPPLY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SUPPLY)

VERSION = "run-20260720-supply-chain-test"
SOURCE_COMMIT = "1" * 40
SAFE_PACKAGE = "Safe.Release.Package"
SAFE_VERSION = "10.0.0"


def write_json(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(SUPPLY.canonical_json_bytes(payload))


def package_assets(
    path: Path,
    *,
    rid: str,
    package_name: str = SAFE_PACKAGE,
    package_version: str = SAFE_VERSION,
) -> Path:
    package_key = f"{package_name}/{package_version}"
    project_key = "Chummer.Presentation/1.0.0"
    digest = base64.b64encode(bytes(range(64))).decode("ascii")
    write_json(
        path,
        {
            "libraries": {
                package_key: {"sha512": digest, "type": "package"},
                project_key: {"type": "project"},
            },
            "targets": {
                f"net10.0/{rid}": {
                    package_key: {"dependencies": {}},
                    project_key: {"dependencies": {package_name: package_version}},
                }
            },
        },
    )
    return path


def exact_artifacts(root: Path, rid: str) -> dict[str, Path]:
    result: dict[str, Path] = {}
    for relative in SUPPLY._expected_artifact_paths(rid):
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(f"exact:{rid}:{relative}".encode("utf-8"))
        result[relative] = path
    return result


def valid_stage(root: Path) -> Path:
    artifact_rows: list[dict[str, object]] = []
    for _, platform, rid in SUPPLY.ACTIVE_TUPLES:
        artifacts = exact_artifacts(root, rid)
        installer_relative = SUPPLY._expected_artifact_paths(rid)[0]
        installer = artifacts[installer_relative]
        row: dict[str, object] = {
            "artifactId": f"avalonia-{rid}-installer",
            "fileName": installer.name,
            "head": "avalonia",
            "kind": "installer",
            "platform": platform,
            "rid": rid,
            "sha256": SUPPLY.sha256_file(installer),
            "sizeBytes": installer.stat().st_size,
            "version": VERSION,
        }
        if rid == "win-x64":
            payload = artifacts[SUPPLY._expected_artifact_paths(rid)[1]]
            row.update(
                {
                    "payloadFileName": payload.name,
                    "payloadSha256": SUPPLY.sha256_file(payload),
                    "payloadSizeBytes": payload.stat().st_size,
                }
            )
        artifact_rows.append(row)
    write_json(
        root / "RELEASE_CHANNEL.generated.json",
        {"artifacts": artifact_rows, "channelId": "preview", "version": VERSION},
    )
    write_valid_supply_chain(
        root,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        supply=SUPPLY,
        require_artifact_bytes=True,
    )
    return root


def fake_scanner(
    path: Path,
    monkeypatch: pytest.MonkeyPatch,
    *,
    response: dict[str, object] | None,
    exit_code: int,
) -> Path:
    response_text = json.dumps(response, separators=(",", ":")) if response is not None else "{}"
    path.write_text(
        "#!/usr/bin/env python3\n"
        "import sys\n"
        "if '--version' in sys.argv:\n"
        f"    print('osv-scanner version: {SUPPLY.OSV_SCANNER_VERSION}')\n"
        "    print('osv-scalibr version: fixture')\n"
        f"    print('commit: {SUPPLY.OSV_SCANNER_COMMIT}')\n"
        "    print('built at: fixture')\n"
        "    raise SystemExit(0)\n"
        f"print({response_text!r})\n"
        + (
            "print('fixture advisory database unavailable', file=sys.stderr)\n"
            if exit_code not in {0, 1}
            else ""
        )
        + f"raise SystemExit({exit_code})\n",
        encoding="utf-8",
    )
    path.chmod(0o755)
    monkeypatch.setattr(SUPPLY, "OSV_SCANNER_SHA256", hashlib.sha256(path.read_bytes()).hexdigest())
    return path.resolve()


def safe_response(rid: str) -> dict[str, object]:
    return {
        "results": [
            {
                "packages": [
                    {
                        "package": {
                            "ecosystem": "NuGet",
                            "name": SAFE_PACKAGE,
                            "version": SAFE_VERSION,
                        },
                        "vulnerabilities": [],
                    }
                ],
                "source": {"path": SUPPLY.SBOM_PATHS[rid], "type": "sbom"},
            }
        ]
    }


def test_cyclonedx_is_deterministic_and_binds_exact_package_and_artifact_identity(
    tmp_path: Path,
) -> None:
    rid = "linux-x64"
    assets = package_assets(tmp_path / "project.assets.json", rid=rid)
    artifacts = exact_artifacts(tmp_path, rid)

    first = SUPPLY.generate_sbom(
        assets_path=assets,
        rid=rid,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        artifacts=artifacts,
    )
    second = SUPPLY.generate_sbom(
        assets_path=assets,
        rid=rid,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        artifacts=artifacts,
    )

    assert SUPPLY.canonical_json_bytes(first) == SUPPLY.canonical_json_bytes(second)
    assert first["bomFormat"] == "CycloneDX"
    assert first["specVersion"] == "1.6"
    package = next(row for row in first["components"] if row.get("purl"))
    assert package["name"] == SAFE_PACKAGE
    assert package["version"] == SAFE_VERSION
    assert package["purl"] == f"pkg:nuget/{SAFE_PACKAGE}@{SAFE_VERSION}"
    assert package["hashes"] == [{"alg": "SHA-512", "content": bytes(range(64)).hex()}]
    artifact = next(row for row in first["components"] if row.get("type") == "file")
    exact_path = artifacts[SUPPLY._expected_artifact_paths(rid)[0]]
    assert artifact["hashes"] == [{"alg": "SHA-256", "content": SUPPLY.sha256_file(exact_path)}]
    assert {row["name"]: row["value"] for row in artifact["properties"]} == {
        "chummer:relative-path": SUPPLY._expected_artifact_paths(rid)[0],
        "chummer:rid": rid,
        "chummer:size-bytes": str(exact_path.stat().st_size),
    }


@pytest.mark.parametrize(
    ("package_name", "package_version"),
    [
        ("System.Text.Json", "7.0.3"),
        ("Microsoft.IdentityModel.Tokens", "6.35.0"),
        ("IdentityModel", "6.2.2"),
    ],
)
def test_legacy_alerted_packages_cannot_be_dismissed(
    tmp_path: Path, package_name: str, package_version: str
) -> None:
    rid = "linux-x64"
    assets = package_assets(
        tmp_path / "project.assets.json",
        rid=rid,
        package_name=package_name,
        package_version=package_version,
    )
    with pytest.raises(SUPPLY.SupplyChainError, match="legacy alerted packages"):
        SUPPLY.generate_sbom(
            assets_path=assets,
            rid=rid,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            artifacts=exact_artifacts(tmp_path, rid),
        )


def test_nonlegacy_versions_are_not_misclassified(tmp_path: Path) -> None:
    rid = "linux-x64"
    for index, (name, version) in enumerate(
        (("System.Text.Json", "8.0.5"), ("Microsoft.IdentityModel.Tokens", "7.6.1"))
    ):
        sbom = SUPPLY.generate_sbom(
            assets_path=package_assets(
                tmp_path / f"project-{index}.assets.json",
                rid=rid,
                package_name=name,
                package_version=version,
            ),
            rid=rid,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            artifacts=exact_artifacts(tmp_path / f"artifact-{index}", rid),
        )
        assert SUPPLY.sbom_package_rows(sbom) == [
            {"name": name, "purl": SUPPLY.purl_for_nuget(name, version), "version": version}
        ]


def test_high_severity_finding_fails_before_receipt_is_emitted(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    rid = "linux-x64"
    response = safe_response(rid)
    response["results"][0]["packages"][0]["vulnerabilities"] = [
        {
            "database_specific": {"severity": "HIGH"},
            "id": "GHSA-fixture-high",
            "modified": "2026-07-20T00:00:00Z",
        }
    ]
    scanner = fake_scanner(tmp_path / "osv-scanner", monkeypatch, response=response, exit_code=1)
    stage = tmp_path / "stage"
    stage.mkdir()

    with pytest.raises(SUPPLY.SupplyChainError, match="high/critical vulnerabilities"):
        SUPPLY.generate_rid_evidence(
            stage_root=stage,
            assets_path=package_assets(tmp_path / "project.assets.json", rid=rid),
            scanner=scanner,
            rid=rid,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            artifacts=exact_artifacts(stage, rid),
        )
    assert not (stage / SUPPLY.SCAN_PATHS[rid]).exists()


def test_scanner_database_or_network_failure_fails_closed(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    rid = "linux-x64"
    scanner = fake_scanner(tmp_path / "osv-scanner", monkeypatch, response=None, exit_code=2)
    stage = tmp_path / "stage"
    stage.mkdir()

    with pytest.raises(SUPPLY.SupplyChainError, match="database/network was unavailable"):
        SUPPLY.generate_rid_evidence(
            stage_root=stage,
            assets_path=package_assets(tmp_path / "project.assets.json", rid=rid),
            scanner=scanner,
            rid=rid,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            artifacts=exact_artifacts(stage, rid),
        )


def test_scanner_nonzero_status_without_findings_is_rejected_as_ambiguous(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    rid = "linux-x64"
    scanner = fake_scanner(
        tmp_path / "osv-scanner",
        monkeypatch,
        response=safe_response(rid),
        exit_code=1,
    )
    stage = tmp_path / "stage"
    stage.mkdir()

    with pytest.raises(SUPPLY.SupplyChainError, match="exit status and vulnerability result disagree"):
        SUPPLY.generate_rid_evidence(
            stage_root=stage,
            assets_path=package_assets(tmp_path / "project.assets.json", rid=rid),
            scanner=scanner,
            rid=rid,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            artifacts=exact_artifacts(stage, rid),
        )


def test_scanner_binary_hash_is_pinned(tmp_path: Path) -> None:
    scanner = tmp_path / "osv-scanner"
    scanner.write_bytes(b"not-the-pinned-scanner")
    scanner.chmod(0o755)
    with pytest.raises(SUPPLY.SupplyChainError, match="pinned SHA-256"):
        SUPPLY._verify_scanner(scanner.resolve())


@pytest.mark.skipif(
    not os.environ.get("CHUMMER_UI_TEST_LIVE_OSV_SCANNER"),
    reason="live pinned-scanner integration is opt-in",
)
def test_official_pinned_scanner_live_response_matches_the_receipt_contract(
    tmp_path: Path,
) -> None:
    rid = "linux-x64"
    scanner = Path(os.environ["CHUMMER_UI_TEST_LIVE_OSV_SCANNER"]).resolve()
    stage = tmp_path / "stage"
    stage.mkdir()

    receipt = SUPPLY.generate_rid_evidence(
        stage_root=stage,
        assets_path=package_assets(tmp_path / "project.assets.json", rid=rid),
        scanner=scanner,
        rid=rid,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        artifacts=exact_artifacts(stage, rid),
    )

    assert receipt["status"] == "passed"
    assert receipt["scanner"] == {
        "binarySha256": SUPPLY.OSV_SCANNER_SHA256,
        "commit": SUPPLY.OSV_SCANNER_COMMIT,
        "exitCode": 0,
        "invocation": [
            "osv-scanner",
            "scan",
            "source",
            "--format",
            "json",
            "--all-packages",
            "--no-ignore",
            "--no-resolve",
            "--verbosity",
            "error",
            "--lockfile",
            SUPPLY.SBOM_PATHS[rid],
        ],
        "name": SUPPLY.OSV_SCANNER_NAME,
        "version": SUPPLY.OSV_SCANNER_VERSION,
    }
    assert receipt["advisoryProvenance"]["source"] == SUPPLY.OSV_DATA_SOURCE
    assert receipt["advisoryProvenance"]["reproducible"] is False
    assert receipt["advisoryProvenance"]["normalization"] == SUPPLY.OSV_RESPONSE_NORMALIZATION
    assert receipt["response"]["results"][0]["source"]["path"] == SUPPLY.SBOM_PATHS[rid]
    assert str(tmp_path) not in json.dumps(receipt, sort_keys=True)


def test_stale_live_advisory_response_is_rejected(tmp_path: Path) -> None:
    stage = valid_stage(tmp_path / "stage")
    scan_path = stage / SUPPLY.SCAN_PATHS["linux-x64"]
    receipt = SUPPLY.read_json(scan_path, "test scan")
    completed = datetime.now(UTC).replace(microsecond=0) - timedelta(days=2)
    receipt["advisoryProvenance"].update(
        {
            "completedAt": SUPPLY.utc_text(completed),
            "freshUntil": SUPPLY.utc_text(completed + SUPPLY.ADVISORY_FRESHNESS),
            "queriedAt": SUPPLY.utc_text(completed),
        }
    )
    write_json(scan_path, receipt)

    with pytest.raises(SUPPLY.SupplyChainError, match="stale, mutable, or unbound"):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


@pytest.mark.parametrize("mutation", ["missing", "extra", "wrong_rid", "artifact_drift"])
def test_exact_evidence_and_artifact_binding_rejects_drift(
    tmp_path: Path, mutation: str
) -> None:
    stage = valid_stage(tmp_path / "stage")
    if mutation == "missing":
        (stage / SUPPLY.SCAN_PATHS["linux-x64"]).unlink()
        pattern = "missing or contains extras"
    elif mutation == "extra":
        (stage / "release-evidence" / "sbom" / "unexpected.json").write_text("{}\n")
        pattern = "missing or contains extras"
    elif mutation == "wrong_rid":
        sbom_path = stage / SUPPLY.SBOM_PATHS["linux-x64"]
        sbom = SUPPLY.read_json(sbom_path, "test SBOM")
        properties = sbom["metadata"]["component"]["properties"]
        next(row for row in properties if row["name"] == "chummer:rid")["value"] = "win-x64"
        write_json(sbom_path, sbom)
        pattern = "root release identity differs"
    else:
        artifact = stage / SUPPLY._expected_artifact_paths("linux-x64")[0]
        artifact.write_bytes(artifact.read_bytes() + b"drift")
        pattern = "artifact bytes changed or differ"

    with pytest.raises(SUPPLY.SupplyChainError, match=pattern):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


def test_receipt_scanner_and_advisory_response_digests_are_enforced(tmp_path: Path) -> None:
    stage = valid_stage(tmp_path / "stage")
    scan_path = stage / SUPPLY.SCAN_PATHS["win-x64"]
    original = SUPPLY.read_json(scan_path, "test scan")

    scanner_drift = copy.deepcopy(original)
    scanner_drift["scanner"]["binarySha256"] = "2" * 64
    write_json(scan_path, scanner_drift)
    with pytest.raises(SUPPLY.SupplyChainError, match="scanner authority differs"):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )

    response_drift = copy.deepcopy(original)
    response_drift["response"]["fixtureMutation"] = True
    write_json(scan_path, response_drift)
    with pytest.raises(SUPPLY.SupplyChainError, match="stale, mutable, or unbound"):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )
