# Blazor Workbench Share/Export Privacy Staged Proof

## Purpose

This staged proof keeps the Chummer Online and proof-compatible Blazor workbench source aligned around private share/export handoff posture for browser output workflows.

It covers visible source posture for redaction profiles, share-link scope, expiry, revocation, audit history, local-only export, and same-origin help affordances on `/blazor/workbench`.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-share-export-privacy-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SHARE_EXPORT_PRIVACY_STAGED_PROOF.generated.json
```

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, share-token issuance, redaction execution, revocation persistence, audit storage, export-policy enforcement, or portal help runtime behavior.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof.
