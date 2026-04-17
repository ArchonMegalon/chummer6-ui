# Workbench Session Handoff

Last updated: 2026-04-17

## Scope

Bring Chummer6 desktop UX much closer to Chummer5a layout posture:

- no dashboard-style first-launch detour
- dense classic chrome
- compact secondary windows
- parity-minded Avalonia and Blazor workbench presentation
- no hidden startup work that can stall first paint
- current desktop packaging should ship the current icon payload
- release pipeline should consume the current pushed UI snapshot

## Last pushed baseline

- Branch: `safe-push-fix-windows-installer-payload-20260401`
- Pushed refs:
  - `origin/safe-push-fix-windows-installer-payload-20260401`
  - `origin/fleet/ui`
  - `origin/main`
- Last pushed UI commit: `b8b4fa4d`
  - message: `Normalize local compatibility tree resolution`

## Uncommitted current slice

Files changed locally:

- `Chummer.Blazor.Desktop/Program.cs`
- `Chummer.Blazor/Components/DesktopAppHost.razor`
- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `Chummer.Blazor.Desktop/Program.cs`
  - stops mounting the full-document web app root into the Photino desktop host element
  - mounts a component-only desktop host instead
- `Chummer.Blazor/Components/DesktopAppHost.razor`
  - adds a minimal desktop-only root that renders `<Routes />` without a second `<html>` shell
- Expected user-facing result:
  - Blazor Desktop should stop hanging on the perpetual loading shell caused by rendering a whole HTML document inside `wwwroot/index.html`'s `<app>` host

## Validation status

What passed:

- `dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal`
  - passed after switching the Photino root to `DesktopAppHost`

What still needs direct runtime verification:

- launch Blazor Desktop after this commit and confirm first paint reaches the actual workbench instead of the black/loading shell
- continue the Avalonia parity pass; the user still reports visible drift from Chummer5a in chrome, density, and attribute presentation

## Next exact commands

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor.Desktop/Program.cs Chummer.Blazor/Components/DesktopAppHost.razor docs/WORKBENCH_SESSION_HANDOFF.md
```

Commit only the three files above:

```bash
git add Chummer.Blazor.Desktop/Program.cs
git add Chummer.Blazor/Components/DesktopAppHost.razor
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Use component-only Blazor desktop host"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next design slices after this commit

1. Launch Blazor Desktop locally to verify that the host-root fix actually removes the stuck loading shell.
2. Audit Avalonia shell against a Chummer5a screenshot pass: menu/toolstrip density, tab strip posture, left rail width, status strip, and attribute layout.
3. Cut the remaining “jump” chrome in Avalonia and Blazor where a classic workbench surface should own the first glance.
4. Push the parity slice before the next mac build so published preview artifacts stop lagging the actual UI repo state.
5. Run a screenshot/audit pass and document only intentional diffs from Chummer5a.

## Resume after interruption

If this session dies from OOM or process pruning, resume with these exact steps:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Blazor.Desktop/Program.cs Chummer.Blazor/Components/DesktopAppHost.razor docs/WORKBENCH_SESSION_HANDOFF.md
dotnet build Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj -v minimal
git add Chummer.Blazor.Desktop/Program.cs Chummer.Blazor/Components/DesktopAppHost.razor docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Use component-only Blazor desktop host"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

Then continue immediately with the Avalonia parity pass; that is still the largest visible debt.

## Important notes

- Do not commit unrelated dirty/generated files in this repo.
- The release pipeline can easily pick up an older-feeling snapshot if `fleet/ui` or `main` is not pushed after each UI slice.
- The user specifically wants Chummer5a to be the layout reference, not just inspiration.
- The current highest-risk visible regressions are still Avalonia chrome density, menu fidelity, and attribute/workbench layout parity.
