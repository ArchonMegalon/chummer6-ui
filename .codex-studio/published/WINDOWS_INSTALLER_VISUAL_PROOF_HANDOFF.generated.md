# Windows Visual Proof Handoff

Generated: 2026-06-27T14:51:19Z

- Status: `ready_for_windows_host`
- Gate summary: Windows desktop exit gate failed: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.
- Only blocker is visual proof: `True`
- Channel: `public_stable`
- Version: `run-20260627-005402`

## Installer

- Artifact: `avalonia-win-x64-installer`
- File: `chummer-avalonia-win-x64-installer.exe`
- URL: `https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe`
- SHA-256: `sha256:04ae1f160e299b8d5613bde3f166cb7b6214e8514927e88af61131ad95eccba4`
- Payload: `chummer-avalonia-win-x64-payload.zip`
- Payload URL: `https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip`

### Local installer bytes found

- `/docker/chummercomplete/chummer-presentation/Docker/Downloads/files/chummer-avalonia-win-x64-installer.exe`
- `/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/files/chummer-avalonia-win-x64-installer.exe`

### Local payload bytes found

- `/docker/chummercomplete/chummer-presentation/Docker/Downloads/files/chummer-avalonia-win-x64-payload.zip`
- `/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/files/chummer-avalonia-win-x64-payload.zip`

## Startup smoke already present

- Status: `pass`
- Version: `run-20260627-005402`
- Release version: `run-20260627-005402`
- Receipt: `/docker/chummercomplete/chummer6-ui/Docker/Downloads/startup-smoke/startup-smoke-avalonia-win-x64.receipt.json`
- Host class: `local-win-x64`
- Matches release candidate: `True`
- Matches installer file: `True`
- Matches installer digest: `True`

## Required screenshots

- `progress`: `windows-installer-progress.png` -> `/docker/chummercomplete/chummer-presentation/.codex-studio/published/windows-installer-visual-proof/windows-installer-progress.png`
- `completion`: `windows-installer-completion.png` -> `/docker/chummercomplete/chummer-presentation/.codex-studio/published/windows-installer-visual-proof/windows-installer-completion.png`

## Gate reasons

- Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.

## Blockers

- none

## Next actions

- On a real Windows host, open `/docker/chummercomplete/chummer-presentation` and run `powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-windows-installer-visual-proof.ps1`.
- Confirm `windows-installer-progress.png` and `windows-installer-completion.png` are written under `/docker/chummercomplete/chummer-presentation/.codex-studio/published/windows-installer-visual-proof`.
- Confirm `WINDOWS_INSTALLER_VISUAL_PROOF.generated.json` is written under `/docker/chummercomplete/chummer-presentation/.codex-studio/published`.
- Rerun `/docker/chummercomplete/chummer-presentation/scripts/materialize-windows-desktop-exit-gate.sh` and then refresh the aggregate desktop executable gate.
