# Blazor Workbench Desktop Install Staged Proof

## Purpose

This source-staged proof keeps browser-to-desktop continuity visible on the user-facing Chummer Online route and proof-compatible /blazor/workbench compatibility route.

The browser client should behave like another Chummer client head while still giving users clear paths to downloads, update status, Docker self-hosting, account state, help, and support.

## Source-Staged Scope

The staged desktop install lane covers:

- a desktop install strip on the user-facing Chummer Online route and proof-compatible compatibility route
- downloads, update channel, release status, account, self-host notes, help, and support shortcuts
- scoped responsive styling for desktop and mobile browser use
- native desktop installer progress chrome using an amber accent bar, deep slate shell, mint progress fill, warm ink metadata, and amber hint text so the downloaded installer still feels connected to the Chummer Online visual system
- native installer high-contrast system-color fallback so Windows accessibility settings override decorative app chrome when required

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-desktop-install-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_DESKTOP_INSTALL_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench desktop install handoff source, style, native installer progress chrome, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, native installer runtime proof, installer proof, release-download proof, account authorization proof, portal-help-runtime proof, support-submission proof, or desktop-equivalent workflow parity.
