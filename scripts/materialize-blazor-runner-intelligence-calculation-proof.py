#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "shared_calculator_contract",
        "path": "Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceCalculator.cs",
        "tokens": [
            "public sealed class RunnerIntelligenceCalculator",
            "RunnerIntelligenceInput",
            "RunnerIntelligenceReport",
            "RunnerWhatIfResult",
            "RunnerRiskInput",
            "CalculatePercentileRank",
            "CalculateRisk",
            "PercentileRank",
            "ChanceFinalSeverityAtOrBelow",
            "ChanceHitsAtOrAbove",
            "ChanceHitsAtOrBelow",
            "DefaultExcludedFields",
            "CharacterNamesField",
            "OwnerIdsField",
            "WorkspaceIdsField",
            "DossierTextField",
            "RunnerIntelligencePrivacy LocalOnly",
            "incomingSeverity",
            "finalSeverityThreshold",
        ],
    },
    {
        "id": "increase_initiative_sample_semantics",
        "path": "Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceSampleFactory.cs",
        "tokens": [
            "BuildIncreaseInitiativeSample",
            "DefaultRunnerId",
            "DefaultRuleset",
            "DefaultCohortLabel",
            "IncreaseInitiativeScenarioId",
            "IncreaseInitiativeLabel",
            "InitiativeStatKey",
            "JazzInventoryKey",
            "IncreaseInitiativeStatDelta",
            "IncreaseInitiativeResistancePool",
            "IncreaseInitiativeRiskThreshold",
            "IncreaseInitiativeIncomingSeverity",
            "IncreaseInitiativeExpectedChanceAtOrBelowThresholdPercent",
            "increase_initiative_force_6",
            "Increase Initiative Force 6",
            "StatDelta: IncreaseInitiativeStatDelta",
            "InventoryItemKeys: new[] { JazzInventoryKey }",
            "ResistancePool: IncreaseInitiativeResistancePool",
            "RiskThreshold: IncreaseInitiativeRiskThreshold",
            "RiskSeverity: IncreaseInitiativeIncomingSeverity",
            "IncreaseInitiativeExpectedChanceAtOrBelowThresholdPercent = 87.3d",
            "{IncreaseInitiativeExpectedChanceAtOrBelowThresholdPercent:0.#}% chance of taking no more than {IncreaseInitiativeRiskThreshold} Stun",
        ],
    },
    {
        "id": "blazor_consumption_path",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "BlazorRunnerIntelligencePreviewService",
            "RunnerIntelligenceReport runnerIntelligencePreview",
            "RunnerWhatIfResult runnerIntelligenceWhatIf",
            "data-runner-intelligence-risk-percent",
            "data-runner-intelligence-percentile-delta",
        ],
    },
    {
        "id": "avalonia_consumption_path",
        "path": "Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceBridge.cs",
        "tokens": [
            "DesktopRunnerIntelligenceBridge",
            "RunnerIntelligenceCalculator",
            "RunnerIntelligenceReport Calculate",
            "CalculatePercentileRank",
            "CalculateRisk",
            "BuildIncreaseInitiativeScenario",
            "RunnerIntelligenceSampleFactory.DefaultRunnerId",
            "RunnerIntelligenceSampleFactory.DefaultRuleset",
            "RunnerIntelligenceSampleFactory.DefaultCohortLabel",
            "CalculateIncreaseInitiativeSample",
            "_scenarioCatalog.BuildIncreaseInitiativeScenario",
        ],
    },
    {
        "id": "contract_doc",
        "path": "docs/BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.md",
        "tokens": [
            "final severity after resistance",
            "21-die resistance pool",
            "runner_intelligence_calculation_note=shared_calculation_source_only_not_rules_engine_or_browser_execution",
            "not authoritative SR rules-engine validation",
        ],
    },
    {
        "id": "docs_index_link",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "docs/BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.md",
            "scripts/materialize-blazor-runner-intelligence-calculation-proof.py",
            "docs/examples/blazor-runner-intelligence-calculation-proof.receipt.example.json",
        ],
    },
    {
        "id": "example_receipt_shape",
        "path": "docs/examples/blazor-runner-intelligence-calculation-proof.receipt.example.json",
        "tokens": [
            '"contract_name": "chummer6-ui.blazor_runner_intelligence_calculation_proof"',
            '"proof_tier": "source_calculation_no_browser_execution"',
            "final severity after resistance",
            "not authoritative SR rules-engine validation",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "RUNNER_INTELLIGENCE_CALCULATION_PROOF",
            "runner_intelligence_calculation_status",
            "runner_intelligence_calculation_tier",
            "shared_calculation_source_only_not_rules_engine_or_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    failures = []
    checks = []

    for check in CHECKS:
        path = check["path"]
        tokens = check["tokens"]
        try:
            text = read_text(path)
        except FileNotFoundError:
            failures.append(f"{path}: missing file")
            checks.append({**check, "status": "failed", "missing_tokens": tokens})
            continue

        missing_tokens = [token for token in tokens if token not in text]
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": "failed" if missing_tokens else "passed",
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    receipt = {
        "contract_name": "chummer6-ui.blazor_runner_intelligence_calculation_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_calculation_no_browser_execution",
        "calculation_lane": "shared_runner_intelligence",
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt proves only that the shared Runner Intelligence calculation seam and sample semantics are source-aligned.",
            "It verifies final severity after resistance is the modeled risk path for the Increase Initiative sample.",
            "It is not authoritative SR rules-engine validation, hosted browser execution proof, Docker execution proof, or hosted cohort aggregation proof.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_runner_intelligence_calculation_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
