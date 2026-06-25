# Blazor Workbench Campaign-Session Staged Proof

## Purpose

This source-staged proof keeps campaign and live-session handoff affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

The browser client should preserve desktop-client confidence by keeping campaign roster, GM review, session notes, rewards, table share, run handoff, and help context near the active character workspace for Chummer Run users.

## Source-Staged Scope

The staged campaign-session lane covers:

- a campaign/session strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- roster, GM review, session notes, rewards, table share, run handoff, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-campaign-session-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route campaign-session source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, campaign-persistence proof, GM-approval proof, reward-mutation proof, table-share proof, run-handoff proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
