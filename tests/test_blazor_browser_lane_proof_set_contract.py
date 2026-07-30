from __future__ import annotations

import importlib.util
import json
from pathlib import Path
from types import ModuleType


REPO_ROOT = Path(__file__).resolve().parents[1]
MATERIALIZER_PATH = REPO_ROOT / "scripts/materialize-blazor-browser-lane-proof-set.py"


def load_materializer() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "materialize_blazor_browser_lane_proof_set",
        MATERIALIZER_PATH,
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def external_host_spec(module: ModuleType, path: Path) -> dict:
    source = next(
        item
        for item in module.REQUIRED_RECEIPTS
        if item["id"] == "external_host_blockers"
    )
    return {**source, "path": path}


def write_external_host_receipt(path: Path, **updates: object) -> None:
    payload: dict[str, object] = {
        "contract_name": "chummer6-ui.external_host_proof_blockers",
        "status": "blocked",
        "browser_route_blocker_count": 0,
        "browser_route_entry_proof_status": "passed",
        "browser_route_entry_proof_shape": "expanded",
        "browser_execution_proof_status": "passed",
        "missing_required_platform_head_rid_tuples": [
            "avalonia:osx-arm64:macos"
        ],
    }
    payload.update(updates)
    path.write_text(json.dumps(payload), encoding="utf-8")


def test_native_host_blocker_does_not_falsely_fail_browser_lane(tmp_path: Path) -> None:
    module = load_materializer()
    receipt_path = tmp_path / "external-host.json"
    write_external_host_receipt(receipt_path)

    result = module.evaluate_receipt(external_host_spec(module, receipt_path))

    assert result["passed"] is True
    assert result["status"] == "blocked"


def test_browser_route_blocker_still_fails_browser_lane(tmp_path: Path) -> None:
    module = load_materializer()
    receipt_path = tmp_path / "external-host.json"
    write_external_host_receipt(receipt_path, browser_route_blocker_count=1)

    result = module.evaluate_receipt(external_host_spec(module, receipt_path))

    assert result["passed"] is False
    assert any(
        check["id"] == "field:browser_route_blocker_count"
        and check["passed"] is False
        for check in result["checks"]
    )


def test_external_host_receipt_contract_is_exact(tmp_path: Path) -> None:
    module = load_materializer()
    receipt_path = tmp_path / "external-host.json"
    write_external_host_receipt(receipt_path, contract_name="generic.pass")

    result = module.evaluate_receipt(external_host_spec(module, receipt_path))

    assert result["passed"] is False
    assert any(
        check["id"] == "contract_name" and check["passed"] is False
        for check in result["checks"]
    )
