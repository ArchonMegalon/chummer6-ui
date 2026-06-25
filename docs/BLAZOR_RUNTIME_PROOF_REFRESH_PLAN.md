# Blazor Runtime Proof Refresh Plan

## Purpose

This plan describes how to promote the staged Blazor browser work into real runtime evidence.

The current staged source lanes widen the browser workbench contract, but source-staged receipts are not enough to claim browser/Desktop parity. Runtime promotion requires refreshing Docker self-host proof, hosted route-entry proof, hosted execution proof, and the browser-lane aggregate after the source stage is aligned.

## Order of Operations

Run these lanes in order when preparing a browser proof refresh.

### 1. Source-Staged Family Set

```bash
bash scripts/ai/milestones/blazor-source-staged-proof-set-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json
```

This confirms source staging is internally aligned. It is not runtime proof.

### 2. Source-Staged Release Boundary

```bash
bash scripts/ai/milestones/blazor-source-staged-release-boundary-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json
```

This confirms staged receipts are not wired into release-readiness aggregation.

### 3. Portal and Operator Source Contracts

```bash
bash scripts/ai/milestones/blazor-portal-installer-handoff-staged-proof-check.sh
bash scripts/ai/milestones/blazor-docker-self-host-operator-staged-proof-check.sh
bash scripts/ai/milestones/blazor-account-support-handoff-staged-proof-check.sh
```

Expected receipts:

```text
.codex-studio/published/BLAZOR_PORTAL_INSTALLER_HANDOFF_STAGED_PROOF.generated.json
.codex-studio/published/BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json
.codex-studio/published/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF.generated.json
```

These confirm source contracts for portal/download/install/account/support handoff. They are not runtime proof. The portal installer handoff receipt explicitly includes the Blazor desktop compatibility installer routes `/downloads/install/blazor-desktop-linux-x64-installer` and `/downloads/install/blazor-desktop-win-x64-installer`, but that remains source-staged route coverage rather than promoted installer runtime proof.

### 4. Docker Self-Host Runtime Proof

```bash
bash scripts/e2e-portal.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json
```

This is the Docker self-host browser runtime evidence.

### 5. Hosted Public-Edge Route Proof

```bash
node scripts/e2e-public-edge.cjs
```

Expected receipt source depends on the hosted route-proof materializer path already used by the public-edge proof lane.

This is route-entry evidence only. It is not workflow execution proof.

### 6. Hosted Public-Edge Execution Proof

```bash
node scripts/e2e-public-edge-playwright.cjs
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json
```

This is hosted browser workflow execution evidence for the promoted `/blazor/workbench` lane.

### 7. Browser-Lane Aggregate

```bash
bash scripts/ai/milestones/blazor-browser-lane-proof-set-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json
```

This aggregate must consume runtime receipts, not source-staged receipts.

## Promotion Rule

A staged workflow family is promotable only after both of these are refreshed and passing:

- Docker self-host browser proof
- hosted public-edge execution proof

Do not use source-staged receipts as release evidence.

## Status Summary

After refresh, use:

```bash
python3 scripts/print_blazor_public_edge_proof_status.py
```

The summary should show source-staged lanes separately from Docker, hosted route-entry, hosted execution, connected-runtime, analytics, and aggregate browser-lane proof.

`scripts/print_blazor_public_edge_proof_status.py` also reports this plan as `runtime_proof_refresh_plan_*` when `.codex-studio/published/BLAZOR_RUNTIME_PROOF_REFRESH_PLAN.generated.json` exists. Those lines are source-plan visibility only and do not replace the Docker, hosted, or aggregate browser receipts.

Example receipt shape: `docs/examples/blazor-runtime-proof-refresh-plan.receipt.example.json`.
