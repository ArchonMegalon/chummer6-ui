# Shared character statistics contract

Character statistics, percentile ranking, and recommendation explanations must be calculated by shared Chummer logic, not by the Blazor UI. Blazor and Avalonia should consume the same result model so the web client and desktop client explain the same character state in the same way.

## Ownership boundary

- Shared core owns character math, percentile ranking, probability/risk calculations, and recommendation generation.
- Blazor renders shared results and exposes route/workflow state.
- Avalonia renders the same shared results in native desktop surfaces.
- UI layers must not duplicate formulas for spells, drugs, gear modifiers, quality modifiers, drain/stun risk, initiative changes, or percentile ranking.

## Input model

The shared statistics engine should accept a privacy-safe character snapshot rather than UI objects.

Required input groups:

- Character identity key: stable local/dossier key, not display name.
- Ruleset: SR5 or future supported rulesets.
- Attribute summary: relevant attributes, limits, derived values, initiative, condition state.
- Skills and qualities: normalized IDs and ranks.
- Magic/resonance state: spells, powers, forms, force limits, drain pools, sustaining constraints.
- Inventory state: gear, drugs, augmentations, active effects, quantities, availability flags.
- Build context: creation/career mode, karma/nuyen budget state, validation state.
- Comparison cohort key: anonymized population/filter key, never raw user data.

Optional input groups:

- Campaign constraints.
- GM/house-rule flags.
- User-selected recommendation goal, such as initiative, survivability, stealth, social, matrix, or drain safety.

## Output model

The shared statistics engine should return a result object suitable for both Blazor and Avalonia.

Required output groups:

- `ResultState`: pending, ready, stale, insufficient-data, or error.
- `MetricResults`: one entry per computed metric.
- `PercentileBand`: normalized band such as top-1, top-3, top-10, above-average, average, below-average, unknown.
- `CohortSummary`: anonymized comparison description and sample-size band.
- `Recommendations`: ranked explainable actions.
- `RiskAssessments`: probability and consequence summaries.
- `Evidence`: reusable explanation lines with source IDs, formula IDs, and input references.
- `PrivacyLevel`: confirms whether result is local-only, anonymized, or publishable.

## Recommendation model

Recommendations must be explainable and reproducible.

Each recommendation should include:

- Stable recommendation ID.
- Goal key, such as improve-initiative or reduce-drain-risk.
- Action type, such as cast-spell, use-drug, equip-gear, change-sustaining-plan, or review-quality.
- Required inputs, such as specific spell ID, force, drug ID, gear ID, or inventory quantity.
- Expected effect summary.
- Risk summary.
- Probability statement.
- Assumption list.
- Blocking reasons if the action cannot currently be taken.

Example shape:

```text
Recommendation: Increase Initiative, Force 6
Expected effect: initiative metric enters top percentile band
Risk: 87% chance of no more than 1 stun damage
Inputs: spell increase-initiative, force 6, current drain pool, inventory drug X
Evidence: spell rules, drain calculation, active inventory, cohort percentile
```

The actual values must come from shared rules logic and cohort data, not UI text.

## Risk model

Risk calculations should expose enough detail to be audited without leaking private character contents.

Required risk outputs:

- Risk model key, such as damage-threshold-probability.
- Threshold, such as no-more-than-1-stun.
- Probability band and exact probability when allowed.
- Damage/consequence type.
- Inputs used by ID, not display payload.
- Formula/source IDs.
- Confidence state: exact, estimated, insufficient-data, or blocked.

## Percentile model

Percentiles must be cohort-aware.

A percentile result must include:

- Metric key.
- Percentile band.
- Cohort key.
- Cohort sample-size band.
- Ruleset.
- Filters applied, such as career/build mode, archetype, karma range, or table/campaign scope.
- Staleness marker.

The UI may say “top 3%” only when the shared result explicitly allows an exact percentile statement. Otherwise it should use a banded statement like “top percentile band”.

## Privacy and telemetry boundary

Allowed in UI metadata or analytics:

- Route/workflow keys.
- Metric keys.
- Percentile bands.
- Result states.
- Recommendation action types.
- Risk model keys.
- Anonymized cohort keys.

Forbidden in analytics or public DOM metadata:

- Character names.
- Owner/account identifiers.
- Raw XML.
- Full inventory payload.
- Exact private build contents.
- Free-form notes.
- File paths.

## Blazor rendering contract

Blazor should render shared results through shell metadata and visible panels.

Relevant shell attributes:

- `data-character-statistics`
- `data-statistics-scope`
- `data-recommendation-mode`
- `data-recommendation-inputs`
- `data-risk-model`
- `data-statistics-result`
- `data-percentile-band`
- `data-recommendation-state`
- `data-calculation-boundary`
- `data-result-consumer`

Blazor may show pending states while shared calculations are unavailable, but it must not invent percentile or probability values.

## Avalonia rendering contract

Avalonia should consume the same shared result object and may render it in native panels, dialogs, inspectors, or character-sheet summaries.

Avalonia-specific UI may differ visually, but must preserve:

- Same metric keys.
- Same percentile bands.
- Same recommendation IDs.
- Same risk summaries.
- Same evidence/source IDs.
- Same privacy rules.

## Initial implementation milestones

1. Define shared result DTOs in a non-Blazor project.
2. Add deterministic unit coverage for percentile band mapping.
3. Add deterministic unit coverage for drain/stun threshold probability.
4. Wire Blazor to render pending/ready/error states from the shared DTO.
5. Wire Avalonia to render the same DTO.
6. Add privacy checks ensuring telemetry never includes private character payloads.

## Initial DTO location

The initial shared DTO surface lives in:

- `Chummer.Presentation/CharacterStatistics/CharacterStatisticsContracts.cs`
- `Chummer.Presentation/CharacterStatistics/CharacterStatisticsServices.cs`
- `Chummer.Presentation/CharacterStatistics/PendingCharacterStatisticsCalculator.cs`
- `Chummer.Presentation/CharacterStatistics/CharacterStatisticsProjection.cs`
- `Chummer.Presentation/CharacterStatistics/CharacterStatisticsProjectionService.cs`

These files are intentionally UI-framework independent. Blazor and Avalonia should consume these contracts rather than defining separate statistics result shapes or calculation entry points in their UI layers.

`PendingCharacterStatisticsCalculator` is a deterministic placeholder. It returns a pending shared-calculation result so UI layers can wire the service boundary, but it must not be treated as a real percentile/risk implementation.
