# Blazor Runner Intelligence Calculation Proof

## Purpose

This proof validates the shared Runner Intelligence calculation seam before runtime browser promotion.

It is intentionally narrower than rules-engine correctness. It proves that Blazor and Avalonia reuse `Chummer.Presentation.RunnerIntelligence`, that the Increase Initiative sample is built through `RunnerIntelligenceSampleFactory` and `IRunnerIntelligenceScenarioCatalog`, and that risk probability is modeled as final severity after resistance rather than raw low-hit probability.
The shared `IRunnerIntelligenceCalculator` also exposes `CalculatePercentileRank` and `CalculateRisk`, so Avalonia can reuse the same percentile and drain/stun probability service directly for native character windows instead of copying Blazor preview math.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-runner-intelligence-calculation-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_RUNNER_INTELLIGENCE_CALCULATION_PROOF.generated.json
```

## Calculation Boundary

The reusable seam exposes:

```text
RunnerIntelligenceCalculator
RunnerIntelligenceScenario
RunnerIntelligenceInput
RunnerIntelligenceReport
IRunnerIntelligenceScenarioCatalog
RunnerWhatIfResult
RunnerRiskInput
CalculatePercentileRank
CalculateRisk
ChanceFinalSeverityAtOrBelow
ChanceHitsAtOrAbove
PercentileRank
```

The shared sample fixture models `increase_initiative_force_6` with Jazz inventory synergy, incoming severity 6, threshold 1, and a 21-die resistance pool to represent the staged 87.3% chance of taking no more than 1 Stun. That exact percentage is owned by `IncreaseInitiativeExpectedChanceAtOrBelowThresholdPercent`, not by UI prose. The rest of the sample semantics are exposed as reusable `RunnerIntelligenceSampleFactory` constants such as `DefaultRunnerId`, `DefaultRuleset`, `DefaultCohortLabel`, `IncreaseInitiativeScenarioId`, `JazzInventoryKey`, `IncreaseInitiativeStatDelta`, `IncreaseInitiativeResistancePool`, `IncreaseInitiativeRiskThreshold`, and `IncreaseInitiativeIncomingSeverity` so Avalonia and Blazor heads do not copy magic values.

The Avalonia-facing `DesktopRunnerIntelligenceBridge` delegates report calculation, percentile ranking, risk estimation, and Increase Initiative scenario construction to the shared calculator/catalog so native panes can reuse the same service without depending on Blazor preview code.

Runner Intelligence privacy exclusions are also owned by the shared model through `RunnerIntelligencePrivacy.DefaultExcludedFields` and named constants such as `CharacterNamesField`, `OwnerIdsField`, `WorkspaceIdsField`, and `DossierTextField`, so Blazor, Avalonia, hosted analytics copy, and self-host docs do not fork sensitive-field policy.

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports this calculation proof separately:

```text
runner_intelligence_calculation_status=
runner_intelligence_calculation_tier=
runner_intelligence_calculation_note=shared_calculation_source_only_not_rules_engine_or_browser_execution
```

These lines are source calculation proof only. They are not browser execution proof, hosted cohort aggregation proof, Docker local benchmark persistence proof, and not authoritative SR rules-engine validation.
