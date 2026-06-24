# Workbench Release Signoff

Purpose: close `WL-202` and `WL-203` with explicit, verifier-backed evidence instead of leaving workbench completion implied by older milestone notes.

Documentation map for the browser lane:

- `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` is the top-level browser-client docs index
- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md` is the primary design and parity contract
- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md` defines the hosted route-entry/workbench proof tier
- `docs/BLAZOR_SELF_HOST_RUNBOOK.md` defines the Docker/self-host operator lane
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` defines the stricter hosted execution-proof tier

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

For local docker-backed release proof, `scripts/e2e-portal.sh` is the canonical executable lane. It boots the repository `portal` Docker profile (`chummer-api`, `chummer-blazor-portal`, `chummer-hub-web-portal`, `chummer-avalonia-browser`, and `chummer-portal`) and materializes `.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json` with the probed base URL, route coverage, and whether the route probe actually ran. The same lane also materializes `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json` so the Docker self-hosted browser workbench proof stays distinct from the broader local release receipt.

For hosted public-edge browser posture, `scripts/e2e-public-edge.cjs` and `scripts/materialize-external-host-proof-blockers.py` now also publish `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`. That hosted receipt proves route-entry posture for `/blazor/`, `/blazor/workbench`, workspace restore route shape, hosted startup-command route shape for `new_character_origin`, `character_roster`, and `master_index` alongside the other promoted startup routes, `/blazor/health`, resumed result-continuation route shapes, and resumed action/committed-action route shapes. The stricter hosted execution tier defined in `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` is now also published as a real passing receipt at `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`, covering resumed workflow execution on `https://chummer.run/blazor/workbench` rather than only self-host Playwright posture.

That hosted execution tier is no longer limited to startup-command evidence. The current verifier-backed contract now spans:

- startup execution for `new_character`, `new_character_origin`, `character_roster`, `master_index`, `open_character`, `open_for_printing`, and `open_for_export`
- dense startup-utility surfaces for `character_roster` and `master_index`
- restored origin/rules continuity, including `new_character_origin` structure and restored `tab-rules`
- restored build-lab continuity on `tab-create`
- dense selection/edit/delete families across gear, qualities, magic, cyberware, and contacts
- resumed result, action, committed-action, and advanced-action families on the promoted `/blazor/workbench` lane

That hosted route-entry tier is now enforced explicitly by `scripts/verify_blazor_public_edge_workbench_proof.py` and `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`, while the stricter hosted execution tier is enforced by `scripts/verify_blazor_public_edge_execution_proof.py` and `scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`.

The promoted browser product shape is now explicit: `/blazor/` is the stable public entry, `/blazor/workbench` is the product-shaped Chummer6 browser client, and `/blazor/preview` is supporting proof surface rather than the primary user promise. The acceptance bar for that lane is tracked in `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`, and the current self-host operator path is documented in `docs/BLAZOR_SELF_HOST_RUNBOOK.md`.

`scripts/print_blazor_public_edge_proof_status.py` is the combined browser-lane proof summary. It reports Docker self-host proof, hosted route-entry proof, hosted execution proof, analytics posture, connected-runtime posture, aggregate proof-set status, and external-host blocker state from the current published receipts.

`scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh` materializes `.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json` and must fail if any required browser-lane receipt is missing, has the wrong contract, or is not in its expected passing/ready state. This aggregate receipt is a release-readiness convenience for the browser lane; it still does not claim full desktop parity beyond the receipts it aggregates.

Release truth for the browser lane therefore splits into three separate statements:

- Docker self-host browser workbench proof exists and is published as `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`
- hosted `chummer.run` route-entry posture exists and is published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`
- hosted `chummer.run` workflow execution is a stricter proof tier, published separately as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`, and it now passes for the promoted `/blazor/workbench` browser workflow lane
- hosted/self-host browser analytics posture is published separately as `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`; it only proves optional Rybbit wiring and privacy boundaries, not workflow parity
- optional connected-runtime portal forwarding posture is published separately as `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`; it proves session/coach/AI routing boundary and signed owner forwarding, not full workflow parity

Hosted execution proof passing removes the old "route posture only" limit for the promoted browser lane, but it still does not by itself prove full browser/Desktop parity breadth outside the workflow families covered by the hosted execution contract and the broader parity ledger.

The next browser-lane proof refresh is staged to add the career/support workflow family across both hosted and Docker self-host proof runners. That staged family covers restored `tab-calendar` section continuity, add/edit/delete career-entry dialogs, add/edit/delete committed result continuations, runner notes editing, notes-save result continuations, and classic move up/down list utilities. This is not yet current published receipt evidence until the hosted and self-host proofs are rerun and their generated receipts are refreshed.

`scripts/ai/milestones/blazor-career-support-staged-proof-check.sh` is the pre-refresh source-alignment check for that staged family. It only proves that the staged route family is wired consistently across source, runners, receipt metadata, status reporting, and docs; it is not a browser execution receipt and must not be treated as a release-passing workflow proof.

The next staged browser-family slice after career/support is identity/SIN/license utility posture under restored `tab-info`. `scripts/ai/milestones/blazor-identity-license-staged-proof-check.sh` is its source-alignment check. It proves only that the workbench affordances, dedicated utility dialogs, hosted/self-host route shapes, status utility, and docs agree; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after identity/SIN/license is combat support utility posture under restored `tab-combat`. `scripts/ai/milestones/blazor-combat-support-staged-proof-check.sh` is its source-alignment check. It proves only that armor, reload, and damage-track routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after combat support is skill maintenance utility posture under restored `tab-skills`. `scripts/ai/milestones/blazor-skill-maintenance-staged-proof-check.sh` is its source-alignment check. It proves only that specialization, removal, and group-edit routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after skill maintenance is magic/resonance support utility posture across restored adept, magician, critter, and technomancer lanes. `scripts/ai/milestones/blazor-magic-support-staged-proof-check.sh` is its source-alignment check. It proves only that adept power, spirit, critter power, and Matrix-program routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after magic/resonance support is gear maintenance utility posture under restored `tab-gear`. `scripts/ai/milestones/blazor-gear-maintenance-staged-proof-check.sh` is its source-alignment check. It proves only that generic add, edit, and remove gear routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after gear maintenance is source/gear utility posture across restored profile and gear lanes. `scripts/ai/milestones/blazor-source-gear-utility-staged-proof-check.sh` is its source-alignment check. It proves only that source viewing, gear source, gear mount, and free/paid cost routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

The next staged browser-family slice after source/gear utilities is magic cleanup utility posture across restored magician and gear lanes. `scripts/ai/milestones/blazor-magic-cleanup-staged-proof-check.sh` is its source-alignment check. It proves only that magic add, spirit binding, magic source, and drug-removal routes are wired across product affordances, hosted/self-host route shapes, status utility, and docs; it is not a hosted or Docker browser execution receipt.

`scripts/print_blazor_public_edge_proof_status.py` reports that staged receipt separately as `career_support_staged_*` when present, with a source-alignment-only note. It must remain separate from the aggregate browser-lane proof set until hosted and self-host browser execution receipts are refreshed.

For the hard Linux desktop exit gate, `scripts/materialize-linux-desktop-exit-gate.sh` is the canonical executable lane. It must build the Linux Avalonia binary, package the primary `.deb` plus fallback archive, install and purge the primary `.deb` inside an isolated dpkg root while running startup smoke from the installed path, run startup smoke against the fallback archive, run the desktop runtime unit-test suite, and publish `.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json`.

For the hard Windows desktop exit gate, `scripts/materialize-windows-desktop-exit-gate.sh` is the canonical executable lane. It must validate that the promoted Avalonia Windows installer is present on the active release shelf, require release-channel digest/size alignment for that installer, require current local release plus desktop workflow parity proofs, require the aggregate Blazor browser-lane proof set, and publish `.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json`.

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
Repo-local docker release proof for the portal/workbench shell is owned here, executed against the downstream public edge, and published as `.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json`. The browser self-host workbench lane is separately published as `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`, and the hosted `chummer.run` browser route-entry lane is published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`.

Hosted `chummer.run` workflow execution proof is separately published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json` and must remain distinct from both the self-host workbench receipt and the hosted route-entry receipt.

## Exit statement

The remaining UI debt is no longer missing shared-shell workbench capability or missing ruleset-specific shell adaptation proof. It is compatibility-cargo cleanup and future feature growth, both of which sit outside the current `E0`/`F0` closure bar.
