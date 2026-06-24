# Blazor Web Client Docs Index

## Purpose

This index defines the current documentation set for the Chummer6 browser-client lane.

The goal is straightforward: `Chummer.Blazor` should ship as a polished web client on `chummer.run`, preserve the same practical user workflow as the Avalonia desktop client, and remain self-hostable through Docker without splitting the product story.

## Primary Documents

- `docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md`: main design spec, product posture, parity rules, workflow bar, and required proof standard
- `docs/MIGRATION_BACKLOG.md`: backlog contract for browser-workbench promotion, parity milestones, and remaining implementation gaps
- `docs/WORKBENCH_RELEASE_SIGNOFF.md`: release-signoff posture for promoted workbench routes and browser workflow proof

## Hosted/Public-Edge Proof Documents

- `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md`: contract for hosted `chummer.run` route-entry/workbench posture proof
- `scripts/verify_blazor_public_edge_workbench_proof.py`: structural verifier for the hosted route-entry/workbench receipt contract
- `scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`: milestone-style wrapper for the hosted route-entry/workbench verifier
- `docs/examples/blazor-public-edge-workbench-proof.receipt.example.json`: example hosted route-entry/workbench receipt shape
- `docs/examples/blazor-public-edge-workbench-proof.expanded.receipt.example.json`: expanded example hosted route-entry/workbench receipt shape
- `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`: contract for hosted `chummer.run` browser workflow execution proof
- `scripts/verify_blazor_public_edge_execution_proof.py`: structural verifier for the hosted execution-proof receipt contract
- `scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`: milestone-style wrapper for the hosted execution-proof verifier
- `docs/examples/blazor-public-edge-execution-proof.receipt.example.json`: example hosted execution-proof receipt shape
- `scripts/print_blazor_public_edge_proof_status.py`: shared status summary utility for self-host proof, hosted route-entry proof, hosted execution proof, analytics posture, connected-runtime posture, and external-host blocker receipts
- `scripts/print_blazor_public_edge_proof_status.py`: also reports the optional staged career/support source-alignment receipt when it has been generated; that status line is not browser execution proof
- `scripts/materialize-blazor-browser-lane-proof-set.py`: aggregate proof-set materializer that fails unless the required browser-lane receipts are all in their expected passing/ready states
- `scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh`: milestone-style wrapper for the aggregate browser-lane proof set
- `scripts/materialize-blazor-career-support-staged-proof.py`: source-structural staged proof materializer for the next career/support workflow refresh
- `scripts/ai/milestones/blazor-career-support-staged-proof-check.sh`: milestone-style wrapper for the staged career/support source-alignment check
- `docs/examples/blazor-career-support-staged-proof.receipt.example.json`: example receipt shape for the staged career/support source-alignment proof
- `.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json`: published aggregate browser-lane proof-set receipt
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`: published hosted execution-proof receipt
- `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`: published hosted route-entry/workbench proof receipt

## Self-Host and Operator Documents

- `docs/BLAZOR_SELF_HOST_RUNBOOK.md`: canonical Docker and operator runbook for the browser workbench lane
- `docs/examples/self-hosted-browser-workbench.env.example`: baseline environment defaults for self-hosted portal/API/browser deployments
- `docs/DESKTOP_RELEASE_PIPELINE.md`: release pipeline notes that still need to stay aligned with the promoted browser route model
- `.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`: published Docker self-host browser workbench receipt included in the combined browser-lane proof status summary

## Analytics and Privacy Posture

- `scripts/materialize-blazor-analytics-posture-proof.py`: proof materializer for optional browser analytics wiring and privacy boundaries
- `scripts/ai/milestones/blazor-analytics-posture-check.sh`: milestone-style wrapper for the analytics posture proof
- `.codex-studio/published/BLAZOR_ANALYTICS_POSTURE.generated.json`: published analytics posture receipt

Hosted `chummer.run` may enable the Rybbit adapter for the Blazor web client, but self-hosted Docker defaults keep analytics disabled with `CHUMMER_ANALYTICS_PROVIDER=none`.

The adapter is limited to route/workflow metadata such as route family, command id, tab id, control id, dialog action id, and boolean fixture/workspace presence. It must not emit character names, aliases, owner ids, workspace ids, file names, document contents, XML, payloads, hashes, or generated dossier text.

## Connected Runtime Posture

- `scripts/materialize-blazor-connected-runtime-posture-proof.py`: proof materializer for optional session, coach, and AI forwarding posture
- `scripts/ai/milestones/blazor-connected-runtime-posture-check.sh`: milestone-style wrapper for connected-runtime posture proof
- `.codex-studio/published/BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json`: published connected-runtime posture receipt

Connected runtime proof is deliberately narrower than workflow parity. It proves that optional session, coach, and AI routes can remain behind the portal boundary and use the signed portal-owner forwarding seam when configured. It does not prove that every downstream connected-runtime workflow is complete.

The browser workbench proof shelf also renders a connected-runtime posture card showing whether session, coach, and AI lanes are configured or off. The card must not expose proxy URLs or owner secrets.

## Route and Workflow Canon

The browser lane currently uses this public route posture:

- `/blazor/`: stable public entry that should resolve into the browser workbench
- `/blazor/workbench`: promoted product-shaped browser client
- `/blazor/preview`: proof/support route, not the primary product promise

Parity claims should be evaluated against workflow families, not against visual resemblance alone:

- startup and recent-work recovery
- runner creation and origin/rules selection
- dense runner-sheet editing across core sections
- dialog-driven add/edit/commit loops
- browser result continuations for save, export, print, and download
- cross-route continuity between workbench, support, downloads, account, and optional coach/session surfaces

The hosted execution contract already spans more than startup posture alone. Its current promoted family set includes:

- startup command execution and dense startup utilities
- origin/rules continuity and build-lab continuity
- dense selectors on gear, qualities, magic, and cyberware lanes
- in-place edit and delete/recovery utilities across contacts, gear, qualities, magic, and cyberware
- resumed result, action, committed-action, and advanced-action families

The next hosted and self-host proof refresh is staged to add the career/support workflow family on the promoted workbench route. That staged family covers `tab-calendar` section resume, `create_entry`, `create_entry&dialog_action=add`, `edit_entry`, `edit_entry&dialog_action=apply`, `delete_entry`, `delete_entry&dialog_action=delete`, `open_notes`, `open_notes&dialog_action=save`, `move_up`, and `move_down`. It remains staged until the public-edge and Docker receipts are regenerated from the updated proof runners.

`scripts/ai/milestones/blazor-career-support-staged-proof-check.sh` can be used before the live proof refresh to verify that product UI, hosted route-entry probing, hosted execution probing, Docker self-host probing, receipt metadata, status reporting, and docs still agree about that staged route family.

## Current Documentation Truth

The documentation set is ahead of full proof completion by design.

Current docs already define the intended shipped posture and the evidence contract. The remaining work is to expand browser-specific proof and receipts until the hosted and self-hosted lanes both support the same release claim.
