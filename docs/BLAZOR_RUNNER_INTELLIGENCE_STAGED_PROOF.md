# Blazor Runner Intelligence Staged Proof

## Purpose

This staged proof turns the Chart LTD idea into a Blazor workbench implementation target named Runner Intelligence.

Runner Intelligence is the character-statistics lane for percentile benchmarks, spell/drug/gear what-if stacks, inventory synergy, and risk-aware build advice. The intended product shape is practical rather than decorative: a runner should be able to see that Initiative is in the top percentile band, model Increase Initiative at a chosen Force, account for drain/stun probability, and include drugs or gear already in inventory without leaking private sheet data.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-runner-intelligence-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_RUNNER_INTELLIGENCE_STAGED_PROOF.generated.json
```

## Staged Controls

The calculation contract is shared in `Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceCalculator.cs`, under `Chummer.Presentation.RunnerIntelligence`, so Blazor and Avalonia can reuse the same percentile, what-if, risk, inventory-synergy, and privacy-output model. `IRunnerIntelligenceCalculator` exposes `CalculatePercentileRank` and `CalculateRisk` for native Avalonia character panes that need the same calculation service without rendering a full browser preview report. `Chummer.Presentation/RunnerIntelligence/RunnerIntelligenceSampleFactory.cs` provides the reusable `BuildIncreaseInitiativeScenario` and `BuildIncreaseInitiativeSample` fixtures for the Increase Initiative scenario, plus named sample constants for default runner/ruleset/cohort values, the scenario id, label, Initiative stat key, Initiative delta, Jazz inventory key, resistance pool, risk threshold, incoming severity, and expected staged risk percentage. It also exposes `IRunnerIntelligenceScenarioCatalog` for dependency-injected UI reuse. `Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligenceServiceCollectionExtensions.cs` provides `AddBlazorRunnerIntelligence` so the browser client registers the shared calculator and scenario catalog through dependency injection. `Chummer.Blazor/RunnerIntelligence/BlazorRunnerIntelligencePreviewService.cs` renders the shared Increase Initiative scenario through the shared calculator rather than duplicating math in Razor. `Chummer.Blazor/Components/Pages/Preview.razor` consumes that service and exposes source-visible `data-runner-intelligence-risk-percent` and `data-runner-intelligence-percentile-delta` markers from `RunnerIntelligenceReport` output. `Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceBridge.cs` is the Avalonia-facing bridge and delegates report calculation, percentile ranking, risk estimation, and Increase Initiative scenario construction to the same calculator and scenario catalog. `Chummer.Desktop.Runtime/RunnerIntelligence/DesktopRunnerIntelligenceServiceCollectionExtensions.cs` provides `AddDesktopRunnerIntelligence` so the Avalonia runtime can register the shared calculator, scenario catalog, and bridge through dependency injection. UI heads should pass their current character projection into `RunnerIntelligenceInput`, `RunnerRiskInput`, or a named `RunnerIntelligenceScenario` and render the returned shared-model output; they should not fork the calculation in page or window code.

The first source-staged implementation slice reserves three Chummer Online and /blazor/workbench compatibility route controls under `tab-stats`:

```text
runner_benchmark
runner_what_if
runner_cohort_privacy
```

`runner_benchmark` covers percentile benchmark posture for Initiative, defense, soak, skill pools, social pools, Matrix pools, and comparable archetype/cohort bands.

`runner_what_if` covers the what-if stack for spells, drugs, gear, adept powers, sustained effects, wound modifiers, action economy, drain/fade/stun risk, legality risk, addiction risk, and nuyen cost. The shared sample models the requested Increase Initiative Force 6 case as final severity after resistance: with incoming severity 6, threshold 1, and a 21-die resistance pool, the reusable calculator reports the staged 87.3% chance of taking no more than 1 Stun. UI heads should read that exact percentage from the shared report/sample constants instead of duplicating it in Blazor or Avalonia copy.

`runner_cohort_privacy` covers hosted `chummer.run` opt-in anonymized benchmark posture and Docker self-host local-only benchmark posture.
The sensitive-field exclusion policy is shared through `RunnerIntelligencePrivacy.DefaultExcludedFields` and named privacy constants, so cohort/privacy UI in Blazor or Avalonia should render the shared exclusion list instead of maintaining separate character, owner, workspace, XML, notes, or dossier-content wording.

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports this staged receipt separately:

```text
runner_intelligence_staged_status=
runner_intelligence_staged_route_count=
runner_intelligence_staged_source_checks=
runner_intelligence_staged_note=source_alignment_only_not_statistical_engine_or_browser_execution
```

These lines are source staging only. They are not statistical-engine proof, browser execution proof, hosted opt-in aggregation proof, Docker self-host local benchmark proof, or rules-engine calculation proof.

## Boundary

This proof checks source-visible affordances, reserved legacy control IDs, desktop-shaped dialog posture, route-entry source, self-host route metadata, release docs, parity docs, docs index links, example receipt shape, source-staged aggregate registration, and status reporting.

It does not calculate percentile distributions, cast spells, apply drugs, mutate inventory, model drain/fade/stun with production rules, prove archetype cohort selection, publish anonymized hosted benchmark aggregation, prove Docker local-only benchmark persistence, or execute browser workflows.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof that exercise Runner Intelligence routes as runtime receipts, plus a calculation proof that validates the percentile and what-if math against authoritative rules-engine fixtures.
