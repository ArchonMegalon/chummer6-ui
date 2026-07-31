# Windows Visual Proof Handoff

Generated: 2026-06-28T19:52:27Z

- Status: `needs_review`
- Gate summary: Windows desktop exit gate passed.
- Only blocker is visual proof: `False`
- Channel: `public_stable`
- Version: `run-20260627-005402`
- Shelf root: `Docker/Downloads`

## Installer

- Artifact: `avalonia-win-x64-installer`
- File: `chummer-avalonia-win-x64-installer.exe`
- URL: `https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe`
- SHA-256: `sha256:d9d25b2c93dbd4887590b52b03431c4aba3c5614dbc4b18ec2f282222067466c`
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
- Progress log: `/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/startup-smoke/windows-installer-progress-avalonia-win-x64.log`
- Progress log present: `True`

## Current visual proof state

- Exists: `True`
- Status: `pass`
- Version: `run-20260627-005402`
- Digest: `sha256:d9d25b2c93dbd4887590b52b03431c4aba3c5614dbc4b18ec2f282222067466c`
- Matches release candidate: `True`
- Matches installer digest: `True`
- Stale: `False`

## Required screenshots

- `progress`: `windows-installer-progress.png` -> `Docker/Downloads/windows-installer-visual-proof/windows-installer-progress.png`
- `completion`: `windows-installer-completion.png` -> `Docker/Downloads/windows-installer-visual-proof/windows-installer-completion.png`

## Gate reasons

- none

## Blockers

- none

## Next actions

- Refresh the existing Windows visual-proof receipt at `Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json` against the staged candidate before promotion.
- On a real Windows host, open the repo checkout that contains the capture script and run `powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\capture-windows-installer-visual-proof.ps1 -ReleaseChannelPath "Docker/Downloads/RELEASE_CHANNEL.generated.json" -OutputPath "Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json"`.
- Confirm `windows-installer-progress.png` and `windows-installer-completion.png` are written under `Docker/Downloads/windows-installer-visual-proof`.
- Confirm `WINDOWS_INSTALLER_VISUAL_PROOF.generated.json` is written under `Docker/Downloads`.
- Rerun the Windows exit gate against the same shelf: `CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="Docker/Downloads/RELEASE_CHANNEL.generated.json" CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="Docker/Downloads/files" CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="Docker/Downloads/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json" bash /docker/chummercomplete/chummer-presentation/scripts/materialize-windows-desktop-exit-gate.sh`.
