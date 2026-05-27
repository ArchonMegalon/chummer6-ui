#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_NO_NOISE_SHELL_GATE.generated.json"
JUSTIFICATION_ARTIFACT = REPO_ROOT / ".codex-studio" / "published" / "CHUMMER5A_CHUMMER6_ONLY_CONTROL_JUSTIFICATION.generated.json"


def main() -> int:
    payload = json.loads(ARTIFACT.read_text(encoding="utf-8"))
    justification_payload = json.loads(JUSTIFICATION_ARTIFACT.read_text(encoding="utf-8"))
    if str(payload.get("status") or "").strip().lower() != "pass":
        raise SystemExit("no-noise shell gate is not passing")

    if str(payload.get("channelId") or "").strip() != "public_stable":
        raise SystemExit("no-noise shell gate must target public_stable")

    hidden_catalog_controls = payload.get("hiddenCatalogControls")
    if not isinstance(hidden_catalog_controls, list):
        raise SystemExit("hiddenCatalogControls is missing")
    normalized_hidden_catalog_controls = {
        str(value).strip()
        for value in hidden_catalog_controls
        if str(value).strip()
    }
    if not normalized_hidden_catalog_controls:
        raise SystemExit("hiddenCatalogControls are empty")

    required_hidden_catalog_controls = justification_payload.get("requiredHiddenCatalogControls")
    if not isinstance(required_hidden_catalog_controls, list) or not required_hidden_catalog_controls:
        raise SystemExit("requiredHiddenCatalogControls are missing from control justification artifact")
    normalized_required_hidden_catalog_controls = {
        str(value).strip()
        for value in required_hidden_catalog_controls
        if str(value).strip()
    }
    if normalized_hidden_catalog_controls != normalized_required_hidden_catalog_controls:
        raise SystemExit(
            "hiddenCatalogControls do not match control justification artifact: "
            f"{sorted(normalized_hidden_catalog_controls)} != {sorted(normalized_required_hidden_catalog_controls)}"
        )

    forbidden_controls = payload.get("forbiddenControls")
    if not isinstance(forbidden_controls, list) or "LoadDemoRunnerButton" not in forbidden_controls:
        raise SystemExit("no-noise shell gate must fail closed on LoadDemoRunnerButton")

    runtime_screenshot_proof = payload.get("runtimeScreenshotProof")
    if not isinstance(runtime_screenshot_proof, str) or "SCREENSHOT_CONTROL_EVIDENCE.generated.json" not in runtime_screenshot_proof:
        raise SystemExit("no-noise shell gate must record runtime screenshot proof")
    if not (REPO_ROOT.parent / runtime_screenshot_proof).exists():
        raise SystemExit("runtime screenshot proof path does not exist")

    test_refs = payload.get("testRefs")
    if not isinstance(test_refs, list) or len(test_refs) < 2:
        raise SystemExit("no-noise shell gate requires executable test refs")

    reasons = payload.get("reasons")
    if not isinstance(reasons, list) or reasons:
        raise SystemExit("no-noise shell gate reasons must be empty on pass")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
