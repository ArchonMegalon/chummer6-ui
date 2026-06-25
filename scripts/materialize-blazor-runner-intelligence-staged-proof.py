#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json",
    )
)

EXPECTED_ROUTES = [
    "/blazor/workbench?workspace=ws-1&tab=tab-stats&control=runner_benchmark",
    "/blazor/workbench?workspace=ws-1&tab=tab-stats&control=runner_what_if",
    "/blazor/workbench?workspace=ws-1&tab=tab-stats&control=runner_cohort_privacy",
]

CHECKS = [
    {
        "id": "product_workbench_affordances",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Open Runner Intelligence benchmarks for @PrimaryRecentWorkspace.ShortLabel",
            "Model Increase Initiative and inventory what-if stack for @PrimaryRecentWorkspace.ShortLabel",
            "Review Runner Intelligence privacy cohorts for @PrimaryRecentWorkspace.ShortLabel",
            "@inject BlazorRunnerIntelligencePreviewService RunnerIntelligencePreviewService",
            "RunnerIntelligencePreviewService.BuildIncreaseInitiativePreview()",
            "data-runner-intelligence-shared-calculator",
            "data-runner-intelligence-sample",
            "data-runner-intelligence-risk-percent",
            "data-runner-intelligence-percentile-delta",
            "runnerIntelligencePreview.WhatIfCards[0]",
            "RunnerIntelligenceReport",
            "sample models final severity after resistance",
        ],
    },
    {
        "id": "shared_control_catalog",
        "path": "Chummer.Presentation/Overview/LegacyUiControlCatalog.cs",
        "tokens": [
            "runner_benchmark",
            "runner_what_if",
            "runner_cohort_privacy",
        ],
    },
    {
        "id": "shared_reusable_calculator",
        "path": "Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceCalculator.cs",
        "tokens": [
            "namespace Chummer.Presentation.RunnerIntelligence",
            "public sealed class RunnerIntelligenceCalculator",
            "RunnerIntelligenceInput",
            "RunnerIntelligenceReport",
            "RunnerRiskInput",
            "CalculatePercentileRank",
            "CalculateRisk",
            "PercentileRank",
            "ChanceHitsAtOrBelow",
            "ChanceHitsAtOrAbove",
            "ChanceFinalSeverityAtOrBelow",
            "RunnerIntelligencePrivacy LocalOnly",
        ],
    },
    {
        "id": "shared_increase_initiative_fixture",
        "path": "Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceSampleFactory.cs",
        "tokens": [
            "namespace Chummer.Presentation.RunnerIntelligence",
            "RunnerIntelligenceSampleFactory",
            "BuildIncreaseInitiativeSample",
            "increase_initiative_force_6",
            "Increase Initiative Force 6",
            "jazz",
            "RunnerIntelligencePrivacy.LocalOnly",
            "DefaultExcludedFields",
            "CharacterNamesField",
            "OwnerIdsField",
            "WorkspaceIdsField",
            "DossierTextField",
            "87% chance of taking no more than 1 Stun",
            "final drain and spellcasting resolution",
        ],
    },
    {
        "id": "avalonia_reuse_bridge",
        "path": "Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceBridge.cs",
        "tokens": [
            "namespace Chummer.Desktop.Runtime.RunnerIntelligence",
            "RunnerIntelligenceCalculator",
            "RunnerIntelligenceInput",
            "RunnerIntelligenceReport",
            "CalculateIncreaseInitiativeSample",
            "RunnerIntelligenceSampleFactory.BuildIncreaseInitiativeSample",
        ],
    },
    {
        "id": "blazor_di_registration",
        "path": "Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligenceServiceCollectionExtensions.cs",
        "tokens": [
            "AddBlazorRunnerIntelligence",
            "RunnerIntelligenceCalculator",
            "BlazorRunnerIntelligencePreviewService",
            "AddScoped<RunnerIntelligenceCalculator>",
            "AddScoped<BlazorRunnerIntelligencePreviewService>",
        ],
    },
    {
        "id": "blazor_preview_service",
        "path": "Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligencePreviewService.cs",
        "tokens": [
            "BlazorRunnerIntelligencePreviewService",
            "RunnerIntelligenceCalculator",
            "RunnerIntelligenceSampleFactory.BuildIncreaseInitiativeSample",
            "BuildIncreaseInitiativePreview",
        ],
    },
    {
        "id": "blazor_program_registration",
        "path": "Chummer.Blazor/Program.cs",
        "tokens": [
            "using Chummer.Blazor.RunnerIntelligence;",
            "AddBlazorRunnerIntelligence",
        ],
    },
    {
        "id": "avalonia_di_registration",
        "path": "Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceServiceCollectionExtensions.cs",
        "tokens": [
            "AddDesktopRunnerIntelligence",
            "RunnerIntelligenceCalculator",
            "DesktopRunnerIntelligenceBridge",
            "IServiceCollection",
        ],
    },
    {
        "id": "desktop_shaped_dialogs",
        "path": "Chummer.Presentation/Overview/DesktopDialogFactory.cs",
        "tokens": [
            "dialog.ui.runner_benchmark",
            "Runner Intelligence",
            "Initiative Percentile",
            "dialog.ui.runner_what_if",
            "Increase Initiative Force 6",
            "87% chance of taking no more than 1 Stun",
            "dialog.ui.runner_cohort_privacy",
            "Opt-in anonymized benchmark cohorts",
            "dossier id",
            "dossier ids",
            "dossier text",
        ],
    },
    {
        "id": "hosted_route_entry_probe",
        "path": "scripts/e2e-public-edge.cjs",
        "tokens": EXPECTED_ROUTES,
    },
    {
        "id": "self_host_receipt_metadata",
        "path": "scripts/e2e-portal.sh",
        "tokens": EXPECTED_ROUTES,
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "Runner Intelligence",
            "blazor-runner-intelligence-staged-proof-check.sh",
            "not statistical-engine proof or browser execution proof",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "Runner Intelligence",
            "runner_benchmark",
            "runner_what_if",
            "runner_cohort_privacy",
            "Increase Initiative Force 6",
        ],
    },
    {
        "id": "contract_doc",
        "path": "docs/BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.md",
        "tokens": [
            "percentile benchmarks",
            "spell/drug/gear what-if stacks",
            "final severity after resistance",
            "21-die resistance pool",
            "runner_intelligence_staged_note=source_alignment_only_not_statistical_engine_or_browser_execution",
            "not statistical-engine proof",
        ],
    },
    {
        "id": "docs_index_contract_link",
        "path": "docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md",
        "tokens": [
            "docs/BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.md",
            "Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceCalculator.cs",
            "Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceSampleFactory.cs",
            "Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceBridge.cs",
            "Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligenceServiceCollectionExtensions.cs",
            "Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligencePreviewService.cs",
            "Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceServiceCollectionExtensions.cs",
            "scripts/materialize-blazor-runner-intelligence-staged-proof.py",
            "docs/examples/blazor-runner-intelligence-staged-proof.receipt.example.json",
        ],
    },
    {
        "id": "example_receipt_shape",
        "path": "docs/examples/blazor-runner-intelligence-staged-proof.receipt.example.json",
        "tokens": [
            '"contract_name": "chummer6-ui.blazor_runner_intelligence_staged_proof"',
            '"proof_tier": "source_staged_no_browser_execution"',
            "runner_benchmark",
            "The shared Increase Initiative sample models final severity after resistance, not raw low-hit probability.",
            "The Blazor preview renders source-visible risk-percent and percentile-delta markers from RunnerIntelligenceReport output.",
            "Do not use this receipt to claim Runner Intelligence percentile or what-if calculation parity on chummer.run.",
        ],
    },
    {
        "id": "calculation_proof_contract",
        "path": "docs/BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.md",
        "tokens": [
            "Runner Intelligence Calculation Proof",
            "blazor-runner-intelligence-calculation-proof-check.sh",
            "BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json",
            "final severity after resistance",
            "not authoritative SR rules-engine validation",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "RUNNER_INTELLIGENCE_STAGED_PROOF",
            "RUNNER_INTELLIGENCE_CALCULATION_PROOF",
            "runner_intelligence_staged_status",
            "runner_intelligence_calculation_status",
            "runner_intelligence_staged_route_count",
            "source_alignment_only_not_statistical_engine_or_browser_execution",
        ],
    },
    {
        "id": "source_staged_aggregate_registration",
        "path": "scripts/materialize-blazor-source-staged-proof-set.py",
        "tokens": [
            "runner_intelligence",
            "BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json",
            "chummer6-ui.blazor_runner_intelligence_staged_proof",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")


def main() -> int:
    checks = []
    failures = []

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
        status = "failed" if missing_tokens else "passed"
        if missing_tokens:
            failures.append(f"{path}: missing {', '.join(missing_tokens)}")
        checks.append(
            {
                "id": check["id"],
                "path": path,
                "status": status,
                "required_token_count": len(tokens),
                "missing_tokens": missing_tokens,
            }
        )

    receipt = {
        "contract_name": "chummer6-ui.blazor_runner_intelligence_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": EXPECTED_ROUTES,
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that source, route metadata, and documentation staging agree.",
            "It is not statistical-engine proof or browser execution proof.",
            "Do not use this receipt to claim Runner Intelligence percentile or what-if calculation parity on chummer.run.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_runner_intelligence_staged_proof:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
