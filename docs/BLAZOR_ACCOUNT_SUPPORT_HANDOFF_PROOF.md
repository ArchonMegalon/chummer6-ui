# Blazor Account and Support Handoff Proof

## Purpose

This document defines the source-staged proof contract for account, owner-context, and support handoff from the promoted Blazor browser workbench.

The browser client should behave like another Chummer desktop client, so account recovery, support, status, and work-continuation routes must stay part of the same portal product shape instead of forcing users into disconnected pages.

## Source-Staged Scope

The staged handoff lane covers source alignment for these portal routes and surfaces:

- `/hub`
- `/hub/`
- `/contact`
- `/status`
- `/home/access`
- `/home/work`
- `/account/work`
- `/account/support`
- `/blazor/workbench`

## Required UX Contract

The Blazor browser lane must keep these ideas true:

- `/blazor/workbench` remains the product-shaped browser client entry.
- Support handoff stays same-origin through `Chummer.Portal`.
- Account routes preserve user intent when authentication is required.
- `/contact` offers a direct support case path.
- `/status` gives release/runtime truth before a user installs or reports a problem.
- Self-host proof distinguishes implicit local owner posture from public account posture.
- Account/support source-staged proof must not be treated as authentication or authorization runtime proof.

## Source-Staged Receipt

The source-staged receipt is:

```text
.codex-studio/published/BLAZOR_ACCOUNT_SUPPORT_HANDOFF_STAGED_PROOF.generated.json
```

Materialize it with:

```bash
bash scripts/ai/milestones/blazor-account-support-handoff-staged-proof-check.sh
```

## Boundary

This proof is source-only. It confirms route expectations are present in scripts and docs, but it does not prove the portal is running, authentication works, cookies are valid, owner propagation is live, or support submissions are accepted.

Runtime claims still require local portal proof, hosted route-entry proof, hosted execution proof, and the connected-runtime/owner-boundary receipts where applicable.
