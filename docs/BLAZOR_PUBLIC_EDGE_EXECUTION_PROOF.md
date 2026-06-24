# Blazor Public-Edge Execution Proof

Purpose: define the minimum acceptable evidence for claiming that the browser-hosted `Chummer.Blazor` workbench on `https://chummer.run/blazor/` executes real user workflows, not just route-entry posture.

This document is the hosted execution-proof contract for the promoted Chummer6 browser client route. It sits under the broader design and parity spec in `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`.

Documentation map:

- `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` is the top-level browser-client docs index
- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md` is the primary design-spec and parity contract
- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md` defines the narrower hosted route-entry tier
- `docs/BLAZOR_SELF_HOST_RUNBOOK.md` covers the separate Docker/self-host operator lane
- `docs/WORKBENCH_RELEASE_SIGNOFF.md` defines the release-truth posture that consumes this proof tier

## What this proof is for

The current hosted receipt, `BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`, proves:

- `/blazor/` resolves into `/blazor/workbench`
- hosted workbench route shapes stay stable
- hosted resume/result/action route shapes are accepted
- the public edge serves the browser shell under the expected path base

That is necessary, but not sufficient.

This proof tier exists to answer a different question:

Can the public `chummer.run` browser client actually execute resumed user workflows with visible browser outcomes?

More precisely:

Can the promoted `/blazor/workbench` route preserve Chummer6 browser workflow continuity on the real hosted edge, instead of only proving that the route shape exists?

The route-entry tier is now a verifier-backed contract rather than a loose published artifact:

- route-entry receipt verifier:
  `scripts/verify_blazor_public_edge_workbench_proof.py`
- milestone-style wrapper:
  `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`
- shared route/execution status summary:
  `scripts/print_blazor_public_edge_proof_status.py`

## Evidence bar

Hosted execution proof is only acceptable when it comes from browser-driven public-edge evidence against `https://chummer.run`, not from self-host Docker success and not from route-only HTTP probes.

Acceptable evidence must include all of the following:

- a browser automation lane that runs against the hosted public edge
- concrete route targets under `/blazor/`
- visible browser assertions after route load
- receipts that record exactly which hosted workflow families were exercised
- non-empty per-family browser checks that each record route, visible assertion, and passing status
- explicit failure when the hosted lane falls back to route-entry posture without workflow completion

The proof bar is design-driven, not just transport-driven. The hosted lane should prove the public browser client behaves like a desktop-style Chummer session in the workflow slots the design spec calls out: resume, continue, commit, and visibly land in updated state.

## Minimum hosted workflow families

The first acceptable hosted execution tier should prove these families:

- startup command execution on the promoted workbench route:
  `/blazor/workbench?command=new_character`
  `/blazor/workbench?command=open_character`
  `/blazor/workbench?command=open_for_printing`
  `/blazor/workbench?command=open_for_export`
- dense startup utility execution on the promoted workbench route:
  `/blazor/workbench?command=character_roster`
  `/blazor/workbench?command=master_index`
  with browser-visible utility markers beyond the dialog title so hosted proof covers dense roster/reference posture instead of only command entry
- origin and rules continuity on the promoted workbench route:
  `/blazor/workbench?command=new_character_origin`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-rules`
  with browser-visible origin-wizard structure and visible rules-environment continuity markers beyond simple tab selection
- build-lab runner continuity on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-create`
  with visible planner, progression, and export/hand-off surfaces so hosted proof covers a denser seeded runner workflow lane
- combat-lane weapon selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-combat&control=combat_add_weapon`
  with visible weapon catalog, accessories, and filter context so hosted proof covers a dense combat utility family as well
- skills-lane skill selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-skills&control=skill_add`
  with visible skill catalog, linked-attribute context, and filter posture so hosted proof covers a dense skills utility family as well
- gear-lane vehicle selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-gear&control=vehicle_add`
  with visible catalog, filters, and selection details so hosted proof covers a dense runner-sheet editing utility rather than only startup or resumed-tab posture
- gear-lane vehicle-mod selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-gear&control=vehicle_mod_add`
  with visible mod catalog plus slot/availability/source detail so hosted proof covers a second dense utility family within the vehicle workflow lane
- qualities-lane quality selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-qualities&control=quality_add`
  with visible category, karma, source, and filter context so hosted proof covers a dense non-gear utility family as well
- qualities-lane quality delete execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-qualities&control=quality_delete`
  with visible karma impact and recovery context so hosted proof covers delete/recovery posture on the qualities lane as well
- magic-lane spell selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-magician&control=spell_add`
  with visible spell catalog plus drain/source/category context so hosted proof covers a dense magic utility family as well
- magic-lane delete execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-magician&control=magic_delete`
  with visible drain-impact and recovery context so hosted proof covers delete/recovery posture on the magic lane as well
- cyberware-lane selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-cyberware&control=cyberware_add`
  with visible catalog plus essence/cost/source/filter context so hosted proof covers a dense cyberware utility family as well
- cyberware-lane edit execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-cyberware&control=cyberware_edit`
  with visible installed-ware context plus live recalculation posture so hosted proof covers in-place cyberware editing as well
- cyberware-lane delete execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-cyberware&control=cyberware_delete`
  with visible installed-ware removal impact and recovery context so hosted proof covers delete/recovery posture on the cyberware lane as well
- gear-lane drug selection execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-gear&control=drug_add`
  with visible catalog plus crash/speed/source details so hosted proof covers another dense classic utility family on the runner sheet
- contacts-lane connection edit execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-contacts&control=contact_connection`
  with visible selected-contact summary context plus compact connection/loyalty edit controls so hosted proof covers a non-add edit utility family
- gear-lane vehicle edit execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-gear&control=vehicle_edit`
  with visible selected-item context, live summary, and garage navigation state so hosted proof covers an edit-in-place runner-sheet utility family
- gear-lane vehicle delete execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-gear&control=vehicle_delete`
  with visible removal impact and recovery context so hosted proof covers delete/recovery posture instead of only add/edit flows
- contacts-lane contact delete execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-contacts&control=contact_remove`
  with visible roster impact and recovery context so hosted proof covers delete/recovery posture outside the gear lane as well
- contacts-lane contact edit execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-contacts&control=contact_edit`
  with visible selected-contact details and edit context so hosted proof covers dense in-place editing on the contacts lane as well
- staged career-entry execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-calendar&control=create_entry`
  with visible compact list/detail editor posture, command follow-through, entry title, and preserved list context so the next hosted proof refresh can widen into the career log / support workflow family
- staged committed career-entry execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-calendar&control=create_entry&dialog_action=add`
  with visible `Entry 'New entry' added.` follow-through so the career log / support workflow family proves commit-result posture rather than dialog entry alone
- staged career-log section continuity on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-calendar`
  with visible career-log landing and add-entry posture so the support workflow family includes section resume before dialog execution
- staged career-entry edit/delete execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-calendar&control=edit_entry`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-calendar&control=delete_entry`
  with visible compact edit posture, removal scope, and recovery context so the career log / support workflow family covers more than add-only utility behavior
- resumed workbench load:
  `/blazor/workbench?workspace=<promoted-workspace-id>`
- recent-work resume affordance visibility on the promoted workbench route:
  visible `Resume BLUE` recent-work links sourced from restored session state
- restored section continuation affordance visibility on the promoted workbench route:
  visible profile/rules/gear/career-log/advanced continuation links for the restored workspace
- restored tab landing execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-info`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-rules`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-gear`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-technomancer`
- restored section-surface content on the promoted workbench route:
  visible section/action markers for profile, rules, inventory, and complex-forms surfaces after those restored tab routes land
- resumed result continuation:
  `/blazor/workbench?workspace=<promoted-workspace-id>&command=save_character_as`
  `/blazor/workbench?workspace=<promoted-workspace-id>&command=export_character`
  `/blazor/workbench?workspace=<promoted-workspace-id>&command=print_character`
- resumed action continuation:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-contacts&control=contact_add`
- advanced restored action affordance visibility on the promoted workbench route:
  visible complex-form/initiation/cyberware/spell continuation links plus the career-entry continuation link for the restored workspace
- advanced action execution on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-technomancer&control=complex_form_add`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-adept&control=initiation_add`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-cyberware&control=cyberware_add`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-magician&control=spell_add`
- resumed committed action:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-contacts&control=contact_add&dialog_action=add`
- advanced committed actions on the promoted workbench route:
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-technomancer&control=complex_form_add&dialog_action=add`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-adept&control=initiation_add&dialog_action=add`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-cyberware&control=cyberware_add&dialog_action=add`
  `/blazor/workbench?workspace=<promoted-workspace-id>&tab=tab-magician&control=spell_add&dialog_action=add`

These are the first promoted families because they map directly to the current Chummer6 browser design contract:

- desktop-like continuity through explicit workspace resume
- browser-safe result continuations for save/export/print
- action-heavy editing through dialog continuation
- committed follow-through that leaves visible sheet state behind

Later hosted tiers should expand to the other workflow families in `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`, especially startup/recent-work recovery, rules/origin flow, dense section-family editing, career/support utility editing, and cross-route continuity.

The hosted Playwright runner is already staged to record `promoted_career_log_continuity` through the `tab-calendar` route, `promoted_career_entry_execution` through the `tab-calendar&control=create_entry` route, `promoted_career_entry_committed_execution` through the matching `dialog_action=add` route, and edit/delete coverage through `promoted_career_entry_edit_execution` plus `promoted_career_entry_delete_execution`. Those staged families are not yet part of the verifier-required set or the current published receipt until the live `chummer.run` proof is refreshed and promoted.

## Required browser-visible assertions

Route acceptance alone does not count.

The hosted lane must assert browser-visible evidence such as:

- resumed workspace copy is rendered
- save/export/print continuation surfaces render browser-visible outcome copy
- action continuation opens the intended dialog surface
- committed action leaves visible state behind after the dialog action completes

The assertions should be tied to the promoted workbench route, not to proof-only route posture. If an assertion succeeds only because `/blazor/preview` still exposes a proof control, that does not satisfy this contract.

Examples of acceptable visible assertions:

- a named dialog heading or field label appears
- a download/export/print outcome panel or notice appears
- a newly committed row or item appears in the resumed workspace

Examples of unacceptable weak assertions:

- HTTP 200 alone
- route string alone
- base href alone
- “page loaded” alone

## Receipt requirements

When hosted execution proof exists, it should be published separately from route-entry proof.

Recommended receipt shape:

- contract name:
  `chummer6-ui.blazor_public_edge_execution_proof`
- proof tier:
  `hosted_promoted_route_execution`
- route lane:
  `promoted_blazor_workbench`
- promoted route base:
  `/blazor/workbench`
- target host:
  `https://chummer.run`
- required workflow family ids:
  `promoted_startup_command_executions`
  `promoted_dense_tool_surfaces`
  `promoted_origin_rules_continuity`
  `promoted_build_lab_continuity`
  `promoted_weapon_selection_execution`
  `promoted_skill_selection_execution`
  `promoted_vehicle_selection_execution`
  `promoted_vehicle_mod_selection_execution`
  `promoted_quality_selection_execution`
  `promoted_quality_delete_execution`
  `promoted_spell_selection_execution`
  `promoted_magic_delete_execution`
  `promoted_cyberware_selection_execution`
  `promoted_cyberware_edit_execution`
  `promoted_cyberware_delete_execution`
  `promoted_drug_selection_execution`
  `promoted_contact_connection_execution`
  `promoted_vehicle_edit_execution`
  `promoted_vehicle_delete_execution`
  `promoted_contact_delete_execution`
  `promoted_contact_edit_execution`
  `promoted_resumed_workspace`
  `promoted_recent_work_affordances`
  `promoted_restored_section_continuations`
  `promoted_restored_tab_landings`
  `promoted_restored_section_content`
  `promoted_result_continuations`
  `promoted_action_continuations`
  `promoted_advanced_action_affordances`
  `promoted_advanced_action_executions`
  `promoted_committed_actions`
  `promoted_advanced_committed_actions`
- route family markers:
  resumed workspace
  resumed result continuations
  resumed action continuations
  resumed committed actions
- per-route browser assertions
- at least one passing `checks[]` entry for every required workflow family
- screenshot or capture references where available
- explicit distinction from `BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`
- explicit indication that the exercised route family belongs to the promoted `/blazor/workbench` browser-client lane

Current scaffold paths:

- hosted browser execution runner:
  `scripts/e2e-public-edge-playwright.cjs`
- wrapper entrypoint:
  `scripts/e2e-public-edge-execution.sh`
- receipt verifier:
  `scripts/verify_blazor_public_edge_execution_proof.py`
- milestone-style verifier wrapper:
  `scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`
- status summary utility:
  `scripts/print_blazor_public_edge_proof_status.py`
- example receipt shape:
  `docs/examples/blazor-public-edge-execution-proof.receipt.example.json`
- published hosted execution receipt:
  `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`

Current published receipt state:

- contract:
  `chummer6-ui.blazor_public_edge_execution_proof`
- status:
  `passed`
- proof tier:
  `hosted_promoted_route_execution`
- route lane:
  `promoted_blazor_workbench`
- promoted route base:
  `/blazor/workbench`

That published receipt exists so downstream artifacts can reference a real hosted execution-proof result without conflating hosted execution with self-host route proof.

Current truth:

- hosted route-entry posture is already published separately
- hosted route-entry posture is now verifier-backed and wired into the main repo verification path
- hosted execution proof is scaffolded, verifier-backed, wired into downstream receipts, and now published as a passing hosted run whose verifier now rejects empty workflow-family labels and requires explicit per-family route/assertion/status checks
- the passing hosted execution receipt proves the promoted `/blazor/workbench` browser workflow lane on `chummer.run`, but it still does not by itself prove every remaining browser/Desktop parity family

## Scaffold invocation

Default behavior:

- target host:
  `https://chummer.run`
- receipt output:
  `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`
- post-run receipt verification:
  `scripts/verify_blazor_public_edge_execution_proof.py`

Supported environment variables:

- `CHUMMER_PORTAL_BASE_URL`
  overrides the target public-edge base URL
- `CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH`
  overrides the generated receipt path

Example invocation:

```bash
bash scripts/e2e-public-edge-execution.sh
```

Equivalent explicit invocation:

```bash
CHUMMER_PORTAL_BASE_URL="https://chummer.run" \
CHUMMER_PUBLIC_EDGE_EXECUTION_PROOF_PATH=".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json" \
node scripts/e2e-public-edge-playwright.cjs
```

If the hosted browser assertions fail, the script writes a failed receipt and exits non-zero.
If the browser assertions pass but the receipt contract is malformed, the wrapper still exits non-zero because it runs the canonical receipt verifier after Playwright completes.

The canonical contract check for the hosted execution receipt is:

```bash
python3 scripts/verify_blazor_public_edge_execution_proof.py
```

The milestone-style equivalent is:

```bash
bash scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh
```

For a compact operator summary of route-proof state versus execution-proof state:

```bash
python3 scripts/print_blazor_public_edge_proof_status.py
```

## What still does not count

None of the following are enough to claim hosted execution proof:

- self-host Playwright proof
- self-host Docker receipts
- hosted route probes without browser assertions
- downstream gates that merely reference the hosted route-entry receipt

## Promotion rule

Until this hosted execution tier exists, public-edge proof may say:

- hosted browser route posture is proven
- hosted resume/result/action route shapes are proven

It may not say:

- hosted resumed workflows are executed end-to-end
- hosted browser parity matches the self-host execution lane
- `chummer.run` has browser workflow proof equivalent to the self-host workbench lane
- the promoted Chummer6 browser client is public-edge parity-complete
