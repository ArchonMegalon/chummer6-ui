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
- `scripts/ai/milestones/ruleset-ui-adaptation-check.sh` for SR4/SR5/SR6 posture, unsupported-state honesty, and cross-head shell adaptation proof.
- `scripts/ai/milestones/b14-flagship-ui-release-gate.sh` for flagship desktop interaction, bundled demo-runner presence, and visibly reactive menu/settings proof.
- `scripts/ai/milestones/veteran-task-time-evidence-gate.sh` for veteran task-time evidence on sourcebooks, roster, print/export, and bounded Blazor fallback proof.
- `scripts/ai/milestones/chummer5a-screenshot-review-gate.sh` for mandatory Chummer5a screenshot-backed compare review on dense builder, master index, roster, and settings surfaces.
- `scripts/ai/milestones/dense-workbench-recovery-gate.sh` for compact classic workbench posture, reduced badge density, row-preserving padding, accessibility without oversized chrome, and screenshot-backed menu/toolstrip familiarity proof.
- `scripts/ai/milestones/classic-dense-workbench-posture-gate.sh` for Avalonia default dense posture, reduced section-header scale, flat form panels, and anti-dashboard workbench chrome proof.
- `scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh` for release-channel/install-media/startup-smoke truth across promoted desktop heads, including fail-honest blocker receipts when artifacts or platform proof are missing.
- `scripts/ai/milestones/b15-localization-release-gate.sh` for shipping-locale truth, explicit fallback honesty, explain localization, and support/install/update language coverage.

Those checks are all part of the normal `scripts/ai/verify.sh` path, so release truth does not depend on ad hoc manual demos.

For local docker-backed release proof, `scripts/e2e-portal.sh` is the canonical executable lane. It boots the downstream public-edge stack from `chummer6-hub` and materializes `.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json` with the probed base URL, route coverage, and whether the route probe actually ran.

For the hard Linux desktop exit gate, `scripts/materialize-linux-desktop-exit-gate.sh` is the canonical executable lane. It must build the Linux Avalonia binary, package the primary `.deb` plus fallback archive, install and purge the primary `.deb` inside an isolated dpkg root while running startup smoke from the installed path, run startup smoke against the fallback archive, run the desktop runtime unit-test suite, and publish `.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json`.

For the hard Windows desktop exit gate, `scripts/materialize-windows-desktop-exit-gate.sh` is the canonical executable lane. It must validate that the promoted Avalonia Windows installer is present on the active release shelf, require release-channel digest/size alignment for that installer, require current local release plus desktop workflow parity proofs, and publish `.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json`.

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
- Localization: explain and workbench chrome must remain localization-safe; `Chummer.Presentation/Explain/RulesetExplainRenderer.cs` is allowed to fail fast on missing localization keys rather than silently falling back to stale copy, and `scripts/ai/milestones/b15-localization-release-gate.sh` is the release-required executable proof lane for shipping locale truth.
- Performance: dense browse and browser delivery must remain under the existing virtualization and browser-isolation guardrails instead of regressing into unbounded table/render paths.

## Ownership note

Installer-capable artifacts and updater integration are owned here.
Promoted release channels, installer/update-feed publication truth, and public `/downloads` state are owned downstream by `chummer6-hub-registry` and rendered by `chummer6-hub`.
Repo-local docker release proof for the portal/workbench shell is owned here, executed against the downstream public edge, and published as `.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json`.

## Exit statement

The remaining UI debt is no longer missing shared-shell workbench capability or missing ruleset-specific shell adaptation proof. It is compatibility-cargo cleanup and future feature growth, both of which sit outside the current `E0`/`F0` closure bar.
