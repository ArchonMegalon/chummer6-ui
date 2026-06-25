# Blazor Workbench Hosting and Privacy Staged Proof

## Purpose

This source-staged proof keeps the Chummer Online and proof-compatible /blazor/workbench compatibility-route hosting and privacy posture visible in the product UI.

The browser client should be understandable as both hosted `chummer.run` software and self-hostable Docker software. Users should also see the analytics boundary where they work, not only in operator docs.

## Source-Staged Scope

The staged hosting/privacy lane covers:

- hosted route posture for clean public `/app`, hosted `/blazor/app`, and proof-compatible `/blazor/workbench`
- Docker self-host posture through the portal/API/Blazor shape
- hosted `chummer.run` Rybbit enablement posture with explicit telemetry limits
- Rybbit is optional and metadata-only analytics copy
- self-host default-off analytics copy, including `CHUMMER_ANALYTICS_PROVIDER=none`
- explicit privacy limits for character, owner, workspace, XML, and dossier content
- scoped responsive styling for desktop and mobile browser use
- status reporting note `source_alignment_only_default_off_rybbit_not_browser_execution`

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-hosting-privacy-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench hosting/privacy source, style, status reporting, and docs agree, including the default-off Rybbit boundary for self-hosted deployments.

It is not hosted browser execution proof, Docker self-host proof, Rybbit service-health proof, analytics delivery proof, screenshot proof, or desktop-equivalent workflow parity.
