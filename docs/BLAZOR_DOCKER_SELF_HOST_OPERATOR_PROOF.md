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

Expected public Chummer Online route:

```text
/blazor/app
```

Expected product/orientation route:

```text
/blazor/home
```

Expected explicit proof-compatible compatibility route:

```text
/blazor/workbench
```

## Required Operator Contract

Self-host users must be able to identify:

- which compose profile starts the portal-backed browser client
- which public URL exposes Chummer Online
- which explicit workbench route remains available for proof-compatible workflows
- which services are part of the portal edge
- where downloads/install handoff lives
- how optional Rybbit analytics is configured as default-off self-host telemetry
- which proof command exercises the local portal lane
- which generated receipt is Docker self-host runtime evidence
- which staged receipts are source-only and not runtime evidence

## Optional Analytics Boundary

Self-host analytics is operator controlled and default-off. The sanitized environment example keeps `CHUMMER_ANALYTICS_PROVIDER=none` and only enables Rybbit when an operator explicitly sets `CHUMMER_ANALYTICS_PROVIDER=rybbit`, `CHUMMER_RYBBIT_SITE_ID`, and either `CHUMMER_RYBBIT_SCRIPT_URL` or `CHUMMER_RYBBIT_BASE_URL`.

Self-host Rybbit analytics remains default-off unless the operator explicitly configures the Rybbit provider and site variables, with session replay and autocapture disabled for Chummer surfaces.

The Docker profile passes the Rybbit variables into the Blazor service so hosted and self-host deployments use the same browser client code path without requiring analytics for rendering.

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

## Documentation Index Requirement

The staged proof also checks `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` for this contract document and the compact example receipt at `docs/examples/blazor-docker-self-host-operator-staged-proof.receipt.example.json`.

This keeps the Docker self-host operator posture discoverable from the top-level Blazor/web-client docs map without treating the staged receipt as Docker runtime proof.

## Boundary

The staged operator proof does not start Docker, probe routes, or prove the browser client renders. It only proves the source contract, docs, and expected command wiring are present.

Use the staged proof before refreshing Docker runtime proof; never use it instead of `BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json`.
