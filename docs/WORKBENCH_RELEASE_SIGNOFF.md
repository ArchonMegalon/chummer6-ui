# Workbench Release Signoff

Purpose: close `WL-202` and `WL-203` with explicit, verifier-backed evidence instead of leaving workbench completion implied by older milestone notes.

## Workbench completion surface

`chummer6-ui` is treated as release-complete for the current workbench/browser/desktop scope when the following verification lanes all stay green:

- `scripts/ai/milestones/b3-build-lab-check.sh` for builder depth.
- `scripts/ai/milestones/b10-contact-network-check.sh` for relationship graph plus heat/faction/favor continuity.
- `scripts/ai/milestones/b9-campaign-journal-check.sh` for planner/calendar and journal depth.
- `scripts/ai/milestones/b8-runtime-inspector-check.sh` for diagnostics and richer Hub UX.
- `scripts/ai/milestones/b12-generated-asset-dispatch-check.sh` for publish, dispatch, review, and approval-aware generated-asset flows.
- `scripts/ai/milestones/b11-npc-persona-studio-check.sh` for operator-facing NPC/persona depth.
- `scripts/ai/milestones/b4-gm-board-spider-feed-check.sh` for moderation-adjacent Spider and board surfaces.

Those checks are all part of the normal `scripts/ai/verify.sh` path, so release truth does not depend on ad hoc manual demos.

## Cross-head hardening proof

`F0` is treated as materially closed for the UI head when these signoff rails remain executable:

- `scripts/ai/milestones/b13-accessibility-signoff-check.sh` for accessibility and browser-shell live-region proof.
- `scripts/ai/milestones/b7-browser-isolation-check.sh` for deployment/browser-constraint proof.
- `scripts/ai/milestones/b2-browse-virtualization-check.sh` for dense-data virtualization discipline.
- `scripts/ai/milestones/p5-ui-kit-shell-chrome-check.sh`
- `scripts/ai/milestones/p5-ui-kit-design-token-check.sh`
- `scripts/ai/milestones/p5-ui-kit-accessibility-state-check.sh`

## Release budgets

- Accessibility: workbench surfaces must keep explicit live/status semantics and the B13 signoff path must stay green.
- Localization: explain and workbench chrome must remain localization-safe; `Chummer.Presentation/Explain/RulesetExplainRenderer.cs` is allowed to fail fast on missing localization keys rather than silently falling back to stale copy.
- Performance: dense browse and browser delivery must remain under the existing virtualization and browser-isolation guardrails instead of regressing into unbounded table/render paths.

## Ownership note

Installer-capable artifacts and updater integration are owned here.
Promoted release channels, installer/update-feed publication truth, and public `/downloads` state are owned downstream by `chummer6-hub-registry` and rendered by `chummer6-hub`.

## Exit statement

The remaining UI debt is no longer missing release-depth workbench capability. It is compatibility-cargo cleanup and future feature growth, both of which sit outside the current `E0`/`F0` closure bar.
