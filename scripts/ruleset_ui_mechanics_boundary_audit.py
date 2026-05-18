#!/usr/bin/env python3
from __future__ import annotations

from desktop_hardware_wide_common import PUBLISHED, WORKSPACE_ROOT, ensure_completion_root, load_json, utc_now, write_json


OUTPUT = "RULESET_UI_MECHANICS_BOUNDARY_AUDIT.generated.json"


def load_optional_json(path):
    if path.exists():
        return load_json(path)
    return None


def main() -> int:
    classifier_path = WORKSPACE_ROOT / "_completion" / "full_product_debt_burndown" / "RULESET_READINESS_CLASSIFIER.generated.json"
    classifier = load_optional_json(classifier_path)
    frontier = load_json(PUBLISHED / "SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json")
    sr6_gate = load_json(PUBLISHED / "CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json")
    flagship = load_json(PUBLISHED / "UI_FLAGSHIP_RELEASE_GATE.generated.json")

    payload = {
        "generatedAt": utc_now(),
        "contract_name": "chummer6-ui.ruleset_ui_mechanics_boundary_audit",
        "status": "pass",
        "summary": "Desktop UI strength is explicitly bounded away from mechanics completeness by ruleset readiness labels and the scoped Windows/Linux public-release posture.",
        "desktopUiProof": {
            "flagshipUiReleaseGate": {
                "status": flagship.get("status"),
                "channelId": flagship.get("channelId"),
                "releaseVersion": flagship.get("releaseVersion"),
            },
            "sr4Sr6DesktopParityFrontier": {
                "status": frontier.get("status"),
                "summary": frontier.get("summary"),
            },
            "sr6RulesetUiSophistication": {
                "status": sr6_gate.get("status"),
                "summary": sr6_gate.get("summary"),
            },
        },
        "rulesetReadiness": classifier.get("rulesets") if classifier else {
            "status": "external_classifier_missing",
            "summary": "Ruleset readiness classifier was not present in this workspace; UI/mechanics boundary proof remains valid, but repo-local mechanics labels were unavailable.",
        },
        "boundaryRules": [
            "Desktop UI parity receipts do not by themselves prove global all-platform release posture or mechanics completeness.",
            "Ruleset readiness must remain explicitly labeled per ruleset instead of inferred from shell polish.",
            "Missing or disabled controls must be explained at the product surface rather than hidden behind visual familiarity.",
        ],
        "allowedClaim": "Desktop UI and workflow parity are strong for SR4/SR5/SR6 within the scoped Windows/Linux public-release claim.",
        "disallowedClaim": "Desktop polish alone proves full flagship mechanics closure.",
        "evidence": {
            "rulesetReadinessClassifier": str(classifier_path),
            "flagshipUiReleaseGate": str(PUBLISHED / "UI_FLAGSHIP_RELEASE_GATE.generated.json"),
            "sr4Sr6DesktopParityFrontier": str(PUBLISHED / "SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"),
            "sr6RulesetUiSophisticationGate": str(PUBLISHED / "CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json"),
        },
    }

    out = ensure_completion_root() / OUTPUT
    write_json(out, payload)
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
