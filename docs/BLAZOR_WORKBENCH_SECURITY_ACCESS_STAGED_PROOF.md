# Blazor Workbench Security-Access Staged Proof

## Purpose

This source-staged proof keeps hosted security and access-control affordances visible on the promoted Blazor workbench route.

The browser client should make sign-in, workspace lock, player/GM roles, session expiry, key rotation, and access audit posture visible near hosted character state.

## Source-Staged Scope

The staged security-access lane covers:

- a hosted security/access strip on the promoted workbench route
- sign-in, workspace lock, roles, session expiry, key rotation, and access audit shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-security-access-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SECURITY_ACCESS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench security-access source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, authentication proof, RBAC proof, session-expiry proof, key-rotation proof, audit-log proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
