from __future__ import annotations

import base64
import hashlib
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


def write_valid_supply_chain(
    root: Path,
    *,
    version: str,
    source_commit: str,
    supply: Any,
    require_artifact_bytes: bool = False,
    now: datetime | None = None,
) -> None:
    now = (now or datetime.now(UTC)).replace(microsecond=0)
    manifest_path = root / "RELEASE_CHANNEL.generated.json"
    manifest = supply.read_json(manifest_path, "test canonical manifest")
    artifact_paths: dict[str, dict[str, Path]] = {}
    for _, platform, rid in supply.ACTIVE_TUPLES:
        paths: dict[str, Path] = {}
        for relative in supply._expected_artifact_paths(rid):
            path = root / relative
            if not path.is_file():
                path = root.parent / f"{root.name}-{PurePathName(relative)}"
                path.write_bytes(f"test-{rid}-{relative}".encode("utf-8"))
            paths[relative] = path
        artifact_paths[rid] = paths
        row = next(
            row
            for row in manifest["artifacts"]
            if row.get("head") == "avalonia"
            and row.get("platform") == platform
            and row.get("rid") == rid
        )
        installer = paths[supply._expected_artifact_paths(rid)[0]]
        row["sha256"] = supply.sha256_file(installer)
        row["sizeBytes"] = installer.stat().st_size
        if rid == "win-x64":
            payload = paths[supply._expected_artifact_paths(rid)[1]]
            row["payloadSha256"] = supply.sha256_file(payload)
            row["payloadSizeBytes"] = payload.stat().st_size
    manifest_path.write_bytes(supply.canonical_json_bytes(manifest))
    for _, _, rid in supply.ACTIVE_TUPLES:
        package_key = "Safe.Release.Package/10.0.0"
        project_key = "Chummer.Presentation/1.0.0"
        assets = {
            "libraries": {
                package_key: {
                    "sha512": base64.b64encode(bytes(range(64))).decode("ascii"),
                    "type": "package",
                },
                project_key: {"type": "project"},
            },
            "targets": {
                f"net10.0/{rid}": {
                    package_key: {"dependencies": {}},
                    project_key: {"dependencies": {"Safe.Release.Package": "10.0.0"}},
                }
            },
        }
        assets_path = root.parent / f"{root.name}-{rid}-project.assets.json"
        supply.write_new_json(assets_path, assets)
        graph = supply._normalized_rid_graph(assets_path, rid)
        authorities = dict(
            getattr(supply, "_TRUSTED_RID_GRAPH_AUTHORITY_BYTES", {})
        )
        authorities[rid] = supply.canonical_json_bytes(
            supply._source_graph_authority_projection(graph, rid)
        )
        supply.RID_GRAPH_SOURCE_AUTHORITY_SHA256[rid] = hashlib.sha256(
            authorities[rid]
        ).hexdigest()
        supply._TRUSTED_RID_GRAPH_AUTHORITY_BYTES = authorities
        artifacts = artifact_paths[rid]
        sbom = supply.generate_sbom(
            assets_path=assets_path,
            rid=rid,
            version=version,
            source_commit=source_commit,
            artifacts=artifacts,
        )
        sbom_path = root / supply.SBOM_PATHS[rid]
        supply.write_new_json(sbom_path, sbom)
        packages = supply.sbom_package_rows(sbom)
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
                    "source": {"path": supply.SBOM_PATHS[rid], "type": "sbom"},
                }
            ]
        }
        receipt = {
            "advisoryProvenance": {
                "completedAt": supply.utc_text(now),
                "freshUntil": supply.utc_text(now + supply.ADVISORY_FRESHNESS),
                "latestAdvisoryModifiedAt": None,
                "mode": supply.OSV_QUERY_MODE,
                "normalization": supply.OSV_RESPONSE_NORMALIZATION,
                "packageQuerySetSha256": supply.compact_json_sha256(packages),
                "queriedAt": supply.utc_text(now),
                "reproducible": False,
                "responseSha256": supply.compact_json_sha256(response),
                "source": supply.OSV_DATA_SOURCE,
            },
            "blockedFindings": [],
            "contractName": supply.SCAN_CONTRACT,
            "contractVersion": supply.CONTRACT_VERSION,
            "findings": [],
            "legacyAlertAssertions": list(supply.LEGACY_ALERT_ASSERTIONS),
            "packages": packages,
            "release": {"channel": "preview", "version": version},
            "response": response,
            "sbom": {
                "path": supply.SBOM_PATHS[rid],
                "serialNumber": sbom["serialNumber"],
                "sha256": supply.sha256_file(sbom_path),
            },
            "scanner": {
                "binarySha256": supply.OSV_SCANNER_SHA256,
                "commit": supply.OSV_SCANNER_COMMIT,
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
                    supply.SBOM_PATHS[rid],
                ],
                "name": supply.OSV_SCANNER_NAME,
                "version": supply.OSV_SCANNER_VERSION,
            },
            "status": "passed",
            "tuple": {
                "head": "avalonia",
                "platform": "windows" if rid == "win-x64" else "linux",
                "rid": rid,
            },
            "unclassifiedFindings": [],
        }
        supply.write_new_json(root / supply.SCAN_PATHS[rid], receipt)
    supply.finalize_gate(
        stage_root=root,
        version=version,
        source_commit=source_commit,
        now=now,
        require_artifact_bytes=require_artifact_bytes,
    )


def PurePathName(relative: str) -> str:
    return relative.replace("/", "-")
