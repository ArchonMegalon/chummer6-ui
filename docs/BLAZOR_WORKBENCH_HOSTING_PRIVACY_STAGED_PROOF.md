# Blazor Workbench Hosting and Privacy Staged Proof

## Purpose

This source-staged proof keeps the promoted Blazor workbench hosting and privacy posture visible in the product UI.

The browser client should be understandable as both hosted `chummer.run` software and self-hostable Docker software. Users should also see the analytics boundary where they work, not only in operator docs.

## Source-Staged Scope

The staged hosting/privacy lane covers:

- hosted route posture for `/blazor/workbench`
- Docker self-host posture through the portal/API/Blazor shape
- Rybbit is optional and metadata-only analytics copy
- explicit privacy limits for character, owner, workspace, XML, and dossier content
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-hosting-privacy-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench hosting/privacy source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, Rybbit service-health proof, analytics delivery proof, screenshot proof, or desktop-equivalent workflow parity.
