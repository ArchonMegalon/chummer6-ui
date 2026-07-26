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
    bind_authority: bool = True,
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
    if bind_authority:
        graph = SUPPLY._normalized_rid_graph(path, rid)
        authorities = dict(
            getattr(SUPPLY, "_TRUSTED_RID_GRAPH_AUTHORITY_BYTES", {})
        )
        authorities[rid] = SUPPLY.canonical_json_bytes(
            SUPPLY._source_graph_authority_projection(graph, rid)
        )
        SUPPLY.RID_GRAPH_SOURCE_AUTHORITY_SHA256[rid] = hashlib.sha256(
            authorities[rid]
        ).hexdigest()
        SUPPLY._TRUSTED_RID_GRAPH_AUTHORITY_BYTES = authorities
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


def rebind_transitive_evidence(stage: Path, rid: str) -> None:
    """Recompute every mutable outer digest after an adversarial inner rewrite."""

    sbom_path = stage / SUPPLY.SBOM_PATHS[rid]
    scan_path = stage / SUPPLY.SCAN_PATHS[rid]
    sbom = SUPPLY.read_json(sbom_path, "test mutated SBOM")
    receipt = SUPPLY.read_json(scan_path, "test mutated scan")
    receipt["sbom"].update(
        {
            "path": SUPPLY.SBOM_PATHS[rid],
            "serialNumber": sbom.get("serialNumber"),
            "sha256": SUPPLY.sha256_file(sbom_path),
        }
    )
    write_json(scan_path, receipt)

    gate_path = stage / SUPPLY.GATE_PATH
    gate = SUPPLY.read_json(gate_path, "test mutated aggregate gate")
    tuple_row = next(row for row in gate["tuples"] if row["tuple"]["rid"] == rid)
    tuple_row["sbom"] = {
        "path": SUPPLY.SBOM_PATHS[rid],
        "sha256": SUPPLY.sha256_file(sbom_path),
        "sizeBytes": sbom_path.stat().st_size,
    }
    tuple_row["scan"] = {
        "path": SUPPLY.SCAN_PATHS[rid],
        "sha256": SUPPLY.sha256_file(scan_path),
        "sizeBytes": scan_path.stat().st_size,
    }
    write_json(gate_path, gate)


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
        "experimental_config": {
            "licenses": {"allowlist": None, "summary": False}
        },
        "results": [
            {
                "packages": [
                    {
                        "package": {
                            "ecosystem": "NuGet",
                            "name": SAFE_PACKAGE,
                            "version": SAFE_VERSION,
                        }
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
    metadata_properties = {
        row["name"]: row["value"] for row in first["metadata"]["properties"]
    }
    graph = json.loads(metadata_properties["chummer:normalized-rid-graph"])
    assert metadata_properties["chummer:normalized-rid-graph-sha256"] == (
        SUPPLY.compact_json_sha256(graph)
    )
    assert graph["projectAssetsSha256"] == SUPPLY.sha256_file(assets)
    project_authority = next(
        row for row in graph["libraries"] if row["type"] == "project"
    )
    assert project_authority["dependencies"] == [
        {"name": SAFE_PACKAGE, "requestedVersion": SAFE_VERSION}
    ]
    assert first["metadata"]["tools"]["components"][0]["hashes"] == [
        {"alg": "SHA-256", "content": SUPPLY.sha256_file(SCRIPT_PATH)}
    ]


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
        bind_authority=False,
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
            "database_specific": {"severity": "LOW"},
            "id": "GHSA-fixture-high",
            "modified": "2026-07-20T00:00:00Z",
            "severity": [
                {
                    "score": "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                    "type": "CVSS_V3",
                }
            ],
        }
    ]
    response["results"][0]["packages"][0]["groups"] = [
        {
            "aliases": ["GHSA-fixture-high"],
            "ids": ["GHSA-fixture-high"],
            "max_severity": "9.8",
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


@pytest.mark.parametrize(
    ("vulnerability", "expected"),
    [
        (
            {
                "database_specific": {"severity": "LOW"},
                "severity": [
                    {
                        "score": "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                        "type": "CVSS_V3",
                    }
                ],
            },
            ("critical", 9.8),
        ),
        (
            {
                "database_specific": {"severity": "MODERATE"},
                "severity": [{"score": "8.1", "type": "CVSS_V3"}],
            },
            ("high", 8.1),
        ),
        (
            {
                "database_specific": {"severity": "UNKNOWN"},
                "severity": [{"score": "9.8", "type": "CVSS_V3"}],
            },
            ("unclassified", None),
        ),
        (
            {
                "database_specific": {"severity": "LOW"},
                "severity": [{"score": True, "type": "CVSS_V3"}],
            },
            ("unclassified", None),
        ),
        (
            {
                "database_specific": {"severity": "LOW"},
                "severity": [
                    {
                        "score": (
                            "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H/AV:P"
                        ),
                        "type": "CVSS_V3",
                    }
                ],
            },
            ("unclassified", None),
        ),
    ],
)
def test_vulnerability_classification_uses_every_signal_and_fails_closed(
    vulnerability: dict[str, object], expected: tuple[str, float | None]
) -> None:
    assert SUPPLY.classify_vulnerability(vulnerability) == expected


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


@pytest.mark.parametrize(
    "mutation",
    [
        "removed_tool_metadata",
        "extra_metadata_claim",
        "replaced_metadata_contract",
        "replaced_tool_identity",
        "serial_drift",
        "dependency_drift",
        "composition_drift",
        "graph_contract_boolean",
    ],
)
def test_self_consistently_rehashed_cyclonedx_drift_is_rejected(
    tmp_path: Path, mutation: str
) -> None:
    stage = valid_stage(tmp_path / "stage")
    rid = "linux-x64"
    sbom_path = stage / SUPPLY.SBOM_PATHS[rid]
    sbom = SUPPLY.read_json(sbom_path, "test SBOM")
    if mutation == "removed_tool_metadata":
        del sbom["metadata"]["tools"]
    elif mutation == "extra_metadata_claim":
        sbom["metadata"]["forgedAuthority"] = "accepted"
    elif mutation == "replaced_metadata_contract":
        next(
            row
            for row in sbom["metadata"]["properties"]
            if row["name"] == "chummer:contract-name"
        )["value"] = "forged.contract"
    elif mutation == "replaced_tool_identity":
        sbom["metadata"]["tools"]["components"][0]["name"] = "forged.generator"
    elif mutation == "serial_drift":
        sbom["serialNumber"] = "urn:uuid:00000000-0000-5000-8000-000000000000"
    elif mutation == "dependency_drift":
        project = next(row for row in sbom["dependencies"] if row["ref"].startswith("project:"))
        project["dependsOn"] = []
    elif mutation == "composition_drift":
        sbom["compositions"][0]["assemblies"] = sbom["compositions"][0]["assemblies"][:-1]
    else:
        graph_property = next(
            row
            for row in sbom["metadata"]["properties"]
            if row["name"] == "chummer:normalized-rid-graph"
        )
        graph = json.loads(graph_property["value"])
        graph["contractVersion"] = True
        graph_property["value"] = SUPPLY.compact_json_text(graph)
        digest_property = next(
            row
            for row in sbom["metadata"]["properties"]
            if row["name"] == "chummer:normalized-rid-graph-sha256"
        )
        digest_property["value"] = SUPPLY.compact_json_sha256(graph)
    write_json(sbom_path, sbom)
    rebind_transitive_evidence(stage, rid)

    with pytest.raises(SUPPLY.SupplyChainError):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


@pytest.mark.parametrize(
    "mutation",
    [
        "extra_scan_claim",
        "extra_provenance_claim",
        "extra_response_claim",
        "extra_scanner_claim",
        "extra_sbom_binding_claim",
        "boolean_contract_version",
        "boolean_scanner_exit_code",
    ],
)
def test_self_consistently_rehashed_scan_schema_drift_is_rejected(
    tmp_path: Path, mutation: str
) -> None:
    stage = valid_stage(tmp_path / "stage")
    rid = "win-x64"
    scan_path = stage / SUPPLY.SCAN_PATHS[rid]
    receipt = SUPPLY.read_json(scan_path, "test scan")
    if mutation == "extra_scan_claim":
        receipt["forgedAuthority"] = "accepted"
    elif mutation == "extra_provenance_claim":
        receipt["advisoryProvenance"]["forgedAuthority"] = "accepted"
    elif mutation == "extra_response_claim":
        receipt["response"]["forgedAuthority"] = "accepted"
        receipt["advisoryProvenance"]["responseSha256"] = (
            SUPPLY.compact_json_sha256(receipt["response"])
        )
    elif mutation == "extra_scanner_claim":
        receipt["scanner"]["forgedAuthority"] = "accepted"
    elif mutation == "extra_sbom_binding_claim":
        receipt["sbom"]["forgedAuthority"] = "accepted"
    elif mutation == "boolean_contract_version":
        receipt["contractVersion"] = True
    else:
        receipt["scanner"]["exitCode"] = True
    write_json(scan_path, receipt)
    rebind_transitive_evidence(stage, rid)

    with pytest.raises(SUPPLY.SupplyChainError):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


@pytest.mark.parametrize(
    "field",
    ["contractVersion", "nestedSizeBytes", "releaseAuthorityRequiresLiveScanner"],
)
def test_aggregate_gate_rejects_boolean_numeric_substitution(
    tmp_path: Path, field: str
) -> None:
    stage = valid_stage(tmp_path / "stage")
    gate_path = stage / SUPPLY.GATE_PATH
    gate = SUPPLY.read_json(gate_path, "test aggregate gate")
    if field == "contractVersion":
        gate["contractVersion"] = True
    elif field == "nestedSizeBytes":
        gate["tuples"][0]["scan"]["sizeBytes"] = True
    else:
        gate["verification"]["releaseAuthorityRequiresLiveScanner"] = 1
    write_json(gate_path, gate)

    with pytest.raises(SUPPLY.SupplyChainError):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


def test_original_self_authorized_rewrite_probe_fails_before_refinalization(
    tmp_path: Path,
) -> None:
    stage = valid_stage(tmp_path / "stage")
    (stage / SUPPLY.GATE_PATH).unlink()
    rid = "linux-x64"
    sbom_path = stage / SUPPLY.SBOM_PATHS[rid]
    sbom = SUPPLY.read_json(sbom_path, "reviewer-mutated SBOM")
    sbom["metadata"].pop("tools", None)
    sbom["metadata"]["properties"] = [
        {"name": "attacker:claim", "value": "forged"}
    ]
    write_json(sbom_path, sbom)

    scan_path = stage / SUPPLY.SCAN_PATHS[rid]
    scan = SUPPLY.read_json(scan_path, "reviewer-mutated scan")
    scan["sbom"]["sha256"] = SUPPLY.sha256_file(sbom_path)
    scan["unexpectedAuthorityClaim"] = "accepted-extra"
    write_json(scan_path, scan)

    with pytest.raises(SUPPLY.SupplyChainError):
        SUPPLY.finalize_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


@pytest.mark.parametrize(
    "mutation",
    [
        "license_boolean_as_zero",
        "package_version_as_integer",
        "group_severity_as_integer",
        "provenance_boolean_as_zero",
    ],
)
def test_recursive_scanner_json_types_reject_python_equality_aliases(
    tmp_path: Path, mutation: str
) -> None:
    stage = valid_stage(tmp_path / "stage")
    rid = "linux-x64"
    scan_path = stage / SUPPLY.SCAN_PATHS[rid]
    receipt = SUPPLY.read_json(scan_path, "test scan")
    response = receipt["response"]
    package_row = response["results"][0]["packages"][0]
    if mutation == "license_boolean_as_zero":
        response["experimental_config"]["licenses"]["summary"] = 0
    elif mutation == "package_version_as_integer":
        package_row["package"]["version"] = 10
    elif mutation == "group_severity_as_integer":
        package_row["vulnerabilities"] = [
            {
                "database_specific": {"severity": "LOW"},
                "id": "GHSA-type-alias-probe",
            }
        ]
        package_row["groups"] = [
            {
                "aliases": ["GHSA-type-alias-probe"],
                "ids": ["GHSA-type-alias-probe"],
                "max_severity": 0,
            }
        ]
    else:
        receipt["advisoryProvenance"]["reproducible"] = 0
    receipt["advisoryProvenance"]["responseSha256"] = (
        SUPPLY.compact_json_sha256(response)
    )
    write_json(scan_path, receipt)
    rebind_transitive_evidence(stage, rid)

    with pytest.raises(SUPPLY.SupplyChainError):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


def test_source_controlled_rid_graph_authority_rejects_fully_rehashed_safe_replacement(
    tmp_path: Path,
) -> None:
    stage = valid_stage(tmp_path / "stage")
    rid = "linux-x64"
    original_authorities = dict(SUPPLY._TRUSTED_RID_GRAPH_AUTHORITY_BYTES)
    original_digests = dict(SUPPLY.RID_GRAPH_SOURCE_AUTHORITY_SHA256)
    (stage / SUPPLY.GATE_PATH).unlink()
    attacker_assets = package_assets(
        tmp_path / "attacker.project.assets.json",
        rid=rid,
        package_name="Attacker.Selected.Safe.Package",
        package_version="99.0.0",
    )
    sbom = SUPPLY.generate_sbom(
        assets_path=attacker_assets,
        rid=rid,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        artifacts={
            relative: stage / relative
            for relative in SUPPLY._expected_artifact_paths(rid)
        },
    )
    sbom_path = stage / SUPPLY.SBOM_PATHS[rid]
    write_json(sbom_path, sbom)
    packages = SUPPLY.sbom_package_rows(sbom)
    response = {
        "experimental_config": {
            "licenses": {"allowlist": None, "summary": False}
        },
        "results": [
            {
                "packages": [
                    {
                        "package": {
                            "ecosystem": "NuGet",
                            "name": row["name"],
                            "version": row["version"],
                        }
                    }
                    for row in packages
                ],
                "source": {"path": SUPPLY.SBOM_PATHS[rid], "type": "sbom"},
            }
        ],
    }
    scan_path = stage / SUPPLY.SCAN_PATHS[rid]
    receipt = SUPPLY.read_json(scan_path, "attacker scan")
    receipt["packages"] = packages
    receipt["response"] = response
    receipt["findings"] = []
    receipt["blockedFindings"] = []
    receipt["unclassifiedFindings"] = []
    receipt["sbom"] = {
        "path": SUPPLY.SBOM_PATHS[rid],
        "serialNumber": sbom["serialNumber"],
        "sha256": SUPPLY.sha256_file(sbom_path),
    }
    receipt["advisoryProvenance"].update(
        {
            "latestAdvisoryModifiedAt": None,
            "packageQuerySetSha256": SUPPLY.compact_json_sha256(packages),
            "responseSha256": SUPPLY.compact_json_sha256(response),
        }
    )
    write_json(scan_path, receipt)
    try:
        SUPPLY.finalize_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )
    finally:
        SUPPLY._TRUSTED_RID_GRAPH_AUTHORITY_BYTES = original_authorities
        SUPPLY.RID_GRAPH_SOURCE_AUTHORITY_SHA256.clear()
        SUPPLY.RID_GRAPH_SOURCE_AUTHORITY_SHA256.update(original_digests)

    with pytest.raises(SUPPLY.SupplyChainError, match="source authority"):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
        )


def test_release_authority_reexecutes_pinned_scanner_and_blocks_live_drift(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    stage = valid_stage(tmp_path / "stage")
    (stage / SUPPLY.GATE_PATH).unlink()
    calls: list[str] = []

    def live_safe_scan(*, stage_root: Path, sbom_relative: str, scanner: Path):
        del stage_root, scanner
        rid = next(rid for rid, path in SUPPLY.SBOM_PATHS.items() if path == sbom_relative)
        now = datetime.now(UTC).replace(microsecond=0)
        calls.append(rid)
        return safe_response(rid), 0, now, now

    monkeypatch.setattr(SUPPLY, "_scan_sbom", live_safe_scan)
    scanner = Path("/exact/pinned/osv-scanner")
    gate = SUPPLY.finalize_gate(
        stage_root=stage,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        scanner=scanner,
        release_authoritative=True,
    )
    assert gate["verification"] == {
        "finalizationMode": SUPPLY.LIVE_VERIFICATION_MODE,
        "offlineReplayMode": SUPPLY.STRUCTURAL_VERIFICATION_MODE,
        "releaseAuthorityRequiresLiveScanner": True,
    }
    SUPPLY.verify_gate(
        stage_root=stage,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        scanner=scanner,
        release_authoritative=True,
    )
    assert sorted(calls) == ["linux-x64", "linux-x64", "win-x64", "win-x64"]

    with pytest.raises(SUPPLY.SupplyChainError, match="requires the pinned scanner"):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            release_authoritative=True,
        )

    def live_blocked_scan(*, stage_root: Path, sbom_relative: str, scanner: Path):
        del stage_root, scanner
        rid = next(rid for rid, path in SUPPLY.SBOM_PATHS.items() if path == sbom_relative)
        response = safe_response(rid)
        package_row = response["results"][0]["packages"][0]
        package_row["vulnerabilities"] = [
            {
                "database_specific": {"severity": "HIGH"},
                "id": "GHSA-live-release-drift",
            }
        ]
        package_row["groups"] = [
            {
                "aliases": ["GHSA-live-release-drift"],
                "ids": ["GHSA-live-release-drift"],
                "max_severity": "HIGH",
            }
        ]
        now = datetime.now(UTC).replace(microsecond=0)
        return response, 1, now, now

    monkeypatch.setattr(SUPPLY, "_scan_sbom", live_blocked_scan)
    with pytest.raises(SUPPLY.SupplyChainError, match="release-blocking"):
        SUPPLY.verify_gate(
            stage_root=stage,
            version=VERSION,
            source_commit=SOURCE_COMMIT,
            scanner=scanner,
            release_authoritative=True,
        )
    assert SUPPLY.verify_gate(
        stage_root=stage,
        version=VERSION,
        source_commit=SOURCE_COMMIT,
        release_authoritative=False,
    )["verification"]["offlineReplayMode"] == SUPPLY.STRUCTURAL_VERIFICATION_MODE


def test_checked_in_rid_graph_authorities_match_pinned_digests() -> None:
    spec = importlib.util.spec_from_file_location(
        "preview_supply_chain_fresh_authority", SCRIPT_PATH
    )
    assert spec is not None and spec.loader is not None
    fresh = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(fresh)
    for _, _, rid in fresh.ACTIVE_TUPLES:
        authority, relative, digest = fresh._trusted_source_graph_authority(rid)
        assert relative == fresh.RID_GRAPH_SOURCE_AUTHORITY_PATHS[rid]
        assert digest == fresh.RID_GRAPH_SOURCE_AUTHORITY_SHA256[rid]
        assert authority["rid"] == rid
        assert sum(
            row["type"] == "package" for row in authority["libraries"]
        ) == 30
        assert sum(
            row["type"] == "project" for row in authority["libraries"]
        ) == 12
        registry_contracts = next(
            row
            for row in authority["libraries"]
            if row["name"] == "Chummer.Hub.Registry.Contracts"
        )
        assert registry_contracts["type"] == "package"
        assert registry_contracts["version"] == "0.0.0-packageplane.20260718.1"
