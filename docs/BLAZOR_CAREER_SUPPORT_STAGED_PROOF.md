# Blazor Career Support Staged Proof

## Purpose

This staged proof keeps the Chummer App and proof-compatible Blazor workbench source aligned around the career/support workflow family.

It covers restored `tab-calendar` section continuity, career entry add/edit/delete dialogs, committed add/edit/delete result continuations, runner notes editing and notes-save continuations, and classic move up/down list utilities on the promoted `/blazor/workbench` route.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-career-support-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json
```

## Status Lines

`scripts/print_blazor_public_edge_proof_status.py` reports this staged receipt separately:

```text
career_support_staged_status=
career_support_staged_route_count=
career_support_staged_source_checks=
career_support_staged_note=source_alignment_only_not_browser_execution
```

These lines show source staging only. They are not browser execution proof and do not replace hosted public-edge execution proof, Docker self-host proof, or browser-lane aggregate proof.

## Boundary

This proof checks product affordances, hosted route-entry probing, hosted execution runner source, Docker self-host runner source, self-host receipt metadata, operator docs, release docs, and status reporting. It does not execute `chummer.run`, Docker self-host, Playwright, dialog actions, persistence, or committed browser mutations.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof that exercise these career/support routes as runtime receipts.
