# Next90 M103 Veteran Certification Review

Receipt: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json`
Screenshot pack: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots`
Source repo: `/docker/chummercomplete/chummer-presentation`
Authority proof repo: `/docker/chummercomplete/chummer6-ui-finish`
Screenshot control evidence: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/SCREENSHOT_CONTROL_EVIDENCE.generated.json`
Difference ledger source: `/docker/chummercomplete/chummer-presentation/docs/CHUMMER5A_VISUAL_DIFFERENCE_LEDGER.json`

| Surface | Parity Question | Promoted-Head Proof | Legacy Familiarity | Screenshot | Sample Colors | SHA-256 |
| --- | --- | --- | --- | --- | ---: | --- |
| menu | Can a veteran find the same top-level command geography in the first minute? | Open the promoted Avalonia head and expand a primary menu to reveal command choices. | Chummer5a top menu roots remain visible as File, Edit, Special, Tools, Windows, and Help. | 02-menu-open-light.png | 3 | d3088ae1666d04cb956af150c6c8b9a18a4a7fe3d4f267925fa8d131d3e6bf08 |
| toolstrip | Can a veteran start normal character work from the same always-visible toolbar posture? | Inspect the initial shell and verify load, import, save, settings, support, and close actions stay in the toolstrip. | Classic flat workbench actions remain immediate toolbar buttons instead of dashboard cards. | 01-initial-shell-light.png | 3 | b1adcc16078e7afe39ee3deb9366446ec96da95532ff7a94f1c20bfdfef6a3a8 |
| roster | Can a veteran find the familiar roster utility without support instructions? | Open Character Roster from the promoted desktop command surface. | The Character Roster utility is still a named utility surface, not hidden behind campaign-only navigation. | 17-character-roster-dialog-light.png | 3 | 7441823a01f6746113694a55897643097494343efa1c5a3bfdd77ea59bd78abb |
| master_index | Can a veteran reach the familiar index/search utility from desktop chrome? | Open Master Index from the promoted desktop command surface. | The Master Index utility remains a named searchable reference surface. | 16-master-index-dialog-light.png | 3 | ccf875ef9191b1dae8fccfcd81c127a73d38275d72798e13ad6b1f19729d1535 |
| settings | Can a veteran find global setup before editing a character? | Open Global Settings from the promoted desktop toolstrip/menu surface. | Global Settings remains a first-minute settings route with source and roster configuration lineage. | 03-settings-open-light.png | 5 | b366f33909544d3b0d4d4867ab1da088ee79477e612da40ef3eef4885fced6b8 |
| import | Can a veteran find the classic import route after landing in the modern workbench? | Load the bundled legacy runner, then open File > Open Character on the promoted desktop head. | Existing .chum5-era import still starts from the desktop shell and exposes the familiar open-character route. | 18-import-dialog-light.png | 5 | 561f18c3a508e99cc4e1f4c08ae6a712fccff56890f61f9a73cda9d622b0f476 |
| translator | Can a veteran find the translator-era localization route and understand its governed replacement state? | Open Translator from the promoted desktop command surface and confirm the governed localization bridge posture. | The Translator utility still exists as a named desktop Tools route rather than buried in generic settings. | 38-translator-dialog-light.png | 3 | ff323a7211779ec301185f13aef024944b3227bd0d4a9ce357c65dc7d299d89d |
| xml_editor | Can a veteran inspect XML amend/custom-data posture without dropping into unsupported hidden tooling? | Open XML Editor from the promoted desktop command surface and verify overlay plus custom-data posture. | The XML editing and amend workflow still exists as a named desktop route with explicit XML bridge posture. | 39-xml-editor-dialog-light.png | 3 | 6766aa4c2a6c82447a933ed9c6453861bb6ffb6d213ccd5910d707e91436923f |
| hero_lab_importer | Can a veteran still find the Hero Lab-specific import route when adjacent import oracles matter? | Open Hero Lab Importer from the promoted desktop Tools surface and confirm the compatibility import payload. | Hero Lab importer remains a named compatibility import route instead of disappearing behind the generic open flow. | 40-hero-lab-importer-dialog-light.png | 4 | bf5bec03a59ffa97b697ca7edba7cd0e0582218814d46e69fba7d033c24db91b |

## Screenshots

- `menu`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/02-menu-open-light.png`
- `toolstrip`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/01-initial-shell-light.png`
- `roster`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/17-character-roster-dialog-light.png`
- `master_index`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/16-master-index-dialog-light.png`
- `settings`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/03-settings-open-light.png`
- `import`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/18-import-dialog-light.png`
- `translator`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/38-translator-dialog-light.png`
- `xml_editor`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/39-xml-editor-dialog-light.png`
- `hero_lab_importer`: `/docker/chummercomplete/chummer-presentation/.codex-studio/published/ui-flagship-release-gate-screenshots/40-hero-lab-importer-dialog-light.png`

## Audited UI Differences

### 01-initial-shell-light.png (`initial_shell`)

- Surface kind: frame
- Parity intent: Workbench-first startup posture with visible menu, core toolbar actions, and no wasted first-paint chrome.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/ChummerMainForm.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/ToolStripControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Evidence anchors:
- Anchor: ShellMenuBarControl.axaml roots: FileMenuButton, EditMenuButton, SpecialMenuButton, ToolsMenuButton, WindowsMenuButton, HelpMenuButton.
- Anchor: ToolStripControl.axaml visible first-paint buttons: SaveButton, PrintButton, CopyButton, DesktopHomeButton, ImportFileButton, CloseWorkspaceButton, SettingsButton.
- Anchor: AvaloniaFlagshipUiGateTests.cs hides ImportRawButton, LoadDemoRunnerButton, CampaignWorkspaceButton, UpdateStatusButton, InstallLinkingButton, SupportButton, and ReportIssueButton on the default workbench.
- Anchor: AvaloniaFlagshipUiGateTests.cs keeps LeftNavigatorRegion, LoadedRunnerTabStripBorder, and RestoreContinuityStatusBorder collapsed before a runner is loaded.
- `ToolStripControl.SaveButton/PrintButton/CopyButton/DesktopHomeButton/ImportFileButton/CloseWorkspaceButton/SettingsButton`: The Avalonia startup strip keeps Save, Print, Copy, New, Open, Close, and Options as first-paint buttons inside a compact WrapPanel instead of duplicating the old ToolStrip widget stack. Legacy posture: Classic WinForms ToolStrip exposed New/Open/Save/Print/Copy as always-visible main-form actions tied to MDI-era chrome. Why it differs: The user still gets one-click muscle memory for the first minute, but the promoted shell is tuned for a dense single-workbench layout rather than a literal WinForms clone.
- `ToolStripControl.ImportRawButton/LoadDemoRunnerButton/CampaignWorkspaceButton/UpdateStatusButton/InstallLinkingButton/SupportButton/ReportIssueButton visibility`: Those buttons still exist in the promoted head, but they stay hidden on the startup screenshot so the workbench does not burn first-paint width on demo, raw XML, campaign, update, linking, support, or bug chrome. Legacy posture: Legacy desktop utility affordances were spread across main-form chrome, menus, and detached helper flows. Why it differs: The first screen is required to feel like Chummer's dense workbench, not a dashboard of secondary operations.
- `MainWindow.LeftNavigatorRegion/SummaryHeaderControl.LoadedRunnerTabStripBorder/SummaryHeaderControl.RestoreContinuityStatusBorder`: The promoted shell mounts navigator, tab-strip, and restore controls in code but collapses them completely until a runner or recovery handoff makes them relevant. Legacy posture: Chummer5a centered the first minute on the main form and did not spend initial vertical space on workspace rails, runner tabs, or recovery dossiers. Why it differs: The modern shell needs those routes later, but startup parity is only credible if the dense workbench still owns the opening frame.

### 02-menu-open-light.png (`menu_open`)

- Surface kind: frame
- Parity intent: Top-level command geography remains recognizable to a veteran in the first minute.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/ChummerMainForm.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/ShellMenuBarControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed menu commands: `exit`, `new_character`, `new_critter`, `open_character`, `open_for_export`, `open_for_printing`, `print_character`, `save_character`, `save_character_as`
- Evidence anchors:
- Anchor: ShellMenuBarControl.axaml declares FileMenuButton, EditMenuButton, SpecialMenuButton, ToolsMenuButton, WindowsMenuButton, and HelpMenuButton.
- Anchor: AvaloniaFlagshipUiGateTests.cs asserts ClassicMenuLabels exactly equal File, Edit, Special, Tools, Windows, Help.
- Anchor: AvaloniaFlagshipUiGateTests.cs requires FileMenuButton to surface open_character and save_character runtime commands.
- Anchor: CatalogOnlyRulesetShellCatalogResolver.cs and ShellChromeBoundary.cs bind current command ids open_character, global_settings, master_index, character_roster, and report_bug into the shell.
- `ShellMenuBarControl.FileMenuButton/EditMenuButton/SpecialMenuButton/ToolsMenuButton/WindowsMenuButton/HelpMenuButton`: The promoted shell keeps the same six roots by id and label, but they render through Avalonia menu primitives and modern shell styling instead of classic WinForms paint. Legacy posture: WinForms MenuStrip roots used the familiar File/Edit/Special/Tools/Windows/Help geography with ampersand accelerators and legacy chrome. Why it differs: Veteran navigation landmarks must survive, but the promoted head is not required to reproduce the old rendering engine's exact typography and accelerator quirks.
- `FileMenuButton -> open_character/save_character runtime commands`: FileMenuButton now dispatches live command ids such as open_character and save_character through the promoted shell runtime instead of static form-event wiring. Legacy posture: The legacy main form wired File/Open and File/Save directly to WinForms handlers. Why it differs: The new parity proof is only honest if the menu is backed by the current command pipeline rather than by a screenshot-only façade.
- `EditMenuButton/SpecialMenuButton/WindowsMenuButton workspace scoping`: The promoted shell keeps the familiar roots visible, but Edit and Special routes only expose live commands when the active workspace actually supports them and Windows no longer implies a full MDI manager. Legacy posture: Legacy menu geography was more static because the main form owned most command surfaces directly. Why it differs: Current menus are required to stay truthful to the active workspace model instead of advertising dead or MDI-only paths.

### 03-settings-open-light.png (`settings_dialog`)

- Surface kind: dialog
- Parity intent: Global setup stays first-minute reachable and still reads like a serious desktop settings surface.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/EditGlobalSettings.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `Background`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ClassicActionStrip`, `ClassicFormPortHostControl`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentPresenter`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `DropDownGlyph`, `EditMenuButton`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedContentHost`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PortContentHost`, `PortTitleText`, `PrintButton`, `ProgressBarRoot`, `RootBorder`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SettingsGlobalList`, `SettingsGlobalSelector`, `SettingsNoticeText`, `SettingsTabs`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs opens the same dialog from SettingsButton and Ctrl+G -> global_settings.
- Anchor: CommandDialogPaneControl.axaml exposes DialogTitleText, DialogFieldsHost, and DialogActionsHost for the inline settings surface.
- Anchor: DesktopDialogFactory.BuildGlobalSettingsFields creates globalSettingsTree, globalSettingsPropertyGrid, globalCharacterRosterPath, and globalPdfViewerPath.
- Anchor: AvaloniaFlagshipUiGateTests.cs keeps FileMenuButton responsive while the Global Settings dialog is still mounted.
- `SettingsButton/Ctrl+G -> global_settings command route`: The promoted shell routes both the toolbar button and the keyboard shortcut through the same global_settings command id and presenter path. Legacy posture: Legacy settings were typically opened through dedicated menu or utility-form routes on the WinForms shell. Why it differs: Modern command routing must be consistent across toolbar, menu, and keyboard surfaces even when the visual destination remains recognizable.
- `CommandDialogPaneControl.DialogTitleText/DialogFieldsHost/DialogActionsHost`: Global Settings is rendered inside the shared shell dialog host with a common title band, field stack, and action bar instead of a separate utility window. Legacy posture: EditGlobalSettings was a standalone WinForms window with its own frame, button row, and modality behavior. Why it differs: The promoted head standardizes dialog hosting so settings, import, utility, and builder dialogs all behave consistently inside the desktop shell.
- `DesktopDialogFactory.globalSettingsTree/globalSettingsPropertyGrid/globalCharacterRosterPath/globalPdfViewerPath`: The new dialog makes the navigation tree, current-pane property grid, roster path, PDF viewer path, and compact-mode fields explicit in one shared field model. Legacy posture: The old settings form used WinForms tabs and heterogeneous controls to distribute options across several pages. Why it differs: The settings content still follows the old mental model, but it is represented through a structured dialog field system that the current shell can verify and reuse.

### 04-loaded-runner-light.png (`loaded_runner`)

- Surface kind: frame
- Parity intent: A loaded runner still reads as a character-first workbench rather than a landing page or dashboard.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicAttributeFactsPanel`, `ClassicCharacterFactsPanel`, `ClassicCharacterSheetBorder`, `ClassicCharacterSummaryTitle`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_LineDownButton`, `PART_LineUpButton`, `PART_PageDownButton`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PART_VerticalScrollBar`, `PrintButton`, `ProgressBarRoot`, `Root`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionHostControl`, `SectionQuickAction_cyberware_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `TrackRect`, `VerticalRoot`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_cyberware_add`
- Observed tab labels: `Runner`
- Observed preview text: `Cyberware
Name: Soma
Metatype: Human
Ruleset: sr5
Initiative: 11 \u002B 2d6
Armor: 12
Essence: 5.34

BOD: 5
AGI: 7
REA: 6
Firearms 1: Automatics 6
Stealth 1: Sneaking 5
Weapons 1: Ares Alpha
Armor 1: Armor Jacket
Cyberware 1: Wired Reflexes 2
Contact 1: Fixer (Loyalty 4 / Connection 5)
Runner Goal: Ready for a flagship shell smoke pass

Payload
{
  "name": "Soma",
  "ruleset": "sr5",
  "metatype": "Human",
  "priority": "Standard",
  "role": "Street Sam",
  "attributes": {
    "Body": 5,
    "Agility": 7,
    "Reaction": 6,
    "Strength": 4,
    "Willpower": 3,
    "Logic": 3
  },
  "combat": {
    "initiative": "11 + 2d6",
    "armor": 12,
    "essence": 5.34
  }
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs loads the bundled Soma runner through LoadDemoRunnerButton and then captures 04-loaded-runner-light.png.
- Anchor: LoadedRunnerTabStripBorder becomes visible after import while LeftNavigatorRegion stays collapsed for the single-runner posture.
- Anchor: LoadedRunnerTabStrip keeps stable info/profile and gear landmarks in SnapshotLoadedRunnerTabs().
- Anchor: SectionRowsList is visible and SectionPreviewBox is non-empty before the loaded-runner screenshot is accepted.
- `SummaryHeaderControl.LoadedRunnerTabStripBorder/LoadedRunnerTabStrip`: The promoted head uses a compact tab-strip card to expose the Runner/Profile/Gear landmarks inside the summary header instead of rebuilding legacy form tabs verbatim. Legacy posture: Chummer5a relied on frmCareer-era tab geography and form chrome to show that a runner was active. Why it differs: The tab posture must still be obvious, but the current shell collapses everything that is not needed for a single loaded runner.
- `MainWindow.LeftNavigatorRegion/WorkspaceStripRegion`: The promoted shell keeps workspace-rail plumbing out of sight for the default loaded-runner case and refuses to reserve width for an empty navigator strip. Legacy posture: Legacy navigation lived inside the main character form without a separate workspace rail concept. Why it differs: Single-runner parity is stronger when the workbench owns the available width instead of emulating multi-workspace chrome that is irrelevant in the screenshot.
- `SectionHostControl.SectionRowsList/SectionPreviewBox`: The promoted shell keeps the same dense list-and-detail rhythm, but it expresses it through a structured row list and a preview payload pane that the tests can snapshot directly. Legacy posture: frmCareer showed dense browse-and-detail landmarks through WinForms tabs, lists, and property-style panes. Why it differs: The behavioral landmark is the browse/detail cadence, not the old control class names.

### 05-dense-section-light.png (`dense_section_light`)

- Surface kind: frame
- Parity intent: The light-theme dense builder keeps the same inspect, compare, and confirm rhythm without wasting space.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_skill_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_skill_add`
- Observed tab labels: `Runner`
- Observed selected rows: `skills[0] = Automatics 6 (Assault Rifles)`
- Observed preview text: `Skills
Skill 1: Automatics 6 (Assault Rifles)
Skill 2: Sneaking 5

Payload
{
  "section": "skills",
  "skills": [
    { "name": "Automatics", "rating": 6, "specialization": "Assault Rifles" },
    { "name": "Sneaking", "rating": 5 }
  ]
}`
- Evidence anchors:
- Anchor: Character_creation_preserves_familiar_dense_builder_rhythm() requires ClassicCharacterSheetBorder to be visible.
- Anchor: Gear_builder_preserves_familiar_browse_detail_confirm_rhythm() checks SectionRowsList rows gear.weapons[0] = Ares Alpha and gear.armor[0] = Armor Jacket.
- Anchor: SectionPreviewBox must contain combat/attributes payload markers before the dense screenshot is accepted.
- Anchor: NoticeBorder and NoticeText stay hidden so routine command noise does not consume visible workbench space.
- `SectionHostControl.ClassicCharacterSheetBorder/ClassicCharacterSummaryTitle/ClassicCharacterFactsPanel`: The promoted shell recreates that effect as a compact section-scoped summary band inside SectionHostControl instead of copying the old form layout exactly. Legacy posture: frmCareer kept the runner summary woven into the character form rather than in detached cards or a landing dashboard. Why it differs: The familiarity target is a dense always-near summary, not a pixel match to every legacy panel edge.
- `SectionHostControl.SectionRowsList`: The Avalonia workbench renders rows as explicit DisplayPath/DisplayValue pairs such as gear.weapons[0] = Ares Alpha and gear.armor[0] = Armor Jacket. Legacy posture: Classic dense sections mixed WinForms lists, trees, and field groups to render gear and related rows. Why it differs: Path/value rows are more mechanically testable and still preserve the dense browse rhythm veterans expect.
- `SectionHostControl.SectionPreviewBox`: The promoted shell keeps a hidden-but-mounted preview payload box that contains section markers such as combat and attributes for the selected row. Legacy posture: Legacy detail panes were embedded in WinForms sublayouts and were not exposed as a structured payload surface. Why it differs: The current head needs a stable, inspectable detail surface while still presenting a visually dense workbench.
- `SectionHostControl.NoticeBorder/NoticeText`: Routine command-dispatch notices are deliberately suppressed on the dense screenshot instead of occupying visible shell chrome. Legacy posture: Legacy utility noise typically stayed out of the central editing pane unless the user explicitly triggered an alert. Why it differs: A loud status banner would break the frmCareer-style serious-work posture more than any missing pixel-perfect border would.

### 06-dense-section-dark.png (`dense_section_dark`)

- Surface kind: frame
- Parity intent: Dark theme remains dense and readable without inheriting legacy dark-mode debt.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Avalonia/App.axaml`
- Observed theme: Dark
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_create_entry`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_create_entry`
- Observed tab labels: `Runner`
- Observed preview text: `Calendar
Entry 1: Downtime recon · +2 karma

Payload
{
  "section": "calendar",
  "diary": [
    { "title": "Downtime recon", "date": "2080-02-14", "karma": 2 }
  ]
}`
- Evidence anchors:
- Anchor: Theme_tokens_preserve_chummer5a_palette_and_readability() asserts ChummerShellActiveMenuBorderBrush and ChummerShellAccentButtonBrush equal #1C4A2D in light and dark palettes.
- Anchor: The same test asserts ChummerShellActiveMenuBackgroundBrush is #1C4A2D and ChummerShellActiveMenuBorderBrush is #90C39A in dark mode.
- Anchor: The dark-theme proof enforces >=12:1 foreground contrast and >=7:1 muted-foreground contrast on shell surfaces.
- Anchor: 06-dense-section-dark.png is captured from the same SectionRowsList workbench after ThemeVariant.Dark is applied.
- `App.axaml ChummerShellActiveMenuBorderBrush/ChummerShellAccentButtonBrush/ChummerShellActiveMenuBackgroundBrush`: The promoted shell uses explicit branded dark tokens such as #1C4A2D and #90C39A instead of whatever the host toolkit would otherwise default to. Legacy posture: Legacy dark mode was inconsistent and inherited WinForms-era palette constraints. Why it differs: Dark mode is allowed to improve materially because the parity contract is about familiar work rhythm, not preserving poor legacy color behavior.
- `App.axaml contrast budgets for ChummerShellForegroundBrush/MutedForegroundBrush/WarningBrush/DangerBrush`: The promoted head hard-gates foreground, muted, warning, and danger contrast ratios in dark mode before the screenshot pack is accepted. Legacy posture: The old client did not enforce hard contrast budgets across every dark surface. Why it differs: Readable dense work matters more than cloning legacy contrast mistakes.
- `Dark-mode SectionRowsList and shell panel chrome`: The same dense list/detail surface is recolored through shared shell brushes and panel classes rather than per-form overrides. Legacy posture: Dark workbench surfaces came from ad hoc panel fills and WinForms border behavior. Why it differs: The promoted desktop needs one consistent dark-theme system across all major surfaces.

### 07-loaded-runner-tabs-light.png (`loaded_runner_tabs`)

- Surface kind: frame
- Parity intent: Visible tab posture survives without reintroducing full MDI chrome.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/SummaryHeaderControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_create_entry`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_create_entry`
- Observed tab labels: `Runner`
- Observed preview text: `Calendar
Entry 1: Downtime recon · +2 karma

Payload
{
  "section": "calendar",
  "diary": [
    { "title": "Downtime recon", "date": "2080-02-14", "karma": 2 }
  ]
}`
- Evidence anchors:
- Anchor: Loaded_runner_preserves_visible_character_tab_posture() requires LoadedRunnerTabStripBorder to be visible and to surface a Runner tab label.
- Anchor: Loaded_runner_header_stays_tab_panel_only_without_metric_cards() forbids NameValueText, AliasValueText, KarmaValueText, SkillsValueText, RuntimeValueText, and RuntimeInspectButton.
- Anchor: Desktop_shell_preserves_classic_dense_center_first_workbench_posture() requires WorkspaceStripRegion to stay absent and LeftNavigatorRegion width to collapse.
- Anchor: SnapshotLoadedRunnerTabs() is expected to contain info/profile and gear landmarks on the loaded runner.
- `SummaryHeaderControl.LoadedRunnerTabStripBorder/LoadedRunnerTabStrip`: The promoted head distills that into a dedicated tab-strip card instead of an MDI-style shell or a second workspace row. Legacy posture: Legacy runner context was expressed through frmCareer-era tabs and main-form chrome. Why it differs: Visible runner tabs are required; full legacy window-management chrome is not.
- `WorkspaceStripRegion/MainWindow.LeftNavigatorRegion`: The promoted shell explicitly collapses workspace-strip and left-rail chrome when only one runner matters. Legacy posture: There was no separate modern workspace strip to keep alive for inactive sessions. Why it differs: The central workbench is more important than showcasing future multi-workspace affordances on this proof.
- `NameValueText/AliasValueText/KarmaValueText/SkillsValueText/RuntimeValueText/RuntimeInspectButton`: The promoted shell intentionally omits those card-style metrics from the header and leaves the header as tab chrome only. Legacy posture: Legacy dense runner work did not stop above the workbench to read hero cards or metric dashboards. Why it differs: Any card stack here would feel less like Chummer5a than the absence of old border styling.

### 08-cyberware-dialog-light.png (`cyberware_dialog`)

- Surface kind: dialog
- Parity intent: Cyberware selection keeps the familiar browse-detail-confirm posture while making implant costs and essence easier to audit.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/Selection Forms/SelectCyberware.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_cyberware_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_cyberware_add`
- Observed tab labels: `Runner`
- Observed preview text: `Cyberware
Runner Goal: Ready for a flagship shell smoke pass

Payload
{
  "section": "profile"
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs selects the row cyberware[0] = Wired Reflexes 2, confirms SectionPreviewBox contains essence 5.34, then clicks SectionQuickAction_cyberware_add.
- Anchor: DialogTitleText must equal Add Cyberware before 08-cyberware-dialog-light.png is captured.
- Anchor: DesktopDialogFactory.BuildCyberwareSelectionFields creates uiCyberwareCategoryTree, uiCyberwareCandidateList, uiCyberwareGrade, uiCyberwareEssence, uiCyberwareCost, uiCyberwareSource, and uiCyberwareSelectionDetails.
- Anchor: Cyberware dialogs expose add/add_more/cancel actions through BuildAddAndMoreActions().
- `SectionQuickAction_cyberware_add -> dialog.ui.cyberware_add`: The promoted shell launches cyberware selection through a named section quick action anchored directly to the dense workbench row context. Legacy posture: Classic cyberware work launched from dedicated selector forms and context menus tied to legacy control trees. Why it differs: Quick actions keep the same user intent visible while fitting the shared section host model.
- `DesktopDialogFactory.uiCyberwareCategoryTree/uiCyberwareCandidateList`: The current dialog uses an explicit Navigation tree and Available Cyberware list field model with predictable ids and layout slots. Legacy posture: Legacy dialogs relied on WinForms tree/list widgets and form-specific layout code for category browsing. Why it differs: The promoted shell needs selection dialogs that are both familiar to browse and mechanically testable across heads.
- `DesktopDialogFactory.uiCyberwareGrade/uiCyberwareSlot/uiCyberwareRating/uiCyberwareMarkup/uiCyberwareDiscount`: The current dialog exposes grade, slot, rating, markup, and discount in a single shared field stack around the selected implant. Legacy posture: Legacy implant modifiers and grade choices were spread across dialog-specific widgets that varied by selection form. Why it differs: The new field model makes the cost and essence implications easier to audit without hiding them behind form-specific control arrangements.
- `DesktopDialogFactory.uiCyberwareEssence/uiCyberwareCost/uiCyberwareSource/uiCyberwareSelectionDetails + add/add_more/cancel`: Essence, cost, source, and selection details remain visible beside a standardized add/add more/cancel action bar. Legacy posture: Older confirmation posture depended on bespoke totals areas and legacy button rows. Why it differs: The current desktop puts explainability and reversible action posture ahead of faithfully cloning the old layout skeleton.

### 09-vehicles-section-light.png (`vehicles_section`)

- Surface kind: frame
- Parity intent: Vehicles and drones stay dense, technical, and easy to re-find from the main workbench.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_vehicle_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_vehicle_add`
- Observed tab labels: `Runner`
- Observed selected rows: `vehicles[0] = Roadmaster · Armor 16 / Handling 3`
- Observed preview text: `Vehicles
Vehicle 1: Roadmaster · Armor 16 / Handling 3

Payload
{
  "section": "vehicles",
  "vehicles": [
    { "name": "Roadmaster", "handling": 3, "armor": 16 }
  ]
}`
- Evidence anchors:
- Anchor: Non_classic_sections_surface_a_named_workbench_context_instead_of_an_untitled_row_dump() requires SectionContextTitleText to equal Vehicles and SectionContextSummaryText to mention Roadmaster.
- Anchor: 09-vehicles-section-light.png is captured after selecting the row vehicles[0] = Roadmaster in SectionRowsList.
- Anchor: Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm() opens SectionQuickAction_vehicle_add with title Add Vehicle / Drone and field Vehicle.
- Anchor: DesktopDialogFactory.BuildVehicleSelectionFields creates uiVehicleCategoryTree, uiVehicleCandidateList, uiVehicleHandling, uiVehicleCost, uiVehicleSource, and uiVehicleSelectionDetails.
- `SectionHostControl.SectionContextTitleText/SectionContextSummaryText`: The promoted workbench adds an explicit Vehicles section header and summary line directly above the rows. Legacy posture: Vehicle work was often spread across dedicated forms or older nested layouts where the current context could be easy to lose. Why it differs: Modern section context cues improve re-findability without changing the underlying builder rhythm.
- `SectionHostControl.SectionRowsList row vehicles[0] = Roadmaster`: Vehicle entries are rendered as dense path/value rows inside the same workbench list that drives the rest of the character builder. Legacy posture: Legacy vehicle editors used form-specific grids and tab stacks for chassis state. Why it differs: Keeping vehicles inside the shared row model reduces shell switching while still exposing the same dense editing cadence.
- `SectionQuickAction_vehicle_add`: The promoted section exposes vehicle creation as a named quick action attached to the current section host instead of a detached form button. Legacy posture: Add Vehicle or Drone often launched from per-form buttons tied to legacy vehicle tabs. Why it differs: Quick actions make the launch point consistent across builder families.
- `DesktopDialogFactory.uiVehicleCategoryTree/uiVehicleCandidateList/uiVehicleHandling/uiVehicleSource`: The current dialog keeps the same browse/detail/confirm loop, but it expresses navigation, candidate list, handling, cost, and source in a shared dialog field model. Legacy posture: The legacy vehicle selector depended on form-specific tree and field arrangements. Why it differs: The shell can standardize the selection mechanics without erasing the vehicle-builder mental model.

### 10-contacts-section-light.png (`contacts_section`)

- Surface kind: frame
- Parity intent: Contacts stay first-class, contextual, and editable without sending the user to a separate dashboard.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_contact_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_contact_add`
- Observed tab labels: `Runner`
- Observed selected rows: `contacts[0] = Fixer (Loyalty 4 / Connection 5)`
- Observed preview text: `Contacts
Contact 1: Fixer (Loyalty 4 / Connection 5)

Payload
{
  "section": "contacts",
  "contacts": [
    { "name": "Fixer", "role": "Broker", "location": "Seattle", "connection": 5, "loyalty": 4 }
  ]
}`
- Evidence anchors:
- Anchor: 10-contacts-section-light.png is captured after SetActiveSectionForTesting("contacts") and selecting contacts[0] = Fixer in SectionRowsList.
- Anchor: SectionHostControl.axaml contains ContactGraphBorder, ContactNodeList, ContactFactionStatusBox, ContactHeatObligationBox, and ContactFavorRailBox for the contacts family.
- Anchor: Contacts_diary_and_support_routes_execute_with_public_path_visibility() opens contact_add with title Add Contact and required field Name.
- Anchor: DesktopDialogFactory.BuildContactAddFields creates uiContactName, uiContactRole, uiContactConnection, uiContactLoyalty, uiContactDetails, and uiContactNotes.
- `SectionHostControl.SectionRowsList row contacts[0] = Fixer`: The promoted shell keeps contacts in the same dense path/value row list used by other builder families, with the selected Fixer entry visible in-frame. Legacy posture: Legacy contacts were edited through older field groups and control clusters separate from the main workbench narrative. Why it differs: The common row model improves re-findability and keeps contacts from feeling like a detached side utility.
- `SectionHostControl.ContactGraphBorder/ContactNodeList/ContactFactionStatusBox/ContactHeatObligationBox/ContactFavorRailBox`: The promoted contacts lane can surface relationship graph, faction status, heat or obligation, and favor-rail context inside the same section host. Legacy posture: Legacy contact editing focused on direct form fields and less on adjacent relationship context surfaces. Why it differs: Contacts are more useful when the surrounding social context is visible where the user is already editing.
- `DesktopDialogFactory.uiContactName/uiContactRole/uiContactConnection/uiContactLoyalty`: The current add-contact dialog keeps the same essential Name/Role/Connection/Loyalty fields but expresses them through the shared dialog field system. Legacy posture: Add Contact relied on legacy dialog controls arranged specifically for the old form stack. Why it differs: The shell can modernize how the fields are hosted without changing what a veteran needs to fill in.
- `DesktopDialogFactory.uiContactDetails/uiContactNotes`: The promoted dialog pairs the edit fields with a Contact Details grid and Notes snippet so the selected social posture stays visible through confirmation. Legacy posture: Legacy contact dialogs offered less structured summary context around the selected role and connection state. Why it differs: Current parity prioritizes explicit context over reproducing the exact old arrangement of textboxes and labels.

### 11-diary-dialog-light.png (`diary_dialog`)

- Surface kind: dialog
- Parity intent: Diary and progression-entry work stays compact, list-oriented, and tied to the active runner.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/Utility Forms/frmExpense.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_create_entry`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_create_entry`
- Observed tab labels: `Runner`
- Observed preview text: `Karma Journal
Entry 1: First extraction · +2 karma

Payload
{
  "section": "progress",
  "diary": [
    { "title": "First extraction", "karma": 2 }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs captures 11-diary-dialog-light.png after clicking SectionQuickAction_create_entry in the progress section.
- Anchor: DialogTitleText must equal Add Entry and the required field label is Entry Title before the screenshot is accepted.
- Anchor: DesktopDialogFactory.BuildEntryEditorFields creates uiEntrySections, uiCreateEntryName, uiEntryDetails, and uiEntryNotes.
- Anchor: Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm() requires SectionPreviewBox to contain diary and karma markers.
- `SectionQuickAction_create_entry -> dialog.ui.create_entry`: The promoted head launches diary entry creation from the progress section's quick-action strip so the action starts in the same dense work context. Legacy posture: Legacy diary and expense flows opened through utility windows and per-form commands. Why it differs: The shared section host is the new center of gravity for editing, even when the underlying mental model remains diary-first.
- `DesktopDialogFactory.uiEntrySections/uiCreateEntryName`: The current dialog reduces that to an explicit Entry/Details/Notes section model plus a first-class Entry Title field. Legacy posture: The old flow used a standalone form with its own control arrangement for entry naming and details. Why it differs: The dialog field contract is reused across the desktop shell, so the diary surface inherits that structure.
- `DesktopDialogFactory.uiEntryDetails/uiEntryNotes`: The promoted dialog keeps a Details grid and Notes snippet visible while the user creates the entry. Legacy posture: Legacy entry utilities carried less structured inline explanation about current posture and list context. Why it differs: Current parity favors explicit context and reversibility during edits.
- `Dialog action ids add/add_more/cancel`: The diary flow uses the shared add/add more/cancel action trio instead of custom button wiring. Legacy posture: Classic confirmation posture was expressed through dialog-specific button rows. Why it differs: A common action contract makes shell behavior consistent across builder dialogs without changing the user's basic commit or cancel choices.

### 12-magic-dialog-light.png (`magic_dialog`)

- Surface kind: dialog
- Parity intent: Magic selection keeps obvious browse, inspect, and confirm landmarks while adapting to the current ruleset field model.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_spell_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_spell_add`
- Observed tab labels: `Runner`
- Observed preview text: `Spells
Spell 1: Stunbolt · Combat

Payload
{
  "section": "spells",
  "spells": [
    { "name": "Stunbolt", "category": "Combat", "drain": "F-3" }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs captures 12-magic-dialog-light.png after clicking SectionQuickAction_spell_add and waiting for DialogTitleText = Add Spell.
- Anchor: Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions() requires the field label Spell and the add action id.
- Anchor: DesktopDialogFactory.BuildSpellSelectionFields creates uiSpellCategoryTree, uiSpellCandidateList, uiSpellName, uiSpellSource, uiSpellSelectionDetails, and uiSpellNotes.
- Anchor: The same test family also exercises adept_power_add and initiation_add so the dialog contract is shared across magic-family routes.
- `SectionQuickAction_spell_add -> dialog.ui.spell_add`: The promoted shell launches Add Spell from a section quick action inside the dense workbench instead of a form-specific toolbar button. Legacy posture: Legacy Add Spell flowed from frmCareer buttons and WinForms selector forms. Why it differs: Magic actions are now routed through the shared section host so every builder family behaves consistently.
- `DesktopDialogFactory.uiSpellCategoryTree/uiSpellCandidateList`: The current dialog exposes an explicit spell category tree and Available Spells list with stable field ids and layout slots. Legacy posture: The old selector used legacy tree and list controls shaped by the WinForms form designer. Why it differs: The selection landmarks stay recognizable, but the structure must fit the shared dialog system.
- `DesktopDialogFactory.uiSpellName/uiSpellCategory/uiSpellSource/uiSpellSelectionDetails`: The promoted dialog keeps Spell, Category, Source, and Selection Details visible in a single normalized field model. Legacy posture: Legacy detail panels varied by form and did not share a common field contract across heads. Why it differs: Current parity values testable structured context more than exact legacy control placement.
- `Dialog action ids add/add_more/cancel`: Add Spell uses the same add/add more/cancel action bar as other builder dialogs, even though the field content is magic-specific. Legacy posture: Classic spell dialogs had bespoke OK-style button rows. Why it differs: The promoted shell standardizes action posture without erasing the spell-selection workflow.

### 13-matrix-dialog-light.png (`matrix_dialog`)

- Surface kind: dialog
- Parity intent: Matrix program selection stays technical and dense without leaving the main workbench shell model.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_matrix_program_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_matrix_program_add`
- Observed tab labels: `Runner`
- Observed preview text: `Aiprograms
Runner Goal: Ready for a flagship shell smoke pass

Payload
{
  "section": "profile"
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs captures 13-matrix-dialog-light.png after clicking SectionQuickAction_matrix_program_add and waiting for Add Program / Cyberdeck Item.
- Anchor: Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions() requires field label Program and action id add.
- Anchor: DesktopDialogFactory.BuildMatrixProgramSelectionFields creates uiMatrixProgramCategoryTree, uiMatrixProgramCandidateList, uiMatrixProgramName, uiMatrixProgramSlot, uiMatrixProgramSource, and uiMatrixProgramSelectionDetails.
- Anchor: The surrounding complexforms section also exposes complex_form_add, proving the matrix family shares one current shell lane rather than separate legacy forms.
- `SectionQuickAction_matrix_program_add -> dialog.ui.matrix_program_add`: The promoted shell exposes Add Program / Cyberdeck Item as a named quick action inside the current section host. Legacy posture: Legacy matrix editing used form-specific selectors and menu/button launch points tied to older control stacks. Why it differs: The workbench is now responsible for launching matrix flows instead of scattering them across detached selectors.
- `DesktopDialogFactory.uiMatrixProgramCategoryTree/uiMatrixProgramCandidateList`: The promoted dialog keeps the same distinction, but it expresses it through an explicit Navigation tree and Available Programs list. Legacy posture: Older dialogs used legacy selector widgets to split programs, deck items, and dongles. Why it differs: The shell can standardize the dialog framework while still preserving the technical browse posture.
- `DesktopDialogFactory.uiMatrixProgramName/uiMatrixProgramSlot/uiMatrixProgramSource/uiMatrixProgramSelectionDetails`: Program name, slot, source, and selection details remain visible as named fields inside the shared dialog contract. Legacy posture: Legacy detail panes were layout-specific and less uniform across matrix subflows. Why it differs: Matrix parity depends on visible slot and source context, not on keeping the old field arrangement byte-for-byte.
- `Dialog action ids add/add_more/cancel`: The promoted matrix dialog uses the same standardized add/add more/cancel action bar as the rest of the builder family. Legacy posture: Legacy matrix selectors used custom button rows and form-event wiring. Why it differs: Consistent action posture across builders is a deliberate modern shell choice.

### 14-advancement-dialog-light.png (`advancement_dialog`)

- Surface kind: dialog
- Parity intent: Advancement stays tied to visible karma history and still makes grade or reward choices explicit before commit.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCareer.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_ContentPresenter`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_ScrollViewer`, `PART_SelectedPipe`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionQuickAction_initiation_add`, `SectionQuickActionsBorder`, `SectionQuickActionsHost`, `SectionRegion`, `SectionRowsBorder`, `SectionRowsList`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`, `border`
- Observed quick actions: `SectionQuickAction_initiation_add`
- Observed tab labels: `Runner`
- Observed preview text: `Initiation & Submersion
Initiation Grade 1: Grade 1 · Metamagic

Payload
{
  "section": "initiationgrades",
  "grades": [
    { "grade": 1, "reward": "Metamagic" }
  ]
}`
- Evidence anchors:
- Anchor: Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm() requires progress[0] = First extraction · +2 karma in SectionRowsList and diary/karma markers in SectionPreviewBox.
- Anchor: AvaloniaFlagshipUiGateTests.cs captures 14-advancement-dialog-light.png after clicking SectionQuickAction_initiation_add and waiting for Add Initiation / Submersion.
- Anchor: The same test requires field label Grade and action id add.
- Anchor: DesktopDialogFactory.BuildInitiationSelectionFields creates uiInitiationTrack, uiInitiationGrade, uiInitiationCandidateList, uiInitiationReward, uiInitiationSelectionDetails, and uiInitiationNotes.
- `SectionRowsList progress[0] = First extraction · +2 karma / SectionPreviewBox diary+karma payload`: The promoted workbench keeps the current progression row and diary or karma payload visible directly in the progress section before the dialog opens. Legacy posture: Legacy advancement cues were distributed across expense, karma, and character forms. Why it differs: Advancement parity is stronger when the running history remains visible in the same lane as the next action.
- `SectionQuickAction_initiation_add -> dialog.ui.initiation_add`: The promoted head launches the initiation flow from the progress workbench through a named quick action. Legacy posture: Initiation and submersion choices launched from legacy buttons and selector forms tied to older section layouts. Why it differs: The quick-action model keeps advancement tools attached to the same dense editing lane.
- `DesktopDialogFactory.uiInitiationTrack/uiInitiationGrade/uiInitiationCandidateList/uiInitiationReward`: The current dialog exposes Track, Grade, Available Rewards, and Reward as normalized fields inside the shared dialog shell. Legacy posture: Legacy initiation dialogs used form-specific controls to mix track, grade, and reward selection. Why it differs: The shell now needs a stable field contract across advancement flows while preserving the same user decisions.
- `Dialog action ids add/add_more/cancel`: The promoted dialog uses the shared add/add more/cancel bar after keeping grade and metamagic or echo context visible. Legacy posture: Classic advancement flows depended on dialog-specific OK/Cancel rows. Why it differs: Standardized commit posture reduces shell variation without changing the core progression workflow.

### 15-creation-section-light.png (`creation_section`)

- Surface kind: frame
- Parity intent: Character creation still feels like a dense builder, not a wizard or marketing surface.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/CharacterCreate.Designer.cs`
- Current reference: `Chummer.Avalonia/Controls/SectionHostControl.axaml`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: Character_creation_preserves_familiar_dense_builder_rhythm() requires ClassicCharacterSheetBorder to be visible in the creation-like surface.
- Anchor: The same test requires SectionRowsList to contain attributes.body = 5, attributes.agility = 7, and skills.firearms[0] = Automatics 6.
- Anchor: SectionPreviewBox must contain attributes and combat markers before the screenshot is accepted.
- Anchor: 15-creation-section-light.png is captured after SetActiveSectionForTesting("attributes") on the loaded-runner workbench, not from a detached wizard window.
- `SectionHostControl.SectionRowsList rows attributes.body/attributes.agility/skills.firearms[0]`: The promoted builder renders creation facts as dense path/value rows inside the same workbench row list used elsewhere. Legacy posture: CharacterCreate used dedicated creation-form controls, pages, and widgets to expose attributes and skills. Why it differs: The dense builder feel survives even though the control implementation is unified with the rest of the current shell.
- `SectionHostControl.ClassicCharacterSheetBorder`: The promoted creation surface uses the same compact summary band to keep the character sheet posture visible while editing attributes. Legacy posture: The old creation experience kept a compact runner summary close to the editing surface rather than delegating it to an external dashboard. Why it differs: One summary-band implementation is cheaper to maintain and still gives the user the same dense builder cue.
- `SectionHostControl.SectionPreviewBox`: The current builder keeps a structured preview payload with attributes and combat markers mounted alongside the dense list. Legacy posture: Legacy creation panes expressed detail context through form-specific fields and labels. Why it differs: The preview surface makes the current builder auditable without forcing a separate wizard step.
- `Loaded-runner workbench hosting instead of a detached creation form`: The promoted head hosts creation-like editing inside the same dense workbench shell and does not bounce the user into a separate wizard frame for this proof. Legacy posture: Character creation lived in a separate dedicated WinForms experience. Why it differs: The modern shell is intentionally consolidated, but it must still feel like a serious creation builder.

### 16-master-index-dialog-light.png (`master_index_dialog`)

- Surface kind: dialog
- Parity intent: Master Index remains a first-class searchable utility with stronger source provenance than the legacy form.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/Utility Forms/MasterIndex.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs opens ToolsMenuButton -> master_index and captures the dialog after AssertDialogContainsAll(..., ["Master Index"]).
- Anchor: DesktopDialogFactory.BuildMasterIndexFields creates masterIndexSections, masterIndexSearch, masterIndexCatalogEntries, masterIndexDetails, masterIndexSnippetPreview, and masterIndexSelectedSource.
- Anchor: The same field builder also creates masterIndexReferenceCoverage, masterIndexReferenceSources, and masterIndexReferenceSourceReceipt.
- Anchor: The dialog closes through the shared close action instead of a standalone utility-form frame.
- `ToolsMenuButton -> master_index / DialogTitleText = Master Index`: The promoted shell preserves the same named utility route, but it opens inside the shared dialog host rather than a separate form window. Legacy posture: Master Index was a named WinForms utility surfaced from the legacy tools lane. Why it differs: The route must stay familiar even though utility hosting is now standardized.
- `DesktopDialogFactory.masterIndexSections/masterIndexSearch/masterIndexCatalogEntries`: The promoted dialog keeps Sections, Search, and Items explicit as named fields inside the shared dialog contract. Legacy posture: Legacy Master Index centered on a dedicated utility form with its own search and result widgets. Why it differs: The shared dialog model gives the current shell stable, testable field ids instead of form-specific widget wiring.
- `DesktopDialogFactory.masterIndexDetails/masterIndexSnippetPreview/masterIndexSelectedSource`: The promoted dialog keeps Details, Snippet Preview, and Source visible together so the selected sourcebook's current provenance is explicit. Legacy posture: Legacy Master Index focused on local reference navigation and did not consistently surface snippet provenance in one normalized panel. Why it differs: Reference truth is now a governed product contract, not just a convenience utility.
- `DesktopDialogFactory.masterIndexReferenceCoverage/masterIndexReferenceSources/masterIndexReferenceSourceReceipt`: The promoted dialog surfaces snippet coverage, reference-source posture, and a reference-source receipt as first-class fields. Legacy posture: The old Master Index did not expose modern receipt-style coverage and source-governance fields. Why it differs: The flagship desktop now has to prove source quality and provenance, not merely show that search results exist.

### 17-character-roster-dialog-light.png (`character_roster_dialog`)

- Surface kind: dialog
- Parity intent: Character Roster remains a named first-class utility for multi-runner context.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/Utility Forms/CharacterRoster.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs opens ToolsMenuButton -> character_roster and captures the dialog after AssertDialogContainsAll(..., ["Character Roster"]).
- Anchor: DesktopDialogFactory.BuildRosterFields creates rosterTree, rosterEntries, rosterSelectedRunner, rosterMugshot, rosterSelectedRunnerStatus, and rosterSelectedRunnerNotes.
- Anchor: The same builder also creates rosterOpenCount, rosterSavedCount, rosterRulesetMix, and rosterActiveWorkspace.
- Anchor: The roster dialog closes through the shared close action instead of a separate utility window frame.
- `ToolsMenuButton -> character_roster / DialogTitleText = Character Roster`: The promoted shell keeps the named Character Roster route but hosts it inside the shared dialog layer. Legacy posture: Character Roster was a standalone WinForms utility reachable from the legacy tools lane. Why it differs: The route stays veteran-familiar while the window-hosting model is intentionally modernized.
- `DesktopDialogFactory.rosterTree/rosterEntries`: The promoted dialog renders Characters and Roster Entries as explicit shared fields, including open-runner ordering and save posture markers. Legacy posture: Legacy roster views depended on utility-form trees and lists with form-specific layout rules. Why it differs: The field contract is reusable across heads and lets the shell verify multi-runner state directly.
- `DesktopDialogFactory.rosterSelectedRunner/rosterMugshot/rosterSelectedRunnerStatus/rosterSelectedRunnerNotes`: The current roster keeps selected-runner summary, mugshot placeholder, status snippet, and notes snippet visible in one standardized detail region. Legacy posture: The old roster centered on the runner list and supporting form fields without the same normalized summary model. Why it differs: Current roster parity needs stronger explicit context about the selected runner and active workspace.
- `DesktopDialogFactory.rosterOpenCount/rosterSavedCount/rosterRulesetMix/rosterActiveWorkspace`: The promoted roster makes open runner count, saved workspace count, ruleset mix, and active workspace explicit at the top of the dialog. Legacy posture: Legacy roster utilities did not expose modern session-level counts and ruleset-mix posture as first-class dialog fields. Why it differs: The current shell has to explain the session state around the roster, not only render the character list.

### 18-import-dialog-light.png (`import_dialog`)

- Surface kind: dialog
- Parity intent: Import still begins from File/Open posture while making current ruleset and payload expectations explicit.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/ChummerMainForm.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed menu commands: `exit`, `new_character`, `new_critter`, `open_character`, `open_for_export`, `open_for_printing`, `print_character`, `save_character`, `save_character_as`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs loads the demo runner, opens FileMenuButton, dispatches open_character, and then captures the dialog after AssertDialogContainsAll(..., ["Open Character"]).
- Anchor: DesktopDialogFactory.CreateOpenCharacterDialog creates importRulesetId and openCharacterXml fields with title Open Character and message Paste Chummer XML to import into a workspace.
- Anchor: The dialog exposes import/cancel actions through the shared dialog action model.
- Anchor: MainWindow.DesktopFileCoordinator.cs still carries the host-facing title Open Character File for the desktop file route.
- `FileMenuButton -> open_character after LoadDemoRunnerButton`: The promoted head keeps that exact shell entry, but it proves it through a runtime command id instead of a legacy form handler. Legacy posture: Veterans expect import to begin from the File/Open route on the main desktop shell. Why it differs: The command pipeline changed, but the user's first step is intentionally still File/Open.
- `DesktopDialogFactory.importRulesetId/openCharacterXml`: The promoted dialog makes Ruleset and Character XML explicit named fields inside the shell dialog model. Legacy posture: Legacy import flows relied on older file-picker or importer-specific forms without a normalized shared field contract. Why it differs: The current desktop needs a consistent import surface that can be audited and reused across related routes.
- `Dialog action ids import/cancel`: Import uses the shared dialog action model with an explicit import primary action and cancel secondary action. Legacy posture: Classic import dialogs used form-specific buttons and callback wiring. Why it differs: Unified action handling is a deliberate shell-level simplification, not a loss of import capability.
- `MainWindow.DesktopFileCoordinator 'Open Character File' host route`: The promoted shell keeps a host-facing Open Character File route while the in-shell dialog focuses on ruleset and payload review. Legacy posture: Legacy import blended desktop-file and importer behavior directly into WinForms main-form flows. Why it differs: Desktop file integration and in-shell import confirmation are intentionally separated in the current architecture.

### 38-translator-dialog-light.png (`translator_dialog`)

- Surface kind: dialog
- Parity intent: Translator remains a named localization workstation while the promoted shell makes lane posture and overlays explicit.
- Legacy reference: `/docker/chummer5a/Translator/TranslatorMain.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed menu commands: `exit`, `new_character`, `new_critter`, `open_character`, `open_for_export`, `open_for_printing`, `print_character`, `save_character`, `save_character_as`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs opens FileMenuButton -> translator and captures the dialog in Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture().
- Anchor: DesktopDialogFactory.BuildTranslatorFields creates translatorSearch, translatorLanePosture, translatorBridgePosture, translatorOverlayCount, and per-language langN fields.
- Anchor: The dialog title and message come from desktop.dialog.translator.title and desktop.dialog.translator.message so the promoted head stays localization-aware.
- Anchor: The translator dialog closes through the shared dialog host instead of a separate translator executable frame.
- `FileMenuButton -> translator / dialog.translator`: The promoted shell keeps Translator as a named runtime-backed route, but it opens inside the shared desktop dialog host. Legacy posture: Classic translator work lived in its own dedicated Translator tool window and project surface. Why it differs: The route stays veteran-recognizable while hosting is consolidated into one governed shell.
- `DesktopDialogFactory.translatorSearch/langN fields`: The promoted dialog exposes search plus a deterministic list of shipping or runtime languages as named shared fields. Legacy posture: The old translator centered on form-specific search and language lists with standalone utility wiring. Why it differs: Shared field ids make translator coverage auditable across heads without losing the recognizable language-workbench rhythm.
- `DesktopDialogFactory.translatorLanePosture/translatorBridgePosture/translatorOverlayCount`: The promoted shell shows translator lane, bridge posture, and enabled overlay count directly in the dialog. Legacy posture: Legacy translator tooling did not surface modern lane posture and overlay governance as first-class UI facts. Why it differs: Current parity has to prove localization readiness and overlay truth, not just that translation editing exists.

### 39-xml-editor-dialog-light.png (`xml_editor_dialog`)

- Surface kind: dialog
- Parity intent: XML amendment/editor posture stays available while the promoted shell makes bridge, overlay, and custom-data truth explicit.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/EditXmlData.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed menu commands: `exit`, `new_character`, `new_critter`, `open_character`, `open_for_export`, `open_for_printing`, `print_character`, `save_character`, `save_character_as`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs opens FileMenuButton -> xml_editor and captures the dialog in Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture().
- Anchor: DesktopDialogFactory.CreateCommandDialog("xml_editor", ...) creates xmlEditorLanePosture, xmlEditorOverlayCount, xmlEditorCustomDataLanePosture, xmlEditorCustomDataDirectoryCount, xmlEditorReceipt, and xmlEditorDialog.
- Anchor: The promoted dialog message explicitly states that edit/import flow is file-first while the XML bridge posture remains visible.
- Anchor: The dialog uses shared apply/cancel actions instead of a dedicated legacy amendment-editor window frame.
- `dialog.xml_editor title/message and apply/cancel action bar`: The promoted shell keeps XML editing as a named route inside the shared dialog host and standard action bar. Legacy posture: The classic XML Amendment Editor was a standalone power-user form with its own save/apply chrome. Why it differs: Window hosting is unified, but the XML-editing posture remains explicit and reachable.
- `DesktopDialogFactory.xmlEditorLanePosture/xmlEditorOverlayCount/xmlEditorCustomDataLanePosture/xmlEditorCustomDataDirectoryCount`: The promoted dialog surfaces bridge posture, overlay counts, and custom-data directory coverage before the user edits XML. Legacy posture: Legacy XML tooling focused on raw amendment authoring and did not foreground bridge posture or custom-data coverage counts. Why it differs: The current shell must explain whether the XML bridge is governed before parity claims can be trusted.
- `DesktopDialogFactory.xmlEditorReceipt/xmlEditorDialog`: The promoted dialog keeps a receipt field plus one shared XML field that exposes the current bridge preview payload. Legacy posture: The old editor showed base XML, amendment XML, and result XML through form-specific text areas. Why it differs: The promoted head optimizes for route proof and deterministic shell fields rather than mirroring every legacy textbox split exactly.

### 40-hero-lab-importer-dialog-light.png (`hero_lab_importer_dialog`)

- Surface kind: dialog
- Parity intent: Hero Lab import remains a named utility while the promoted shell exposes import-oracle coverage and adjacent SR6 proof.
- Legacy reference: `/docker/chummer5a/Chummer/Forms/Utility Forms/HeroLabImporter.Designer.cs`
- Current reference: `Chummer.Presentation/Overview/DesktopDialogFactory.cs`
- Observed theme: Light
- Observed named controls: `AttributeBaseEditor_AGI`, `AttributeBaseEditor_BOD`, `AttributeBaseEditor_REA`, `AttributeKarmaEditor_AGI`, `AttributeKarmaEditor_BOD`, `AttributeKarmaEditor_REA`, `AttributeParityEditorBorder`, `AttributeParityRow_AGI`, `AttributeParityRow_BOD`, `AttributeParityRow_REA`, `AttributeParityRowsHost`, `CenterShellRegion`, `CharacterRosterControl`, `CharacterStateText`, `ChevronPath`, `ClassicActionStrip`, `ClassicMenuBarControl`, `ClassicStatusStripControl`, `ClassicToolStripControl`, `CloseWorkspaceButton`, `ComplianceStateText`, `ContentRegion`, `CopyButton`, `DeterminateRoot`, `EditMenuButton`, `ExpandCollapseChevron`, `ExpandCollapseChevronBorder`, `FileMenuButton`, `HelpMenuButton`, `HorizonsButton`, `ImportFileButton`, `IndeterminateRoot`, `LoadedRunnerTabStrip`, `LoadedRunnerTabStripBorder`, `MenuBarPanel`, `MenuBarRegion`, `OpenForExportButton`, `OpenForPrintingButton`, `PART_BorderElement`, `PART_ContentPresenter`, `PART_DecreaseButton`, `PART_ExpandCollapseChevron`, `PART_ExpandCollapseChevronContainer`, `PART_Header`, `PART_HeaderPresenter`, `PART_IncreaseButton`, `PART_Indicator`, `PART_ItemsPresenter`, `PART_LayoutRoot`, `PART_Popup`, `PART_SelectedPipe`, `PART_Spinner`, `PART_SpinnerPanel`, `PART_TextBox`, `PART_TextPresenter`, `PART_TransparencyFallback`, `PrintButton`, `ProgressBarRoot`, `RosterPaneRegion`, `RosterTree`, `RuleEnvironmentStudioButton`, `SaveButton`, `SectionActionTabStrip`, `SectionActionTabStripBorder`, `SectionContextBorder`, `SectionContextSummaryText`, `SectionContextTitleText`, `SectionHostControl`, `SectionRegion`, `ServiceStateText`, `SettingsButton`, `SpecialMenuButton`, `StatusStripRegion`, `StatusText`, `StatusTextBorder`, `TimeStateText`, `ToolStripRegion`, `ToolsMenuButton`, `WindowsMenuButton`, `WorkbenchProgressBar`
- Observed menu commands: `exit`, `new_character`, `new_critter`, `open_character`, `open_for_export`, `open_for_printing`, `print_character`, `save_character`, `save_character_as`
- Observed tab labels: `Runner`
- Observed preview text: `Attributes
Attribute 1: Body 4
Attribute 2: Agility 5
Attribute 3: Reaction 5

Payload
{
  "sectionId": "attributes",
  "attributes": [
    {
      "name": "Body",
      "baseValue": 3,
      "karmaValue": 1,
      "totalValue": 4,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    },
    {
      "name": "Agility",
      "baseValue": 5,
      "karmaValue": 0,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 4,
      "baseUnlocked": true
    },
    {
      "name": "Reaction",
      "baseValue": 4,
      "karmaValue": 1,
      "totalValue": 5,
      "metatypeMin": 1,
      "metatypeMax": 6,
      "metatypeAugMax": 9,
      "priorityMaximum": 6,
      "karmaMaximum": 5,
      "baseUnlocked": true
    }
  ]
}`
- Evidence anchors:
- Anchor: AvaloniaFlagshipUiGateTests.cs opens FileMenuButton -> hero_lab_importer and captures the dialog in Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture().
- Anchor: DesktopDialogFactory.CreateCommandDialog("hero_lab_importer", ...) creates heroLabSource, importRulesetId, heroLabImportOracleLanePosture, heroLabImportOracleCoverage, heroLabFixtureCount, heroLabImportOracleMatrix, heroLabImportOracleReceipt, heroLabAdjacentSr6OracleReceipt, and heroLabXml.
- Anchor: The dialog summary text reports import-oracle posture, coverage counts, and adjacent SR6 oracle posture when master-index data is available.
- Anchor: The importer uses shared import/cancel actions instead of a separate Hero Lab utility form frame.
- `FileMenuButton -> hero_lab_importer / dialog.hero_lab_importer`: The promoted shell keeps Hero Lab Importer as a named route, but hosts it in the shared dialog layer. Legacy posture: Classic Hero Lab import opened as a dedicated importer utility from the desktop toolset. Why it differs: The route remains familiar while the shell standardizes utility hosting.
- `DesktopDialogFactory.heroLabSource/importRulesetId/heroLabXml`: The promoted dialog keeps source, ruleset, and XML payload fields explicit inside the shared dialog contract. Legacy posture: The legacy importer focused on source file selection and payload-specific form widgets. Why it differs: The shared shell needs deterministic field ids for import proof and auditability.
- `DesktopDialogFactory.heroLabImportOracleLanePosture/heroLabImportOracleCoverage/heroLabImportOracleMatrix/heroLabImportOracleReceipt/heroLabAdjacentSr6OracleReceipt`: The promoted shell surfaces import-oracle posture, coverage, receipt text, and adjacent SR6 oracle evidence directly in the dialog. Legacy posture: The old importer did not publish import-oracle coverage, matrix, or adjacent SR6 proof as first-class form facts. Why it differs: Current parity has to prove governed import truth instead of merely exposing an importer entry point.

