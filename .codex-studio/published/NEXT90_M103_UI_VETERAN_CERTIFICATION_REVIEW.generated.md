# Next90 M103 Veteran Certification Review

Package: `next90-m103-ui-veteran-certification`
Frontier: `2257965187`
Landed commit: `a8e4f92c`
Promoted desktop head: `avalonia`
Receipt: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json`
Proof command: `bash scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh`
Screenshot pack: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots`

This review packet summarizes the screenshot-backed parity checks that keep the promoted desktop head familiar to Chummer5a veterans.

| Surface | Veteran question | Gesture proved on promoted head | Chummer5a baseline | Screenshot | Size | Fresh vs source | SHA-256 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| toolstrip | Can a veteran start normal character work from the same always-visible toolbar posture? | Inspect the initial shell and verify load, import, save, settings, support, and close actions stay in the toolstrip. | Classic flat workbench actions remain immediate toolbar buttons instead of dashboard cards. | 01-initial-shell-light.png | 1280x800 | fresh | 2491a56cd9da78af8c4e6795ad086bb5d9fa39960486e26aeebf7611e4f37556 |
| menu | Can a veteran find the same top-level command geography in the first minute? | Open the promoted Avalonia head and expand a primary menu to reveal command choices. | Chummer5a top menu roots remain visible as File, Edit, Special, Tools, Windows, and Help. | 02-menu-open-light.png | 1280x800 | fresh | daf168881b7a4e817d46008acb66302559d435b701da8910465b2adb4589f381 |
| settings | Can a veteran find global setup before editing a character? | Open Global Settings from the promoted desktop toolstrip/menu surface. | Global Settings remains a first-minute settings route with source and roster configuration lineage. | 03-settings-open-light.png | 1280x800 | fresh | 94da1ae9e16f4da06ac10e69c1934bb320c7938525f91d5f875d38a3ecf213e1 |
| import | Can a veteran bring an existing character into the workbench without browser-only ritual? | Load the bundled legacy runner through the promoted desktop import path. | Existing .chum5-era import starts from an obvious desktop import action and lands in the loaded-runner workbench. | 18-import-dialog-light.png | 1280x800 | fresh | 95ae9a65b162bac13b28a75e980804a59002dcf233806fbf96b371be756c496d |
| master index | Can a veteran reach the familiar index/search utility from desktop chrome? | Open Master Index from the promoted desktop command surface. | The Master Index utility remains a named searchable reference surface. | 16-master-index-dialog-light.png | 1280x800 | fresh | e941c746d714e9546a705252b1bf1ecf532fd0fff000eb0a2dbd798844bdf9f1 |
| roster | Can a veteran find the familiar roster utility without support instructions? | Open Character Roster from the promoted desktop command surface. | The Character Roster utility is still a named utility surface, not hidden behind campaign-only navigation. | 17-character-roster-dialog-light.png | 1280x800 | fresh | a0f2311ec011e169616069cd1e3705083236b7f0687affb69ed5eec0a9c2ab0a |

## Screenshot paths

- `toolstrip`: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots/01-initial-shell-light.png`
- `menu`: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots/02-menu-open-light.png`
- `settings`: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots/03-settings-open-light.png`
- `import`: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots/18-import-dialog-light.png`
- `master_index`: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots/16-master-index-dialog-light.png`
- `roster`: `/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/ui-flagship-release-gate-screenshots/17-character-roster-dialog-light.png`

## Legacy baselines

- `toolstrip`: `/docker/chummer5a/Chummer/Forms/ChummerMainForm.Designer.cs` (`f7a0a6a78c5dd72e7c8e3a4be83bf9c2db7e18bb2729d8c901c0431168037f4d`)
- `menu`: `/docker/chummer5a/Chummer/Forms/ChummerMainForm.Designer.cs` (`f7a0a6a78c5dd72e7c8e3a4be83bf9c2db7e18bb2729d8c901c0431168037f4d`)
- `settings`: `/docker/chummer5a/Chummer/Forms/EditGlobalSettings.Designer.cs` (`8b5070f37ee7231fec6b4a1c01525845d23c15342942c5300025f3f7bf9df88a`)
- `import`: `/docker/chummer5a/Chummer/Forms/Utility Forms/HeroLabImporter.Designer.cs` (`7c189fc3d2aeb80d946ae5e2793eac73553a701bc423f53e8385fac9f1e70daa`)
- `master_index`: `/docker/chummer5a/Chummer/Forms/Utility Forms/MasterIndex.Designer.cs` (`0faa79543933d5e6f6372722e2c04ec20df5f5483083d221712a5f104b48f7b7`)
- `roster`: `/docker/chummer5a/Chummer/Forms/Utility Forms/CharacterRoster.Designer.cs` (`fe93903fdf104df824d55d0da9be7e8b10edbbd3cf3fd380d5c378d2c43478eb`)

## Source proof anchors

- `toolstrip`:
  - `toolstrip`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/ToolStripControl.axaml`
  - `test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `menu`:
  - `menu`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/ShellMenuBarControl.axaml`
  - `test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `settings`:
  - `toolstrip`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/ToolStripControl.axaml`
  - `event_handlers`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/MainWindow.EventHandlers.cs`
  - `test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `import`:
  - `toolstrip`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/Controls/ToolStripControl.axaml`
  - `event_handlers`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Avalonia/MainWindow.EventHandlers.cs`
  - `test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `master_index`:
  - `presenter_test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
  - `test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `roster`:
  - `presenter_test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
  - `test`: `/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`

## Screenshot capture health

| Surface | Screenshot mtime (UTC) | Newest source proof (UTC) | Distinct sampled colors | Content non-blank |
| --- | --- | --- | --- | --- |
| toolstrip | 2026-04-26T10:00:45.433089Z | 2026-04-26T09:57:21.497826Z | 6 | true |
| menu | 2026-04-26T10:00:45.434089Z | 2026-04-26T09:57:21.497826Z | 5 | true |
| settings | 2026-04-26T10:00:45.434089Z | 2026-04-26T09:57:21.497826Z | 6 | true |
| import | 2026-04-26T10:00:45.440089Z | 2026-04-26T09:57:21.497826Z | 6 | true |
| master index | 2026-04-26T10:00:45.439089Z | 2026-04-26T09:57:21.497826Z | 6 | true |
| roster | 2026-04-26T10:00:45.440089Z | 2026-04-26T09:57:21.497826Z | 6 | true |

## Review posture

- Required promoted platforms: `linux, windows, macos`
- Required surface count: `6`
- All required screenshots present: `true`
- All screenshots fresh against source proof: `true`
- All screenshots meet desktop review size: `true`
- All screenshots show non-blank sampled content: `true`
- Queue completion action: `verify_closed_package_only`
- Do-not-reopen reason: M103 chummer6-ui veteran certification is complete; future shards must verify this receipt, registry row, design queue row, Fleet queue row, and direct proof command instead of recapturing promoted-head Chummer5a screenshot parity.
