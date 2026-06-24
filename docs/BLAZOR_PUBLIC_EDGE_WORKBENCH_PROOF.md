# Blazor Public-Edge Workbench Route Proof

Purpose: define the minimum acceptable evidence for claiming that the browser-hosted `Chummer.Blazor` workbench on `https://chummer.run/blazor/` is publicly reachable under the promoted route model with stable route-entry posture.

This document is the hosted route-entry proof contract for the promoted Chummer6 browser client route. It is intentionally narrower than the hosted execution-proof contract in `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`.

Documentation map:

- `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` is the top-level browser-client docs index
- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md` is the primary design-spec and parity contract
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md` defines the stricter hosted workflow-execution tier
- `docs/BLAZOR_SELF_HOST_RUNBOOK.md` covers the separate Docker/self-host operator lane
- `docs/WORKBENCH_RELEASE_SIGNOFF.md` defines the release-truth posture that consumes this proof tier

## What this proof is for

This proof tier answers a narrow but necessary question:

Does the public `chummer.run` edge expose the promoted browser-client route family under `/blazor/` with stable route-entry posture?

More precisely:

Can the public edge serve the browser shell, preserve the promoted `/blazor/workbench` route family, and accept the current resume/result/action route shapes without claiming full browser workflow execution?

This proof tier does not claim that the hosted browser client completed a real user workflow. It only claims that the public route family exists, is reachable, and preserves the expected promoted route shapes.

## Evidence bar

Hosted route-entry proof is only acceptable when it comes from probes against the public `https://chummer.run` edge.

Acceptable evidence must include all of the following:

- explicit route probes under `/blazor/`
- a published receipt distinct from self-host and hosted execution receipts
- a verifier-backed structural contract for the published receipt
- explicit distinction between route-entry posture and workflow execution

Route-entry proof is required, but it is never sufficient for parity or desktop-equivalent claims by itself.

## Minimum hosted route families

The current hosted route-entry tier should cover these route families:

- public entry and health:
  `/blazor/`
  `/blazor/health`
- promoted workbench route:
  `/blazor/workbench`
- restored workspace route shape:
  `/blazor/workbench?workspace=ws-1`
- startup deep-link route shape:
  `/blazor/preview?command=new_character`
- resumed result-continuation route shapes:
  `/blazor/workbench?workspace=ws-1&command=save_character_as`
  `/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download`
  `/blazor/workbench?workspace=ws-1&command=print_character`
- resumed action-continuation route shape:
  `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add`
- resumed committed-action route shape:
  `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add`

These route families establish that:

- `/blazor/` resolves into the promoted browser lane
- the promoted workbench route exists on the public edge
- restored workspace, result, action, and committed-action route shapes are publicly accepted

Later hosted route-entry tiers may grow as the probe set expands, but the receipt must always stay honest about whether it is proving route posture or workflow execution.

The verifier currently enforces the core published route family as mandatory and treats the newer startup-command and advanced-action probe families as an all-or-nothing expansion set. That keeps the current published receipt valid while preventing a partially regenerated expanded receipt from being treated as complete.

## Receipt requirements

When hosted route-entry proof exists, it should be published separately from hosted execution proof.

Required receipt contract:

- contract name:
  `chummer6-ui.blazor_public_edge_workbench_proof`
- target host:
  `https://chummer.run`
- proof shape:
  `core` for the currently published minimal route family
  `expanded` for the newer promoted startup-command and advanced-action route family
  older receipts may omit this field, but any receipt that includes it must match the marker/workflow/route set it declares
- route probe execution truth:
  `runtime_required=true`
  `route_probe_executed=true` when probes actually ran
- route-proof markers:
  `public_blazor_root_redirect`
  `public_blazor_health`
  `public_workbench_route`
  `public_workspace_restore_route`
  `public_startup_deep_link_route`
  `public_result_continuation_routes`
  `public_action_continuation_routes`
  `public_committed_action_route`
- workflow-shape markers:
  `blazor_root_redirect`
  `workbench_route`
  `workspace_resume_route_shape`
  `new_character_deep_link_route_shape`
  `result_continuation_route_shapes`
  `action_continuation_route_shapes`
  `committed_action_route_shape`
- required proof routes:
  `/blazor/`
  `/blazor/health`
  `/blazor/workbench`
  `/blazor/workbench?workspace=ws-1`
  `/blazor/preview?command=new_character`
  `/blazor/workbench?workspace=ws-1&command=save_character_as`
  `/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download`
  `/blazor/workbench?workspace=ws-1&command=print_character`
  `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add`
  `/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add`
- route probe count must match the number of proof routes
- route probe failures must be empty for a passing receipt

## Current verifier and wrapper

Current contract enforcement paths:

- route-entry receipt verifier:
  `scripts/verify_blazor_public_edge_workbench_proof.py`
- milestone-style verifier wrapper:
  `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`
- shared route/execution status summary:
  `scripts/print_blazor_public_edge_proof_status.py`
- example receipt shape:
  `docs/examples/blazor-public-edge-workbench-proof.receipt.example.json`
- expanded example receipt shape:
  `docs/examples/blazor-public-edge-workbench-proof.expanded.receipt.example.json`
- current published receipt:
  `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`

## Current truth

- hosted route-entry posture is published separately from self-host and hosted execution proof
- hosted route-entry posture is verifier-backed and wired into the main repo verification path
- hosted route-entry proof does not prove browser workflow completion
- hosted execution proof remains the stricter next tier required for public desktop-equivalent workflow claims
