# Release Build Handoff

Generated: 2026-06-28T19:52:27Z

- Stage dir: `Docker/Downloads`
- Channel: `public_stable`
- Version: `run-20260627-005402`
- Artifact count: `2`
- Promotion ready: `False`

## Artifacts

- `avalonia-linux-x64-installer` -> `chummer-avalonia-linux-x64-installer.deb` (linux / linux-x64)
- `avalonia-win-x64-installer` -> `chummer-avalonia-win-x64-installer.exe` (windows / win-x64)

## Startup Smoke

- `avalonia:linux:linux-x64`: `pass`
- `avalonia:windows:win-x64`: `pass`

## Windows Exit Gate Refresh

- Status: `passed`
- JSON: `Docker/Downloads/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json`
- Script: `/docker/chummercomplete/chummer-presentation/scripts/materialize-windows-desktop-exit-gate.sh`
- Blocking mode: `none`
- Summary: Windows desktop exit gate passed.

## Windows Visual Proof Handoff

- Status: `needs_review`
- JSON: `Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json`
- Markdown: `Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md`
- Visual proof receipt target: `Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json`
- Summary: Windows desktop exit gate passed.

## Remaining Blockers

- Windows visual-proof handoff is not ready; inspect the staged handoff packet before asking a Windows operator to continue.

## Next Actions

- Inspect the Windows visual-proof handoff packet and fix the staged shelf mismatch before promotion: Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json
- Publish the verified bundle with CHUMMER_RELEASE_UPLOAD_TOKEN once all required platform tuples are promotable.
