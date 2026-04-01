# Ruleset UI Directive

Purpose: make ruleset-specific workbench/browser/desktop follow-through explicit in `chummer6-ui` so Fleet and local workers do not confuse shared shell maturity with per-ruleset product completion.

This directive is the UI-side companion to the core-side ruleset execution matrices:

- SR4 oracle extraction and promotion planning
- SR5 provider-host and parity completion
- SR6 runtime-reconciliation and parity completion

Core owns mechanics truth.
UI owns how each ruleset is presented, explained, routed, badged, validated, and constrained across the workbench/browser/desktop product.

## Boundary rule

UI must adapt to ruleset truth without becoming a second rules engine.

That means `chummer6-ui` owns:

- ruleset selection and posture messaging
- shell/workflow/tab/action routing per registered ruleset
- ruleset-specific section availability and empty/error states
- Build Lab, Runtime Inspector, RuleProfile, RulePack, browse, and NPC/build-kit presentation depth
- explain, validation, disabled-reason, and unsupported-capability rendering
- import/export/print labels, file naming, and ruleset-safe affordances
- cross-head parity and release-proof coverage for the supported ruleset matrix

It must not own:

- Shadowrun rules math
- canonical capability execution
- XML parsing internals
- rule-profile or rule-pack authority

## Current UI risk

The workbench shell is materially complete as a shared product surface, but ruleset-specific adaptation depth is not yet modeled as a first-class queue lane.

That creates three risks:

1. SR4 can look merely “missing engine work” when the UI still needs explicit experimental/preview posture.
2. SR5 can look “done” because the workbench ships, even though the default runtime lane still terminates in an unavailable deterministic host.
3. SR6 can look fully supported because it is default-registered, even though its core rules host still returns experimental diagnostics.

## Ruleset posture by lane

| Ruleset | Core posture | Required UI posture |
| --- | --- | --- |
| SR4 | scaffolded/experimental; not on the default runtime path | explicit preview posture, no hidden implication of full workbench parity, import/export affordances only where the engine/contracts can actually support them |
| SR5 | default runtime lane, richer oracle/parity surface, but deterministic host still unavailable today | primary workbench lane, but must surface provider-unavailable gaps honestly until core completes the host |
| SR6 | default runtime lane, but experimental rules host | either preview/beta messaging in the workbench or a default-selection change once core decides the runtime posture |

## UI-owned adaptation matrix

| Work item | Priority | Domain | UI-owned work | Depends on core truth | Acceptance proof |
| --- | --- | --- | --- | --- | --- |
| `UI-RS-01` | P0 | Ruleset posture and honesty | surface per-ruleset badges, warnings, and unsupported-capability messaging in shell/workspace views so SR4/SR5/SR6 do not all look equally complete | ruleset registration state, capability diagnostics, runtime profile status | both heads render explicit ruleset posture without inventing mechanics locally |
| `UI-RS-02` | P0 | Shell/workflow routing | keep commands, tabs, actions, and workflow surfaces driven only by ruleset catalogs and explicit policy, with no hidden shared fallback that erases ruleset differences | ruleset shell catalogs, workflow/action contracts | dual-head parity tests prove the same ruleset surfaces render in Avalonia and Blazor |
| `UI-RS-03` | P0 | Section availability and empty/error states | render unsupported or incomplete sections with explicit ruleset-aware states instead of blank panes or fake parity | workspace section contracts and validation receipts | unsupported sections are obvious, localized, and non-destructive |
| `UI-RS-04` | P1 | Build Lab, build kits, and browse | adapt Build Lab intake, starter paths, build-kit browsing, and NPC vault browsing per ruleset without leaking unsupported paths | build-kit manifests, NPC/encounter packets, section contracts | SR4/SR5/SR6 entry surfaces only advertise flows that the underlying lane can actually support |
| `UI-RS-05` | P1 | Runtime Inspector and runtime-profile UX | render RuleProfile, RulePack, capability, and runtime-fingerprint status per ruleset with honest unsupported/preview states | runtime inspector projections, profile compatibility receipts, capability descriptors | runtime diagnostics stay ruleset-aware and do not imply provider availability when core reports otherwise |
| `UI-RS-06` | P1 | Explain and validation rendering | render explain traces, disabled reasons, validation warnings, and capability errors as structured/localized ruleset-aware UI | explain payloads, validation summaries, capability diagnostics | missing-provider and experimental diagnostics are visible and actionable in both heads |
| `UI-RS-07` | P1 | Import/export and print affordances | keep file naming, picker copy, export actions, and print/download affordances aligned with `.chum4`, `.chum5`, and `.chum6` support reality | workspace codec/export contracts | users can tell which import/export flows are supported for each ruleset without trial-and-error |
| `UI-RS-08` | P0 gate | Cross-head ruleset acceptance matrix | add or extend parity/acceptance coverage so the supported ruleset matrix is explicit across Avalonia and Blazor | all ruleset catalogs, posture state, diagnostics contracts | release-proof rails fail if a ruleset surface drifts between heads or claims unsupported depth |

## Recommended execution order

1. `UI-RS-01` ruleset posture and honesty
2. `UI-RS-02` shell/workflow routing
3. `UI-RS-03` section availability and empty/error states
4. `UI-RS-05` Runtime Inspector and runtime-profile UX
5. `UI-RS-06` Explain and validation rendering
6. `UI-RS-04` Build Lab, build kits, and browse
7. `UI-RS-07` import/export and print affordances
8. `UI-RS-08` cross-head ruleset acceptance matrix

## Immediate next slice

The next highest-signal UI slice is:

- make per-ruleset posture explicit in shell/workspace chrome
- add SR4/SR5/SR6-specific unsupported-state rendering for incomplete sections and unavailable capabilities
- extend cross-head parity coverage so ruleset posture and available surfaces stay aligned between Avalonia and Blazor

## Non-goals

- implementing Shadowrun math in the UI repo
- parsing legacy XML directly in UI code
- treating shared workbench completion as proof that all rulesets are equally complete
- masking core experimental/provider-unavailable states behind generic UI copy
