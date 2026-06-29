from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
HORIZON_RECEIPT = PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json"
EXECUTION_PROOF = PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json"
COMPILE_MANIFEST = PUBLISHED / "compile.manifest.json"
MATERIALIZER = REPO_ROOT / "scripts" / "materialize-blazor-public-edge-execution-horizon.py"
DOCS_INDEX = REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md"
SIGNOFF_DOC = REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md"
EXECUTION_DOC = REPO_ROOT / "docs" / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md"


def _read_json(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    assert isinstance(payload, dict)
    return payload


def test_execution_horizon_materializer_uses_verifier_sets_and_failed_sidecar_boundary() -> None:
    source = MATERIALIZER.read_text(encoding="utf-8")

    assert "SMOKE_REQUIRED_WORKFLOW_FAMILY_IDS" in source
    assert "AVAILABLE_WORKFLOW_FAMILY_IDS" in source
    assert "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.failed.generated.json" in source
    assert "does_not_upgrade_smoke_to_full" in source
    assert "full_scope_requires_current_passing_full_receipt" in source
    assert "published full-scope hosted execution proof is missing full-required workflow families" in source


def test_execution_horizon_receipt_tracks_smoke_and_full_matrix_separately() -> None:
    horizon = _read_json(HORIZON_RECEIPT)
    execution = _read_json(EXECUTION_PROOF)
    horizons = {item["id"]: item for item in horizon["horizons"]}
    current_scope = execution["playwright_scope"]

    assert horizon["contract_name"] == "chummer6-ui.blazor_public_edge_execution_horizon"
    assert horizon["status"] == "passed"
    assert horizon["boundary"]["does_not_upgrade_smoke_to_full"] is True
    assert horizons["near_term_hosted_smoke_execution"]["status"] == "proven"

    full_horizon = horizons["mid_term_full_live_public_edge_execution_matrix"]
    if current_scope == "full":
        assert full_horizon["status"] == "proven"
        assert full_horizon["missing_workflow_family_ids"] == []
    else:
        assert current_scope == "smoke"
        assert full_horizon["status"] == "not_proven"
        assert full_horizon["missing_workflow_family_ids"]


def test_execution_horizon_is_indexed_and_documented() -> None:
    artifacts = set(_read_json(COMPILE_MANIFEST)["artifacts"])
    docs_index = DOCS_INDEX.read_text(encoding="utf-8")
    signoff = SIGNOFF_DOC.read_text(encoding="utf-8")
    execution_doc = EXECUTION_DOC.read_text(encoding="utf-8")

    assert "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json" in artifacts
    assert "scripts/materialize-blazor-public-edge-execution-horizon.py" in docs_index
    assert "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json" in docs_index
    assert "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json" in signoff
    assert "must not upgrade smoke execution evidence into a full live public-edge matrix claim" in signoff
    assert "python3 scripts/materialize-blazor-public-edge-execution-horizon.py" in execution_doc
