# Blazor Docker Self-Host Operator Proof

## Purpose

This document defines the source-staged operator proof contract for running the browser Chummer client as a self-hosted Docker surface.

The promoted self-host shape is portal-backed:

```text
Chummer.Portal -> /blazor/* -> Chummer.Blazor
Chummer.Portal -> /api/* -> Chummer.Api
Chummer.Portal -> /downloads/* -> desktop release shelf
```

Raw Blazor hosting is not the product shape for self-host users.

## Expected Docker Profile

The canonical local/self-host lane is the `portal` Docker Compose profile used by `scripts/e2e-portal.sh`.

Expected services:

- `chummer-api`
- `chummer-blazor-portal`
- `chummer-hub-web-portal`
- `chummer-avalonia-browser`
- `chummer-portal`

Expected Blazor path base:

```text
/blazor
```

Expected public workbench route:

```text
/blazor/workbench
```

## Required Operator Contract

Self-host users must be able to identify:

- which compose profile starts the portal-backed browser client
- which public URL exposes the browser workbench
- which services are part of the portal edge
- where downloads/install handoff lives
- which proof command exercises the local portal lane
- which generated receipt is Docker self-host runtime evidence
- which staged receipts are source-only and not runtime evidence

## Canonical Runtime Proof Command

Runtime proof remains:

```bash
bash scripts/e2e-portal.sh
```

That command owns Docker startup and writes the real self-host receipt:

```text
.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json
```

## Source-Staged Operator Proof Command

Source alignment for the operator contract is checked by:

```bash
bash scripts/ai/milestones/blazor-docker-self-host-operator-staged-proof-check.sh
```

It writes:

```text
.codex-studio/published/BLAZOR_DOCKER_SELF_HOST_OPERATOR_STAGED_PROOF.generated.json
```

## Boundary

The staged operator proof does not start Docker, probe routes, or prove the browser workbench renders. It only proves the source contract, docs, and expected command wiring are present.

Use the staged proof before refreshing Docker runtime proof; never use it instead of `BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`.
