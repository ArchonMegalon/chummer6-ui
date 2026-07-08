# Workbench Session Handoff

Last updated: 2026-07-08T03:29:47+02:00

## Cross-Codex Refresh (2026-07-08T03:29:47+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is `Handoff refresh (2026-07-08T03:08:58+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:release_ready`
- Do not claim stable, release-ready, or flagship-ready while `release_posture:non_flagship_channel` remains.
- SR6 shell parity slice completed on this lane:
  - aligned `WorkflowParityGateTests` message assertions to the current Origin Dossier wizard/build copy updates (`Pick only the basics, then build the story...`, `Read this first...`), keeping parity checks in `Runtime_backed_origin_dossier_story_first_flow_materializes_gm_constraints_before_sr4_bp_build` focused on behavior instead of obsolete wording.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
      - result: clean
  - build:
    - `dotnet build Chummer.Tests/Chummer.Tests.csproj`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:27.01`
  - focused tests:
    - `dotnet exec Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Runtime_backed_origin_dossier_story_first_flow_materializes_gm_constraints_before_sr4_bp_build"`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 395ms`
    - `dotnet exec Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Origin_"`
      - result: `72 total`, `72 succeeded`, `0 failed`, `0 skipped`
      - duration: `2m 46s 703ms`
- Scope note:
  - this repo slice remains focused on SR6 origin/dialog parity and does not alter publish-lane portability controls in this cycle. External blocker posture and publish truth remain controlled by `chummer.run-services/NEXT_SESSION_HANDOFF.md` and `RELEASE_BLOCKERS.generated.json`.

## Cross-Codex Refresh (2026-07-08T03:17:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- keep SR6 shell parity for stale one-line origin book-preview titles:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so `BuildOriginBookPreviewDisplayValue(...)` now canonicalizes stale `"OldAlias: Origin Dossier"` titles to the current `newCharacterWorkflowAlias`-derived form when the body payload is title-only or title-first.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` with the same single-line title recovery behavior so standalone Origin Dossier build surfaces follow the same canonical identity treatment as Blazor.
  - added `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` coverage for a stale one-line book-preview title (`Runner: Origin Dossier`) normalizing to `Dossier: Origin Dossier` in browser shell.
  - added `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` coverage for the same stale one-line normalization in the standalone shell surface.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
      - result: clean
  - build:
    - `dotnet build Chummer.Tests/Chummer.Tests.csproj`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:19.11`
  - focused tests:
    - `dotnet exec Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~DialogHost_origin_build_recovers_stale_one_line_book_preview_title"`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 706ms`
    - `dotnet exec Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Standalone_origin_build_recovers_stale_one_line_book_preview_title"`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 806ms`
    - `dotnet exec Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~DialogHost_origin_build_recovers_preview_title_and_constraints_route_when_hidden_values_are_stale|FullyQualifiedName~Standalone_origin_build_recovers_preview_title_and_constraints_route_when_hidden_values_are_stale|DialogHost_origin_build_recovers_stale_one_line_book_preview_title|Standalone_origin_build_recovers_stale_one_line_book_preview_title"`
      - result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`
      - duration: `5s 588ms`
- Scope note:
  - this slice completes the one-line stale title recovery path for Origin Dossier book-preview display in Blazor and Avalonia only. Publish-lane state, blocker handling, and release-posture assertions remain unchanged and unresolved until external evidence is delivered.

## Cross-Codex Refresh (2026-07-08T03:03:01+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin build book-preview body recovery advanced again in the SR6 parity lane:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so `BuildOriginBookPreviewDisplayValue(...)` now reconstructs a full fallback preview body from `newCharacterOriginSummary`, `newCharacterOriginImplications` build/GM lines, and fallback GM requirements from `newCharacterOriginBuildLogic` when hidden preview text is blank or title-only.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` with the same fallback reconstruction for standalone Origin Dossier build rendering.
  - added `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` coverage asserting blank hidden book-preview fields still render summary, build-shape, GM constraints, and canonical closing sentence in browser shell.
  - added `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` parity runtime proof for the standalone shell.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` to assert the Avalonia origin build helper shape and structured-value extraction for GM/build lines.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:32.41`
  - focused presentation proofs:
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_build_recovers_book_preview_body_when_hidden_preview_is_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 895ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_build_recovers_book_preview_body_when_hidden_preview_is_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 390ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `800ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier book-preview display recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T02:53:37+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin dossier-link notes recovery advanced again in the SR6 parity lane:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` with `BuildOriginDossierLinkNotesDisplayValue()` so the canonical Origin Dossier link-note copy is owned in one place instead of duplicated across shells.
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier build shell now normalizes the visible `newCharacterOriginDossierLinkNotes` field back to the canonical note text instead of trusting stale hidden copy.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier build shell applies the same display-side notes normalization before rendering the snippet field.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a focused proof that stale `Chummer Online` note copy still displays the clean Origin Dossier note text in the browser shell.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with the same stale-notes runtime proof for the Avalonia standalone build surface.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now requires `displayDossierLinkNotesField` normalization and the factory-backed notes helper path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:07:47.01`
  - focused presentation proofs:
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_build_recovers_dossier_link_notes_when_hidden_notes_are_stale" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 424ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_build_recovers_dossier_link_notes_when_hidden_notes_are_stale" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 824ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 408ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier dossier-link-note display recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T02:42:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin build-story display recovery advanced again in the SR6 parity lane:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier build shell now normalizes the visible `newCharacterOriginStory` panel from the hidden `newCharacterOriginSummary` field when the story field arrives blank.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier build shell applies the same display-side story fallback before rendering the narrative panel.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a focused proof that a blank hidden build-story field still renders the original story text in the browser shell.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with the same blank-story-field runtime proof for the Avalonia standalone build surface.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now requires `displayStoryField` normalization plus the dedicated `BuildOriginStoryDisplayValue(...)` helper path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:09:01.44`
  - focused presentation proofs:
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_build_recovers_story_panel_when_hidden_story_is_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 890ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_build_recovers_story_panel_when_hidden_story_is_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 182ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 699ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier build-story display recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T02:29:27+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin wizard display recovery advanced again in the SR6 parity lane:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` with `NormalizeOriginWizardDialogForDisplay(...)` / `NormalizeOriginWizardFieldsForDisplay(...)` so the UI heads can rebuild origin-wizard hidden display fields from the current raw origin inputs instead of trusting stale or blank summary payloads.
  - updated `Chummer.Presentation/AssemblyInfo.cs` so `Chummer.Blazor` can consume the same presentation-internal origin-wizard display projection already available to Avalonia and tests, without widening the helper to a public API.
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier wizard now renders its visible summary strips and story preview from the normalized display dialog, while still using the raw dialog for interactive input controls.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier wizard now renders its summary strips and story preview from normalized display fields instead of mixing raw select tokens or trusting blank hidden display values.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a focused proof that blank hidden wizard summary/preview fields still render the original visible metatype, archetype, path, GM summary, pressure, and story preview in the browser shell.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with the same blank-hidden-display-field runtime proof for the Avalonia standalone wizard surface.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now requires the factory-backed wizard display projection and the normalized summary/story-preview field path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/AssemblyInfo.cs Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:12:45.63`
  - focused presentation proofs:
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_wizard_recovers_summary_and_story_preview_when_hidden_display_fields_are_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 398ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_wizard_recovers_summary_and_story_preview_when_hidden_display_fields_are_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 932ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 708ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier wizard display recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T02:08:23+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin build-shell summary recovery advanced again in the SR6 parity lane:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier build shell now recovers the visible `Ruleset` summary from the hidden dossier route and the visible `Method` summary from the build-translation grid when `newCharacterWorkflowRulesetId` or `newCharacterWorkflowBuildMethod` arrive blank.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier build shell applies the same display-side recovery for the summary strip and the visible route field instead of drifting to default `SR5` / `Pending`.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a focused proof that blank workflow summary fields still display the original SR4 route plus the recovered `SR4` and `BP` summary values in the browser shell.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with the same blank-workflow-field runtime proof for the Avalonia standalone build surface.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now requires `GetOriginRulesetLabelForDisplay(...)`, `GetOriginBuildMethodForDisplay(...)`, and the helper paths that mine the hidden route/build-logic fields.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:34.04`
  - focused presentation proofs:
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_build_recovers_summary_ruleset_and_method_when_workflow_fields_are_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 611ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_build_recovers_summary_ruleset_and_method_when_workflow_fields_are_blank" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 842ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 702ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier build-summary recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T02:00:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin build-shell display hardening advanced again in the SR6 parity lane:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier build shell now normalizes the visible `newCharacterOriginBookPreview` title paragraph and the visible `newCharacterOriginImplications` dossier-link line from current workflow alias/ruleset state instead of trusting stale hidden text.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier build shell applies the same display-side normalization before rendering the book preview and constraints surfaces.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a focused proof that stale hidden origin values now still render `Dossier: Origin Dossier` plus the clean `/app?command=new_character_origin&ruleset=sr4&alias=Dossier` constraints route in the browser shell.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with the same stale-value runtime proof for the Avalonia standalone build surface.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now requires `displayBookField` / `displayImplicationsField` normalization and the dedicated display-value helper paths.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:44.94`
  - focused presentation proofs:
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_build_recovers_preview_title_and_constraints_route_when_hidden_values_are_stale" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 382ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_build_recovers_preview_title_and_constraints_route_when_hidden_values_are_stale" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 302ms`
    - `./Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal --no-progress`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 518ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier build-surface display recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T01:47:50+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin shell-notice route recovery advanced again in the coordinator lane:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `BuildOriginDossierOnlineRoute(...)` is now callable inside the presentation assembly, allowing other origin paths to rebuild the clean route from current ruleset/alias state instead of trusting stale hidden payloads.
  - updated `Chummer.Presentation/Overview/DialogCoordinator.cs` so `show_origin_dossier_link` now rebuilds the notice route from `newCharacterWorkflowRulesetId` plus the current workflow/identity alias and no longer falls back to the generic `/app?command=new_character_origin` link when the hidden route field is blank.
  - extended `Chummer.Tests/Presentation/DialogCoordinatorTests.cs` with a focused proof that a blank hidden route plus stale `Runner` alias still publishes the clean `/app?command=new_character_origin&ruleset=sr4&alias=Dossier` notice route.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Presentation/Overview/DialogCoordinator.cs Chummer.Tests/Presentation/DialogCoordinatorTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:48.31`
  - focused presentation proofs:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_origin_wizard_generates_alice_build_translation_and_handoff" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 598ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_show_origin_dossier_link_rebuilds_clean_route_when_hidden_link_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 697ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_open_origin_guided_chargen_restores_origin_marker_when_hidden_seed_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 525ms`
- Scope note:
  - this slice hardens only the Origin Dossier coordinator notice-route recovery path on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T01:42:13+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin route recovery hardened again in the specialized shell lane:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier build shell now rebuilds the visible `newCharacterOriginDossierLink` route from the current ruleset and normalized dossier alias instead of trusting a blank hidden URL field.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier build shell now applies the same display-side route recovery before rendering the read-only origin link field.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a direct rendering proof that a blank hidden origin-route field plus stale `Runner` alias still displays the clean `/app?command=new_character_origin&ruleset=sr4&alias=Dossier` route.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with a standalone runtime proof that the same blank-link/stale-alias build state still displays the clean recovered route in the Avalonia origin build surface.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now requires the `displayDossierLinkField` normalization path plus the dedicated `BuildOriginDossierDisplayRoute(...)` helper.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
      - result: clean
  - verification builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:02.13`
  - focused presentation proofs:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_specialized_shells_fail_closed_to_dossier_identity_when_hidden_values_are_stale" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `5s 102ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_build_recovers_clean_route_when_hidden_link_value_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `4s 503ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 601ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_wizard_fail_closes_identity_fields_to_dossier_defaults_when_hidden_values_are_stale" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `6s 418ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_build_recovers_clean_route_when_hidden_link_value_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `5s 705ms`
- Scope note:
  - this slice hardens only specialized Origin Dossier route recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T01:34:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin shell fail-closed display parity moved forward again in the current tree:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the specialized Origin Dossier browser shell now normalizes hidden `newCharacterName` / `newCharacterAlias` / `newCharacterWorkflowAlias` identity values for display, restoring `New dossier` / `Dossier` when stale origin state arrives blank or regresses to `New runner` / `Runner`.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the standalone Origin Dossier shell now applies the same display-side normalization in the advanced wizard identity group and the build-handoff summary strip, instead of trusting raw hidden identity values verbatim.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with a direct rendering proof that the specialized browser shell still displays dossier-facing identity when hidden origin identity values are blank or stale.
  - extended `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs` with a standalone runtime proof that the advanced Origin Dossier identity fields still display `New dossier` / `Dossier` when stale hidden values arrive blank or runner-seeded.
  - refreshed `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-shell owner now fail-closes regressions back to raw `nameField` / `aliasField` display wiring and requires the renderer-local identity normalization helper path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
      - result: clean
  - verification builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:47.68`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:10.24`
  - focused presentation proofs:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `4s 228ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_origin_specialized_shells_fail_closed_to_dossier_identity_when_hidden_values_are_stale" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `4s 328ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `2s 308ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Standalone_origin_wizard_fail_closes_identity_fields_to_dossier_defaults_when_hidden_values_are_stale" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 657ms`
- Scope note:
  - this slice hardens only renderer-local Origin Dossier identity display recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T01:21:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins fail-closed identity hardening advanced again across the continuation-builder and coordinator seams:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the shared workflow identity fallback now lives in `ResolveWorkflowIdentityName(...)` / `ResolveWorkflowIdentityAlias(...)`, and both private workflow builders now apply origin-aware `New dossier` / `Dossier` defaults themselves instead of relying only on `BuildNewCharacterContinuationDialog(...)` to normalize blank origin identity first.
  - updated `Chummer.Presentation/Overview/DialogCoordinator.cs` so `open_origin_guided_chargen` now restores `approved_origin_story` when the hidden `newCharacterOriginAliceSeedSource` marker arrives blank or whitespace, instead of only when it is `null`.
  - extended `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` with direct reflection-based proofs for both private workflow builders so origin-tagged blank identity no longer fails open to `New runner` / `Runner` if a future caller bypasses the continuation dispatcher.
  - extended `Chummer.Tests/Presentation/DialogCoordinatorTests.cs` with an end-to-end proof that an origin build dialog with blank hidden seed marker plus blank workflow identity still opens the guided workflow with `approved_origin_story` and dossier-facing defaults.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
      - result: clean
    - `git diff --check -- Chummer.Presentation/Overview/DialogCoordinator.cs Chummer.Tests/Presentation/DialogCoordinatorTests.cs`
      - result: clean
  - verification builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:15.74`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:25.21`
  - focused presentation proofs:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterContinuationDialog_origin_source_restores_dossier_defaults_when_identity_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 722ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterPriorityWorkflowDialog_origin_source_restores_dossier_defaults_when_identity_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 898ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterKarmaWorkflowDialog_origin_source_restores_dossier_defaults_when_identity_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 701ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_open_origin_guided_chargen_restores_origin_marker_when_hidden_seed_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 094ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_complete_new_character_workflow_origin_continuation_restores_dossier_defaults_when_identity_fields_are_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 014ms`
- Scope note:
  - this slice hardens only Origin Dossier workflow identity and hidden origin-marker recovery on the current tree. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T01:07:46+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins continuation-factory hardening moved forward again in the dialog-state lane:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the continuation-dialog factory now normalizes blank workflow name/alias values to `New dossier` / `Dossier` when `workflowOriginSource` is `approved_origin_story`, instead of seeding a blank-origin continuation path with the generic `New runner` / `Runner` defaults.
  - extended `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` with a focused proof that an origin-sourced continuation dialog built from blank identity inputs now keeps dossier-facing defaults and preserves the hidden `newCharacterWorkflowOriginSource` marker.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:13.08`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterContinuationDialog_uses_priority_route_for_priority_tables" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `967ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterContinuationDialog_origin_source_restores_dossier_defaults_when_identity_is_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `819ms`
- Scope note:
  - this slice hardens only the Origin Dossier continuation-dialog factory defaults for origin-sourced blank identity inputs. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T01:04:08+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins continuation-path hardening moved forward again in the dialog/coordinator lane:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so continuation dialogs now carry a hidden `newCharacterWorkflowOriginSource` context field, allowing origin-sourced guided chargen to preserve its dossier identity semantics through the later workflow-completion step.
  - updated `Chummer.Presentation/Overview/DialogCoordinator.cs` so `open_origin_guided_chargen` forwards the current origin source marker into the continuation dialog, and `complete_new_character_workflow` now restores `New dossier` / `Dossier` defaults instead of silently falling back to generic runner identity when an origin-sourced continuation dialog arrives with blank workflow name or alias values.
  - extended `Chummer.Tests/Presentation/DialogCoordinatorTests.cs` with an end-to-end proof that an origin-sourced continuation dialog with blank workflow identity fields still imports dossier-facing XML and notice text rather than regressing to `Runner`.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Presentation/Overview/DialogCoordinator.cs Chummer.Tests/Presentation/DialogCoordinatorTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:05:13.05`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_complete_new_character_workflow_imports_workspace_and_closes_dialog_on_success" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 315ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_complete_new_character_workflow_origin_continuation_restores_dossier_defaults_when_identity_fields_are_blank" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 301ms`
- Scope note:
  - this slice hardens only the Origin Dossier continuation/completion identity fallback path. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:54:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins browser-route parity moved forward again in the SSR/public-edge lane:
  - updated `Chummer.Blazor/Components/App.razor` so both the visible SSR Origin Dossier fallback detail block and the underlying `WorkbenchFallbackDialog("Origin Dossier", ...)` seed now use the current story-first copy, `Pick only the basics, then build the story. Advanced controls are optional.`
  - updated `scripts/e2e-public-edge-playwright.cjs` so the hosted origin-wizard audit now expects that same current story-first copy instead of the older pre-polish wording.
  - extended the existing route/contract owners in `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` and `tests/test_blazor_public_edge_execution_contract.py` so regressions back to the older Origin wizard fallback wording are now caught both in the SSR route payload and in the public-edge Playwright contract.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/App.razor scripts/e2e-public-edge-playwright.cjs Chummer.Tests/Presentation/AppRouteSurfaceTests.cs tests/test_blazor_public_edge_execution_contract.py`
      - result: clean
  - focused Python contract proof:
    - `python3 -m pytest -q tests/test_blazor_public_edge_execution_contract.py -k 'blazor_route_host_uses_fast_no_prerender_with_visible_workbench_fallback or origin_wizard_copy_matches_origin_shell_parity'`
      - result: `2 passed, 9 deselected in 0.06s`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:49.84`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_route_renders_ssr_fallback_shell_and_bootstrap_script" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 699ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_origin_dossier_query_builds_story_first_fallback_dialog" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `416ms`
- Scope note:
  - this slice hardens only the Origin Dossier SSR/public-edge fallback copy parity and its public-edge probe contract. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:50:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins dialog/UI parity moved forward again in the underlying dialog-state lane:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `BuildNewCharacterOriginWizardDialog(...)` now uses the same story-first lead as the specialized Origin wizard shell, `Pick only the basics, then build the story. Advanced controls are optional.`
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `BuildNewCharacterOriginBuildDialog(...)` now uses the same book-preview lead as the specialized Origin build shell, `Read this first. Character creation starts after the story feels right.`
  - extended the existing runtime owner `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the Origin wizard/build dialog states now fail closed on regressions back to the older pre-polish message copy even if a future consumer renders the dialog message outside the current specialized shell path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:38.18`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginWizardDialog_materializes_origin_seed_and_recommendation_fields" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 084ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginBuildDialog_translates_origin_into_alice_guided_build_summary" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `935ms`
- Scope note:
  - this slice hardens only the Origin Dossier dialog-state message parity between the factory and the specialized shells. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:46:01+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins dialog/UI polish moved forward again in the desktop/browser shell lane:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the shared top-level dialog banner no longer renders for the specialized Origin Dossier wizard/build shells; the browser now relies on the dedicated Origin shell panels instead of stacking the generic dialog message above them.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the Avalonia dialog banner suppression list now also covers `dialog.new_character.origin_wizard` and `dialog.new_character.origin_build`, keeping the desktop shell aligned with the browser on the same clutter-reduction rule.
  - extended the existing owners in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` and `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so regressions that reintroduce the generic Origin dialog banner are now caught in both the browser render path and the Avalonia shell source contract.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:10.47`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 744ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 034ms`
- Scope note:
  - this slice hardens only the specialized Origin Dossier banner-suppression rule across browser and Avalonia shells. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:39:21+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins dialog/UI parity moved forward again in the desktop/browser shell lane:
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the Avalonia Origin Dossier advanced-story-controls section now carries the same shared browser note, `Optional dossier identity, life-path steering, and GM guidance for the story packet.`
  - removed the older per-subpanel advanced-copy leads from the Avalonia `Dossier`, `Life Path`, and `GM Steering` groups so the desktop specialized Origin shell stays closer to the current browser surface instead of repeating a noisier legacy explanation block.
  - extended the existing owner `Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet` in `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so it now fail-closes regressions back to the older advanced-story-controls lead copy while still enforcing the current Origin Dossier terminology and route-copy truth inside the specialized Avalonia shell source.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:58.80`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `397ms`
- Scope note:
  - this slice hardens the Avalonia/browser advanced-story-controls copy parity for the specialized Origin Dossier shell only. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:31:26+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins dialog/UI parity moved forward again in the desktop/browser shell lane:
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the Origin Dossier advanced-story-controls identity subpanel now uses `Dossier` instead of `Runner`, matching the browser-side Origin Dossier shell and the existing browser owner expectations for the advanced subpanel headings.
  - extended the existing owner `Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet` in `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so it now fail-closes regressions back to `CreateLegacyFieldGroup(... "Runner" ...)` inside the specialized Avalonia Origin Dossier shell source.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:42.10`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `891ms`
- Scope note:
  - this slice hardens the Avalonia/browser terminology parity for the Origin Dossier advanced story-controls identity subpanel. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:27:51+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origins dialog/UI parity moved forward in the desktop/browser shell lane:
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the Origin Dossier build-handoff summary strip now labels the first summary slot as `Dossier` instead of `Runner`, matching the browser-side Origin Dossier surface and the current dossier-facing terminology already enforced in the dialog factory and Blazor shell tests.
  - refreshed the existing owner `Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet` in `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so it now fail-closes regressions back to `(\"Runner\", aliasField.Value)` inside the specialized Avalonia Origin Dossier shell source.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:48.63`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `781ms`
- Scope note:
  - this slice hardens one concrete Avalonia/browser Origin Dossier terminology mismatch in the specialized build-handoff shell. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:20:58+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Local Codex/day1 wrapper portability moved forward again in the repo tooling lane:
  - updated `scripts/ai/run_codex.sh`, `scripts/ai/run_codex_resume.sh`, `scripts/ai/day1-clean-artifacts.sh`, `scripts/ai/day1-all-milestones.sh`, `scripts/ai/day1-p1-run.sh`, `scripts/ai/day1-p1-loop.sh`, and `scripts/ai/day1-p1-setup.sh` so their local sibling-script resolution now uses a physical `SCRIPT_DIR` via `pwd -P` before sourcing `_env.sh`.
  - updated `scripts/ai/milestones/b11-npc-persona-studio-check.sh` so its local script-dir header is also physical before deriving its alias-safe repo root.
  - refreshed the existing Python owners in `tests/test_desktop_downloads_local_release_policy.py` so the Codex/day1/shared-env wrapper cluster now fails closed on regressions back to the older non-physical `SCRIPT_DIR` header.
  - refreshed the existing C# owner `Npc_persona_studio_milestone_guard_uses_alias_safe_repo_root_resolution` in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so it now also fail-closes regressions back to the older non-physical `SCRIPT_DIR` header for the NPC Persona Studio milestone guard.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/run_codex.sh scripts/ai/run_codex_resume.sh scripts/ai/day1-clean-artifacts.sh scripts/ai/day1-all-milestones.sh scripts/ai/day1-p1-run.sh scripts/ai/day1-p1-loop.sh scripts/ai/day1-p1-setup.sh scripts/ai/milestones/b11-npc-persona-studio-check.sh tests/test_desktop_downloads_local_release_policy.py Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/run_codex.sh`
      - result: clean
    - `bash -n scripts/ai/run_codex_resume.sh`
      - result: clean
    - `bash -n scripts/ai/day1-clean-artifacts.sh`
      - result: clean
    - `bash -n scripts/ai/day1-all-milestones.sh`
      - result: clean
    - `bash -n scripts/ai/day1-p1-run.sh`
      - result: clean
    - `bash -n scripts/ai/day1-p1-loop.sh`
      - result: clean
    - `bash -n scripts/ai/day1-p1-setup.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/b11-npc-persona-studio-check.sh`
      - result: clean
  - focused Python owner proof:
    - `python3 -m pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'codex_wrappers_resolve_repo_root_from_script_location or day1_wrappers_resolve_repo_root_from_shared_env_contract or shared_env_utility_wrappers_use_script_dir_env_contract or day1_setup_avoids_bash4_collectors_and_associative_arrays'`
      - result: `4 passed, 32 deselected in 0.17s`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:30.15`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Npc_persona_studio_milestone_guard_uses_alias_safe_repo_root_resolution" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `790ms`
- Scope note:
  - this slice hardens only the local Codex/day1 wrapper cluster plus the NPC Persona Studio milestone guard and their owner-backed portability assertions. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:17:47+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Local package-plane helper portability moved forward in the repo tooling lane:
  - updated `scripts/ai/_env.sh` to resolve its own location physically with `pwd -P` before deriving `REPO_ROOT_PHYSICAL`, while keeping the existing `CHUMMER_UI_REPO_ROOT_ALIAS` contract for the logical repo root when the alias resolves to the same physical checkout.
  - updated the thin sibling-script wrappers `scripts/ai/build.sh`, `scripts/ai/test.sh`, `scripts/ai/restore.sh`, `scripts/ai/clean.sh`, `scripts/ai/format.sh`, `scripts/ai/coverage.sh`, `scripts/ai/test-matrix.sh`, `scripts/ai/test-native-host-matrix.sh`, and `scripts/ai/with-package-plane.sh` to resolve their local script directory physically before sourcing `_env.sh` or dispatching to sibling helpers.
  - refreshed the existing compliance owner `Package_plane_defaults_stay_explicit_and_repo_local_helpers_use_them` in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so it now fail-closes regressions back to the older non-physical thin-wrapper header and asserts the new `_env.sh` / `with-package-plane.sh` physical script-dir contract.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/_env.sh scripts/ai/with-package-plane.sh scripts/ai/build.sh scripts/ai/test.sh scripts/ai/restore.sh scripts/ai/clean.sh scripts/ai/format.sh scripts/ai/coverage.sh scripts/ai/test-matrix.sh scripts/ai/test-native-host-matrix.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/_env.sh`
      - result: clean
    - `bash -n scripts/ai/with-package-plane.sh`
      - result: clean
    - `bash -n scripts/ai/build.sh`
      - result: clean
    - `bash -n scripts/ai/test.sh`
      - result: clean
    - `bash -n scripts/ai/restore.sh`
      - result: clean
    - `bash -n scripts/ai/clean.sh`
      - result: clean
    - `bash -n scripts/ai/format.sh`
      - result: clean
    - `bash -n scripts/ai/coverage.sh`
      - result: clean
    - `bash -n scripts/ai/test-matrix.sh`
      - result: clean
    - `bash -n scripts/ai/test-native-host-matrix.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:28.01`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Package_plane_defaults_stay_explicit_and_repo_local_helpers_use_them" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `400ms`
- Scope note:
  - this slice hardens only the local `scripts/ai` package-plane helper cluster and its owner-backed portability assertions. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:13:17+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Desktop mouse-first journey portability moved forward in the SR4/SR5/SR6 live-runner lane:
  - updated `scripts/run-desktop-mouse-first-journey-matrix.sh` to the same alias-safe repo-root contract used by the retained wrapper stack: resolve `SCRIPT_DIR_PHYSICAL` / `REPO_ROOT_PHYSICAL` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, preserve the logical alias path only when it resolves to the same physical checkout, and keep `SCRIPT_DIR="$REPO_ROOT/scripts"` with the matrix output rooted under the current checkout alias.
  - added a focused Python owner in `tests/test_desktop_executable_exit_gate_contract.py` so the mouse-first journey matrix runner now fails closed on regressions back to the older non-alias-aware `SCRIPT_DIR` / `REPO_ROOT` header.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/run-desktop-mouse-first-journey-matrix.sh tests/test_desktop_executable_exit_gate_contract.py`
      - result: clean
  - shell syntax:
    - `bash -n scripts/run-desktop-mouse-first-journey-matrix.sh`
      - result: clean
  - focused Python owner proof:
    - `python3 -m pytest -q tests/test_desktop_executable_exit_gate_contract.py -k 'mouse_first_journey_matrix_runner_uses_alias_safe_repo_root_contract or desktop_gate_scripts_default_repo_aliases_to_the_current_checkout'`
      - result: `2 passed, 8 deselected in 0.03s`
- Scope note:
  - this slice hardens only the local mouse-first journey matrix runner’s root-resolution portability and its owner-backed regression coverage. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-08T00:10:47+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Browser-shell portability and public-route truth moved forward again in the SR6 shell lane:
  - `scripts/e2e-public-edge-execution.sh` now uses the alias-safe repo-root contract already adopted across the retained wrapper stack: resolve `SCRIPT_DIR_PHYSICAL` / `REPO_ROOT_PHYSICAL` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, preserve the logical alias only when it resolves to the same physical checkout, pin `SCRIPT_DIR="$REPO_ROOT/scripts"`, and derive `WORKSPACE_ROOT` physically from `REPO_ROOT_PHYSICAL`.
  - the same runner keeps its portable Playwright discovery order intact across `NODE_PATH`, `CHUMMER_PLAYWRIGHT_NODE_PATH`, `CHUMMER_PLAYWRIGHT_ROOT/node_modules`, the sibling `chummer.run-services/node_modules`, sibling `node_modules`, and `scripts/node_modules`.
  - `scripts/e2e-ui.sh` now uses the same alias-safe repo-root / physical-workspace contract, defaults `PLAYWRIGHT_SAMPLE_FILE` to the repo-local `Chummer.Tests/TestFiles/BLUE.chum5`, and extends local Playwright lookup through `CHUMMER_PLAYWRIGHT_NODE_PATH`, `CHUMMER_PLAYWRIGHT_ROOT/node_modules`, and physical sibling workspace node-module roots.
  - `scripts/e2e-ui-playwright.cjs`, `scripts/e2e-public-edge.cjs`, `scripts/e2e-portal.cjs`, `scripts/portal-signoff-fixture.cjs`, and `scripts/materialize-blazor-workbench-roster-hierarchy-staged-proof.py` are now realigned to the current public-browser copy truth from `Chummer.Blazor/Components/Pages/Home.razor`: the public hero marker is `Chummer Online for real dossier work.` and the preview-route marker remains `Preview Chummer Online workflows without changing the public route.`
  - `Chummer.Tests/Compliance/MigrationComplianceTests.cs` was refreshed so the browser-shell compliance owners now fail closed on the alias-safe `e2e-ui.sh` contract and the current dossier-facing public-shell markers instead of the retired `Chummer Browser Workbench` / `real runner work` copy.
- Verification completed for these browser-shell slices:
  - touched-file hygiene:
    - `git diff --check -- scripts/e2e-public-edge-execution.sh tests/test_blazor_public_edge_execution_contract.py Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs`
      - result: clean
    - `git diff --check -- scripts/e2e-ui.sh scripts/e2e-ui-playwright.cjs scripts/e2e-public-edge.cjs scripts/e2e-portal.cjs scripts/portal-signoff-fixture.cjs scripts/materialize-blazor-workbench-roster-hierarchy-staged-proof.py Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - syntax / parse proof:
    - `bash -n scripts/e2e-public-edge-execution.sh`
      - result: clean
    - `python3 -m pytest -q tests/test_blazor_public_edge_execution_contract.py -k 'public_edge_execution_shell_wrapper_uses_alias_safe_repo_root_and_physical_workspace_root'`
      - result: `1 passed, 9 deselected in 0.07s`
    - `bash -n scripts/e2e-ui.sh`
      - result: clean
    - `node --check scripts/e2e-ui-playwright.cjs`
      - result: clean
    - `node --check scripts/e2e-public-edge.cjs`
      - result: clean
    - `node --check scripts/e2e-portal.cjs`
      - result: clean
    - `node --check scripts/portal-signoff-fixture.cjs`
      - result: clean
    - `python3 -m py_compile scripts/materialize-blazor-workbench-roster-hierarchy-staged-proof.py`
      - result: clean
  - focused Python owner proof:
    - `python3 -m pytest -q tests/test_blazor_portal_route_probe_contract.py`
      - result: `8 passed in 0.01s`
    - `python3 -m pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'public_edge_e2e_enforces_direct_public_installer_handoff_routes or portal_e2e_distinguishes_public_desktop_installer_handoffs_from_account_gated_routes'`
      - result: `2 passed, 34 deselected in 0.09s`
  - verification builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:42.59`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:39.84`
  - focused compliance / presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.DesktopExecutableGateComplianceTests.Hosted_public_edge_execution_tooling_paths_stay_wired_across_docs_and_downstream_capture_surfaces" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `460ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Local_e2e_wrappers_fail_closed_and_default_to_live_playwright" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 497ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Playwright_ui_e2e_gate_is_present_for_phase4_gate" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 410ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Portal_playwright_e2e_uses_portal_stack_dependencies" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 206ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Home_renders_truthful_public_navigation_and_browser_desktop_boundaries" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `779ms`
- Scope note:
  - these slices harden the browser-shell execution wrapper, local UI e2e wrapper, and public-route probe truth around the current dossier-facing browser shell. They do not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T23:54:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Release portability moved forward again in the nightly/support-script lane:
  - updated `scripts/publish-latest-nightly-to-downloads.sh`, `scripts/resolve-hub-registry-root.sh`, and `scripts/preflight-macos-packaging.sh` to the alias-safe repo-root contract used by the retained wrapper stack: resolve `SCRIPT_DIR_PHYSICAL`/`REPO_ROOT_PHYSICAL` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - updated `scripts/publish-latest-nightly-to-downloads.sh` to derive its default `WORKSPACE_ROOT` physically from `REPO_ROOT_PHYSICAL`, so `_staging` and the default `chummer.run-services/Chummer.Portal/downloads` sibling target keep resolving correctly even when this repo is entered through an alias path.
  - updated `scripts/resolve-hub-registry-root.sh` to derive sibling checkout candidates from a physical `WORKSPACE_ROOT` instead of recomputing them from the logical repo alias, which keeps hub-registry discovery fail-closed and deterministic under alias-root entry.
  - extended `tests/test_desktop_downloads_local_release_policy.py` so the nightly publisher, hub-registry resolver, and macOS packaging preflight now fail-close regressions back to the older non-alias-aware header, and so the hub-registry resolver also locks the new physical sibling-candidate contract.
  - refreshed the existing C# release-policy owners `Desktop_download_matrix_enforces_daily_release_window_and_targeted_manual_builds` and `Runbook_supports_download_manifest_generation_mode` in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so they cover the new nightly/resolver portability contract and align their stale daily-window / installer-media wording to the current script and docs surface.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/publish-latest-nightly-to-downloads.sh scripts/resolve-hub-registry-root.sh scripts/preflight-macos-packaging.sh tests/test_desktop_downloads_local_release_policy.py Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
    - `git diff --check -- Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/publish-latest-nightly-to-downloads.sh`
      - result: clean
    - `bash -n scripts/resolve-hub-registry-root.sh`
      - result: clean
    - `bash -n scripts/preflight-macos-packaging.sh`
      - result: clean
  - focused Python owner proof:
    - `python3 -m pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'latest_nightly_publish_preflights_windows_bootstrap_payload_metadata or release_support_shell_scripts_use_alias_safe_repo_root or resolve_hub_registry_root_prefers_physical_workspace_sibling_candidates'`
      - result: `3 passed, 33 deselected in 0.16s`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:19.83`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Desktop_download_matrix_enforces_daily_release_window_and_targeted_manual_builds" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `508ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Runbook_supports_download_manifest_generation_mode" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `482ms`
- Scope note:
  - this slice hardens only the nightly publication / hub-registry discovery / macOS packaging-preflight portability contract and the matching owner-backed release-policy assertions. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T23:39:48+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Runbook/release-helper portability moved forward again in the owner-backed desktop release lane:
  - updated `scripts/runbook.sh`, `scripts/runbook-strict-host-gates.sh`, `scripts/check-host-gate-prereqs.sh`, `scripts/validate-amend-manifests.sh`, and `scripts/generate-parity-checklist.sh` to the alias-safe repo-root contract used by the retained wrapper stack: resolve `SCRIPT_DIR_PHYSICAL`/`REPO_ROOT_PHYSICAL` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - added a focused Python owner in `tests/test_desktop_downloads_local_release_policy.py` so the runbook/release helper cluster now fail-closes regressions back to the older non-alias-aware script header.
  - extended the existing C# owner `Runbook_supports_download_manifest_generation_mode` in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so it now fail-closes the same header regression for `runbook.sh`, `runbook-strict-host-gates.sh`, `check-host-gate-prereqs.sh`, `validate-amend-manifests.sh`, and `generate-parity-checklist.sh`.
  - refreshed the same C# owner’s stale manifest/publisher expectations to the current nounset-safe `portal_artifact_count` and `promoted_file_count` logging strings used by the release scripts.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/runbook.sh scripts/runbook-strict-host-gates.sh scripts/check-host-gate-prereqs.sh scripts/validate-amend-manifests.sh scripts/generate-parity-checklist.sh tests/test_desktop_downloads_local_release_policy.py Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
    - `git diff --check -- Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/runbook.sh`
      - result: clean
    - `bash -n scripts/runbook-strict-host-gates.sh`
      - result: clean
    - `bash -n scripts/check-host-gate-prereqs.sh`
      - result: clean
    - `bash -n scripts/validate-amend-manifests.sh`
      - result: clean
    - `bash -n scripts/generate-parity-checklist.sh`
      - result: clean
  - focused Python owner proof:
    - `python3 -m pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'runbook_release_shell_scripts_use_alias_safe_repo_root'`
      - result: `1 passed, 33 deselected in 0.16s`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:50.57`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Runbook_supports_download_manifest_generation_mode" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `412ms`
- Scope note:
  - this slice hardens only the root-resolution portability of the runbook/release helper cluster plus the matching owner-backed release-policy assertions. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T23:32:17+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Release-script portability moved forward again in the owner-backed desktop publish lane:
  - updated `scripts/build-desktop-installer.sh`, `scripts/generate-releases-manifest.sh`, `scripts/verify-releases-manifest.sh`, `scripts/publish-download-bundle.sh`, `scripts/publish-download-bundle-http.sh`, `scripts/publish-download-bundle-s3.sh`, and `scripts/run-desktop-startup-smoke.sh` to the alias-safe repo-root contract used by the retained wrapper stack: resolve `SCRIPT_DIR_PHYSICAL`/`REPO_ROOT_PHYSICAL` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - updated `scripts/publish-download-bundle.sh` to derive `WORKSPACE_ROOT` physically and keep the default `ROOT_RELEASE_BLOCKERS_PATH` pinned to `$WORKSPACE_ROOT/RELEASE_BLOCKERS.generated.json`, so sibling blocker truth stays correct even when the repo is entered through an alias path.
  - extended `tests/test_desktop_downloads_local_release_policy.py`, `Chummer.Tests/Compliance/MigrationComplianceTests.cs`, and `Chummer.Tests/Compliance/DesktopInstallerParityComplianceTests.cs` so the existing desktop-release owners now fail closed on regressions back to the older non-alias-aware script header, and so publish/download bundle coverage also locks the new `WORKSPACE_ROOT` blocker-path rule.
  - refreshed the `run-desktop-startup-smoke.sh` owner assertion in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` to match the current timeout-backed `winepath` fallback contract instead of the stale pre-timeout string.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/build-desktop-installer.sh scripts/generate-releases-manifest.sh scripts/verify-releases-manifest.sh scripts/publish-download-bundle.sh scripts/publish-download-bundle-http.sh scripts/publish-download-bundle-s3.sh scripts/run-desktop-startup-smoke.sh tests/test_desktop_downloads_local_release_policy.py Chummer.Tests/Compliance/MigrationComplianceTests.cs Chummer.Tests/Compliance/DesktopInstallerParityComplianceTests.cs`
      - result: clean
    - `git diff --check -- Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/build-desktop-installer.sh`
      - result: clean
    - `bash -n scripts/generate-releases-manifest.sh`
      - result: clean
    - `bash -n scripts/verify-releases-manifest.sh`
      - result: clean
    - `bash -n scripts/publish-download-bundle.sh`
      - result: clean
    - `bash -n scripts/publish-download-bundle-http.sh`
      - result: clean
    - `bash -n scripts/publish-download-bundle-s3.sh`
      - result: clean
    - `bash -n scripts/run-desktop-startup-smoke.sh`
      - result: clean
  - focused Python owner proof:
    - `python3 -m pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'ui_release_shell_scripts_use_nounset_safe_array_count or windows_startup_smoke_prefers_local_bootstrap_payload_sidecar_when_present or public_stable_publish_download_bundle_requires_root_release_truth_clearance'`
      - result: `3 passed, 30 deselected in 0.21s`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:28.99`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Desktop_download_matrix_includes_avalonia_and_blazor_desktop_artifacts" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 083ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.DesktopInstallerParityComplianceTests.Windows_installer_publish_lanes_gate_bootstrap_payload_before_promotion" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 001ms`
- Scope note:
  - this slice hardens only the root-resolution and blocker-path portability of the retained desktop release scripts plus their matching owner-backed compliance checks. It does not change shared release posture, publish-lane clearance, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T23:15:24+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Browser signoff portability and fallback-fixture hardening moved forward again in the owner-backed `B7` lane:
  - updated `scripts/ai/milestones/b7-browser-isolation-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing portal/browser owner in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so it now fail-closes regressions back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form for `b7-browser-isolation-check.sh`.
  - refreshed `scripts/portal-signoff-fixture.cjs` to satisfy the current `scripts/e2e-portal.cjs` contract used by the `B7` strict runtime-fallback lane, including the current portal landing, docs/help/status/contact, download-manifest/install-route, blazor entry, hub health, and OpenAPI fixture surfaces.
  - realigned the same `MigrationComplianceTests.cs` portal route-probe owner assertions to the current `scripts/e2e-public-edge.cjs` wording and route set, replacing stale expectations around the old `/login?next=%2Faccount` `endsWith` shape, the old `What Is Chummer?` copy, and the old `/blazor/workbench?command=...` route strings with the current `includes`, `what-is-chummer`, `app?command=character_roster`, `blazor/home`, `blazor/app`, and `/play`/`/status` redirect-tail assertions.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/b7-browser-isolation-check.sh scripts/portal-signoff-fixture.cjs Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - syntax:
    - `bash -n scripts/ai/milestones/b7-browser-isolation-check.sh`
      - result: clean
    - `node --check scripts/portal-signoff-fixture.cjs`
      - result: clean
  - direct runtime fallback proof:
    - `CHUMMER_B7_RUNTIME_REQUIRED=1 CHUMMER_B7_ALLOW_RUNTIME_SKIP=0 CHUMMER_B7_ENABLE_RUNTIME_FIXTURE=1 CHUMMER_B7_RUNTIME_FIXTURE_PORT=38091 CHUMMER_PORTAL_SIGNOFF_BASE_URL=http://127.0.0.1:9 bash scripts/ai/milestones/b7-browser-isolation-check.sh`
      - result: `PASS`
      - fixture fallback now completes `portal E2E completed` and `strict probe executed against local runtime fixture`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:07.07`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Portal_playwright_e2e_uses_portal_stack_dependencies" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 085ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.B7_strict_signoff_uses_local_runtime_fixture_when_remote_target_is_unreachable" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 532ms`
- Scope note:
  - this slice hardens only the `b7-browser-isolation-check` root-resolution contract, the local `B7` runtime fallback fixture, and the matching owner-backed portal/public-edge assertions. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:56:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Legacy parity milestone-helper portability moved forward again by hardening five existing-owner guards to the same alias-safe repo-root contract as the rest of the retained wrapper stack:
  - updated `scripts/ai/milestones/chummer5a-layout-hard-gate.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Chummer5a_layout_hard_gate_derives_legacy_avalonia_blazor_catalog_and_release_subproofs` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
  - updated `scripts/ai/milestones/chummer5a-legacy-equivalent-chrome-gate.sh` to the same alias-safe repo-root contract.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Chummer5a_legacy_equivalent_chrome_gate_derives_policy_source_absence_and_tester_wiring_subproofs` so it also fail-closes regressions back to the older non-alias-aware repo-root form.
  - updated `scripts/ai/milestones/chummer5a-muscle-memory-parity-gate.sh` to the same alias-safe repo-root contract.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Chummer5a_muscle_memory_parity_gate_derives_full_scope_policy_dialog_widget_and_wiring_subproofs` so it also fail-closes regressions back to the older non-alias-aware repo-root form.
  - updated `scripts/ai/milestones/chummer4-sr4-muscle-memory-parity-gate.sh` to the same alias-safe repo-root contract.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Chummer4_sr4_muscle_memory_parity_gate_derives_policy_workflow_dialog_seed_and_wiring_subproofs` so it also fail-closes regressions back to the older non-alias-aware repo-root form.
  - updated `scripts/ai/milestones/sr6-shared-muscle-memory-gate.sh` to the same alias-safe repo-root contract.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Sr6_shared_muscle_memory_parity_gate_derives_policy_runtime_inventory_and_wiring_subproofs` so it also fail-closes regressions back to the older non-alias-aware repo-root form.
- Verification completed for these slices:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/chummer5a-layout-hard-gate.sh scripts/ai/milestones/chummer5a-legacy-equivalent-chrome-gate.sh scripts/ai/milestones/chummer5a-muscle-memory-parity-gate.sh scripts/ai/milestones/chummer4-sr4-muscle-memory-parity-gate.sh scripts/ai/milestones/sr6-shared-muscle-memory-gate.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/chummer5a-layout-hard-gate.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/chummer5a-legacy-equivalent-chrome-gate.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/chummer5a-muscle-memory-parity-gate.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/chummer4-sr4-muscle-memory-parity-gate.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/sr6-shared-muscle-memory-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:13.31`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Chummer5a_layout_hard_gate_derives_legacy_avalonia_blazor_catalog_and_release_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 924ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Chummer5a_legacy_equivalent_chrome_gate_derives_policy_source_absence_and_tester_wiring_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `4s 194ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Chummer5a_muscle_memory_parity_gate_derives_full_scope_policy_dialog_widget_and_wiring_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 797ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Chummer4_sr4_muscle_memory_parity_gate_derives_policy_workflow_dialog_seed_and_wiring_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 396ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Sr6_shared_muscle_memory_parity_gate_derives_policy_runtime_inventory_and_wiring_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `3s 398ms`
- Scope note:
  - these slices harden only the `chummer5a-layout-hard-gate`, `chummer5a-legacy-equivalent-chrome-gate`, `chummer5a-muscle-memory-parity-gate`, `chummer4-sr4-muscle-memory-parity-gate`, and `sr6-shared-muscle-memory-gate` root-resolution contracts. They do not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:50:02+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Legacy-parity milestone-helper portability moved forward again by hardening two existing-owner guards to the same alias-safe repo-root contract as the rest of the retained wrapper stack:
  - updated `scripts/ai/milestones/design-authorized-parity-softening-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Design_authorized_parity_softening_gate_requires_explicit_design_backing_for_any_intentional_divergence` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
  - updated `scripts/ai/milestones/chummer-shared-legacy-equivalent-chrome-gate.sh` to the same alias-safe repo-root contract.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Shared_legacy_equivalent_chrome_gate_derives_runtime_receipt_and_wiring_subproofs` so it also fail-closes regressions back to the older non-alias-aware repo-root form.
- Verification completed for these slices:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/design-authorized-parity-softening-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
    - `git diff --check -- scripts/ai/milestones/chummer-shared-legacy-equivalent-chrome-gate.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/design-authorized-parity-softening-check.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/chummer-shared-legacy-equivalent-chrome-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - design-authorized duration: `00:01:31.12`
      - shared-legacy duration: `00:01:20.70`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Design_authorized_parity_softening_gate_requires_explicit_design_backing_for_any_intentional_divergence" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `435ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Shared_legacy_equivalent_chrome_gate_derives_runtime_receipt_and_wiring_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `463ms`
- Scope note:
  - these slices harden only the `design-authorized-parity-softening-check` and `chummer-shared-legacy-equivalent-chrome-gate` root-resolution contracts. They do not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:43:34+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Next90 milestone-helper portability moved forward again by hardening two existing-owner guards to the same alias-safe repo-root contract as the rest of the retained wrapper stack:
  - updated `scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Next90_m113_gm_prep_roster_surface_check_validates_blazor_self_host_workbench_proof_payload` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
  - updated `scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh` to the same alias-safe repo-root contract.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Next90_m103_veteran_certification_guard_binds_screenshots_to_promoted_avalonia_head` so it also fail-closes regressions back to the older non-alias-aware repo-root form.
- Verification completed for these slices:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
    - `git diff --check -- scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/next90-m113-ui-gm-prep-roster-surface-check.sh`
      - result: clean
    - `bash -n scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build --disable-build-servers Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:30.67`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Next90_m113_gm_prep_roster_surface_check_validates_blazor_self_host_workbench_proof_payload" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `972ms`
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Next90_m103_veteran_certification_guard_binds_screenshots_to_promoted_avalonia_head" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `809ms`
- Scope note:
  - these slices harden only the `next90-m113-ui-gm-prep-roster-surface-check` and `next90-m103-ui-veteran-certification-check` root-resolution contracts. They do not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:32:53+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the SR6 ruleset-ui sophistication gate to the same alias-safe repo-root contract as the neighboring ruleset/parity wrappers:
  - updated `scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Sr6_ruleset_ui_sophistication_gate_derives_policy_runtime_receipts_and_release_wiring_subproofs` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:05.68`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Sr6_ruleset_ui_sophistication_gate_derives_policy_runtime_receipts_and_release_wiring_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `402ms`
- Scope note:
  - this slice hardens only the `sr6-ruleset-ui-sophistication-gate` root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:23:28+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the SR4/SR6 desktop-parity frontier receipt helper to the same alias-safe repo-root contract as the neighboring ruleset/parity wrappers:
  - updated `scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Sr4_sr6_frontier_receipt_derives_release_workflow_ruleset_alignment_and_gate_subproofs` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/sr4-sr6-desktop-parity-frontier-receipt.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:26.08`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Sr4_sr6_frontier_receipt_derives_release_workflow_ruleset_alignment_and_gate_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `487ms`
- Scope note:
  - this slice hardens only the `sr4-sr6-desktop-parity-frontier-receipt` root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:22:01+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the ruleset-adaptation guard to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/ruleset-ui-adaptation-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Ruleset_ui_adaptation_guard_derives_directive_catalog_shell_test_and_release_channel_subproofs` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/ruleset-ui-adaptation-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/ruleset-ui-adaptation-check.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:54.66`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Ruleset_ui_adaptation_guard_derives_directive_catalog_shell_test_and_release_channel_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `469ms`
- Scope note:
  - this slice hardens only the `ruleset-ui-adaptation-check` root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:18:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the `M142` direct-workflow proof guard to the same alias-safe repo-root contract as the already-hardened sibling route-proof helpers:
  - updated `scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh` to resolve `repo_root_physical` with `pwd -P`, derive `workspace_root` from the physical checkout, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Next90_m142_direct_workflow_guard_stays_desktop_route_local` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form or the older logical-alias-derived `workspace_root="$(cd "$repo_root/.." && pwd)"` form.
  - refreshed that same owner’s portal route-string assertion to match the current `scripts/e2e-portal-playwright.cjs` contract, which now keeps `/blazor/` on `/blazor/` instead of asserting an older redirect-to-workbench wording.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/next90-m142-ui-direct-workflow-proof-check.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:17.09`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Next90_m142_direct_workflow_guard_stays_desktop_route_local" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `496ms`
- Scope note:
  - this slice hardens only the `next90-m142-ui-direct-workflow-proof-check` root-resolution contract and keeps its focused owner aligned to the current route-local portal wording. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:11:14+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the dense workbench recovery gate to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/dense-workbench-recovery-gate.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Dense_workbench_recovery_gate_derives_budget_layout_and_screenshot_subproofs` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/dense-workbench-recovery-gate.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/dense-workbench-recovery-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:08.24`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Dense_workbench_recovery_gate_derives_budget_layout_and_screenshot_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `796ms`
- Scope note:
  - this slice hardens only the `dense-workbench-recovery-gate` root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:07:47+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the classic dense workbench posture gate to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/classic-dense-workbench-posture-gate.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended the existing `Chummer.Tests/Compliance/MigrationComplianceTests.cs` owner `Classic_dense_workbench_posture_gate_derives_feedback_density_layout_and_regression_subproofs` so it now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/classic-dense-workbench-posture-gate.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/classic-dense-workbench-posture-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:06.81`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Classic_dense_workbench_posture_gate_derives_feedback_density_layout_and_regression_subproofs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `511ms`
- Scope note:
  - this slice hardens only the `classic-dense-workbench-posture-gate` root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T22:01:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T21:51:38+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the `B8` runtime-inspector guard to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/b8-runtime-inspector-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` with `Runtime_inspector_milestone_guard_uses_alias_safe_repo_root_resolution` so the guard now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/b8-runtime-inspector-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/b8-runtime-inspector-check.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:25.61`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Runtime_inspector_milestone_guard_uses_alias_safe_repo_root_resolution" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `4s 907ms`
- Scope note:
  - this slice hardens only the `B8` runtime-inspector milestone helper root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:57:31+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening `ui-gold-proof-depth-gate.sh` to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/ui-gold-proof-depth-gate.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs` so `Hosted_public_edge_workbench_proof_shape_propagates_to_downstream_milestone_consumers` now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form in the gold-proof gate.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/ui-gold-proof-depth-gate.sh Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/ui-gold-proof-depth-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:43.40`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.DesktopExecutableGateComplianceTests.Hosted_public_edge_workbench_proof_shape_propagates_to_downstream_milestone_consumers" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `574ms`
- Scope note:
  - this slice hardens only the `ui-gold-proof-depth-gate` milestone helper root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:54:36+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward again by hardening the veteran task-time evidence gate to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/veteran-task-time-evidence-gate.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` with `Veteran_task_time_gate_uses_alias_safe_repo_root_resolution` so the gate now fail-closes any regression back to the older non-alias-aware `repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/veteran-task-time-evidence-gate.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/veteran-task-time-evidence-gate.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:21.40`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Veteran_task_time_gate_uses_alias_safe_repo_root_resolution" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `1s 076ms`
- Scope note:
  - this slice hardens only the veteran task-time milestone helper root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:50:31+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Milestone-helper portability moved forward by hardening the `B11-NPC` guard to the same alias-safe repo-root contract as the other retained milestone wrappers:
  - updated `scripts/ai/milestones/b11-npc-persona-studio-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` with `Npc_persona_studio_milestone_guard_uses_alias_safe_repo_root_resolution` so the guard now fail-closes any regression back to the older non-alias-aware `REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"` form.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/b11-npc-persona-studio-check.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/b11-npc-persona-studio-check.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:42.25`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Npc_persona_studio_milestone_guard_uses_alias_safe_repo_root_resolution" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `430ms`
- Scope note:
  - this slice hardens only the `B11-NPC` milestone helper root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:45:09+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Utility-wrapper portability moved forward by normalizing the last small local helpers that were still sourcing `_env.sh` through `$(dirname "$0")`:
  - updated `scripts/ai/clean.sh` and `scripts/ai/format.sh` to resolve `SCRIPT_DIR` from `BASH_SOURCE[0]` and source `scripts/ai/_env.sh` through that shared contract.
  - updated `tests/test_desktop_downloads_local_release_policy.py` so the focused Python ownership surface now fail-closes regressions across the shared-env utility wrapper family (`clean.sh`, `format.sh`, `test-matrix.sh`, `coverage.sh`) in addition to the earlier Codex and day1 wrappers.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/clean.sh scripts/ai/format.sh tests/test_desktop_downloads_local_release_policy.py`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/clean.sh`
      - result: clean
    - `bash -n scripts/ai/format.sh`
      - result: clean
  - focused Python proof:
    - `python3 -m py_compile tests/test_desktop_downloads_local_release_policy.py`
      - result: clean
    - `pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'shared_env_utility_wrappers_use_script_dir_env_contract or codex_wrappers_resolve_repo_root_from_script_location or day1_wrappers_resolve_repo_root_from_shared_env_contract or day1_setup_avoids_bash4_collectors_and_associative_arrays'`
      - result: `4 passed`, `29 deselected`
      - duration: `2.59s`
- Scope note:
  - this slice hardens only the shared-env utility wrapper contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:41:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Day1 wrapper portability moved forward by bringing the remaining local day1 launcher family onto the shared alias-safe `_env.sh` contract:
  - updated `scripts/ai/day1-clean-artifacts.sh`, `scripts/ai/day1-all-milestones.sh`, `scripts/ai/day1-p1-run.sh`, and `scripts/ai/day1-p1-loop.sh` so they source `scripts/ai/_env.sh` through `SCRIPT_DIR` instead of re-deriving repo roots with plain `pwd`.
  - updated `scripts/ai/day1-p1-setup.sh` so the solution-maintenance helper consumes the shared `REPO_ROOT` instead of recomputing `repo_root` from `SCRIPT_DIR`.
  - updated `tests/test_desktop_downloads_local_release_policy.py` so the focused Python ownership surface now fail-closes regressions across the whole day1 wrapper family as well as the `day1-p1-setup.sh` helper.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/day1-clean-artifacts.sh scripts/ai/day1-all-milestones.sh scripts/ai/day1-p1-run.sh scripts/ai/day1-p1-loop.sh scripts/ai/day1-p1-setup.sh tests/test_desktop_downloads_local_release_policy.py`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/day1-clean-artifacts.sh`
      - result: clean
    - `bash -n scripts/ai/day1-all-milestones.sh`
      - result: clean
    - `bash -n scripts/ai/day1-p1-run.sh`
      - result: clean
    - `bash -n scripts/ai/day1-p1-loop.sh`
      - result: clean
    - `bash -n scripts/ai/day1-p1-setup.sh`
      - result: clean
  - focused Python proof:
    - `python3 -m py_compile tests/test_desktop_downloads_local_release_policy.py`
      - result: clean
    - `pytest -q tests/test_desktop_downloads_local_release_policy.py -k 'codex_wrappers_resolve_repo_root_from_script_location or day1_wrappers_resolve_repo_root_from_shared_env_contract or day1_setup_avoids_bash4_collectors_and_associative_arrays'`
      - result: `3 passed`, `29 deselected`
      - duration: `1.10s`
- Scope note:
  - this slice hardens only the local day1 wrapper/root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:37:05+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Codex launcher wrapper portability moved forward by removing the last duplicated non-alias-safe repo-root resolver from the local launcher entrypoints:
  - updated `scripts/ai/run_codex.sh` to source `scripts/ai/_env.sh` through `SCRIPT_DIR` and rely on the shared alias-safe `REPO_ROOT` before entering the checkout.
  - updated `scripts/ai/run_codex_resume.sh` the same way so resume mode follows the same logical-alias/physical-checkout contract as the rest of the hardened AI wrapper stack.
  - updated `tests/test_desktop_downloads_local_release_policy.py` so `test_codex_wrappers_resolve_repo_root_from_script_location` now fail-closes any regression back to direct `REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"` or `source "$REPO_ROOT/scripts/ai/_env.sh"` usage in those wrappers.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/run_codex.sh scripts/ai/run_codex_resume.sh tests/test_desktop_downloads_local_release_policy.py`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/run_codex.sh`
      - result: clean
    - `bash -n scripts/ai/run_codex_resume.sh`
      - result: clean
  - focused Python proof:
    - `python3 -m py_compile tests/test_desktop_downloads_local_release_policy.py`
      - result: clean
    - `pytest -q tests/test_desktop_downloads_local_release_policy.py -k codex_wrappers_resolve_repo_root_from_script_location`
      - result: `1 passed`, `30 deselected`
      - duration: `0.73s`
- Scope note:
  - this slice hardens only the local Codex launcher wrapper root-resolution contract. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:34:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Shared AI-wrapper portability moved forward by hardening the package-plane environment stack instead of only patching leaf entrypoints:
  - updated `scripts/ai/_env.sh` to resolve `REPO_ROOT_PHYSICAL` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical repo alias only when it resolves to the same physical checkout.
  - updated `scripts/ai/with-package-plane.sh` to consume the shared alias-safe `REPO_ROOT`, keep `cd "$repo_root"` on the logical checkout path, and derive `workspace_root` from the physical checkout so sibling compatibility-tree projects still resolve from the real workspace.
  - extended `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so `Package_plane_defaults_stay_explicit_and_repo_local_helpers_use_them` now fail-closes any regression back to the older non-alias-aware root resolution in either `_env.sh` or `with-package-plane.sh`.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/_env.sh scripts/ai/with-package-plane.sh Chummer.Tests/Compliance/MigrationComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/_env.sh`
      - result: clean
    - `bash -n scripts/ai/with-package-plane.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:06.05`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.MigrationComplianceTests.Package_plane_defaults_stay_explicit_and_repo_local_helpers_use_them" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `902ms`
- Scope note:
  - this slice hardens the shared AI wrapper/package-plane root-resolution contract only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:27:03+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Release-script portability moved forward again by hardening the shared standard verify entrypoint to the same alias-safe repo-root contract as the executable and workflow gate wrappers:
  - updated `scripts/ai/verify.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - extended `Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs` so the existing alias-safe root-resolution guard now also pins `verify.sh`, not just the desktop executable gate and platform-specific exit-gate scripts.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/verify.sh Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/verify.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:42.92`
  - focused compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.DesktopExecutableGateComplianceTests.Windows_and_macos_exit_gate_materializers_do_not_resolve_proof_from_legacy_chummer5a_paths" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`
      - duration: `504ms`
- Scope note:
  - this slice hardens the standard verify entrypoint's repo-root portability only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T21:23:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Release-script portability moved forward again by bringing the interactive control inventory guard onto the same alias-safe repo-root contract as the other hardened milestone wrappers:
  - updated `scripts/ai/milestones/interactive-control-inventory-check.sh` to resolve `repo_root_physical` with `pwd -P`, accept `CHUMMER_UI_REPO_ROOT_ALIAS`, and preserve the logical alias path only when it resolves to the same physical checkout.
  - updated `Chummer.Tests/Compliance/InteractiveControlInventoryComplianceTests.cs` with `Interactive_control_inventory_guard_uses_alias_safe_repo_root_resolution` so the script now fail-closes if it regresses to the older non-alias-aware `pwd` root resolution.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/interactive-control-inventory-check.sh Chummer.Tests/Compliance/InteractiveControlInventoryComplianceTests.cs`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/interactive-control-inventory-check.sh`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:51.19`
  - focused compliance proof pack:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.InteractiveControlInventoryComplianceTests.Interactive_control_inventory_guard_stays_in_standard_verify_path|FullyQualifiedName=Chummer.Tests.Compliance.InteractiveControlInventoryComplianceTests.Interactive_control_inventory_guard_uses_alias_safe_repo_root_resolution" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`
      - duration: `534ms`
- Scope note:
  - this slice hardens interactive-control inventory script portability only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T19:29:25+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The classic menu interaction matrix is now pinned across both browser shell surfaces instead of only one route variant:
  - extended `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` with `App_route_classic_menu_closes_when_route_context_changes`.
  - extended `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` with `Workbench_classic_menu_closes_when_escape_is_pressed`.
  - the app-route route-change proof now captures the real UI nuance: when `/app?command=character_roster` navigates to `/app?command=new_character_origin`, the menu does not merely close in place; the destination swaps into the startup shell and removes the classic menu surface entirely while surfacing Origin Dossier startup metadata.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:37.44`
  - focused menu-interaction matrix:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_classic_menu_opens_on_click_and_closes_when_focus_moves_back_to_the_surface|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_classic_menu_closes_when_escape_is_pressed|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_classic_menu_closes_when_route_context_changes|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_classic_menu_closes_when_route_context_changes|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_classic_menu_closes_when_escape_is_pressed" --output Normal`
      - result: `5 total`, `5 succeeded`, `0 failed`, `0 skipped`
      - duration: `1.235s`
- Scope note:
  - this slice hardens cross-route browser menu behavior only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T19:21:36+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The classic browser menu behavior that now lives inside tracked `Preview.razor` has focused interaction coverage for route-reset and keyboard-close behavior instead of only render smoke:
  - updated `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to add `App_route_classic_menu_closes_when_escape_is_pressed`.
  - updated `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to add `Workbench_classic_menu_closes_when_route_context_changes`.
  - these proofs pin the two behavioral edges that mattered after migrating the menu-state partial into the tracked Razor file: `Escape` must close the open app-route menu, and a route-signature change must reset the workbench menu state instead of leaving flyouts open across navigation.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:57.32`
  - focused menu-interaction proof pack:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_classic_menu_opens_on_click_and_closes_when_focus_moves_back_to_the_surface|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_classic_menu_closes_when_escape_is_pressed|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_classic_menu_closes_when_route_context_changes" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`
      - duration: `1.105s`
- Scope note:
  - this slice hardens browser menu interaction behavior only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T19:15:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The repo no longer depends on local-only compiled route or headless-helper files for this lane:
  - merged the classic menu state/route-reset partial from the former local `Chummer.Blazor/Components/Pages/Preview.ClassicMenus.cs` file into the tracked `Chummer.Blazor/Components/Pages/Preview.razor` code block, then removed the local-only partial file.
  - moved the headless-session synchronization helper into tracked `Chummer.Tests/Presentation/TestContextLocator.cs`, then removed the local-only `Chummer.Tests/Presentation/AvaloniaHeadlessSessionGate.cs` compile target.
  - moved the portal public-route contract assertions into tracked `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`, then removed the local-only `Chummer.Tests/Presentation/PortalAppRouteContractTests.cs` compile target.
  - deleted the still-untracked duplicate `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` harness now that `AppRouteSurfaceTests` owns the route proof and the project/guardrails fail closed if that duplicate include returns.
  - updated `Chummer.Tests/Chummer.Tests.csproj` plus `Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs` so the verification assembly now rejects those local-only test-support file includes explicitly.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/TestContextLocator.cs Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs Chummer.Tests/Chummer.Tests.csproj`
      - result: clean for diff issues; git emitted only the repo's existing LF->CRLF warning for `Chummer.Tests.csproj`
  - targeted worktree probe:
    - `git status --short -- Chummer.Blazor/Components/Pages Chummer.Tests/Presentation Chummer.Tests/Compliance Chummer.Tests/Chummer.Tests.csproj`
      - result: only modified tracked files remain in these paths; the removed local-only files no longer appear as `??`
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:15.64`
  - focused positive proof pack:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Portal_program_redirects_clean_public_app_route_to_hosted_blazor_app_and_preserves_query_string|FullyQualifiedName=Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Portal_program_keeps_clean_public_app_route_in_openapi_and_route_registry|FullyQualifiedName=Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Preview_renders_explicit_boundary_banner_around_desktop_shell|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_renders_character_roster_without_preview_scaffolding|FullyQualifiedName=Chummer.Tests.Compliance.ArchitectureGuardrailTests.Verification_test_project_keeps_tracked_workbench_route_suite_and_rejects_duplicate_local_harness|FullyQualifiedName=Chummer.Tests.Compliance.ArchitectureGuardrailTests.Verification_test_project_does_not_depend_on_local_portal_or_headless_support_files" --output Normal`
      - result: `6 total`, `6 succeeded`, `0 failed`, `0 skipped`
      - duration: `1.106s`
  - removed-class absence probe:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
      - result: `Zero tests ran`, `total 0`, `failed 0`, `succeeded 0`, `skipped 0`, duration `314ms`
      - note: the MSTest runner returned exit code `8` for the zero-match filter, which is expected for this absence probe rather than a failing test
- Scope note:
  - this slice hardens repo-local source ownership and test-assembly shape only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T19:06:04+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The repo-local portability cleanup continued past the earlier workflow-family migration so the assertions now live only in tracked verification surfaces inside existing test files:
  - migrated the desktop exit-gate bash3 portability probes out of the former scratch file into `tests/test_desktop_executable_exit_gate_contract.py`, including the `RELEASE_PROMOTED_TUPLE` collector loop and the Linux keep-roots retention guard.
  - migrated the release-shell nounset-safe array-count probes plus the remaining AI wrapper/day1 setup portability assertions into `tests/test_desktop_downloads_local_release_policy.py`.
  - migrated the startup-smoke case-conversion guard into `tests/test_windows_bootstrap_download_smoke_contract.py`.
  - `git status --short -- tests` now shows no `??` portability probes; the old local scratch files were removed instead of left as parallel verification surfaces.
- Verification completed for these slices:
  - touched-file hygiene:
    - `git diff --check -- docs/WORKBENCH_SESSION_HANDOFF.md tests/test_chummer5a_parity_tester.py tests/test_desktop_downloads_local_release_policy.py tests/test_desktop_executable_exit_gate_contract.py tests/test_windows_bootstrap_download_smoke_contract.py`
      - result: clean
  - tests worktree probe:
    - `git status --short -- tests docs/WORKBENCH_SESSION_HANDOFF.md`
      - result: only modified tracked files remain in `tests/`; no untracked portability probes remain
  - focused migration pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_executable_exit_gate_contract.py tests/test_desktop_downloads_local_release_policy.py tests/test_windows_bootstrap_download_smoke_contract.py`
      - result: `42 passed`
      - duration: `0.19s`
  - focused tracked AI-shell follow-up:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_downloads_local_release_policy.py`
      - result: `31 passed`
      - duration: `0.13s`
  - final combined end-state pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_chummer5a_parity_tester.py tests/test_desktop_downloads_local_release_policy.py tests/test_desktop_executable_exit_gate_contract.py tests/test_windows_bootstrap_download_smoke_contract.py`
      - result: `58 passed`
      - duration: `2.19s`
- Scope note:
  - this slice hardens repo-local portability coverage ownership only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T19:00:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The workflow-family portability and receipt-contract coverage now lives entirely in tracked repo tests instead of the two local scratch pytest files:
  - updated `tests/test_chummer5a_parity_tester.py` to resolve `REPO_ROOT` from the active checkout, then added `test_workflow_family_execution_receipts_materializer_autostarts_and_retries_local_api` and `test_sr6_workflow_parity_wrapper_chains_execution_materializers_behind_single_skip_switch`.
  - updated `tests/test_desktop_executable_exit_gate_contract.py` to resolve `REPO_ROOT` from the active checkout, then added `test_release_gate_milestone_scripts_avoid_bash4_mapfile_collectors`, `test_desktop_executable_exit_gate_avoids_bash4_case_conversion_for_tuple_receipt_paths`, and `test_workflow_family_parity_wrappers_fallback_when_flock_is_unavailable`.
  - removed the duplicate local scratch files `tests/test_release_gate_milestone_bash_portability.py` and `tests/test_workflow_family_execution_receipts_contract.py` so this lane no longer depends on untracked pytest artifacts for the portability assertions that were just added.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- tests/test_chummer5a_parity_tester.py tests/test_desktop_executable_exit_gate_contract.py`
      - result: clean
  - tests worktree probe:
    - `git status --short -- tests`
      - result: the deleted scratch files no longer appear; other modified and untracked test files remain elsewhere in the dirty tree and were left untouched
  - shell syntax:
    - `bash -n scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh`
      - result: clean
  - focused tracked portability/contracts pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_executable_exit_gate_contract.py tests/test_chummer5a_parity_tester.py`
      - result: `21 passed`
      - duration: `2.12s`
- Scope note:
  - this slice migrates current portability assertions into tracked repo tests and removes the duplicate local scratch probes. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T18:55:33+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The SR4/SR6 workflow-family parity wrappers now keep their chain lock on systems where `flock` is unavailable:
  - updated `scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh` and `scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh` to keep the existing `flock` path when available, but fall back to a bash3-safe lock-directory + pid-file loop when `flock` is missing.
  - the fallback records the owner pid, reclaims stale lock directories when the recorded pid is no longer alive, and releases the fallback lock through an `EXIT` trap instead of silently dropping serialization on non-Linux shells.
  - updated the tracked repo-local portability guard `tests/test_release_gate_milestone_bash_portability.py` plus the local workflow-family contract probe `tests/test_workflow_family_execution_receipts_contract.py` so the current lock-fallback structure is explicitly pinned.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh tests/test_release_gate_milestone_bash_portability.py tests/test_workflow_family_execution_receipts_contract.py`
      - result: clean
  - shell syntax:
    - `bash -n scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh`
      - result: clean
  - focused portability/contracts pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_release_gate_milestone_bash_portability.py tests/test_workflow_family_execution_receipts_contract.py`
      - result: `5 passed`
      - duration: `0.07s`
- Scope note:
  - this slice hardens workflow-family wrapper portability only. It does not change shared release posture, publish-lane receipts, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T18:50:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- A compliance guard now pins the test-project exclusion so the duplicate local workbench harness cannot quietly re-enter the verification assembly:
  - updated `Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs` to add `Verification_test_project_keeps_tracked_workbench_route_suite_and_rejects_duplicate_local_harness`.
  - the new guard reads `Chummer.Tests/Chummer.Tests.csproj`, asserts that the tracked `Presentation\AppRouteSurfaceTests.cs` entry remains present, and fail-closes if `Presentation\AppShellBaseHrefTests.cs` is reintroduced as a compile include.
  - this turns the prior project-file cleanup into an explicit regression check instead of a one-off repo edit.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs`
      - result: clean
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:36.44`
  - focused guard proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.ArchitectureGuardrailTests.Verification_test_project_keeps_tracked_workbench_route_suite_and_rejects_duplicate_local_harness" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `6.797s`
- Scope note:
  - this slice adds regression coverage only. It does not delete the local untracked `AppShellBaseHrefTests.cs` file and it does not change the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T18:45:11+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The Linux/net10 test project is now hardened against the duplicate local workbench harness without deleting that local file:
  - updated `Chummer.Tests/Chummer.Tests.csproj` to stop explicitly compiling `Presentation/AppShellBaseHrefTests.cs`, while keeping the tracked `Presentation/AppRouteSurfaceTests.cs` suite in place.
  - this means the untracked duplicate file can remain on disk as a scratch artifact without polluting the repo’s verification assembly, while the tracked route-proof suite continues to carry the authoritative workbench compatibility coverage.
  - the earlier direct method-inventory migration still stands: the tracked suite already subsumes the duplicate harness’s current public test-method inventory, and the project file now reflects that ownership.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Chummer.Tests.csproj`
      - result: clean for diff issues; git emitted only the repo’s existing LF->CRLF warning for the project file
  - inclusion probe:
    - `rg -n "AppShellBaseHrefTests|AppRouteSurfaceTests" Chummer.Tests/Chummer.Tests.csproj`
      - result: only `Presentation\AppRouteSurfaceTests.cs` remains explicitly included
  - verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:08:55.62`
  - focused tracked proof after the exclusion:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_open_dossier_route_renders_open_dossier_workflow_metadata|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_fallback_query_metadata_matches_compatibility_route_contract|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_output_dialog_action_queries_publish_specific_download_heading_and_continuation" --output Normal`
      - result: `44 total`, `44 succeeded`, `0 failed`, `0 skipped`, duration `1.553s`
  - duplicate-harness assembly-shape probe:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
      - result: `Zero tests ran`, `total 0`, `failed 0`, `succeeded 0`, `skipped 0`, duration `791ms`
      - note: the MSTest runner returned exit code `8` for the zero-match filter, which is expected for this absence probe rather than a failing test
- Scope note:
  - this slice hardens the test assembly shape only; it does not delete the local untracked `AppShellBaseHrefTests.cs` file and it does not change the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T18:32:24+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Tracked `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` now fully subsumes the current public method inventory of the still-untracked `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` harness:
  - migrated the hosted `/blazor/workbench` SSR render proofs for open-dossier, save/save-as, supported dialog routes, output/action/data-pack visible chrome, build-lab fallback, control-dialog fallback, and committed-result banners into the tracked suite.
  - migrated the remaining helper-level workbench command/workflow contract proofs for tool routes, data-pack routes, action routes, output routes, download dialog-action routes, control-only identity inference, and the broad compatibility-route metadata matrix into the tracked suite.
  - a direct public-method inventory comparison between the tracked and untracked files now reports no unique method names left in `AppShellBaseHrefTests.cs`; the tracked suite owns the route truth, although the local untracked file still exists and will still compile if left in place.
- Verification completed for these slices:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - helper-inventory check:
    - public-method inventory comparison of `AppShellBaseHrefTests.cs` versus `AppRouteSurfaceTests.cs`
      - result: no unique method names remain in the untracked file
  - render-slice verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:04.98`
  - render-slice focused proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_open_dossier_route_renders_open_dossier_workflow_metadata|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_save_as_route_renders_save_workflow_result_without_dialog_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_save_route_renders_save_workflow_result_without_dialog_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_supported_dialog_routes_render_clean_app_continuations_while_preserving_dialog_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_output_routes_render_specific_visible_chrome_without_dialog_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_action_routes_render_specific_visible_chrome_without_dialog_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_data_pack_routes_render_specific_visible_chrome_without_dialog_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_new_character_route_uses_build_lab_classic_chrome_while_preserving_new_runner_dialog|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_control_dialog_route_uses_workflow_classic_chrome_while_preserving_dialog_title|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_committed_result_fallback_renders_supported_result_banner" --output Normal`
      - result: `50 total`, `50 succeeded`, `0 failed`, `0 skipped`, duration `1.403s`
  - helper-slice verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:12.25`
  - helper-slice focused proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_tool_command_queries_publish_specific_tool_workflow_identity|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_data_pack_command_queries_publish_expected_workflow_identity_without_dialog_payload|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_action_command_queries_publish_expected_workflow_identity_without_dialog_payload|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_output_command_queries_publish_expected_workflow_identity|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_output_dialog_action_queries_publish_specific_download_heading_and_continuation|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_control_only_queries_infer_interactive_shell_identity|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_fallback_query_metadata_matches_compatibility_route_contract" --output Normal`
      - result: `99 total`, `99 succeeded`, `0 failed`, `0 skipped`, duration `533ms`
- Scope note:
  - this work hardens tracked SSR fallback and helper-level workbench route proof so the current lane no longer depends on the separate untracked harness for route truth.
  - the local `AppShellBaseHrefTests.cs` file still exists as an untracked duplicate; future hygiene work can decide whether to remove or explicitly version it, but that decision is no longer coupled to route-proof coverage.

## Cross-Codex Refresh (2026-07-07T18:17:24+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Tracked helper-contract coverage for the core workbench fallback entrypoints advanced again, further reducing reliance on the still-untracked `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` harness:
  - updated tracked `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to add the direct `BuildWorkbenchFallback` proofs for the Origin Dossier entrypoint, committed-result routes, the standard new-character fallback, character roster, and master index.
  - the tracked suite now directly proves the current helper-level command/workflow metadata, result-text, result-route, dialog payload, and committed-result contracts for those core compatibility routes instead of relying on the separate untracked test harness.
  - this also keeps the route-contract hardening aligned with the current clean-route Origin Dossier wording and the current dossier-facing roster/build labels.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:12:08.62`
  - focused tracked helper proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_origin_dossier_query_builds_story_first_fallback_dialog|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_committed_result_query_prefers_result_banner_over_dialog_payload|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_new_character_query_defaults_to_blue_build_lab_identity|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_character_roster_query_uses_character_roster_workflow_identity|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_master_index_query_uses_master_index_workflow_identity" --output Normal`
      - result: `14 total`, `14 succeeded`, `0 failed`, `0 skipped`, duration `1.201s`
- Scope note:
  - this slice hardens tracked helper-level fallback contracts for the core compatibility entrypoints only. It does not delete the still-untracked `AppShellBaseHrefTests.cs` file yet, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T18:01:34+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Tracked helper-level route sanitation coverage advanced again, further shrinking the unique value that only existed in the still-untracked `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` harness:
  - updated tracked `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to add the direct `BuildWorkbenchFallback` and static helper proof for query-token normalization and sanitized public/workbench href generation.
  - the tracked suite now explicitly proves that `workspace=storm ops`, `runner=Ghost/One`, and `fixture=alpha beta` normalize to `storm-ops`, `Ghost-One`, and `alpha-beta`, and that the generated `BuildWorkbenchHref` / `BuildPublicAppHref` results keep the same clean route contract expected by the hosted shell continuations.
  - added the small tracked reflection helpers needed for that direct `App` contract proof so later migrations from the untracked harness no longer need to reintroduce them.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:49.81`
  - focused tracked helper proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Workbench_fallback_normalizes_query_tokens_and_emits_sanitized_relative_hrefs" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `410ms`
- Scope note:
  - this slice hardens tracked helper-level route sanitation proof only. It does not delete the still-untracked `AppShellBaseHrefTests.cs` file yet, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:56:05+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Tracked hosted-workbench normalization coverage advanced again, further reducing reliance on the still-untracked `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` harness:
  - updated tracked `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to add hosted `/blazor/workbench` document-render proofs for custom runner, workspace, and fixture normalization on the SSR fallback shell.
  - the tracked suite now proves that a custom route like `workspace=storm ops&runner=Ghost/One&fixture=alpha beta` renders normalized shell state (`storm-ops`, `Ghost-One`, `alpha-beta`) while keeping the public `/app` continuations clean and free of an accidental `runner=` query leak.
  - the tracked suite also now proves that the hosted download/export output continuations for those normalized routes preserve the clean public app hrefs and the expected custom-runner visible copy.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:39.62`
  - focused tracked normalization proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_fallback_uses_normalized_runner_label_for_custom_runner_urls|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_output_fallback_uses_custom_runner_copy_without_polluting_clean_app_href|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_output_dialog_action_fallback_preserves_clean_app_action_continuation" --output Normal`
      - result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `912ms`
- Scope note:
  - this slice hardens tracked hosted-workbench route normalization and clean app-href proof only. It does not delete the still-untracked `AppShellBaseHrefTests.cs` file yet, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:49:45+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Tracked release-portability coverage for the hosted Blazor shell advanced again, reducing reliance on the still-untracked `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` harness:
  - updated tracked `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to add hosted document and direct `App` helper proofs for base-href, static-asset, and service-worker behavior across `/app`, `/online`, `/blazor/app`, and `/blazor/workbench`.
  - the tracked suite now explicitly proves that hosted `/blazor/*` routes keep `/blazor/` as the app base while the clean public `/app` and `/online` routes still resolve static assets back through the hosted `/blazor/` path, preserving current release-script and deployment portability assumptions.
  - the tracked suite also now proves that the full hosted `/blazor/app` document render emits the `/blazor/` base href and expected static asset paths without collapsing into the SSR workbench fallback shell.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:11.84`
  - focused tracked portability proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_app_route_renders_blazor_base_href_with_static_assets_and_no_ssr_fallback|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Clean_public_app_route_uses_root_base_href_while_preserving_hosted_asset_paths|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_app_route_keeps_blazor_base_href|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Clean_online_alias_route_uses_root_base_href_while_preserving_hosted_asset_paths|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_route_keeps_blazor_base_href" --output Normal`
      - result: `5 total`, `5 succeeded`, `0 failed`, `0 skipped`, duration `730ms`
- Scope note:
  - this slice hardens tracked hosted-route portability proof only. It does not delete the still-untracked `AppShellBaseHrefTests.cs` file yet, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:45:14+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The shared Origin Dossier build-handoff note now says `clean route` in both the browser and Avalonia dialog renderers:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the build-handoff note now says `Use this clean route to reopen Origin Dossier without publishing the story text.` instead of the older generic route sentence.
  - updated `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` so the legacy standalone dialog lead uses the same clean-route wording, keeping the native SR6 dialog surface aligned with the Blazor build-handoff pane.
  - updated `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the rendered build-handoff proof now pins the clean-route note and explicitly rejects the removed older sentence.
  - updated `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the Avalonia origin-dialog source gate now pins the clean-route note text inside the specialized origin surface and explicitly rejects the removed older sentence.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Avalonia/DesktopDialogWindow.axaml.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: clean
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:05:23.94`
  - focused renderer/source proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes|FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Origin_dossier_dialogs_use_specialized_shell_surfaces_instead_of_generic_field_sheet" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `1.686s`
- Scope note:
  - this slice tightens the shared Origin Dossier handoff note wording only. It does not rename the preview card heading, the in-app assistant action label, or the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:38:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The native install-link window now uses the same clean-route Origin Dossier CTA wording as the guarded browser shell:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so the install-link-only `desktop.install_link.button.open_origin_dossier` resource now says `Open clean Origin Dossier route` in English and corresponding clean-route wording in the shipped `de-DE`, `fr-FR`, `ja-JP`, `pt-BR`, and `zh-CN` locale tables.
  - kept `Chummer.Avalonia/DesktopInstallLinkingWindow.cs` on the same resource key, so the native window inherits the tightened route wording without changing its runtime behavior.
  - updated `Chummer.Tests/Presentation/DesktopThemeManagerTests.cs` so the localization-catalog proof now pins the clean-route install-link label and explicitly rejects the removed shorter English label for that key.
  - updated `Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs` so the install-link window source audit now also pins the clean-route localization value and explicitly rejects the removed shorter label on the native install-link path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n "desktop\\.install_link\\.button\\.open_origin_dossier|Open clean Origin Dossier route|Open Origin Dossier" Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs Chummer.Avalonia/DesktopInstallLinkingWindow.cs Chummer.Tests/Presentation/DesktopThemeManagerTests.cs`
      - result: the install-link resource now uses clean-route wording across the shipped locale tables, the native window still reads the same isolated key, and the old shorter English value survives only in negative assertions or unrelated in-app command labels outside this slice
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:36.53`
  - focused native proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopThemeManagerTests.Report_and_account_windows_use_explicit_shell_window_surfaces|FullyQualifiedName=Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests.Install_link_window_contains_first_run_optional_tools_visibility_choice" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `478ms`
- Scope note:
  - this slice tightens the native install-link route label only. It does not rename the separate in-app assistant action label `Open Origin Dossier`, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:31:59+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- The latest hosted `/blazor/workbench` Origin Dossier fallback proof no longer relies only on the untracked `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` file:
  - updated tracked `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` to render the full `Chummer.Blazor.Components.App` document under a fixed hosted `/blazor/` navigation context.
  - added a tracked proof that the hosted `/blazor/workbench?command=new_character_origin` SSR fallback keeps `<base href="/blazor/" />`, exposes the origin-dossier compatibility metadata, renders `Continue Origin Dossier on the clean route.`, and uses the dossier-specific `Open clean Origin Dossier route` CTA instead of the generic `Continue this workflow on Chummer Online` link label.
  - added the small fixed-navigation helper in that tracked file so future hosted-app SSR route proofs can stay in tracked coverage without depending on the separate untracked test harness.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:04:03.31`
  - focused tracked SSR proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Hosted_blazor_workbench_origin_route_renders_clean_origin_dossier_result_cta" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `910ms`
- Scope note:
  - this slice hardens the tracked proof surface for the current hosted Origin Dossier fallback route. It does not delete or normalize the still-untracked `AppShellBaseHrefTests.cs` file yet, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:25:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier clean-route CTA parity advanced again in the current tree:
  - updated `Chummer.Blazor/Components/Layout/DesktopShell.razor` so the install-claim shell button for `desktop-install-origin-dossier` now says `Open clean Origin Dossier route` instead of the shorter `Open Origin Dossier`, keeping that guarded desktop-shell affordance aligned with the routed shell notice and origin build handoff CTA.
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the public preview proof card still describes opening Origin Dossier directly in the browser, but the actionable link now says `Open clean Origin Dossier route`, matching the actual `/app?command=new_character_origin` destination.
  - updated `Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs` so the install-claim source proof now pins the clean-route CTA and explicitly rejects the removed shorter label on that shell surface.
  - updated `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` so the preview proof-card test now pins the clean-route dossier CTA and explicitly rejects the removed shorter link label while leaving the broader card heading alone.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Layout/DesktopShell.razor Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n "Open clean Origin Dossier route|Open Origin Dossier" Chummer.Blazor/Components/Layout/DesktopShell.razor Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
      - result: the touched install-claim and preview CTAs now use `Open clean Origin Dossier route`; the older shorter `Open Origin Dossier` wording remains only in the preview card heading and the new negative assertions within this touched proof set
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:44.84`
  - focused install/preview proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Preview_renders_explicit_boundary_banner_around_desktop_shell|FullyQualifiedName=Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests.Windows_install_link_gate_copy_stays_fail_closed_until_user_claims_online" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `985ms`
- Scope note:
  - this slice tightens the clean-route CTA wording on the install-claim shell and public preview proof card only. It does not rename the broader preview card heading, the native localized install-link window resource, or the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:17:24+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier `/workbench` fallback continuation copy hardened again in the current tree:
  - updated `Chummer.Blazor/Components/App.razor` so the origin-only compatibility fallback now says `Continue Origin Dossier on the clean route.` instead of `Continue Origin Dossier on Chummer Online.`
  - updated `Chummer.Blazor/Components/App.razor` so the origin-only result-panel anchor now says `Open clean Origin Dossier route`, while non-origin compatibility fallbacks still keep the generic `Continue this workflow on Chummer Online` label.
  - updated `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` so the hosted `/blazor/workbench?command=new_character_origin` and direct fallback-object proofs now pin the clean-route result text and the dossier-specific result-link label, and explicitly reject the generic fallback anchor wording on the origin route.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/App.razor Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'Continue Origin Dossier on Chummer Online|Continue Origin Dossier on the clean route|Open clean Origin Dossier route|Continue this workflow on Chummer Online' Chummer.Blazor/Components/App.razor Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
      - result: the origin-only compatibility fallback now uses the clean-route wording; the generic `Continue this workflow on Chummer Online` anchor remains only on non-origin fallback proofs
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:30.73`
  - focused workbench fallback proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppShellBaseHrefTests.Hosted_blazor_workbench_route_renders_ssr_fallback_shell_and_bootstrap_script|FullyQualifiedName=Chummer.Tests.Presentation.AppShellBaseHrefTests.Workbench_origin_dossier_query_builds_story_first_fallback_dialog" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `1.000s`
- Scope note:
  - this slice tightens the Origin Dossier compatibility-fallback continuation wording only. It does not rename the broader hosted-app product name elsewhere, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:12:26+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier build-handoff link-note copy hardened again in the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the `newCharacterOriginDossierLinkNotes` field now says `Opens the clean Origin Dossier route directly. The story text stays local until you publish it.` instead of describing a generic Chummer Online workflow, keeping the note aligned with the route CTA and shell-notice wording.
  - updated `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the origin build-handoff factory proof now pins `clean Origin Dossier route` in the link note and explicitly rejects the removed generic workflow sentence.
  - updated `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the rendered origin build pane now proves the clean-route link-note wording and rejects the removed generic workflow sentence.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'clean Origin Dossier route|Opens Chummer Online directly into the Origin Dossier workflow' Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: the touched origin build surfaces now use the clean-route note wording; the removed generic workflow sentence survives only in new negative assertions
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:33.66`
  - focused origin factory/component proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginBuildDialog_translates_origin_into_alice_guided_build_summary|FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `756ms`
- Scope note:
  - this slice tightens the Origin Dossier build-handoff link-note copy only. It does not rename other broader hosted-app wording, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:08:18+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier shell-notice route CTA hardened again in the current tree:
  - updated `Chummer.Blazor/Components/Layout/DesktopShell.razor` so the actionable shell notice for `Origin Dossier link: ...` now says `Open clean Origin Dossier route` instead of `Open Origin Dossier on Chummer Online`, aligning the desktop-shell notice affordance with the dedicated build-handoff route CTA.
  - updated `Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs` so the shell notice proof now pins the clean-route wording and explicitly rejects the removed broader-host wording.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Layout/DesktopShell.razor Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'Open Origin Dossier on Chummer Online|Open clean Origin Dossier route' Chummer.Blazor/Components/Layout/DesktopShell.razor Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs`
      - result: the touched shell-notice surface now uses only the clean-route dossier CTA; the removed broader-host wording survives only in the new negative assertion
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:36.50`
  - focused shell notice proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopShellStartupSyncTests.DesktopShell_origin_dossier_notice_renders_actionable_clean_route_affordance" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `827ms`
- Scope note:
  - this slice tightens the Origin Dossier shell-notice CTA only. It does not rename other broader hosted-app wording, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T17:05:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier build-handoff action copy hardened again in the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the `show_origin_dossier_link` action now renders as `Show Origin Dossier link` instead of the shorter generic `Show dossier link`, keeping the action label aligned with the surrounding `Origin Dossier Link` section.
  - updated `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the origin build-handoff factory proof now pins the dossier-specific action label.
  - updated `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the specialized rendered origin build pane now proves the visible `Show Origin Dossier link` action label and explicitly rejects the removed generic wording.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'Show dossier link|Show Origin Dossier link' Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: the touched origin build surfaces now use only the dossier-specific action label; the removed generic wording survives only in the new negative assertion
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:38.21`
  - focused origin factory/component proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginBuildDialog_translates_origin_into_alice_guided_build_summary|FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `803ms`
- Scope note:
  - this slice tightens the Origin Dossier build-handoff action label only. It does not rename the broader hosted app surface, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T16:59:39+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier build-handoff route CTA hardened again in the current tree:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the read-only origin dossier route action in the build-handoff pane now says `Open clean Origin Dossier route` instead of the generic `Open clean Chummer Online route`, keeping the CTA aligned with the surrounding `Origin Dossier Link` section and the route it actually opens.
  - updated `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the specialized origin wizard/build browser-pane proof now pins the dossier-specific CTA wording and explicitly rejects the removed generic route label.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'Open clean Chummer Online route|Open clean Origin Dossier route' Chummer.Blazor/Components/Shell/DialogHost.razor Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: the touched dialog-host surface now uses only the dossier-specific route CTA; the removed generic wording survives only in the new negative assertion
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:45.19`
  - focused origin component proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes|FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_keeps_origin_advanced_controls_open_across_dialog_rerenders|FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_keeps_origin_advanced_controls_open_across_multiple_origin_select_changes" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `863ms`
- Scope note:
  - this slice tightens the Origin Dossier build-handoff route CTA only. It does not rename the broader hosted app surface, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T16:55:06+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Public app startup-rail copy hardened again in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the generic non-origin `/app` and `/online` startup panel now uses `Return to Character Roster` instead of the shorter `Return to roster`, keeping the public app action rail aligned with the tighter roster wording already used on the Origin Dossier panel.
  - updated `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` so the existing app and `/online` output-route shell proofs now pin `Return to Character Roster`, keep `Open Build Lab`, and explicitly reject the removed shorter roster label across the shared generic startup-panel path.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n "Return to roster|Return to Character Roster|Open Build Lab" Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
      - result: the generic and origin app-route startup rails now both use `Return to Character Roster`; `Return to roster` survives only in the new negative assertions
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:15.40`
  - focused shared startup-panel proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_output_queries_render_specific_output_copy|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Online_alias_output_queries_render_specific_output_copy" --output Normal`
      - result: `8 total`, `8 succeeded`, `0 failed`, `0 skipped`, duration `1.236s`
- Scope note:
  - this slice tightens the generic public app startup-rail label only. It does not relabel the classic file-menu `New runner` affordances, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T16:49:36+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is now `Handoff refresh (2026-07-07T16:44:39+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier entry normalization hardened again in the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `CreateCommandDialog("new_character_origin", ...)` now lets the origin wizard apply dossier defaults instead of pre-injecting `New runner` / `Runner`.
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the origin wizard, origin build handoff, dossier route helper, and origin book preview all normalize inherited untouched `New runner` / `Runner` seeds into `New dossier` / `Dossier`.
  - extended `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the direct `new_character_origin` command path now proves dossier defaults without a profile seed, and stale runner-seed identity now proves normalization through the guided build handoff.
  - extended `Chummer.Tests/Presentation/DialogCoordinatorTests.cs` so the `Start Origin Dossier` action from the standard new-character dialog now proves untouched runner defaults are converted into dossier identity before the story-first wizard opens.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs Chummer.Tests/Presentation/DialogCoordinatorTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n "NormalizeOriginSeed|CreateCommandDialog_new_character_origin_defaults_to_dossier_identity_without_profile_seed|BuildNewCharacterOriginDialogs_normalize_runner_seed_identity_into_dossier_defaults|CoordinateAsync_start_from_origin_normalizes_untouched_runner_defaults_into_dossier_identity" Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs Chummer.Tests/Presentation/DialogCoordinatorTests.cs`
      - result: origin normalization now has explicit factory and coordinator proof coverage in the touched files
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:52.94`
  - focused origin entry/handoff proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_new_character_origin_defaults_to_dossier_identity_without_profile_seed|FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginWizardDialog_materializes_origin_seed_and_recommendation_fields|FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginDialogs_default_to_dossier_identity_when_no_seed_name_or_alias_is_provided|FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginDialogs_normalize_runner_seed_identity_into_dossier_defaults|FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginBuildDialog_translates_origin_into_alice_guided_build_summary|FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_start_from_origin_opens_origin_wizard_dialog|FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_start_from_origin_normalizes_untouched_runner_defaults_into_dossier_identity|FullyQualifiedName=Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_origin_wizard_generates_alice_build_translation_and_handoff" --output Normal`
      - result: `8 total`, `8 succeeded`, `0 failed`, `0 skipped`, duration `561ms`
- Scope note:
  - this slice hardens origin-entry default normalization only. It does not relabel the standard new-character `New runner` dialog defaults that current parity tests still pin, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T16:37:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is now `Handoff refresh (2026-07-07T16:22:50+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:release_ready`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier route-entry copy hardened again in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the public Origin Dossier startup panel now says `Start the story-first dossier path.` instead of `character path`.
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the public Origin Dossier action rail now uses `Use standard character creation` and `Return to Character Roster` instead of the shorter generic labels.
  - updated `Chummer.Blazor/Components/App.razor` so the workbench fallback summary for `new_character_origin` now says `Start the story-first dossier path for {runnerLabel}.` instead of `character path`.
  - updated `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` so the `/app` and `/online` Origin Dossier route proofs now pin the dossier-path heading, the tightened action labels, and the absence of the removed character-path copy.
  - updated `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` so the workbench fallback proof now pins the dossier-path summary string.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Pages/Preview.razor Chummer.Blazor/Components/App.razor Chummer.Tests/Presentation/AppRouteSurfaceTests.cs Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'story-first character path|story-first dossier path|Use standard character creation|Return to Character Roster' Chummer.Blazor/Components/Pages/Preview.razor Chummer.Blazor/Components/App.razor Chummer.Tests/Presentation/AppRouteSurfaceTests.cs Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
      - result: only dossier-path wording remains in the touched source; the removed character-path wording survives only in the new negative assertions
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:45.22`
  - focused route/fallback proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.App_origin_dossier_command_opens_dossier_builder_without_falling_back_to_roster|FullyQualifiedName=Chummer.Tests.Presentation.AppRouteSurfaceTests.Online_alias_origin_dossier_command_opens_dossier_builder_without_falling_back_to_roster|FullyQualifiedName=Chummer.Tests.Presentation.AppShellBaseHrefTests.Workbench_origin_dossier_query_builds_story_first_fallback_dialog" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `1.375s`
- Scope note:
  - this slice hardens Origin Dossier route-entry wording on the public app route and workbench fallback only. It does not relabel the broader `New runner` standard creation affordances that current parity/public preview tests still pin, and it does not affect the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T16:32:53+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is now `Handoff refresh (2026-07-07T16:20:19+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:release_ready`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Origin Dossier copy hardening advanced in the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so direct `BuildNewCharacterOriginWizardDialog(...)` entry now seeds hidden default identity fields as `New dossier` and `Dossier` instead of the older runner wording.
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the generated origin story summary now says the background/turning-point/training path pushed `this dossier path` toward the chosen work instead of `this runner`.
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the guided build handoff generated from an origin wizard now carries `New dossier` / `Dossier` defaults when no explicit seed name or alias exists.
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the origin book preview fallback now starts with `Dossier: Origin Dossier` instead of `Runner: Origin Dossier` when no alias seed is present.
  - updated `Chummer.Presentation/Overview/DialogCoordinator.cs` so the origin-build action fallback that opens guided character creation stays aligned with the new dossier defaults if workflow name/alias fields are absent.
  - extended `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so origin factory proofs now reject `this runner` in the visible origin summary and directly pin the dossier defaults plus the dossier-facing book preview across both the origin wizard and the origin build handoff.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Presentation/Overview/DialogCoordinator.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
      - result: clean
  - follow-up search:
    - `rg -n 'this runner|New dossier|Dossier"' Chummer.Presentation/Overview/DesktopDialogFactory.cs Chummer.Presentation/Overview/DialogCoordinator.cs Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
      - result: the touched origin factory/coordinator surfaces now carry dossier defaults; `this runner` survives only in the new negative assertions
  - incremental test build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:26.41`
      - caveat: this fast-path build alone was not authoritative for the changed presentation dependency
  - reference-aware verification build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:08.88`
  - focused origin factory proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginWizardDialog_materializes_origin_seed_and_recommendation_fields|FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginDialogs_default_to_dossier_identity_when_no_seed_name_or_alias_is_provided|FullyQualifiedName=Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginBuildDialog_translates_origin_into_alice_guided_build_summary" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `605ms`
- Scope note:
  - this slice hardens direct Origin Dossier defaults and generated origin summary copy only. It does not change the standard new-character dialog defaults, the broader classic-shell `New runner` route labels that existing parity tests still pin, or the shared external release blockers.

## Cross-Codex Refresh (2026-07-07T16:19:28+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is still `Handoff refresh (2026-07-07T16:04:26+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:final_gold_janitor`
  - `release_truth:release_ready`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Portal/OpenAPI route copy hardened in the current tree:
  - updated `Chummer.Portal/Program.cs` so the `/blazor/` OpenAPI route summary now says `Open the hosted Blazor browser entry that resolves into Chummer Online` instead of the older `stable` wording.
  - updated `Chummer.Portal/Program.cs` so the docs explorer route-family label for `/blazor/` now renders as `Hosted browser entry` instead of `Stable browser entry`.
  - updated `Chummer.Tests/Presentation/PortalAppRouteContractTests.cs` so the existing route-registry source contract now pins the hosted wording and explicitly rejects the removed stable wording.
  - updated `docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md` and `scripts/e2e-portal.cjs` so the route-family documentation and portal probe stay aligned with the new hosted label.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Portal/Program.cs Chummer.Tests/Presentation/PortalAppRouteContractTests.cs docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md scripts/e2e-portal.cjs`
      - result: clean
  - focused probe-contract pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_blazor_portal_route_probe_contract.py`
      - result: `8 passed in 0.02s`
  - incremental test build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:10.38`
  - targeted portal source-contract proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.PortalAppRouteContractTests.Portal_program_keeps_clean_public_app_route_in_openapi_and_route_registry" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `796ms`
  - follow-up searches:
    - `rg -n "Stable browser entry|stable Blazor browser entry" Chummer.Portal/Program.cs Chummer.Tests/Presentation/PortalAppRouteContractTests.cs docs/BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md scripts/e2e-portal.cjs`
      - result: the removed stable wording survives only inside the new negative assertions in `PortalAppRouteContractTests.cs`
    - `rg -n '\bstable\b' --glob '!Chummer.Tests/**' --glob '!tests/**' --glob '!docs/**' --glob '!scripts/**' --glob '!**/bin/**' --glob '!**/obj/**' Chummer.Blazor Chummer.Portal Chummer.Presentation Chummer.Avalonia Chummer.Api`
      - result: remaining source hits are limited to technical metadata or non-release semantics such as `releaseChannel: "stable"` signing metadata, relationship/faction status wording, and internal diagnostic comments/messages
- Scope note:
  - this slice hardens portal route-contract copy and its direct probe/source proofs only. It does not change the shared external release blockers, the public downloads lane, or the unresolved Windows visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T16:10:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- The latest canonical external handoff section is `Handoff refresh (2026-07-07T16:04:26+02:00)`. Treat its blocker set as current release truth:
  - `release_posture:non_flagship_channel`
  - `release_truth:final_gold_janitor`
  - `release_truth:release_ready`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable, release-ready, or flagship-ready while any root blocker remains, especially while the Windows visual-audit proof bundle is still missing from `/docker/chummercomplete/chummer.run-services/.state/incoming_windows_installer_gold_proof/windows-installer-gold-proof-80655fd79a09.zip`.
- Runtime-inspector clean-state copy hardened in the current tree:
  - updated `Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor` so the Rule Profile badge now says `current` instead of `stable` when attention count is zero.
  - updated `Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor` so the System details clean diff badge now says `no diff` instead of `stable` when migration, compatibility, and warning counts are all zero.
  - extended `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` with `RuntimeInspectorPanel_uses_current_and_no_diff_badges_for_clean_local_diagnostics()` so the clean SR6 runtime projection now proves the new badge copy and explicitly rejects the removed `stable` badge text while still rendering the environment-diff rail.
- Verification completed for this slice:
  - touched-file hygiene:
    - `git diff --check -- Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: clean
  - targeted test discovery:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --list-tests | rg "RuntimeInspectorPanel"`
      - result: both runtime-inspector presentation tests are present in the built assembly, including the new clean-state badge proof
  - component build with project references:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:16.61`
  - reference-aware test build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:33.73`
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.RuntimeInspectorPanel_renders_rule_profile_and_rulepack_diagnostics_surfaces|FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.RuntimeInspectorPanel_uses_current_and_no_diff_badges_for_clean_local_diagnostics" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `4.295s`
  - follow-up search:
    - `rg -n '\bstable\b' Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
      - result: no remaining `stable` source text in the runtime-inspector component; the only remaining match in the touched test file is the new negative assertion
- Scope note:
  - this slice hardens runtime-inspector clean-state copy and its direct Blazor proof only. It does not change the shared external release blockers, the publish lane, or the unresolved Windows visual-audit blocker.
- Verification caveat for future slices:
  - `BuildProjectReferences=false` is not a trustworthy verification path for Blazor component edits in this repo because `Chummer.Tests/bin` can keep a stale copied `Chummer.Blazor.dll`. For component/UI slices, refresh the referenced project or rebuild `Chummer.Tests` with project references enabled before trusting direct test-assembly runs.

## Cross-Codex Refresh (2026-07-07T15:56:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- UI/copy polish advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Showcase.razor` so the public build-lab team-coverage copy now says `Coverage score stays grounded with Face and Legwork already covered before the first campaign handoff.` instead of the older `stays stable` wording.
  - extended `Chummer.Tests/Presentation/CampaignSpineShowcaseComponentTests.cs` and `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the rendered build-lab coverage rail now pins the new grounded wording and explicitly rejects the removed stable phrase.
  - updated `Chummer.Tests/Presentation/WorkspaceSectionRendererTests.cs` so the workspace-section projection test now asserts the grounded coverage summary directly instead of only carrying it incidentally inside fixture payload data.
- Verification completed for this slice:
  - focused presentation proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Presentation.CampaignSpineShowcaseComponentTests.BuildLabPanel_renders_decision_rail_and_watchouts|FullyQualifiedName=Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane_renders_build_lab_projection_from_contract_payload|FullyQualifiedName=Chummer.Tests.Presentation.WorkspaceSectionRendererTests.RenderSectionAsync_projects_build_lab_state_from_contract_payload" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `853ms`
  - incremental test build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:02.91`
  - follow-up search:
    - `rg -n 'Coverage score stays stable with Face and Legwork already covered|Coverage score stays grounded with Face and Legwork already covered' Chummer.Blazor/Components/Pages/Showcase.razor Chummer.Tests/Presentation/CampaignSpineShowcaseComponentTests.cs Chummer.Tests/Presentation/BlazorShellComponentTests.cs Chummer.Tests/Presentation/WorkspaceSectionRendererTests.cs`
      - result: only grounded wording remains in source payloads; the stable wording survives only inside the new negative assertions
- Scope note:
  - this slice is copy polish for the public build-lab team-coverage surface and its direct render/projection proofs only. It does not change the shared external release blockers, the intentionally external release-matrix lane, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:45:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Release-script portability advanced again in the current tree:
  - updated `scripts/materialize-linux-desktop-exit-gate.sh`, `scripts/materialize-macos-desktop-exit-gate.sh`, and `scripts/materialize-windows-desktop-exit-gate.sh` so `CHUMMER_UI_REPO_ROOT_ALIAS` now defaults to the script-resolved `REPO_ROOT_PHYSICAL` instead of the legacy `/docker/chummercomplete/chummer6-ui` checkout path.
  - updated the milestone script family `scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh`, `materialize-desktop-visual-familiarity-exit-gate.sh`, `materialize-desktop-workflow-execution-gate.sh`, `chummer5a-desktop-workflow-parity-check.sh`, `chummer5a-screenshot-review-gate.sh`, `sr4-desktop-workflow-parity-check.sh`, `sr6-desktop-workflow-parity-check.sh`, and `b14-flagship-ui-release-gate.sh` so their repo-alias fallback now resolves from `repo_root_physical` instead of the old canonical checkout name.
  - updated the embedded Python helper inside `materialize-desktop-executable-exit-gate.sh` so its repo-root fallback now uses `Path.cwd()` after the shell has already `cd`'d into the active repo, instead of falling back to the old absolute checkout path.
  - extended `tests/test_desktop_executable_exit_gate_contract.py` and `Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs` so the repo-alias portability guard now pins the new repo-root-derived fallback across the desktop gate script family and explicitly rejects the removed legacy checkout path.
- Verification completed for this slice:
  - focused Python contract + bash portability pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_executable_exit_gate_contract.py tests/test_desktop_exit_gate_bash_portability.py tests/test_release_gate_milestone_bash_portability.py`
      - result: `8 passed in 0.04s`
  - incremental test build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:52.83`
  - targeted compliance proof:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.DesktopExecutableGateComplianceTests.Windows_and_macos_exit_gate_materializers_do_not_resolve_proof_from_legacy_chummer5a_paths" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `483ms`
  - follow-up searches:
    - `rg -n '/docker/chummercomplete/chummer6-ui' scripts/materialize-linux-desktop-exit-gate.sh scripts/materialize-macos-desktop-exit-gate.sh scripts/materialize-windows-desktop-exit-gate.sh scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh scripts/ai/milestones/chummer5a-desktop-workflow-parity-check.sh scripts/ai/milestones/chummer5a-screenshot-review-gate.sh scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh scripts/ai/milestones/b14-flagship-ui-release-gate.sh tests/test_desktop_executable_exit_gate_contract.py Chummer.Tests/Compliance/DesktopExecutableGateComplianceTests.cs`
      - result: only the new negative assertions in `tests/test_desktop_executable_exit_gate_contract.py` still match
- Scope note:
  - this slice hardens repo-local alias-root portability across the desktop gate/parity scripts only. It does not change the shared external release blockers, the intentionally external release-matrix guard lane, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:38:02+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Presentation-lane hardening advanced in two repo-local slices:
  - updated `Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`, `Chummer.Tests/ExternalHostProofBlockersTests.cs`, and `Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs` so the presentation/Avalonia test helpers now resolve source and fixture files from the active repo root and known search roots instead of falling back to legacy absolute checkout paths.
  - added a direct architecture guard that now rejects those legacy checkout roots in the presentation test helpers and external host proof blocker test source.
  - updated `Chummer.Blazor/Components/Layout/DesktopShell.Flagship.cs` so the empty-state marquee copy no longer says `claiming flagship continuity`; it now uses neutral dossier continuity wording.
  - extended `Chummer.Tests/Presentation/DesktopClaimCopyLanguageTests.cs` so shell copy now has a direct source guard that rejects `flagship continuity` claim language.
- Verification completed for this slice:
  - incremental test build after the presentation-helper portability changes:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:55.09`
  - focused portability/source-path proofs:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName=Chummer.Tests.Compliance.ArchitectureGuardrailTests.Blazor_head_exposes_health_endpoint|FullyQualifiedName=Chummer.Tests.Compliance.ArchitectureGuardrailTests.Presentation_test_helpers_do_not_hardcode_legacy_checkout_roots|FullyQualifiedName=Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Blazor_root_route_ownership_stays_with_desktop_shell_anchor_and_moves_showcase_off_root|FullyQualifiedName=Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Avalonia_startup_keeps_the_workbench_as_first_paint_but_still_invokes_restore_continuation_when_needed|FullyQualifiedName=Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head" --output Normal`
      - result: `5 total`, `5 succeeded`, `0 failed`, `0 skipped`, duration `16.744s`
  - incremental test build after the shell-copy wording change:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:12.42`
  - focused copy-language proofs:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopClaimCopyLanguageTests" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `1.796s`
  - follow-up searches:
    - `rg -n '/docker/chummercomplete/chummer-presentation|/docker/chummercomplete/chummer6-ui|/docker/chummercomplete/chummer6-ui-finish' Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs Chummer.Tests/ExternalHostProofBlockersTests.cs Chummer.Tests/Compliance/ArchitectureGuardrailTests.cs`
      - result: only the new negative guard assertions in `ArchitectureGuardrailTests.cs` still match
    - `rg -n 'claiming flagship continuity|flagship continuity' Chummer.Blazor/Components/Layout/DesktopShell.Flagship.cs Chummer.Tests/Presentation/DesktopClaimCopyLanguageTests.cs`
      - result: only the new negative guard assertions in `DesktopClaimCopyLanguageTests.cs` still match
- Scope note:
  - this pass hardens repo-local presentation tests and browser-shell copy only. It does not change the shared external release blockers, the alias-based desktop gate root logic, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:30:00+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Repo-local manifest portability advanced again in the current tree:
  - updated `scripts/generate-releases-manifest.sh` so the UI localization release-gate generator/path helpers now prefer the current checkout, the adjacent `../chummer6-ui` checkout, and `PRESENTATION_MIRROR_ROOT` instead of silently falling back to the legacy absolute `/docker/chummercomplete/chummer-presentation` and `/docker/chummercomplete/chummer6-ui` proof paths.
  - extended `tests/test_desktop_downloads_local_release_policy.py` so the local release-policy guard now pins those repo-local/configured UI localization roots and explicitly rejects the removed absolute fallback paths.
- Verification completed for this slice:
  - focused Python release-policy + portability pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_downloads_local_release_policy.py tests/test_release_gate_milestone_bash_portability.py tests/test_public_windows_payload_metadata.py`
      - result: `34 passed in 0.68s`
  - focused bash portability pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_day1_setup_bash_portability.py tests/test_desktop_exit_gate_bash_portability.py tests/test_startup_smoke_bash_portability.py`
      - result: `4 passed in 0.14s`
  - follow-up search:
    - `rg -n '/docker/chummercomplete/chummer-presentation|/docker/chummercomplete/chummer6-ui' scripts/generate-releases-manifest.sh tests/test_desktop_downloads_local_release_policy.py`
      - result: only the negative guard assertions in `tests/test_desktop_downloads_local_release_policy.py` still match
- Scope note:
  - this slice hardens only the repo-local UI localization release-gate fallback path selection inside the manifest generator. It does not change the shared external release blockers, the alias-based desktop exit-gate root logic, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:22:52+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Additional branch-local portability hardening landed in the current tree:
  - updated `scripts/generate-releases-manifest.sh` so `PRESENTATION_MIRROR_ROOT` now defaults to `"$REPO_ROOT"` instead of `/docker/chummercomplete/chummer-presentation`; the presentation-downloads mirror sync is now repo-local by default and only mirrors elsewhere when explicitly pointed at a different checkout.
  - extended `tests/test_desktop_downloads_local_release_policy.py` so the manifest generator policy guard now pins the repo-local mirror default and explicitly rejects the old sibling-checkout default.
  - updated `scripts/ai/run_codex.sh` and `scripts/ai/run_codex_resume.sh` so both wrappers resolve `REPO_ROOT` from `SCRIPT_DIR` and source `scripts/ai/_env.sh` from the current checkout instead of `cd`-ing into the sibling canonical repo.
  - added `tests/test_ai_wrapper_repo_portability.py` so the wrapper entrypoints now have a direct repo-root portability guard.
- Verification completed for this slice:
  - local release/download policy pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_downloads_local_release_policy.py tests/test_release_shell_array_portability.py tests/test_public_windows_payload_metadata.py tests/test_windows_installer_payload_gate.py`
      - result: `50 passed in 11.04s`
  - wrapper portability guard:
    - `python3 -m pytest -q --import-mode=importlib tests/test_ai_wrapper_repo_portability.py`
      - result: `1 passed in 0.06s`
  - follow-up search:
    - `rg -n 'cd "/docker/chummercomplete/chummer-presentation"|REPO_ROOT="\$\(cd "\$SCRIPT_DIR/../.." && pwd\)"|source "\$REPO_ROOT/scripts/ai/_env.sh"' scripts/ai/run_codex.sh scripts/ai/run_codex_resume.sh`
      - result: only the repo-relative `REPO_ROOT` and `source "$REPO_ROOT/scripts/ai/_env.sh"` matches remain
- Scope note:
  - this slice hardens only repo-local manifest/wrapper portability. It does not change the shared external release-matrix lane, shared blocker receipts, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:18:52+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Release-gate shell fallback portability advanced in the current tree:
  - updated `scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh`, `scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh`, and `scripts/ai/milestones/b14-flagship-ui-release-gate.sh` so each now uses the repo-local portal mirror path `"$repo_root/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json"` instead of hard-wiring the sibling `/docker/chummercomplete/chummer-presentation/...` path.
  - extended `tests/test_desktop_executable_exit_gate_contract.py` so the Python guard now pins the repo-local fallback path across all three scripts and explicitly rejects the old sibling-root path.
  - updated `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so the compliance guard now expects the repo-local portal release-channel fallback string in the executable gate materializer.
- Verification completed for this slice:
  - focused Python contract:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_executable_exit_gate_contract.py`
      - result: `3 passed in 0.20s`
  - incremental test build:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:46.14`
  - targeted compliance proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Desktop_executable_exit_gate_prefers_registry_release_truth_with_repo_local_fallback_and_counts_macos_dmg_media" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `596ms`
  - follow-up search:
    - `rg -n '/docker/chummercomplete/chummer-presentation/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json|\$repo_root/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json' scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh scripts/ai/milestones/b14-flagship-ui-release-gate.sh`
      - result: only the repo-local `"$repo_root/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json"` matches remain
- Scope note:
  - this slice hardens only repo-local release-gate shell fallback portability and its direct guards; it does not change shared blocker receipts, the external release-matrix lane, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:11:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Repo-root portability advanced across the repo-local Python contract/docs guards:
  - updated `tests/test_downloads_publication_scope.py`, `tests/test_windows_bootstrap_download_smoke_contract.py`, `tests/test_desktop_external_deploy_readiness.py`, `tests/test_windows_installer_update_handoff_gate.py`, `tests/test_chummer5a_parity_tester.py`, `tests/test_desktop_executable_exit_gate_contract.py`, and `tests/test_chummer_flagship_docs_generator.py` so each now resolves `REPO_ROOT` from `Path(__file__).resolve().parents[1]` instead of hard-wiring `/docker/chummercomplete/chummer-presentation`.
  - this keeps the repo-local release/download/update/docs contract coverage attached to the current checkout instead of passing only because the sibling canonical repo happens to exist on disk.
  - the only remaining Python absolute-path hit under `tests/` is `tests/test_desktop_release_matrix_gate.py`, which still intentionally points at the shared external release-matrix lane rather than this repo-local branch lane.
- Verification completed for this slice:
  - focused repo-root portability pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_downloads_publication_scope.py tests/test_windows_bootstrap_download_smoke_contract.py tests/test_desktop_external_deploy_readiness.py tests/test_windows_installer_update_handoff_gate.py tests/test_chummer5a_parity_tester.py tests/test_desktop_executable_exit_gate_contract.py tests/test_chummer_flagship_docs_generator.py`
      - result: `32 passed in 5.10s`
  - follow-up search:
    - `rg -n "/docker/chummercomplete/chummer-presentation" tests -g '*.py'`
      - result: only `tests/test_desktop_release_matrix_gate.py` still matches
- Scope note:
  - this slice hardens only repo-local test portability for release/download/update/docs guards; it does not change the shared external release-matrix lane, shared blocker receipts, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T15:08:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Repo-local downloads shelf parity and payload-gate hardening advanced in the current tree:
  - updated `tests/test_public_windows_payload_metadata.py` so the payload metadata contract now resolves the current repo root with `Path(__file__).resolve().parents[1]` instead of hard-wiring the sibling `/docker/chummercomplete/chummer-presentation` checkout.
  - the same test now validates this repo’s own `Docker/Downloads` canonical snapshot and this repo’s own `Chummer.Portal/downloads` mirror, which keeps the design/product lane isolated from unrelated shared-repo drift while still enforcing local payload metadata truth.
  - resynced `Chummer.Portal/downloads/releases.json`, `Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json`, and the published installer/payload files under `Chummer.Portal/downloads/files/` to the current canonical `Docker/Downloads` snapshot so the repo-local public shelf matches the current promoted bundle metadata again.
- Verification completed for this slice:
  - focused payload gate:
    - `python3 /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/scripts/verify-windows-installer-payloads.py --files-dir /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Portal/downloads/files --manifest /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Portal/downloads/releases.json --manifest /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json --require-embedded-bootstrap-metadata --require-manifest-row`
      - result: `windows_installer_payload_gate:ok checked=1`
  - focused Python contract/runtime pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_public_windows_payload_metadata.py tests/test_portal_release_shelf_runtime.py tests/test_windows_installer_payload_gate.py tests/test_blazor_public_edge_execution_contract.py tests/test_blazor_pwa_contract.py`
      - result: `39 passed in 43.53s`
- Scope note:
  - this slice hardens only the current repo’s downloads manifests/tests and the repo-local portal shelf mirror; it does not change shared run-services blocker truth, shared public downloads truth, or the unresolved Windows visual-audit release blocker.

## Cross-Codex Refresh (2026-07-07T14:58:00+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Public/browser claim-copy hardening advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Home.razor` and `Chummer.Blazor/Components/Shared/BuildLabHandoffPanel.razor` so the dossier-card summary now says `Persistent dossier identity.` instead of the release-adjacent `Stable dossier identity.`
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the Character Roster helper now says `Keep a campaign roster pinned.` and the release-channel helper now says `Surface published and preview channel posture.` instead of the old stable-claim wording.
  - updated `Chummer.Blazor/Components/Pages/Showcase.razor` plus the direct BUnit proofs in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` and `Chummer.Tests/Presentation/CampaignSpineShowcaseComponentTests.cs` so home, workbench, handoff, and showcase renders pin the new copy and explicitly reject the removed stable phrases.
- Verification completed for this slice:
  - baseline portability and route-contract pack:
    - `python3 -m pytest -q --import-mode=importlib tests/test_day1_setup_bash_portability.py tests/test_desktop_exit_gate_bash_portability.py tests/test_release_gate_milestone_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_workflow_family_execution_receipts_contract.py tests/test_blazor_portal_route_probe_contract.py`
      - result: `17 passed in 0.60s`
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:13.33`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:33.92`
  - focused public/home/workbench/showcase proof pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Home_renders_truthful_public_navigation_and_browser_desktop_boundaries|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_route_renders_product_facing_browser_entrypoint_with_preview_link|FullyQualifiedName~Chummer.Tests.Presentation.CampaignSpineShowcaseComponentTests.BuildLabHandoffPanel_renders_dossier_and_campaign_outputs|FullyQualifiedName~Chummer.Tests.Presentation.CampaignSpineShowcaseComponentTests.Showcase_renders_build_lab_rules_and_creator_showcase_panels" --output Normal`
      - result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `3s 886ms`
- Scope note:
  - this slice removes repo-local stable/readiness phrasing from public browser surfaces and direct proof cards only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:34:57+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- File-intake dossier parity and portal route-proof alignment advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the workbench file-intake helper now says `Review dossier XML in the browser` and `Paste or stage Chummer dossier XML.` instead of stale runner-XML wording.
  - refreshed the direct `/workbench` proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` so the browser entrypoint test now pins the dossier-XML intake wording and explicitly rejects the old runner-XML strings.
  - updated `scripts/e2e-portal-playwright.cjs` and `tests/test_blazor_portal_route_probe_contract.py` so the portal route-probe contract now tracks the current workbench markers `Import dossier XML` and `Saved Dossiers` instead of the stale `Import runner XML` / `Saved Runners` expectations.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:51.81`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:58.93`
  - focused workbench proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_route_renders_product_facing_browser_entrypoint_with_preview_link" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `2s 616ms`
  - portal route-probe checks:
    - `node --check /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/scripts/e2e-portal-playwright.cjs`
      - result: parse clean
    - `python3 -m pytest -q --import-mode=importlib tests/test_blazor_portal_route_probe_contract.py`
      - result: `8 passed in 0.02s`
- Scope note:
  - this slice strengthens repo-local workbench file-intake wording and portal route-proof contracts only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:30:22+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- App-route sample-dossier entrypoint parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the app-route file-menu action and roster-header action now say `Open sample dossier` instead of the stale generic `Open example` label for the seeded/example browser route.
  - refreshed the direct app-route proofs in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` and `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` so both suites now pin the `Open sample dossier` file-menu label, confirm the roster action strip includes the same wording, and explicitly reject `Open example`.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:51.83`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:02.04`
  - focused app-route proof pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.App_route_renders_character_roster_without_preview_scaffolding|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_renders_character_roster_without_preview_scaffolding" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `2s 594ms`
- Scope note:
  - this slice strengthens repo-local app-route example-entry wording and direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:24:22+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Sample-dossier onboarding parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the workbench first-run lane now says `try a sample dossier`, the shortcut label says `Sample dossier`, the supporting note says `Open a guided example dossier.`, and the sample roster link also now says `Sample dossier` instead of stale runner wording.
  - refreshed the direct `/workbench` proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` so the browser entrypoint test now pins the sample-dossier onboarding/roster wording and explicitly rejects `sample runner` / `guided example runner` copy.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:15.59`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:46.23`
  - focused workbench proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_route_renders_product_facing_browser_entrypoint_with_preview_link" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `1s 203ms`
- Scope note:
  - this slice strengthens repo-local workbench onboarding/roster wording and direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:20:09+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Seeded-dossier preview and workbench parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the workbench starter card and preview proof cards now say `Open a live seeded dossier`, `Seed Build Lab with a real SR5 dossier`, `Open the same seeded dossier on Rules`, and the print/export/save proof summaries now consistently refer to the published `BLUE` dossier instead of stale runner wording.
  - refreshed the direct proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` so the default `/preview` render now pins the seeded dossier wording across the Build Lab, Rules, and save/export proof cards, while the `/workbench` render keeps the seeded dossier heading proof without incorrectly asserting no-session descriptions on a restored-session route.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:04.47`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:03.37`
  - focused preview/workbench proof pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Preview_renders_explicit_boundary_banner_around_desktop_shell|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_route_renders_product_facing_browser_entrypoint_with_preview_link" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `1s 184ms`
- Scope note:
  - this slice strengthens repo-local seeded-dossier preview/workbench wording and direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:12:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Public home and dossier-card handoff parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Home.razor` so the public hero now says `Chummer Online for real dossier work.` and the home-route dossier-card projection summary now says `Stable dossier identity.`
  - updated `Chummer.Blazor/Components/Shared/BuildLabHandoffPanel.razor` and `Chummer.Blazor/Components/Pages/Showcase.razor` so the build-lab/showcase dossier-card summaries now say `Stable dossier identity.` and `Stable dossier identity with campaign continuity attached.` instead of stale runner wording.
  - refreshed the direct proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` and `CampaignSpineShowcaseComponentTests.cs` so the home render, isolated build-lab handoff panel, and showcase page now pin the dossier-facing hero/projection wording.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:17.50`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:37.76`
  - focused public/showcase proof pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Home_renders_truthful_public_navigation_and_browser_desktop_boundaries|FullyQualifiedName~Chummer.Tests.Presentation.CampaignSpineShowcaseComponentTests.BuildLabHandoffPanel_renders_dossier_and_campaign_outputs|FullyQualifiedName~Chummer.Tests.Presentation.CampaignSpineShowcaseComponentTests.Showcase_renders_build_lab_rules_and_creator_showcase_panels" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `1s 201ms`
- Scope note:
  - this slice strengthens repo-local public home and dossier-card handoff wording only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:08:27+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Preview proof-card and density dossier parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the workbench density helper now says `maximum dossier context`, the Complex Forms preview card says `seeded dossier`, and the Contacts continuity card now says `Keep dossier context on Contacts` with `seeded browser dossier` wording instead of stale runner phrasing.
  - extended the direct preview-surface proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` so the default `/preview` render now pins the technomancer and Contacts dossier-facing copy while explicitly rejecting the stale `Keep runner context on Contacts` wording, and the `/workbench` render now pins the density helper plus explicitly rejects `maximum runner context`.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:16.38`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:01.62`
  - focused preview/workbench proof pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Preview_renders_explicit_boundary_banner_around_desktop_shell|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_route_keeps_dossier_copy_for_context_and_search_shortcuts" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `2s 281ms`
- Scope note:
  - this slice strengthens repo-local preview proof-card and workbench density wording only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T11:01:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- MDI-strip dossier parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Shell/MdiStrip.razor` so the per-workspace close affordance now uses `Close dossier` instead of the stale `Close runner` label.
  - extended the existing direct BUnit proof in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the unsaved-workspace strip test now also pins the close-button `title` and `aria-label` as `Close dossier`.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:10.22`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:04.37`
  - focused MDI-strip proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.MdiStrip_shows_unsaved_marker_for_workspace_without_save_receipt" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `794ms`
- Scope note:
  - this slice strengthens repo-local MDI-strip shell wording and direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:58:08+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Public route and hosted fallback settings/workspace guidance parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the shared `/app`, `/online`, and `/workbench` route guidance now says `dossier defaults`, `open dossier`, and `requested dossier context` instead of the stale runner phrasing across settings copy, blocked startup guidance, and workspace continuation summaries.
  - updated `Chummer.Blazor/Components/App.razor` so the hosted workbench fallback summaries for `global_settings` and `character_settings` now say `dossier defaults` and `creating and validating dossiers`.
  - refreshed the direct route/fallback proof in `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`, `PublicPreviewSurfaceTests.cs`, and `AppShellBaseHrefTests.cs` so the route matrix now pins the dossier-facing settings fragments, blocked startup guidance, and route continuation wording across the public app, `/online` alias, compatibility route, and SSR fallback query path.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:22.11`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:32.92`
  - focused route/fallback proof pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Public_tool_commands_open_shared_shell_without_falling_back_to_roster|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Public_workspace_gated_startup_routes_render_blocked_copy_without_dispatching_command|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_workspace_tab_queries_render_specific_workflow_shell_copy|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Public_app_runner_intelligence_control_routes_render_stats_shells_and_handle_controls|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_tool_commands_render_specific_shell_copy|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_workspace_gated_startup_routes_render_blocked_copy_without_dispatching_command|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Workbench_tool_command_queries_publish_specific_tool_workflow_identity" --output Normal`
      - result: `151 total`, `151 succeeded`, `0 failed`, `0 skipped`, duration `3s 440ms`
- Scope note:
  - this slice strengthens repo-local route-shell and hosted fallback wording only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:50:01+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Startup-workbench dossier parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Shell/SectionPane.razor` so the intro note now says `Start a fresh dossier, reopen a saved dossier, or jump straight into classic utilities from Chummer Online.` instead of mixing a stale runner noun into the dossier-facing startup surface.
  - extended the existing direct BUnit proof in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the startup workbench now pins that full dossier-facing intro sentence and explicitly rejects the old `Start a fresh runner` wording while leaving the intentional `New runner` command labels untouched.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:19.75`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:31.22`
  - focused startup-workbench proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane_renders_startup_workbench_with_first_class_restore_and_utility_actions" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `823ms`
- Scope note:
  - this slice strengthens repo-local startup-workbench intro wording and direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:47:46+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Origin Dossier dialog-host parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the advanced Origin Dossier note now says `Optional dossier identity ...`, the wizard subpanel heading is `Dossier` instead of `Runner`, and the build-handoff summary metric is also labeled `Dossier`.
  - extended the existing direct BUnit proof in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the origin wizard now pins the dossier note plus the ordered `Dossier` / `Life Path` / `GM Steering` headings, while the build handoff pins the ordered `Dossier` / `Ruleset` / `Method` summary labels and explicitly rejects stale `Runner` text in the rendered build surface.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:03.71`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:45.06`
  - focused dialog-host proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `818ms`
- Scope note:
  - this slice strengthens repo-local Origin Dossier dialog-host wording and direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:39:34+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop shell title fallback parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Layout/DesktopShell.razor` so the user-facing shell title fallback now says `Dossier` instead of the stale `Runner` label when no profile alias/name is loaded.
  - extended the existing direct desktop-shell proof in `Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs` so it now also pins that `Dossier` fallback alongside the already-verified dossier skip-link and readiness labels.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:55.41`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:06.24`
  - focused desktop-shell proof:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellRulesetCatalogTests.DesktopShell_uses_dossier_navigation_copy_for_skip_link_and_readiness_summary" --output Normal`
      - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `1s 014ms`
- Scope note:
  - this slice strengthens repo-local desktop shell title fallback wording only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:34:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop shell accessibility/readiness parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Layout/DesktopShell.razor` so both shell branches now use the dossier-facing skip-link copy `Skip to dossier`, and the active shell readiness rail now labels the open-workspace counter as `Dossiers` instead of `Runners`.
  - added direct BUnit proof in `Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs` so the normal shell branch now pins the dossier skip-link text plus the `Dossiers` readiness label and `1 open` summary.
  - refreshed `Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs` so the install-claim shell source guard now requires `Skip to dossier` and explicitly rejects `Skip to runner`.
  - refreshed `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so the phase-4 shell suite presence gate now requires that new desktop-shell proof.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:25.62`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:38.64`
  - focused desktop-shell/compliance pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellRulesetCatalogTests.DesktopShell_uses_dossier_navigation_copy_for_skip_link_and_readiness_summary|FullyQualifiedName~Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests.Windows_install_link_gate_copy_stays_fail_closed_until_user_claims_online|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Blazor_shell_component_suite_is_present_for_phase4_gate" --output Normal`
      - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `1s 039ms`
- Scope note:
  - this slice strengthens repo-local desktop shell accessibility/readiness copy only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:29:23+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Result-panel dossier parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Shell/ResultPanel.razor` so the browser save receipt now says `Dossier:` and `This dossier is saved and ready to reopen.` instead of the stale runner wording.
  - added direct BUnit proof in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the saved-browser receipt now requires the dossier label, the saved dossier id, and the dossier-facing status copy while explicitly rejecting the old runner phrases.
  - refreshed `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so the phase-4 shell suite presence gate now requires that new result-panel proof.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:19.34`
    - initial `Chummer.Tests` incremental build before the test-state fix:
      - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:30.82`
    - final `Chummer.Tests` rebuild after fixing the direct test state to set `HasSavedWorkspace = true`:
      - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:37.22`
  - focused result/compliance pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.ResultPanel_save_receipt_uses_dossier_copy_when_workspace_is_saved|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Blazor_shell_component_suite_is_present_for_phase4_gate" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `1s 362ms`
- Scope note:
  - this slice strengthens repo-local result-panel save receipt wording and its direct/compliance proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:23:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Dialog-host roster hierarchy parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Shell/DialogHost.razor` so the browser roster hierarchy now treats dossier/watch-folder placeholder rows as non-draggable presentation rows instead of stale `runner` candidates by centralizing the placeholder detection on `no saved dossiers`, `no saved runners`, `no watched files`, and `empty`.
  - the same slice updated the visible roster hierarchy copy to `Dossier library tree`, `Create your own folder hierarchy, then drag dossiers or custom folders onto any directory.`, and `Drop a dossier or link row here ...` for the directory drop affordance.
  - added direct BUnit proof in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the roster dialog now pins the dossier toolbar copy, drop-target tooltip, and the non-draggable empty-state line semantics.
  - refreshed `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so the phase-4 shell suite presence gate now requires that new roster dialog proof.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:29.03`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:47.63`
  - focused dialog/compliance pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_roster_hierarchy_uses_dossier_copy_and_keeps_empty_state_non_draggable|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Blazor_shell_component_suite_is_present_for_phase4_gate" --output Normal`
      - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `894ms`
- Scope note:
  - this slice strengthens repo-local dialog-host roster semantics and dossier-facing copy only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:15:26+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Preview/browser-shell dossier parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the public `/app` roster accessibility labels now say `Dossier folders`, `Organize dossiers`, `Selected dossier`, `Dossier summary`, and `Open dossier Kestrel` instead of the stale runner wording.
  - the same slice updated the compatibility-route shortcut rails to `Selection-sensitive dossier actions`, `Create from the selected dossier lane.`, `Return to the active dossier context.`, and `Find, group, and organize existing dossiers.`
  - refreshed direct proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` and `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`; the workbench-strip proof now uses a dedicated `/workbench?workspace=preview-ws` render because the default `/preview` route does not emit that strip.
  - refreshed the preview source guard in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` so the dossier-facing roster/shortcut labels stay pinned to live source.
- Verification completed for this slice:
  - incremental builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:02:52.54`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:03:10.11`
    - final incremental test-project rebuild after adding the dedicated workbench proof:
      - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:30.53`
  - focused preview/app/compliance pack:
    - `dotnet /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_route_keeps_dossier_copy_for_context_and_search_shortcuts|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.App_route_renders_character_roster_without_preview_scaffolding|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_renders_character_roster_without_preview_scaffolding|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Preview_surface_keeps_browser_proof_cards_for_shared_startup_workflows" --output Normal`
      - result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `1s 109ms`
- Scope note:
  - this slice strengthens repo-local browser roster and compatibility-route shortcut dossier wording only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T10:02:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Character-roster dossier parity and adjacent browser-shell copy advanced in the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs`, `DialogCoordinator.cs`, `RosterHierarchyState.cs`, and the related presentation tests so the character-roster dialog, roster status, move/open notices, watched-file portrait text, and roster hierarchy staged proof now use dossier-facing wording (`Open Dossiers`, `Saved Dossiers`, `Active Dossier`, `Dossier Status`, `Move Dossier to Directory`, `watched dossier sibling`, and matching roster notices).
  - updated `Chummer.Blazor/Components/App.razor` so the SSR workbench fallback keeps the roster dialog and dirty-footer copy aligned with the dossier lane (`Group dossiers into your own folders.` and `Unsaved dossier`).
  - closed the adjacent stale browser-shell copy that blocked the roster hierarchy staged proof by updating `Chummer.Blazor/Components/Pages/Preview.razor`, `StatusStrip.razor`, and `SectionPane.razor` to `Find, group, and organize existing dossiers.`, `Dossier: loaded/none`, and `Select a tab to render a dossier section.`
  - refreshed `scripts/materialize-blazor-workbench-roster-hierarchy-staged-proof.py` so the staged proof now tracks the current `Home.razor`/`Preview.razor` truth (`Workflow trust`, `ChummerOnlinePromiseAriaLabel`, and `DemoStartupCommandId="@EffectiveStartupCommandId"`), then aligned the exact migration guard expectations in `MigrationComplianceTests.cs` to the live preview/home/test source.
- Verification completed for this slice:
  - incremental final-state builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:45.14`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:42.53`
  - focused presentation/compliance pack:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests|FullyQualifiedName~Chummer.Tests.Presentation.DialogCoordinatorTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Preview_surface_keeps_browser_proof_cards_for_shared_startup_workflows" --output Normal`
      - result: `1061 total`, `1061 succeeded`, `0 failed`, `0 skipped`, duration `1m 43s 534ms`
  - source-staged proof:
    - `python3 scripts/materialize-blazor-workbench-roster-hierarchy-staged-proof.py`
      - result: `blazor_workbench_roster_hierarchy_staged_proof:passed`
- Scope note:
  - this slice strengthens repo-local character-roster/dialog dossier wording, the adjacent browser-shell dossier copy needed by the staged roster proof, and the matching direct/staged guardrails only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T09:10:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is still:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Preview-shell dossier parity advanced in the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the `/app` menu, classic `/workbench` shell, roster/status panels, recovery strip, workflow ledger, workspace tabs, activity feed, and restored-lane descriptions now use dossier-facing open/continue/import/save nouns while intentionally keeping creation phrasing like `New runner`.
  - updated `Chummer.Blazor/Components/Pages/Preview.razor.css` so the pseudo classic frame now says `View   Dossier` and `Open Dossier`.
  - updated `Chummer.Presentation/UiKit/ShellChromeBoundary.cs` so shared shell labels now say `Print Dossier...` and `Export Dossier...`.
  - refreshed direct proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`, `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`, and `Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs`; while verifying, also aligned the stale `save_character_as` title expectations in `AppRouteSurfaceTests.cs` to the live `Save Dossier As` route behavior.
- Verification completed for this slice:
  - incremental final-state builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:01:12.69`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:28.49`
  - focused presentation pack:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests" --output Normal`
      - result: `617 total`, `617 succeeded`, `0 failed`, `0 skipped`, duration `12s 230ms`
- Scope note:
  - this slice strengthens repo-local preview/app/classic-shell dossier wording parity and its direct proof only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T08:49:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Browser and classic-shell save surfaces are now aligned with the dossier lane on the current tree:
  - updated `Chummer.Blazor/Components/App.razor` so hosted `/workbench` fallback save routes now surface `Save Dossier`, `Save Dossier As`, and `Download Dossier`, with summary/result continuation copy moved from runner wording to dossier workflow and dossier download wording.
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the browser command palette, save/session strip, status-bar save handoff text, committed result banners, route panel titles, route summaries, and route-open posture text now use dossier-facing save language; the adjacent command-palette print affordance now says `Print Dossier`, and the workbench route aria label now says `Workbench command palette posture`.
  - updated `Chummer.Blazor/Components/Shell/MetadataPanel.razor` so the panel now says `Dossier Metadata`, `Dossier ID`, `Update Dossier Metadata`, `Save Dossier`, `Load Dossier`, and `Dossier id`.
  - `Chummer.Presentation/UiKit/ShellChromeBoundary.cs` already carried the newer dossier-facing save/open labels in the current tree; while verifying this slice, the file was found truncated mid-method, so the missing `AccessibilityPrimitiveBoundary.BuildStatusAnnouncement(...)` and `ResolveAccessibilityAttribute(...)` tail was restored from `HEAD` without undoing the newer label changes.
  - refreshed direct proof in `DesktopInstallLinkingShellChromeTests.cs`, `BlazorShellComponentTests.cs`, `AppShellBaseHrefTests.cs`, `PublicPreviewSurfaceTests.cs`, and `AppRouteSurfaceTests.cs`.
- Verification completed for this slice:
  - incremental final-state builds:
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:17.73`
    - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:BuildProjectReferences=false`
      - result: `Build succeeded`
      - warnings/errors: `0 Warning(s)`, `0 Error(s)`
      - duration: `00:00:04.25`
  - focused presentation pack:
    - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests.FormatCommandLabel_keeps_shared_shell_commands_human_facing|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.MetadataPanel_uses_dossier_metadata_copy|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Hosted_blazor_workbench_output_fallback_uses_custom_runner_copy_without_polluting_clean_app_href|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Hosted_blazor_workbench_output_dialog_action_fallback_preserves_clean_app_action_continuation|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Hosted_blazor_workbench_save_as_route_renders_save_workflow_result_without_dialog_fallback|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Hosted_blazor_workbench_save_route_renders_save_workflow_result_without_dialog_fallback|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Workbench_output_command_queries_publish_expected_workflow_identity|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Workbench_output_dialog_action_queries_publish_specific_download_heading_and_continuation|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_output_routes_render_committed_result_banner|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_output_routes_render_specific_chrome_copy_while_metadata_stays_category_level|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Preview_fixture_output_query_renders_committed_result_banner|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Preview_result_routes_render_specific_output_copy|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_output_queries_render_committed_result_banner|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.App_route_output_queries_render_specific_output_copy|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Online_alias_output_queries_render_specific_output_copy|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Online_alias_output_queries_render_committed_result_banner" --output Normal`
      - result: `73 total`, `73 succeeded`, `0 failed`, `0 skipped`, duration `1s 989ms`
  - source-staged proof scripts:
    - `python3 scripts/materialize-blazor-workbench-save-session-staged-proof.py`
      - result: `blazor_workbench_save_session_staged_proof:passed`
    - `python3 scripts/materialize-blazor-workbench-command-palette-staged-proof.py`
      - result: `blazor_workbench_command_palette_staged_proof:ok`
- Scope note:
  - this slice strengthens repo-local browser/classic save-copy parity and the adjacent browser command-palette/save-session wording only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T08:15:03+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The remaining ja-jp and zh-cn shell-state snapshot field labels are now localized instead of leaking English shell tokens:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.shell.state.snapshot` now uses `オープン` / `保存` / `前回コマンド` for `ja-jp` and `已打开` / `保存` / `上一命令` for `zh-cn`.
  - updated `Chummer.Presentation/Shell/DesktopMouseFirstJourneyVisibleShellStateReader.cs` so the toolstrip snapshot parser now accepts those localized Japanese and Chinese field labels in addition to the previously supported English, German, French, and Portuguese tokens.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` and added a new reader-side regression in `Chummer.Tests/Presentation/DesktopMouseFirstJourneyVisibleShellStateReaderTests.cs`, then verified the internal parser behavior with a disposable reflection harness because the direct MSTest app filter still does not select the internal reader class cleanly in this environment.
- Verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false && dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_banner_and_snapshot_use_dossier_language_across_shipping_locales" --output Normal`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - build duration: `00:13:41.74`
    - focused runner result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `4s 599ms`
  - disposable reflection harness:
    - temporary `net10.0` console project under `/tmp/chummer-shellstate-harness`
    - referenced `Chummer.Presentation.csproj`
    - invoked `DesktopMouseFirstJourneyVisibleShellStateReader.ParseToolStripStatusState(...)` via reflection for the new `ja-jp` and `zh-cn` dossier-facing snapshot strings
    - result: `shell-state reader ja/zh reflection harness passed`
- Scope note:
  - this slice tightens repo-local shell snapshot localization for ja-jp and zh-cn only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T07:58:06+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop shell banner and shell-state snapshot copy are now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.shell.banner` now says `Dossier Workbench` across the shipped locales instead of the stale `Runner Workbench` banner.
  - the same slice moved `desktop.shell.state.snapshot` to dossier-facing snapshot keys, replacing `workspace=` / `arbeitsbereich=` / `espace=` leakage with `dossier=` / `ドシエ=` / `dossie=` / `档案=` while keeping the existing locale-specific status scaffolding.
  - updated `Chummer.Presentation/Shell/DesktopMouseFirstJourneyVisibleShellStateReader.cs` so the shell-state parser now accepts the current dossier-facing workspace-strip and toolstrip snapshot labels, while remaining tolerant of the older workspace-era tokens.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs`, `Chummer.Tests/Compliance/InteractiveControlInventoryComplianceTests.cs`, and `scripts/ai/milestones/interactive-control-inventory-check.sh`, then supplemented the internal reader coverage with a disposable reflection harness against `Chummer.Presentation` because the direct MSTest app filter would not select the internal-reader class cleanly in this environment.
- Verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false && dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_workspace_strip_uses_dossier_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_banner_and_snapshot_use_dossier_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopMouseFirstJourneyVisibleShellStateReaderTests|FullyQualifiedName~Chummer.Tests.Compliance.InteractiveControlInventoryComplianceTests.Interactive_control_inventory_guard_pins_standalone_controls_main_window_routes_and_b14_consumption" --output Normal`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - build duration: `00:04:53.33`
    - focused runner result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `4s 403ms`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_workspace_strip_uses_dossier_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_banner_and_snapshot_use_dossier_language_across_shipping_locales" --output Normal`
    - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `3s 195ms`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Compliance.InteractiveControlInventoryComplianceTests.Interactive_control_inventory_guard_pins_standalone_controls_main_window_routes_and_b14_consumption" --output Normal`
    - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `2s 398ms`
  - disposable reflection harness:
    - temporary `net10.0` console project under `/tmp/chummer-shellstate-harness`
    - referenced `Chummer.Presentation.csproj`
    - invoked `DesktopMouseFirstJourneyVisibleShellStateReader.ParseWorkspaceStripState`, `ParseToolStripStatusState`, and `Read(...)` via reflection for dossier-facing `en-us` and `de-de` snapshot inputs
    - result: `shell-state reader reflection harness passed`
- Scope note:
  - this slice strengthens repo-local desktop shell localization and parser parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T07:39:43+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Campaign shell labels and campaign-surface localization are now aligned with the current campaign/dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so the shipped locales no longer surface `campaign workspace` or raw `workspace` wording for `desktop.shell.tool.campaign_workspace`, `desktop.shell.feedback.campaign_workspace_reviewed`, `desktop.campaign.title`, `desktop.campaign.heading`, `desktop.campaign.intro.*`, and `desktop.campaign.status.*`.
  - this included the one remaining `de-de` local-fallback sentence that still said `Arbeitsbereich`, plus broader `fr-fr`, `ja-jp`, `pt-br`, and `zh-cn` replacements so those campaign intros and status summaries now follow the simpler campaign-facing wording already used by the default catalog.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` so campaign shell labels, campaign intro copy, campaign status copy, and the adjacent campaign restore copy stay pinned across the shipped locales.
- Verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false && dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_campaign_shell_labels_use_campaign_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_campaign_intro_copy_uses_campaign_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_campaign_status_copy_uses_campaign_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_campaign_restore_and_reopen_copy_uses_campaign_and_dossier_language_across_shipping_locales" --output Normal`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - build duration: `00:04:15.49`
    - test result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `1s 902ms`
- Scope note:
  - this slice strengthens repo-local campaign localization parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T07:28:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop shell action labels and workspace-strip summaries are now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.shell.tool.save_workspace` / `desktop.shell.tool.close_active_workspace` now say `Save Dossier` / `Close Active Dossier` in the default catalog and carry dossier-facing localized values across `de-de`, `fr-fr`, `ja-jp`, `pt-br`, and `zh-cn`.
  - the same slice updated `desktop.shell.workspace_strip.heading`, `desktop.shell.workspace_strip.summary`, and `desktop.shell.workspace_strip.empty` so the shell strip now reports the current dossier instead of the stale runner/workspace wording across the shipped locales.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` so both the shell action labels and the workspace-strip heading/summary/empty-state strings stay pinned to dossier language.
- Verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false && dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Character_settings_notice_uses_dossier_language_in_primary_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Close_feedback_uses_dossier_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_actions_use_dossier_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Desktop_shell_workspace_strip_uses_dossier_language_across_shipping_locales" --output Normal`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - build duration: `00:02:46.12`
    - test result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `2s 009ms`
- Scope note:
  - this slice strengthens repo-local desktop shell localization parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T07:24:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop settings notice and close-feedback localization are now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.dialog.character_settings.notice.updated` now says `Dossier settings updated.` in the default catalog, with the `de-de` localized value pinned by direct proof.
  - the same slice updated `desktop.shell.feedback.no_active_workspace` so the close feedback now says `no active dossier to close` across `en-us`, `de-de`, `fr-fr`, `ja-jp`, `pt-br`, and `zh-cn`.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` so both the character-settings notice and the shipped-locale close-feedback wording stay pinned to the dossier lane.
- Verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false && dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Close_feedback_uses_dossier_language_across_shipping_locales|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests.Character_settings_notice_uses_dossier_language_in_primary_locales" --output Normal`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - build duration: `00:01:08.60`
    - test result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `3s 100ms`
- Scope note:
  - this slice strengthens repo-local desktop settings and close-feedback localization parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T07:16:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Close-window browser and presenter wording are now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/WorkspaceOverviewLifecycleCoordinator.cs` so close-window lifecycle notices now say `Closed active dossier.` / `Active dossier was already closed.` and keep the switch notice in the same dossier language.
  - updated `Chummer.Blazor/Components/App.razor` so the hosted `close_window` and `close_all` action fallback summaries now say `active dossier window` / `open dossier windows`.
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the public-app and compatibility-route close-window fallback cards now say `active-dossier close posture ...` instead of the stale active-runner wording.
  - refreshed direct proof in `CharacterOverviewPresenterTests`, `AppShellBaseHrefTests`, `AppRouteSurfaceTests`, and `PublicPreviewSurfaceTests` so the active-dossier close wording stays pinned across presenter state and both browser fallback surfaces.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:01:09.25`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.ExecuteCommandAsync_close_window_switches_to_previous_workspace|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Hosted_blazor_workbench_action_routes_render_specific_visible_chrome_without_dialog_fallback|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests.Workbench_action_command_queries_publish_expected_workflow_identity_without_dialog_payload|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Public_tool_commands_open_shared_shell_without_falling_back_to_roster|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_tool_commands_render_specific_shell_copy" --output Normal`
    - result: `117 total`, `117 succeeded`, `0 failed`, `0 skipped`, duration `19s 114ms`
- Scope note:
  - this slice strengthens repo-local close-window/browser fallback wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T06:37:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- ALICE blank-state surface labeling is now aligned with the current dossier/create lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopAliceAssistant.cs` so the fallback `character_create` plan now surfaces `Character Create` instead of the stale `New runner` label when no dossier is open and ALICE is working from the blank state.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the existing blank-state ALICE dialog test now also pins `Surface | Character Create` alongside the dossier-facing empty-state message and mode options.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:07:05.55`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_auto_alice_without_active_character_still_offers_build_origin_and_rules_modes" --output Normal`
    - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `1s 691ms`
- Scope note:
  - this slice strengthens repo-local ALICE surface-label parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T06:29:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Runner Intelligence utility wording is now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the `runner_benchmark` dialog now says `Compare this dossier ...` instead of `Compare this runner ...`.
  - the same slice moved `runner_what_if` to `without mutating the active dossier ...` and updated `runner_cohort_privacy` so the surrounding message and excluded-data field now refer to the private dossier and dossier names instead of stale runner wording.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the benchmark, what-if, and privacy utility dialogs now pin the dossier-facing wording while leaving the `Runner Intelligence` product names intact.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:12:49.84`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateUiControlDialog_runner_intelligence_utilities_use_dossier_language" --output Normal`
    - result: `1 total`, `1 succeeded`, `0 failed`, `0 skipped`, duration `1s 896ms`
- Scope note:
  - this slice strengthens repo-local utility wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T06:12:59+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Utility delete and source-detail wording are now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `show_source` now says source references stay visible without pushing the dossier view off screen.
  - the same slice moved the `gear_delete`, `cyberware_delete`, and `drug_delete` utility copy away from stale runner wording to `dossier inventory only`, `active dossier inventory`, `active dossier`, and `dossier ledger only` where those delete-impact and delete-notes fields describe scope.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the source utility and the gear/cyberware/drug delete dialogs now pin the dossier-facing wording in their notes/impact fields.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:04:45.68`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateUiControlDialog_show_source_uses_compact_source_detail_posture|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateUiControlDialog_gear_delete_uses_impact_posture|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateUiControlDialog_cyberware_delete_uses_legacy_delete_posture|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateUiControlDialog_drug_delete_uses_legacy_delete_posture" --output Normal`
    - result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `14s 903ms`
- Scope note:
  - this slice strengthens repo-local utility wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T06:05:33+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Metadata, notes, and ALICE blank-state wording are now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `CreateMetadataDialog` now says `Apply dossier profile metadata changes to the active dossier.` and the `open_notes` utility now says `Edit dossier notes in a compact text utility pane.`
  - updated `Chummer.Presentation/Overview/DesktopAliceAssistant.cs` so the blank-state ALICE prompt now says `No dossier is open yet ...` instead of the stale runner wording while preserving the guided origin-dossier handoff.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the metadata dialog, notes utility, and blank-state ALICE prompt stay pinned to dossier wording.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:02:41.15`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateMetadataDialog_prefills_profile_name_alias_and_notes|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateUiControlDialog_open_notes_uses_character_notes_preference|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_auto_alice_without_active_character_still_offers_build_origin_and_rules_modes" --output Normal`
    - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `2s 303ms`
- Scope note:
  - this slice strengthens repo-local overview/dialog wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:59:38+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Dice and initiative utility roster context is now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the `dice_roller` utility now uses `Active Dossier`, `Open Dossiers`, `No active dossier.`, and `Initiative preview uses the active dossier ...` instead of stale runner/workspace wording for open-dossier context.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the dice utility now pins both the populated roster-context dossier copy and the no-open-dossier empty state, while keeping the existing SR4 legacy-label/action-order checks intact.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:01:44.70`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_dice_roller_surfaces_initiative_preview_and_roster_context|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_dice_roller_without_open_workspaces_uses_dossier_empty_state|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_dice_roller_uses_sr4_legacy_labels_and_action_order" --output Normal`
    - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `1s 316ms`
- Scope note:
  - this slice strengthens repo-local dice-utility dossier wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:55:04+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop export-utility wording is now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so `data_exporter` now previews `Dossier: {workspace}` and `export_character` now uses `Export Dossier`, `Export the selected dossier bundle.`, and `Dossier: {workspace}`.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the export utilities stay pinned to dossier wording alongside the previously verified open/import/print/export dialog slices.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:00:07.60`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_print_character_uses_dense_print_posture|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_open_character_uses_import_template|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_print_and_export_staging_use_dossier_language|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_export_utilities_use_dossier_language" --output Normal`
    - result: `4 total`, `4 succeeded`, `0 failed`, `0 skipped`, duration `996ms`
- Current tree note:
  - the earlier `Preview.razor` classic-menu compile scare did not reproduce on the current worktree; `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Scope note:
  - this slice strengthens repo-local export dialog wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:45:13+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop command-dialog wording is now aligned with the current dossier/staging lane on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopDialogFactory.cs` so the `open_character`, `open_for_printing`, `open_for_export`, and `print_character` command dialogs now avoid stale runner wording.
  - this changed the user-facing dialog titles/messages to `Open Dossier`, `Open Print Staging`, `Open Export Staging`, and `Print Dossier`.
  - the same slice changed the import utility copy to `Paste dossier XML ...`, `Review the imported summary before applying this SR6 dossier import.`, `Dossier XML`, and `Current dossier`.
  - refreshed direct proof in `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs` so the open/import/print/export dialog strings stay pinned to dossier/staging wording.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:02:58.34`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_print_character_uses_dense_print_posture|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_open_character_uses_import_template|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_print_and_export_staging_use_dossier_language" --output Normal`
    - result: `3 total`, `3 succeeded`, `0 failed`, `0 skipped`, duration `3s 300ms`
- Scope note:
  - this slice strengthens repo-local dialog/UI wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:39:51+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Generic shell and SR6 import wording is now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Rulesets/RulesetUiDirectiveCatalog.cs` so the shared import fallback and the SR6 import headings/actions/placeholders now avoid stale runner-file wording in the current dossier lane.
  - this moved the shared fallback to `Import Dossier File`, `Raw Dossier XML Review`, `Import Dossier XML`, and `(no dossier file selected)`.
  - the same slice moved SR6 import copy to `Import SR6 Dossier File`, `SR6 Dossier XML Review`, `Import SR6 Dossier XML`, and `(no SR6 dossier file selected)`.
  - refreshed direct proof in `Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs` so the shared/SR6 import helpers stay pinned to dossier wording alongside the earlier shell-copy expectations.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:02:16.43`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.RulesetUiDirectiveCatalogTests" --output Normal`
    - result: `8 total`, `8 succeeded`, `0 failed`, `0 skipped`, duration `2s 596ms`
- Scope note:
  - this slice strengthens repo-local SR6 import-lane wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:32:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth is currently:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Generic shell and SR6 ruleset-shell wording is now aligned with the current dossier lane on the current tree:
  - updated `Chummer.Presentation/Rulesets/RulesetUiDirectiveCatalog.cs` so the shared shell and SR6 user-facing open/resume/follow-through copy now avoid stale runner language in the current dossier lane.
  - the same slice also moved the generic shell heading/empty-state fallback from `Open Runners` / `No open runner` to `Open Dossiers` / `No open dossier`, and changed the SR6 empty-state from `No open SR6 runner` to `No open SR6 dossier`.
  - updated `Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor` so the navigator now uses ruleset-specific open labels for button titles/aria labels, uses the ruleset-catalog empty state instead of a hardcoded runner message, and drops the hardcoded `Close runner` wording in favor of neutral close copy.
  - refreshed direct proof in `Chummer.Tests/Presentation/RulesetUiDirectiveCatalogTests.cs` and `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so the shared/SR6 dossier wording and the navigator tooltip/empty-state text stay pinned.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:09:12.12`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.RulesetUiDirectiveCatalogTests|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.MdiStrip_uses_ruleset_specific_empty_state_when_no_workspace_is_open|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.OpenWorkspaceTree_renders_open_and_close_actions" --output Normal`
    - result: `9 total`, `9 succeeded`, `0 failed`, `0 skipped`, duration `3s 002ms`
- Scope note:
  - this slice strengthens repo-local SR6 shell wording parity and navigator polish only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:16:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Canonical blocker truth now includes:
  - `release_posture:non_flagship_channel`
  - `proof:ui_localization_release_gate`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Install-link and linked-device follow-through wording is now aligned with the current dossier lane across shipping locales on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.install_link.button.open_work`, `desktop.install_link.status.opened_work_route`, `desktop.install_link.status.unable_open_work_route`, `desktop.install_link.summary.next_safe_action_claimed`, and `desktop.devices.context.access_claimed` now avoid stale runner/workspace language for `en-us`, `de-de`, `fr-fr`, `ja-jp`, `pt-br`, and `zh-cn`.
  - expanded `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` so direct localization proof now pins the exact dossier-facing install-link and linked-device strings for those locales in `Desktop_install_link_and_devices_copy_uses_dossier_language_across_shipping_locales`.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:02:36.55`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests" --output Normal`
    - result: `16 total`, `16 succeeded`, `0 failed`, `0 skipped`, duration `23s 506ms`
- Scope note:
  - this slice strengthens repo-local install-link and linked-device wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T05:10:29+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Campaign reopen/restore terminology is now aligned with the current dossier lane across shipping locales on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.home.intro.ready_current_campaign_workspace`, `desktop.home.button.open_current_campaign_workspace`, `desktop.campaign.section.recent_workspaces`, `desktop.campaign.restore.latest_workspace`, and `desktop.campaign.restore.no_workspace` now avoid stale runner/workspace language for `en-us`, `de-de`, `fr-fr`, `ja-jp`, `pt-br`, and `zh-cn`.
  - expanded `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` so direct localization proof now pins the exact dossier-facing current-campaign reopen and restore-copy strings for those locales in `Desktop_campaign_restore_and_reopen_copy_uses_campaign_and_dossier_language_across_shipping_locales`.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `00:00:07.58`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests" --output Normal`
    - result: `15 total`, `15 succeeded`, `0 failed`, `0 skipped`, duration `13s 518ms`.
- Scope note:
  - this slice strengthens repo-local desktop campaign reopen/restore wording parity only; it does not change the shared Windows visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:57:10+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Desktop-home localization parity is now aligned with the current dossier wording across the shipped locales on the current tree:
  - updated `Chummer.Presentation/Overview/DesktopLocalizationCatalog.cs` so `desktop.home.section.recent_workspaces`, `desktop.home.intro.ready_recent_workspaces`, `desktop.home.workspace_summary.empty`, `desktop.home.button.open_current_workspace`, `desktop.home.button.open_work_support`, and `desktop.home.button.open_workspace_followthrough` now use dossier-facing copy for `en-us`, `de-de`, `fr-fr`, `ja-jp`, `pt-br`, and `zh-cn`.
  - the recent-workspace intro and empty-state copy no longer fall back to stale runner/workspace language in the non-primary shipping locales.
  - the adjacent desktop-home recents/open/help action labels now match the same dossier wording lane instead of mixing dossier copy with stale runner/workspace labels.
  - expanded direct proof in `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs` so the shipping-locale catalog pack pins the exact dossier-facing strings for both the recent-workspace copy and the desktop-home action labels.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `1m 47.93s`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests" --output Normal`
    - result: `14 total`, `14 succeeded`, `0 failed`, `0 skipped`, duration `7s 090ms`
- Scope note:
  - this slice strengthens repo-local desktop-home localization parity only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:43:59+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Public preview/browser-shell startup and staging labels are now aligned with the current dossier/staging naming on the current tree:
  - updated `Chummer.Blazor/Components/Pages/Preview.razor` so the import/startup cards use `Import an existing dossier`, `Open Dossier`, `Open Print Staging`, and `Open Export Staging`, and the roster shortcuts now say `Keep recent dossiers one click away` plus `Bring desktop and self-hosted dossier files forward.`
  - refreshed direct proof in `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` and the source guard in `Chummer.Tests/Compliance/MigrationComplianceTests.cs` to pin those preview labels.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `3m 07.67s`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Preview_surface_keeps_browser_proof_cards_for_shared_startup_workflows" --output Normal`
    - result: `257 total`, `257 succeeded`, `0 failed`, `0 skipped`, duration `20s 892ms`
- Scope note:
  - this slice strengthens repo-local public preview/browser-shell wording parity only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:36:49+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Startup-workbench copy is now internally consistent with the current dossier-open shell language:
  - updated `Chummer.Blazor/Components/Shell/SectionPane.razor` so the startup intro says `reopen a saved dossier`, the empty-state card says `Recent Dossiers` and `No recent dossiers yet`, and the recent-item subtitle now says `Restore this Chummer Online dossier continuation.`
  - refreshed the existing direct BUnit proof in `Chummer.Tests/Presentation/BlazorShellComponentTests.cs` so both the populated and empty startup-workbench states pin the dossier wording.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `1m 30.26s`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane_renders_startup_workbench_with_first_class_restore_and_utility_actions|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane_startup_workbench_without_recent_runners_uses_open_dossier_copy" --output Normal`
    - result: `2 total`, `2 succeeded`, `0 failed`, `0 skipped`, duration `1s 614ms`
- Scope note:
  - this slice strengthens repo-local startup-workbench wording consistency only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:32:11+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Classic shell copy is now aligned with the current dossier/import staging language on the current tree:
  - changed `Chummer.Presentation/UiKit/ShellChromeBoundary.cs` so `open_character`, `open_for_printing`, and `open_for_export` now format as `Open Dossier...`, `Open Print Staging...`, and `Open Export Staging...`.
  - updated `Chummer.Blazor/Components/Shell/SectionPane.razor` so the no-recents startup note now tells the user to use `Open Dossier...` to restore a dossier from disk.
  - expanded the direct shell/UI proof in `DesktopInstallLinkingShellChromeTests` and `BlazorShellComponentTests` so the new classic-shell labels and the startup-workbench note stay pinned.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
    - duration: `3m 43.54s`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane_startup_workbench_without_recent_runners_uses_open_dossier_copy" --output Normal`
    - result: `29 total`, `29 succeeded`, `0 failed`, `0 skipped`, duration `652ms`
- Scope note:
  - this slice strengthens repo-local shell wording and startup-workbench copy parity only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:26:46+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Classic shell naming parity is now aligned with the current route/dialog surface for character settings:
  - changed `Chummer.Presentation/UiKit/ShellChromeBoundary.cs` so `character_settings` now formats as `Character Settings` instead of the stale `Runner Settings`.
  - expanded `Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs` so the direct shell-chrome proof also pins `character_settings` alongside `runtime_inspector` and the newer rules-data commands.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests" --output Normal`
    - result: `28 total`, `28 succeeded`, `0 failed`, `0 skipped`, duration `819ms`
- Scope note:
  - this slice strengthens repo-local shell-chrome wording parity only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:22:46+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Classic shell command-label fidelity is now tightened on the current tree:
  - added the missing `runtime_inspector` human label to `Chummer.Presentation/UiKit/ShellChromeBoundary.cs` so classic shell chrome no longer falls back to a lowercased `"runtime inspector"` string.
  - expanded `Chummer.Tests/Presentation/DesktopInstallLinkingShellChromeTests.cs` with direct proof that `ShellChromeBoundary.FormatCommandLabel(...)` stays human-facing for `runtime_inspector` and the newer rules-data commands.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopInstallLinkingShellChromeTests" --output Normal`
    - result: `28 total`, `28 succeeded`, `0 failed`, `0 skipped`, duration `637ms`
- Scope note:
  - this slice strengthens repo-local shell-chrome fidelity only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:19:43+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Release-script portability proof now directly covers the HTTP downloads publisher on the current tree:
  - expanded `tests/test_release_shell_array_portability.py` so `scripts/publish-download-bundle-http.sh` is pinned alongside the existing manifest/build/S3/verify scripts.
  - the new guard locks the bash3-safe `array_count()` / `array_values_nul()` helpers, the `windows_payload_gate_args_count` and `upload_file_count` paths, and the NUL-safe direct-upload loop while explicitly forbidding raw `${#...[@]}` checks and the old `for file_path in "${upload_files[@]}"` loop from returning.
  - the same focused pack re-checked the current manifest release-channel normalization and the public-stable root-blocker fail-closed contract.
- Verification completed for this slice:
  - `bash -n scripts/build-desktop-installer.sh scripts/generate-releases-manifest.sh scripts/publish-download-bundle-http.sh scripts/publish-download-bundle-s3.sh scripts/verify-releases-manifest.sh`
    - result: parsed cleanly
  - `python3 -m pytest -q --import-mode=importlib tests/test_release_shell_array_portability.py tests/test_desktop_downloads_local_release_policy.py tests/test_windows_installer_payload_gate.py -k "ui_release_shell_scripts_use_nounset_safe_array_count or release_manifest_generation_uses_portable_release_channel_normalization or public_stable_publish_download_bundle_requires_root_release_truth_clearance or stable_publish_download_bundle_refuses_non_posture_root_blockers or s3_publish_windows_payload_gate_allows_empty_only_before_installers_are_added"`
    - result: `5 passed`, `40 deselected`, duration `0.25s`
- Scope note:
  - this slice strengthens repo-local release-script portability and public-stable blocker guardrails only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:15:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Shared shell download/result retry behavior and overview command dispatch are now freshly pinned on the current tree:
  - added presenter-level proof in `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs` that `print_preview` follows the print-preview receipt lane and that the rules-data commands publish the shared “Rules data posture ready ...” notice without requiring an open workspace.
  - re-verified the retryable shell download/export/print receipts in `DesktopShellDownloadDispatchTests` and the shared command classification additions in `OverviewCommandPolicyTests`.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellDownloadDispatchTests|FullyQualifiedName~Chummer.Tests.Presentation.OverviewCommandPolicyTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.Print_preview_command_prepares_html_preview|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.Rules_data_commands_publish_shared_notice_without_requiring_workspace" --output Normal`
    - result: `43 total`, `43 succeeded`, `0 failed`, `0 skipped`, duration `1s 644ms`
- Scope note:
  - this slice strengthens repo-local shell/dispatcher verification only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:11:59+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Release-script portability and stable-publication guardrails are now tightened in the current lane:
  - expanded `tests/test_release_gate_milestone_bash_portability.py` with a focused guard for `scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh`.
  - the new guard pins the bash3-safe `upper_ascii()` helper and tuple-receipt path construction for Linux/Windows/macOS while explicitly forbidding `${head^^}` and `${rid^^}` from returning to the milestone script.
  - the focused verification pack also re-checked the `publish-download-bundle.sh` public-stable blocker-clearance contract so stable publication continues to fail closed unless root release truth clears everything except `release_posture:non_flagship_channel`.
- Verification completed for this slice:
  - `bash -n scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh scripts/publish-download-bundle.sh`
    - result: parsed cleanly
  - `python3 -m pytest -q --import-mode=importlib tests/test_release_gate_milestone_bash_portability.py tests/test_desktop_downloads_local_release_policy.py -k "desktop_executable_exit_gate_avoids_bash4_case_conversion_for_tuple_receipt_paths or release_gate_milestone_scripts_avoid_bash4_mapfile_collectors or public_stable_publish_download_bundle_requires_root_release_truth_clearance"`
    - result: `3 passed`, `25 deselected`
- Scope note:
  - this slice strengthens repo-local release-script portability and stable-publication guardrails only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:09:47+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Origin Dossier remount and scroll-restore hardening are now freshly verified on the current tree:
  - the current `App.razor` fallback-shell restore path and `DesktopDialogWindow.axaml.cs` transient refresh handling are backed by the focused Blazor/Avalonia origin-dialog pack instead of only source inspection.
  - this specifically covers the newer field-anchor-before-viewport behavior, the longer transient refresh grace window, remounted dialog-host advanced-control persistence, and active-combo viewport-anchor preservation through live select refreshes.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.App_restoreDialogScroll_prefers_origin_field_anchor_before_advanced_panel_anchor|FullyQualifiedName~Chummer.Tests.Presentation.DesktopWindowContrastTests.Origin_dossier_" --output Normal`
    - result: `22 total`, `22 succeeded`, `0 failed`, `0 skipped`, duration `12s 884ms`
- Scope note:
  - this slice strengthens repo-local dialog/UI verification only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:07:22+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- PWA, public-edge execution, and Windows payload metadata contracts are freshly verified on the current tree:
  - the installable PWA shell markers in `Chummer.Blazor/Components/App.razor`, `manifest.webmanifest`, `service-worker.js`, and `offline.html` still match the repo-local contract suite.
  - `scripts/e2e-public-edge-playwright.cjs` parses cleanly after the latest retry/continuation-query updates, and the public-edge execution contract suite remains green.
  - the desktop release-matrix and public Windows payload metadata contract checks are also green, so the current bootstrap payload/download-shelf surface still matches the checked-in verifier expectations.
- Verification completed for this slice:
  - `node --check scripts/e2e-public-edge-playwright.cjs`
    - result: parsed cleanly
  - `python3 -m pytest -q --import-mode=importlib tests/test_blazor_pwa_contract.py tests/test_blazor_public_edge_execution_contract.py tests/test_desktop_release_matrix_gate.py tests/test_public_windows_payload_metadata.py`
    - result: `18 passed`
- Scope note:
  - this slice strengthens repo-local contract verification only; it does not change the shared Windows installer visual-audit blocker or release posture.

## Cross-Codex Refresh (2026-07-07T04:05:22+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Public route and hosted base-href parity are freshly re-verified on the current tree:
  - the focused direct `Chummer.Tests` route pack covering `AppRouteSurfaceTests`, `PublicPreviewSurfaceTests`, `PortalAppRouteContractTests`, and `AppShellBaseHrefTests` is clean again after the recent `App.razor` / `Preview.razor` / `Program.cs` churn.
  - the portal runtime and route-probe Python pack is also green, so `/app`, `/online`, and hosted `/blazor/app` redirect/base-href behavior are backed by both static and live-runtime receipts.
- Workflow-family execution hardening is now pinned by a repo-local contract test instead of living only in script logic:
  - added `tests/test_workflow_family_execution_receipts_contract.py`.
  - the new guard locks the local API autostart/retry contract in `scripts/ai/milestones/materialize-sr-workflow-family-execution-receipts.sh`, including the local default base URL `http://127.0.0.1:8088`, `/api/workspaces` plus `/api/shell/bootstrap` probes, `CHUMMER_API_AUTOSTART*` overrides, missing-API retry path, and emitted autostart evidence fields.
  - it also locks the chained SR6 wrapper behavior in `scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh` so the execution, verification, and aggregate materializers continue to sit behind the single `CHUMMER_SR6_WORKFLOW_PARITY_SKIP_DEPENDENCY_MATERIALIZE` switch and chain lock.
- Verification completed for this slice:
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `756 total`, `756 succeeded`, `0 failed`, `0 skipped`, duration `22s 322ms`
  - `python3 -m pytest -q --import-mode=importlib tests/test_blazor_portal_route_probe_contract.py tests/test_portal_release_shelf_runtime.py`
    - result: `12 passed`
  - `bash -n scripts/ai/milestones/materialize-sr-workflow-family-execution-receipts.sh scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh`
    - result: parsed cleanly
  - `python3 -m pytest -q --import-mode=importlib tests/test_workflow_family_execution_receipts_contract.py tests/test_desktop_executable_exit_gate_contract.py`
    - result: `4 passed`
- Scope note:
  - this slice strengthens route/runtime verification and workflow-family script regression coverage only; it does not change the shared Windows installer visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T03:59:58+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The former API-skip bucket is now converted into real Presentation proof in the current lane:
  - brought up `chummer-api` with `docker compose -f /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/docker-compose.yml up -d chummer-api`.
  - verified the runtime on host port `0.0.0.0:8088` and confirmed `/api/workspaces?maxCount=1` returned a live JSON payload instead of the earlier socket-unavailable skip condition.
  - normalized the remaining volatile dual-head snapshot fields in `Chummer.Tests/Presentation/DualHeadAcceptanceTests.cs` so parity now masks runtime-generated runner/workspace ids in `Runner:` export preview copy and `autoAliceWorkspaceId`.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DualHeadAcceptanceTests" --output Normal > /tmp/chummer_dual_head_20260707_r3.log 2>&1`
    - result: `30 total`, `30 succeeded`, `0 failed`, `0 skipped`, duration `2m 16s 161ms`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation" --output Normal > /tmp/chummer_presentation_full_20260707_r4.log 2>&1`
    - result: `1617 total`, `1617 succeeded`, `0 failed`, `0 skipped`, duration `6m 23s 060ms`
- Scope note:
  - this strengthens repo-local verification only; it does not soften or clear the shared external Windows installer visual-audit blocker.

## Cross-Codex Refresh (2026-07-07T03:30:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Presentation verification is re-hardened in the current lane:
  - added `Chummer.Tests/Presentation/AvaloniaHeadlessSessionGate.cs` and included it in `Chummer.Tests/Chummer.Tests.csproj`.
  - moved the remaining Avalonia-only harnesses onto gated `HeadlessUnitTestSession` execution so full-pack runs stop tripping `Call from invalid thread` across `AvaloniaFlagshipUiGateTests`, `DesktopWindowContrastTests`, `DesktopTrustPanelFactoryTests`, and `AvaloniaHeadlessSmokeTests`.
  - refreshed stale Presentation expectations to current product truth across `DesktopClaimCopyLanguageTests`, `DesktopSupportDiagnosticsTextTests`, `WorkflowParityGateTests`, `CharacterOverviewPresenterTests`, `DesktopHomeCampaignProjectorTests`, and `AvaloniaFlagshipUiGateTests`.
- Verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests|FullyQualifiedName~Chummer.Tests.Presentation.DesktopHomeCampaignProjectorTests|FullyQualifiedName~Client_label_visibility_gate_keeps_profile_rows_and_priority_labels_visible_without_collapsible_profile_chrome|FullyQualifiedName~Chummer.Tests.Presentation.DesktopWindowContrastTests" --output Normal`
    - result: `84 passed`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopWindowContrastTests|FullyQualifiedName~Chummer.Tests.Presentation.DesktopTrustPanelFactoryTests|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaHeadlessSmokeTests|FullyQualifiedName~Character_creation_preserves_familiar_dense_builder_rhythm" --output Normal`
    - result: `28 passed`
  - `dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll --filter "FullyQualifiedName~Chummer.Tests.Presentation" --output Normal > /tmp/chummer_presentation_full_20260707_r3.log 2>&1`
    - result: `1617 total`, `1587 succeeded`, `0 failed`, `30 skipped`, duration `6m 07s 638ms`
- Environment note:
  - the `30` skips are still `DualHeadAcceptanceTests` inconclusive receipts caused by `http://chummer-api:8080/` returning `Resource temporarily unavailable`; that remains external runtime availability, not a product regression in this repo slice.

## Cross-Codex Refresh (2026-07-07T02:26:51+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The remaining bash4-only helper setup path is now hardened too, so the current `scripts/` and `scripts/ai` tree is clear of `mapfile`, `readarray`, and associative-array usage:
  - `scripts/ai/day1-p1-setup.sh`
    - replaced the `mapfile`-based project list collectors with a shared `collect_solution_projects()` reader loop.
    - replaced the associative-array membership checks with a bash3-safe `array_contains_exact()` helper for both remove and add passes.
    - this keeps the `Chummer.Presentation.sln` bootstrap/setup path aligned with the same portability posture as the release/proof scripts that call into it.
  - `tests/test_day1_setup_bash_portability.py`
    - added a focused guard that pins the new collector/membership helpers and explicitly forbids `mapfile` and `declare -A` in the setup script.
- Focused verification completed for this slice:
  - `bash -n scripts/ai/day1-p1-setup.sh`
    - result: parsed cleanly
  - `pytest -q tests/test_day1_setup_bash_portability.py`
    - result: `1 passed`
  - `pytest -q tests/test_day1_setup_bash_portability.py tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_release_gate_milestone_bash_portability.py`
    - result: `6 passed`
  - `rg -n "declare -A|typeset -A|mapfile|readarray" scripts scripts/ai`
    - result: no matches
- Scope note:
  - this slice widened beyond the active release/proof lane only because the remaining bash4-only helper script was still on the path used by milestone checks and `day1-p1-run.sh`.

## Cross-Codex Refresh (2026-07-07T02:24:36+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The Linux desktop exit gate no longer depends on bash4 associative arrays for run-retention cleanup:
  - `scripts/materialize-linux-desktop-exit-gate.sh`
    - replaced the `declare -A keep_roots` set in `prune_old_run_roots()` with a temp-file-backed keep-list that marks roots via `printf '%s\n' >> "$keep_roots_file"` and checks membership with `grep -Fqx --`.
    - this keeps the run-retention cleanup path bash3-safe while preserving the same retention semantics for the current run, latest symlink target, live-owner roots, and retained recent runs.
  - `tests/test_desktop_exit_gate_bash_portability.py`
    - added a Linux-specific guard that pins the `keep_roots_file` pattern and explicitly forbids `declare -A keep_roots=()`.
- Focused verification completed for this slice:
  - `bash -n scripts/materialize-linux-desktop-exit-gate.sh`
    - result: parsed cleanly
  - `pytest -q tests/test_desktop_exit_gate_bash_portability.py`
    - result: `2 passed`
  - `pytest -q tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_release_gate_milestone_bash_portability.py`
    - result: `5 passed`
- Scope note:
  - after this slice, the remaining `mapfile` / `declare -A` usage under `scripts/ai` is still confined to `scripts/ai/day1-p1-setup.sh`, which remains outside the active release/proof lane.

## Cross-Codex Refresh (2026-07-07T02:22:23+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The Chummer5a parity milestone script now matches the bash3-safe release portability posture used by the other release-gate scripts:
  - `scripts/ai/milestones/chummer5a-ultimate-parity-tester.sh`
    - replaced the null-delimited fixture collector `mapfile -d '' fixtures` with an explicit `while IFS= read -r -d '' fixture_path; do fixtures+=("$fixture_path"); done` loop.
    - this keeps the full-fixture parity proof lane portable without relying on bash4-only array loading.
  - `tests/test_release_gate_milestone_bash_portability.py`
    - expanded the milestone portability guard to cover the parity tester in addition to the two release-gate materializers.
- Focused verification completed for this slice:
  - `bash -n scripts/ai/milestones/chummer5a-ultimate-parity-tester.sh`
    - result: parsed cleanly
  - `pytest -q tests/test_release_gate_milestone_bash_portability.py`
    - result: `1 passed`
  - `pytest -q tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_release_gate_milestone_bash_portability.py`
    - result: `4 passed`
- Scope note:
  - the only remaining `mapfile` usage under `scripts/ai` is currently in `scripts/ai/day1-p1-setup.sh`, which is outside the active release/proof lane.

## Cross-Codex Refresh (2026-07-07T02:20:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Milestone release-gate scripts now use bash3-safe collectors instead of bash4-only `mapfile` in the release portability lane:
  - `scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh`
    - replaced the runtime screenshot candidate collector with an explicit `while IFS= read -r ...; do ...+=(); done` loop before appending the fallback screenshot directories.
  - `scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh`
    - replaced the dependency refresh environment collector with the same bash3-safe loop before invoking `env ... bash "$dependency_script"`.
  - `tests/test_release_gate_milestone_bash_portability.py`
    - added a focused guard that pins the new collector snippets and forbids `mapfile -t` in both milestone release-gate scripts.
- Focused verification completed for this slice:
  - `bash -n scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh`
  - `bash -n scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh`
    - result: both parsed cleanly
  - `pytest -q tests/test_release_gate_milestone_bash_portability.py`
    - result: `1 passed`
  - `pytest -q tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_release_gate_milestone_bash_portability.py`
    - result: `4 passed`

## Cross-Codex Refresh (2026-07-07T02:16:50+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Public-edge contract coverage now matches the actual route-surface normalization and startup-label contract instead of stale pre-normalization literals:
  - `tests/test_blazor_public_edge_execution_contract.py`
    - updated the workbench execution-state assertions to match the current `Preview.razor` contract where committed-result tuples are keyed on `NormalizeShellDataToken(...)` output and use wildcard command slots such as `(_, "create-entry", "add")`.
    - updated the startup-label assertions to match the current `(normalizedCommand, NormalizeShellDataToken(DialogAction))` switch and the staging-aware labels `Open Print Staging` / `Open Export Staging`.
    - this closes the release-lane contract drift where Python guardrails still expected underscore-era or pre-staging literals even though the live route surface had already moved to hyphenated shell-data tokens and staging labels.
- Focused verification completed for this slice:
  - `pytest -q tests/test_blazor_public_edge_execution_contract.py`
    - result: `9 passed`
  - `pytest -q tests/test_desktop_downloads_local_release_policy.py tests/test_portal_release_shelf_runtime.py tests/test_windows_installer_payload_gate.py tests/test_blazor_public_edge_execution_contract.py tests/test_blazor_portal_route_probe_contract.py tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py`
    - result: `68 passed`
- Extra note:
  - the failing assertions were in the Python contract tests, not in the Blazor source: `Preview.razor` already expressed the intended normalized route contract and staging labels.

## Cross-Codex Refresh (2026-07-07T02:11:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- No-workspace startup routes now fail closed at the route surface instead of advertising gated utilities as if they open immediately:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `/app`, `/online`, and `/workbench` now publish `data-startup-command-state="blocked"` for shared commands that require an open runner when the route has no workspace or fixture context.
    - the route surfaces now pass `DemoStartupCommandId` through `EffectiveStartupCommandId`, so blocked startup routes keep the shared shell chrome visible without dispatching the gated command.
    - blocked startup copy now explicitly tells the user that `character_settings`, `copy`, and `data_exporter` require an open runner before Chummer Online can continue from the route.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added public-route proof that `/app` and `/online` keep the shared shell visible, mark the startup command as `blocked`, suppress presenter dispatch, and render route-level open-runner guidance for `character_settings`, `copy`, and `data_exporter`.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added matching compatibility-route proof for `/workbench?command=character_settings|copy|data_exporter`.
- Focused verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Blazor/Chummer.Blazor.csproj --no-restore -m:1 -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane|FullyQualifiedName~Chummer.Tests.Presentation.CommandAvailabilityEvaluatorTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Public_tool_commands_open_shared_shell_without_falling_back_to_roster|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests.Public_workspace_gated_startup_routes_render_blocked_copy_without_dispatching_command|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_tool_commands_render_specific_shell_copy|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests.Workbench_workspace_gated_startup_routes_render_blocked_copy_without_dispatching_command" --output Normal`
    - result: `132 passed`
- Extra notes:
  - a focused test build with `-p:BuildProjectReferences=false` can leave stale project-reference outputs in `Chummer.Tests/bin` after Razor edits; if route-surface receipts look contradictory, rebuild `Chummer.Blazor` first or temporarily enable project references before trusting the direct test binary.
  - `dotnet test` on this repo still errors under the .NET 10 SDK unless the Microsoft Testing Platform "new dotnet test experience" is explicitly enabled, so the authoritative receipt remains the direct `Chummer.Tests` binary run.

## Cross-Codex Refresh (2026-07-07T01:52:33+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Startup command forwarding now honors the shared availability contract instead of letting disabled commands slip through the browser shell bridge:
  - `Chummer.Blazor/Components/Layout/DesktopShell.Commands.cs`
    - added command-definition resolution against the current overview/shell catalogs before forwarding.
    - if a command is known and disabled for the current state, the desktop shell now stops after the shell-presenter pass instead of always dispatching into the overview presenter bridge.
    - this closes the no-workspace startup leak where browser-surface deep links could still reach gated commands such as `data_exporter`.
  - `Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs`
    - added focused proof that `ExecuteCommandFromSurfaceAsync` keeps `data_exporter` blocked without a workspace while still allowing the startup-safe `xml_editor` path through.
  - `Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
    - startup workbench coverage now uses `DefaultCommandAvailabilityEvaluator` instead of `_ => true` and explicitly proves the surfaced startup actions remain enabled through the shared contract.
- Focused verification completed for this slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildProjectReferences=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests.ExecuteCommandFromSurfaceAsync_honors_shared_startup_command_availability_before_forwarding_to_overview_presenter|FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests.ExecuteCommandAsync_keeps_startup_dialog_commands_off_the_workspace_sync_path|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.SectionPane_renders_startup_workbench_with_first_class_restore_and_utility_actions|FullyQualifiedName~Chummer.Tests.Presentation.CommandAvailabilityEvaluatorTests.IsCommandEnabled_honors_shared_utility_workspace_gating" --output Normal`
    - result: `5 passed`
- Extra note:
  - `dotnet test` on this repo now errors under the .NET 10 SDK unless the Microsoft Testing Platform "new dotnet test experience" is explicitly enabled, so the authoritative focused receipt for this slice is the direct `Chummer.Tests` binary run above.

## Cross-Codex Refresh (2026-07-07T01:35:21+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Shared command metadata now stays aligned across the hosted `/workbench` compatibility resolver and the core SR5/SR6 shell catalogs instead of drifting on menu placement and workspace gating:
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - aligned shared command metadata with the core shell contract:
      - `update` and `restart` now sit in `help`
      - `switch_ruleset` stays in `special`
      - `data_exporter` now requires an open workspace
      - `xml_editor` remains startup-safe without an open workspace
  - `../../chummer-core-engine/Chummer.Rulesets.Hosting/Presentation/AppCommandCatalog.cs`
  - `../../chummer-core-engine/Chummer.Rulesets.Sr5/Sr5ShellCatalogs.cs`
  - `../../chummer-core-engine/Chummer.Rulesets.Sr6/Sr6ShellCatalogs.cs`
    - aligned the same shared command metadata:
      - `switch_ruleset` now uses `special`
      - `report_bug`, `update`, and `restart` now sit in `help`
      - `xml_editor` no longer requires an open workspace
      - `data_exporter` continues to require an open workspace
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - upgraded the id-only parity guard into a metadata parity guard covering `Id`, `LabelKey`, `Group`, `RequiresOpenCharacter`, and `EnabledByDefault` against the hosting app catalog and the SR5/SR6 shell definition providers.
  - `Chummer.Tests/Presentation/CommandAvailabilityEvaluatorTests.cs`
    - added explicit startup gating proof that `xml_editor` stays enabled without an open workspace while `data_exporter` stays gated behind one.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests" --output Normal`
    - result: `5 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CommandAvailabilityEvaluatorTests" --output Normal`
    - result: `6 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.ExecuteCommandAsync_all_catalog_commands_are_handled|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.ExecuteCommandAsync_dialog_commands_use_non_generic_dialog_templates|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture" --output Normal`
    - result: `3 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_xml_editor_surfaces_xml_bridge_posture|FullyQualifiedName~Chummer.Tests.RulesetSeamContractsTests.Presentation_catalogs_support_ruleset_filtering_without_changing_sr5_defaults" --output Normal`
    - result: `2 passed`
- Extra note:
  - a follow-up attempt to run the separate `chummer-core-engine/Chummer.Tests` wrapper suite for `ShellCatalogAndRulesetDetectionTests` built successfully but the `dotnet test` host stalled after compilation, so the authoritative verification for this slice is the focused `Chummer.Tests` run above.

## Cross-Codex Refresh (2026-07-07T01:19:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Shared command catalogs now keep the compatibility resolver, the hosting SR5 app catalog, and the SR5/SR6 ruleset shell catalogs on the same browser-shell inventory instead of drifting on AI/origin, rules-data, and diagnostics commands:
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - added `runtime_inspector` to the compatibility command inventory so the hosted `/workbench` fallback and the shared route surfaces now expose the same diagnostics command family as the core hosting catalogs.
  - `../../chummer-core-engine/Chummer.Rulesets.Hosting/Presentation/AppCommandCatalog.cs`
    - added the missing shared commands that recent browser-shell parity slices already depend on: `auto_alice`, `new_character_origin`, `open_sourcebooks`, `open_errata`, `open_custom_data`, `update_data_packs`, `validate_data_scope`, `open_data_folder`, `show_login_video`, and `exit`.
  - `../../chummer-core-engine/Chummer.Rulesets.Sr5/Sr5ShellCatalogs.cs`
  - `../../chummer-core-engine/Chummer.Rulesets.Sr6/Sr6ShellCatalogs.cs`
    - added the same shared AI/origin, rules-data, and login-video commands so the SR5/SR6 ruleset shell definition providers stop lagging behind the compatibility resolver inventory.
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - added a parity guard that compares resolver command ids against `AppCommandCatalog.All` plus the SR5 and SR6 ruleset shell definition providers.
    - refreshed the expected compatibility inventory to include `runtime_inspector`.
  - `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
  - `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
    - widened the generic dialog-coverage lists so `auto_alice` and `new_character_origin` now stay inside the same non-generic presenter/factory proof as the rest of the shared dialog command family.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests" --output Normal`
    - result: `4 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.OverviewCommandPolicyTests" --output Normal`
    - result: `25 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.ExecuteCommandAsync_all_catalog_commands_are_handled" --output Normal`
    - result: `1 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.ExecuteCommandAsync_dialog_commands_use_non_generic_dialog_templates" --output Normal`
    - result: `1 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions" --output Normal`
    - result: `1 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.RulesetSeamContractsTests.Presentation_catalogs_support_ruleset_filtering_without_changing_sr5_defaults" --output Normal`
    - result: `1 passed`

## Cross-Codex Refresh (2026-07-07T01:04:11+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Shared editor-relay commands now keep explicit startup-shell identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup chrome:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat `copy` and `paste` as first-class startup shells with explicit workflow keys, frame titles, and route-aware relay summaries on `/workbench`, `/app`, and `/online`.
    - public startup panels for this two-command family now use relay-specific `Open the shared ... relay.` wording instead of collapsing to the generic startup fallback copy.
  - `Chummer.Blazor/Components/App.razor`
    - hosted compatibility fallback now resolves explicit workflow ids, section headings, summaries, and clean workspace-preserving `/app?workspace=...&command=copy|paste` continuations for the same two commands without inventing dialog payloads.
    - the hosted SSR shell now pins `copy` and `paste` identities instead of collapsing them into the default profile shell.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility-route shell-copy proof for `/workbench?command=copy` and `/workbench?command=paste`.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added `/app` and `/online` proof that the same commands render command-specific relay shells rather than falling back to the roster landing surface.
    - tightened the public shell-copy assertion to accept relay-specific wording while keeping the rest of the shell contract strict.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added helper-level and rendered hosted SSR fallback proof for `copy` and `paste`, including workspace-preserving clean `/app` continuations and metadata coverage.
  - `Chummer.Tests/Presentation/OverviewCommandPolicyTests.cs`
    - added explicit policy proof that `copy` and `paste` remain known shared editor-relay commands and stay outside dialog-command handling.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `175 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `571 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.OverviewCommandPolicyTests" --output Normal`
    - result: `25 passed`

## Cross-Codex Refresh (2026-07-07T00:53:31+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Shared-command inventory hardening now matches the compatibility route surface that the recent browser-shell parity slices depend on:
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - refreshed the expected compatibility command inventory so resolver coverage now includes the six rules-data commands already exposed by `CatalogOnlyRulesetShellCatalogResolver`.
    - this closes a stale test gap where the resolver shipped a broader shared-command set than its own inventory test asserted.
  - `Chummer.Tests/Presentation/OverviewCommandPolicyTests.cs`
    - added explicit policy proof that `open_sourcebooks`, `open_errata`, `open_custom_data`, `update_data_packs`, `validate_data_scope`, `open_data_folder`, `new_critter`, `restart`, `exit`, `close_window`, and `close_all` remain known shared commands while staying outside dialog-command handling.
    - this locks the route-parity intent for the non-dialog rules-data and action command families instead of relying only on broader catalog coverage.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests|FullyQualifiedName~Chummer.Tests.Presentation.OverviewCommandPolicyTests" --output Normal`
    - result: `26 passed`

## Cross-Codex Refresh (2026-07-07T00:48:52+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Remaining compatibility startup action routes now keep explicit workflow identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup or dossier chrome:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat `new_critter`, `restart`, `exit`, `close_window`, and `close_all` as first-class startup shells with explicit status labels, workflow keys, frame titles, and route-aware summaries on `/workbench`, `/app`, and `/online`.
    - public startup panels for that five-command family now follow the same `Open the shared ...` shell-title contract as the earlier tool/data-pack slices instead of collapsing back to generic shell copy.
  - `Chummer.Blazor/Components/App.razor`
    - hosted compatibility fallback now resolves explicit workflow ids, section headings, workflow labels, and clean `/app?command=...` continuations for the same five-command family without inventing dialog payloads.
    - the hosted SSR shell now pins `new-critter`, `restart`, `exit`, `close-window`, and `close-all` identities, including workspace-preserving `/app` continuations for `restart`, `close_window`, and `close_all`.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility-route shell-copy proof for the five action-style startup commands on `/workbench`.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added `/app` and `/online` proof that the same command family renders command-specific startup shells instead of falling back to the roster landing surface.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added helper-level and rendered hosted SSR fallback proof for the same five-command family, including clean `/app` continuations, workspace-preserving action continuations where required, and no-dialog fallback posture.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `169 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `563 passed`

## Cross-Codex Refresh (2026-07-07T00:34:45+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Remaining dialog-backed compatibility startup routes now keep explicit workflow identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup or dossier chrome:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat `dice_roller`, `data_exporter`, `print_setup`, `print_multiple`, `update`, `new_window`, `wiki`, `discord`, `show_login_video`, `revision_history`, and `dumpshock` as first-class startup shells with explicit status labels, workflow keys, frame titles, and route-aware summaries on `/workbench`, `/app`, and `/online`.
    - public startup panels for that eleven-command family now follow the same `Open the shared ...` shell-title contract as the earlier tool/data-pack command slices instead of introducing family-specific panel naming.
  - `Chummer.Blazor/Components/App.razor`
    - hosted compatibility fallback now resolves explicit workflow ids, section headings, workflow labels, dialog payloads, and clean `/app?command=...` continuations for the same eleven-command family.
    - the hosted SSR shell now pins `dice-roller`, `data-exporter`, `print-setup`, `print-multiple`, `update`, `new-window`, `wiki`, `discord`, `login-video`, `revision-history`, and `issue-tracker` identities instead of collapsing those routes into generic startup fallback copy.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility-route shell-copy proof for the eleven dialog-backed startup commands on `/workbench`.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added `/app` and `/online` proof that the same command family renders command-specific startup shells instead of falling back to the roster landing surface.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added helper-level and rendered hosted SSR fallback proof for the same eleven-command family, including clean `/app?command=...` continuations, workflow-label overrides for `update` and `show_login_video`, and dialog-backed fallback posture.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `154 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `543 passed`

## Cross-Codex Refresh (2026-07-07T00:14:22+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Workbench rules/data-pack posture now keeps explicit workflow identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup or dossier chrome:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - the rules/data-pack strip now points to compatibility command routes for `open_sourcebooks`, `open_errata`, `open_custom_data`, `update_data_packs`, `validate_data_scope`, and `open_data_folder` instead of preview-only links.
    - the strip now uses same-origin help via `/help`, and browser route surfaces now treat the six commands as first-class startup shells with explicit status labels, workflow keys, frame titles, and route-aware summaries on `/workbench`, `/app`, and `/online`.
  - `Chummer.Blazor/Components/App.razor`
    - added fallback workflow ids, headings, summaries, and clean `/app?command=...` continuations for the six-command data-pack family without inventing dialog payloads.
    - hosted compatibility fallback now emits `sourcebooks`, `errata`, `custom-data`, `update-pack`, `validation-scope`, and `data-folder` identities with command-specific result text instead of collapsing those routes into tab-based dossier defaults.
  - `Chummer.Presentation/Overview/OverviewCommandPolicy.cs`
    - added the six data-pack commands to the known shared command contract so browser startup bootstrap accepts them as real route commands.
  - `Chummer.Presentation/Overview/OverviewCommandDispatcher.cs`
    - added non-dialog shared dispatch handling for the data-pack family so live browser startup routes do not fall through to unimplemented-command errors.
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - added the six data-pack commands to the compatibility shell catalog so startup bootstrap can cross the shared shell and presenter bridge instead of stopping on unknown-command shell errors.
  - `Chummer.Presentation/UiKit/ShellChromeBoundary.cs`
    - added visible command labels for the data-pack family so shared shell chrome keeps stable names when these commands surface through compatibility menus or command labels.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added helper-level and rendered hosted SSR fallback proof for the six-command data-pack family, including clean `/app` continuations, no-dialog fallback posture, and compatibility metadata coverage.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility route proof that `/workbench?command=open_sourcebooks|open_errata|open_custom_data|update_data_packs|validate_data_scope|open_data_folder` renders command-specific `data-active-workflow` values and shell copy.
    - added proof that the visible rules/data strip publishes compatibility links plus same-origin `/help`.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added public `/app` and `/online` proof for the same six-command family so those startup routes no longer rely on coarse startup-command labels only.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `bash scripts/ai/milestones/blazor-workbench-data-packs-staged-proof-check.sh`
    - result: `blazor_workbench_data_packs_staged_proof:passed`
    - receipt: `.codex-studio/published/BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.generated.json`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `121 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `499 passed`


## Cross-Codex Refresh (2026-07-06T23:43:34+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The live-lanes `auto_alice` startup route now keeps explicit workflow identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup or dossier chrome:
  - `Chummer.Blazor/Components/App.razor`
    - added fallback workflow identity, result copy, and clean `/app?command=auto_alice` continuation handling for `auto_alice`.
    - the hosted compatibility fallback now uses the shorter `Assistant` workflow label in the classic titlebar/footer while preserving the full `Auto ALICE` dialog title inside the section body.
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat `auto_alice` as a first-class startup shell with explicit status labels, workflow keys, frame titles, and route-aware summaries on `/workbench`, `/app`, and `/online`.
    - compatibility/public shell copy now uses the live-lanes `Assistant` wording instead of the generic startup fallback wording.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded rendered SSR fallback and helper-level proof for `auto_alice`, including clean `/app` continuation and metadata coverage.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility route proof that `/workbench?command=auto_alice` renders command-specific `data-active-workflow` and assistant-specific shell copy.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added public `/app` and `/online` proof for `auto_alice` so the live-lanes assistant startup route no longer relies on coarse startup-command labels only.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `103 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `474 passed`
  - `git diff --check -- Chummer.Blazor/Components/App.razor Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/AppShellBaseHrefTests.cs Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs Chummer.Tests/Presentation/AppRouteSurfaceTests.cs docs/WORKBENCH_SESSION_HANDOFF.md`
    - result: clean

## Cross-Codex Refresh (2026-07-06T23:34:39+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Settings/support/diagnostics startup commands now keep explicit workflow identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup or dossier chrome:
  - `Chummer.Blazor/Components/App.razor`
    - added command-specific fallback workflow ids, workflow labels, headings, summaries, clean `/app?command=...` continuations, and dialog payloads for `character_settings`, `switch_ruleset`, `report_bug`, `about`, and `runtime_inspector`.
    - the hosted compatibility fallback now keeps `report_bug` on the shorter `Support` workflow label in the classic titlebar/footer while retaining the full `Support and bug reporting` dialog title inside the section body.
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat the five commands as first-class startup shells with explicit status labels, workflow keys, frame titles, and route-aware summaries on `/workbench`, `/app`, and `/online`.
    - compatibility/public shell copy now uses `Character Settings`, `Switch Ruleset`, `Support`, `About Chummer`, and `Runtime Inspector` instead of the generic startup fallback wording.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded rendered SSR fallback and helper-level proof for the five-command family, including clean `/app` continuations and metadata matrix coverage.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility route proof that `/workbench?command=character_settings|switch_ruleset|report_bug|about|runtime_inspector` renders command-specific `data-active-workflow` values and shell copy.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added public `/app` and `/online` proof for the same command family so those startup routes no longer rely on coarse startup-command labels only.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `100 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `469 passed`
  - `git diff --check -- Chummer.Blazor/Components/App.razor Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/AppShellBaseHrefTests.cs Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs Chummer.Tests/Presentation/AppRouteSurfaceTests.cs docs/WORKBENCH_SESSION_HANDOFF.md`
    - result: clean

## Cross-Codex Refresh (2026-07-06T23:08:50+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Browser tool commands now keep explicit workflow identity across the hosted `/workbench` compatibility shell and the clean public `/app` and `/online` routes instead of degrading to generic startup or dossier chrome:
  - `Chummer.Blazor/Components/App.razor`
    - added command-specific fallback workflow ids, headings, summaries, clean `/app?command=...` continuations, and dialog payloads for `global_settings`, `translator`, `xml_editor`, and `hero_lab_importer`.
    - the hosted compatibility fallback now emits `global-settings`, `translator`, `xml-editor`, and `hero-lab-importer` identities with tool-specific result text instead of collapsing those commands into tab-based defaults.
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat the four tool commands as first-class startup shells with explicit status labels, workflow keys, frame titles, and route-aware summaries on `/workbench`, `/app`, and `/online`.
    - compatibility/public shell copy now uses `Global Settings`, `Translator`, `XML Editor`, and `Hero Lab Importer` instead of the generic `Startup command` / `Open Chummer Online` fallback wording.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded rendered SSR fallback and helper-level proof for the four tool commands, including clean `/app` continuations and metadata matrix coverage.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility route proof that `/workbench?command=global_settings|translator|xml_editor|hero_lab_importer` renders command-specific `data-active-workflow` values and shell copy.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added public `/app` and `/online` proof for the same tool-command family so those startup routes no longer rely on coarse startup-command labels only.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `85 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `444 passed`
  - `git diff --check -- Chummer.Blazor/Components/App.razor Chummer.Blazor/Components/Pages/Preview.razor Chummer.Tests/Presentation/AppShellBaseHrefTests.cs Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs Chummer.Tests/Presentation/AppRouteSurfaceTests.cs docs/WORKBENCH_SESSION_HANDOFF.md`
    - result: clean

## Cross-Codex Refresh (2026-07-06T22:55:50+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- `master_index` now has real workflow identity across compatibility and clean public browser routes instead of degrading to generic dossier/profile chrome:
  - `Chummer.Blazor/Components/App.razor`
    - `/workbench?command=master_index` now resolves to `ActiveWorkflow=master-index`, `Title=Master Index`, `SectionHeading=Master Index`, and summary copy for rules/gear/qualities/spells/reference search.
    - the compatibility fallback now emits a clean continuation back to `/app?command=master_index` with `Continue Master Index on Chummer Online.`
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - browser route surfaces now treat `master_index` as its own shell workflow instead of falling through to the generic dossier workflow.
    - compatibility and public app/online copy now use `Master Index`, `master-index`, `Master Index shell`, and route summaries that explain the search/reference role instead of generic startup or dossier language.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added rendered and helper-level compatibility fallback proof for `master_index`, including clean `/app` continuation and metadata matrix coverage.
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added compatibility route proof that `/workbench?command=master_index` renders `data-active-workflow="master-index"` with `Master Index` classic chrome and browser frame copy.
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added `/app?command=master_index` and `/online?command=master_index` proof so the public routes now pin `master-index` shell identity instead of relying on coarse startup-command labels only.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `73 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `424 passed`

## Cross-Codex Refresh (2026-07-06T22:40:33+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now has direct proof for the remaining posture/contract metadata family that the interactive compatibility shell already publishes:
  - `Chummer.Blazor/Components/App.razor`
    - the SSR root exposes roster/dossier state attrs plus operating-posture/auth/calculation attrs including `data-roster-selected-node`, `data-dossier-state`, `data-dossier-storage`, `data-validation-state`, `data-privacy-mode`, `data-analytics-scope`, `data-hosting-mode`, `data-deployment-target`, `data-self-hostable`, `data-container-target`, `data-auth-gate`, `data-session-state`, `data-login-target`, `data-auth-return-policy`, `data-calculation-owner`, `data-statistics-runtime`, `data-character-statistics`, `data-statistics-scope`, `data-recommendation-mode`, `data-recommendation-inputs`, `data-risk-model`, `data-calculation-boundary`, and `data-result-consumer`.
    - fallback metadata differentiates `character_roster` as `runner-active` and `new_character_origin` as `origin-draft` while keeping the shell scoped to `local-preview`, `hosted-or-self-hosted`, and `docker`.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded rendered SSR assertions so the hosted origin shell now pins the full posture contract instead of only the earlier high-level output/route family attrs.
    - expanded helper identity coverage for new-character, roster, normalized control routes, and a route-contract matrix covering open, roster, origin, save/download, export, and control queries.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `72 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T22:26:26+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now publishes the same high-level compatibility/output metadata family as the interactive compatibility shell:
  - `Chummer.Blazor/Components/App.razor`
    - added `OutputWorkflow`, `OutputState`, and `OutputTarget` to the fallback contract and rendered them as `data-output-*` attrs on the SSR shell.
    - added compatibility route identity fields for `RouteFamily`, `RouteSurface`, `RouteAlias`, `ClientKind`, and `ParityTarget`.
    - SSR fallback route metadata now reports compatibility posture with `data-route-family="compatibility"`, `data-route-surface="compatibility"`, `data-route-alias="none"`, `data-client-kind="web-desktop"`, and `data-parity-target="desktop-client"`.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded rendered SSR assertions for origin, save, download, new-character, and control-dialog routes to pin the new `data-output-*` and compatibility route attrs.
    - added helper-level fallback metadata proof covering non-output, save, export, and control routes.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `70 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T22:18:25+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now mirrors the interactive compatibility shell's classic chrome more closely for dialog-bearing routes:
  - `Chummer.Blazor/Components/App.razor`
    - `Title` now stays workflow/status scoped for the classic shell instead of reusing dialog titles for commands and controls that open dialogs.
    - `new_character` now renders `Build Lab` in the classic titlebar/status chrome while preserving the dialog title `New runner`.
    - dialog-bearing controls such as `complex_form_add` now keep workflow chrome like `Matrix` while preserving dialog titles like `Add Complex Form`.
    - added the classic status footer to the SSR fallback with the same ready/dirty/rules/checks/privacy/workflow structure used by the interactive compatibility shell.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - helper-level control-route and new-character fallback assertions now pin workflow-scoped `Title` values instead of dialog titles.
    - added rendered hosted-blazor proof for the classic status footer plus the split between workflow chrome and dialog title on `new_character` and `complex_form_add`.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `66 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T22:05:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now matches the interactive compatibility shell's command-specific visible output labels while preserving category-level workflow metadata:
  - `Chummer.Blazor/Components/App.razor`
    - added a shared output-command label resolver for visible SSR fallback copy on save/print/export command routes.
    - non-download output routes now use command-specific titlebar and section headings such as `Prepare Runner Download`, `Prepare Print Preview`, `Open Print Preview`, `Open Print Staging`, `Open Export Staging`, and `Prepare Export Package`.
    - `open_for_printing` and `open_for_export` dialog titles now match the same staging labels instead of the older `Open for ...` wording.
    - `data-active-workflow` remains category-level (`save`, `print`, `export`) so route metadata and downstream workflow grouping do not change.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - updated helper-level output-command fallback expectations so `Title` and `SectionHeading` both pin the new visible command labels across the full output command set.
    - added rendered hosted-blazor proof for visible titlebar and `<h2>` copy on save/download-prep, print-preview, print-prep, export-prep, and staging routes.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `64 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:52:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now aligns visible output download section headings with the existing download-specific title/result copy:
  - `Chummer.Blazor/Components/App.razor`
    - `BuildWorkbenchFallback` now passes normalized `dialog_action` into section-heading resolution.
    - `save_character_as&dialog_action=download` now renders `Download Runner` instead of the generic `Save` heading.
    - `export_character&dialog_action=download` now renders `Download Export Package` instead of the generic `Export` heading.
    - non-download output routes keep their existing prepared-state headings and copy.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added rendered hosted-blazor proof that the `<h2>` heading matches the download-specific title for both supported output download routes.
    - added helper-level fallback assertions that pin `Title`, `SectionHeading`, summary, result text, and clean `/app` continuation hrefs for the same two routes.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `61 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:44:41+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback committed-result set now matches the interactive route committed-result set for restored workbench actions:
  - `Chummer.Blazor/Components/App.razor`
    - added missing SSR committed results for `contact_add&dialog_action=add` and `critter_power_add&dialog_action=add`.
    - these now render the same visible results as the interactive workbench/preview/public app routes: `Contact 'Fixer' added.` and `Critter power 'Natural Weapon' added.`
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded the committed-result fallback helper test from one route to the full supported committed-result set.
    - added rendered SSR fallback proof for the two newly restored contact/critter result banners.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `59 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:37:37+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now preserves supported output `dialog_action=download` continuations:
  - `Chummer.Blazor/Components/App.razor`
    - `BuildWorkbenchFallback` now passes normalized `dialog_action` into fallback title, summary, result text, and result route resolution.
    - `save_character_as&dialog_action=download` renders download-specific fallback copy and continues to `/app?...&command=save_character_as&dialog_action=download`.
    - `export_character&dialog_action=download` renders export-download-specific fallback copy and continues to `/app?...&command=export_character&dialog_action=download`.
    - unsupported output dialog actions are still omitted from clean public continuation hrefs.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added rendered custom fixture/runner coverage for both supported output download continuations.
    - the proof pins `data-dialog-action="download"`, download-specific copy, fixture-aware clean `/app` hrefs, and the no-runner-on-`/app` invariant.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `48 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:31:02+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now preserves normalized custom fixture identity in generated pre-hydration continuation links:
  - `Chummer.Blazor/Components/App.razor`
    - `BuildWorkbenchHref` remains reflection-compatible for existing six-argument callers, but now delegates to a fixture-aware core helper.
    - `BuildFallbackWorkbenchHref` and restored workbench actions carry `fixture=` when the route has a non-default fixture.
    - clean public `/app` continuation and command-action hrefs also carry non-default `fixture=`, while the implicit default `blue` fixture remains omitted.
    - custom `runner=` remains workbench-only; clean `/app` hrefs stay workspace/fixture scoped and do not gain unsupported runner query state.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - custom runner fallback proof now pins `fixture=alpha-beta` through restored continuations, restored actions, and public app command actions.
    - command-result proof now pins personalized custom-runner copy plus fixture-aware clean app hrefs and rejects runner pollution on `/app`.
    - static href helper proof now covers the five-argument fixture-aware `BuildPublicAppHref` overload while preserving the existing three- and four-argument contracts.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - static fallback parity checks:
    - restored continuations: `6/6`, missing `[]`, extra `[]`
    - restored actions: `68/68`, missing `[]`, extra `[]`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `46 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:22:00+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Added a test-only guard for the custom-runner SSR fallback command path:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - `Hosted_blazor_workbench_output_fallback_uses_custom_runner_copy_without_polluting_clean_app_href` now proves command-result copy uses the normalized runner label.
    - the same proof keeps clean public `/app` continuation hrefs workspace-only and explicitly rejects adding unsupported `runner=` to those public app hrefs.
  - No production behavior changed in this follow-up slice.
- Static SSR fallback template parity remains unchanged:
  - restored continuations: `6/6`, missing/extra `0/0`
  - restored actions: `68/68`, missing/extra `0/0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `46 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:16:24+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now preserves normalized custom runner identity in generated pre-hydration workbench links:
  - `Chummer.Blazor/Components/App.razor`
    - `BuildWorkbenchHref` accepts an optional runner and emits `runner=` only when the normalized runner is non-empty and not the default `BLUE`.
    - `BuildFallbackWorkbenchHref` now routes fallback menu, recovery, and restored-continuation links through the current `WorkbenchFallback`.
    - restored workbench action hrefs now receive the fallback runner; clean public `/app` command actions remain unchanged.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - the custom-runner fallback proof now pins `runner=Ghost-One` on restored-continuation and restored-action workbench hrefs.
    - the href helper proof keeps default/no-runner routes free of `runner=`, while custom-runner routes preserve the normalized token.
- Static SSR fallback template parity after this slice:
  - interactive restored-continuation labels: `6`
  - SSR fallback restored-continuation templates: `6`
  - missing continuation label count: `0`
  - extra continuation label count: `0`
  - interactive restored-action labels: `68`
  - SSR fallback restored-action templates: `68`
  - missing action label count: `0`
  - extra action label count: `0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `45 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:07:13+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now uses the normalized route runner label instead of hardcoded `BLUE` for custom runner URLs:
  - `Chummer.Blazor/Components/App.razor`
    - titlebar, recovery links, restored-continuation links, restored-action labels, and command result text now use `workbenchFallback.Runner`.
    - restored-action label templates remain aligned with the interactive `PRV` label inventory by replacing the `BLUE` token at render time.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added rendered fallback proof for `runner=Ghost/One` / `workspace=storm ops`.
    - the proof pins normalized runner/workspace output such as `Ghost-One`, `storm-ops`, `Save Ghost-One in browser`, and route-preserving action hrefs.
- Static SSR fallback template parity after this slice:
  - interactive restored-continuation labels: `6`
  - SSR fallback restored-continuation templates: `6`
  - missing continuation label count: `0`
  - extra continuation label count: `0`
  - interactive restored-action labels: `68`
  - SSR fallback restored-action templates: `68`
  - missing action label count: `0`
  - extra action label count: `0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `45 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T21:00:02+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `proof:release_channel`
  - `release_truth:final_gold_janitor`
  - `release_truth:release_ready`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback restored-continuations card now matches the interactive restored-continuations label inventory after normalizing `PRV` to `BLUE`:
  - `Chummer.Blazor/Components/App.razor`
    - added the missing `Resume BLUE on SIN/license review` continuation.
    - the new continuation routes to `workbench?workspace=blue-workspace&tab=tab-info&control=identity_license_edit`, matching the interactive card's profile/SIN review target.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - pinned the rendered fallback label and href before interactive hydration.
- Static restored-continuations parity after this slice:
  - interactive restored-continuation labels: `6`
  - SSR fallback restored-continuation labels: `6`
  - missing label count: `0`
  - extra label count: `0`
  - fallback continuation controls: `1`
  - controls outside current parity contract set: `0`
- Existing restored-actions parity remains green:
  - interactive restored-action labels: `68`
  - SSR fallback restored-action labels: `68`
  - missing action label count: `0`
  - extra action label count: `0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `44 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T20:54:37+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `proof:release_channel`
  - `release_truth:public_edge_postdeploy_gate`
  - `release_truth:final_gold_janitor`
  - `release_truth:release_ready`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback restored-actions card now matches the interactive restored-actions card's action-label inventory after normalizing `PRV` to `BLUE`:
  - `Chummer.Blazor/Components/App.razor`
    - expanded `WorkbenchFallbackActions` from the earlier route-aware subset to the full interactive restored-action label set.
    - added public `/app` command actions for save, download, export, export download, and print preview routes.
    - added missing workbench control actions for contacts, combat weapon add, skill add, critter dialog add, vehicles, Runner Intelligence, magic/quality/drug variants, and add-and-keep dialog routes.
    - added a dialog-action-aware `BuildPublicAppHref` overload while preserving the existing three-argument reflection contract.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - expanded rendered SSR fallback href proof to cover representative contact, public app command, vehicle, Runner Intelligence, quality, and spell dialog-action links.
    - adjusted the static reflection helper to resolve overloaded static methods by argument count.
- Static fallback restored-actions parity after this slice:
  - interactive restored-action labels: `68`
  - SSR fallback restored-action labels: `68`
  - missing label count: `0`
  - extra label count: `0`
  - fallback workbench action controls: `52`
  - controls outside current parity contract set: `0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `44 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T20:47:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `proof:release_channel`
  - `release_truth:public_edge_postdeploy_gate`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback restored-actions card now preserves real tab/control/dialog routes instead of linking every action label back to the base workspace route:
  - `Chummer.Blazor/Components/App.razor`
    - replaced the plain `WorkbenchFallbackActionLabels` list with route-aware `WorkbenchFallbackAction` records.
    - restored action links now call `BuildWorkbenchHref(workspace, tab, control, dialogAction)` with the intended route target.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added rendered-markup proof for representative SSR fallback restored action links:
      - `create_entry&dialog_action=add`
      - `open_notes&dialog_action=save`
      - `combat_add_armor`
      - `matrix_program_add`
      - `show_source`
      - `magic_add`
      - `drug_delete`
- Static fallback action coverage after this slice:
  - route-aware fallback action controls counted: `34`
  - controls outside current parity contract set: `0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `44 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T20:41:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `proof:release_channel`
  - `release_truth:public_edge_postdeploy_gate`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now infers interactive shell identity for control-only fallback routes instead of dropping most controls into generic `workbench` / `Profile` state:
  - `Chummer.Blazor/Components/App.razor`
    - replaced substring-only control tab inference with exact mappings for all current recursive UI-control parity IDs.
    - expanded fallback tab-to-workflow mapping for Profile, Rules, Gear, Combat, Career, Matrix, Contacts, Cyberware, Qualities, Skills, Adept, Magic, Critter, and Stats shells.
    - aligned fallback section headings/summaries for those shell families.
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - added representative control-only fallback identity coverage for:
      - `contact_add`
      - `gear_edit`
      - `combat_reload`
      - `move_down`
      - `open_notes`
      - `show_source`
      - `runner_benchmark`
      - `skill_group`
      - `cyberware_delete`
      - `quality_delete`
      - `adept_power_add`
      - `magic_delete`
      - `critter_power_add`
      - `matrix_program_add`
- Static control-map coverage after this slice:
  - parity contracts counted: `53`
  - SSR fallback mapped controls: `53`
  - missing count: `0`
  - extra count: `0`
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `43 passed`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T20:30:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains non-flagship and externally blocked:
  - `release_posture:non_flagship_channel`
  - `proof:release_channel`
  - `release_truth:public_edge_postdeploy_gate`
  - `release_truth:windows_installer_visual_audit`
- Release-script portability verification was refreshed without changing publish posture:
  - `python3 -m pytest tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_desktop_exit_gate_bash_portability.py -q`
    - result: `3 passed`
  - `bash -n scripts/generate-releases-manifest.sh scripts/publish-download-bundle.sh scripts/build-desktop-installer.sh scripts/publish-download-bundle-s3.sh scripts/verify-releases-manifest.sh scripts/run-desktop-startup-smoke.sh scripts/materialize-macos-desktop-exit-gate.sh scripts/materialize-linux-desktop-exit-gate.sh scripts/materialize-windows-desktop-exit-gate.sh`
    - result: `passed`
- Practical runtime truth after this verification:
  - the current release and desktop gate scripts satisfy the checked Bash portability invariants for nounset-safe array counts, Bash 3-safe case conversion handling, and no `mapfile` dependency in desktop exit gates.
  - this is script hardening evidence only; it does not clear public-edge postdeploy, release-channel, or Windows visual-audit release blockers.

## Cross-Codex Refresh (2026-07-06T20:29:52+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth from the latest controller handoff includes:
  - `release_posture:non_flagship_channel`
  - `proof:release_channel`
  - `release_truth:public_edge_postdeploy_gate`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Career move utility actions now have direct route-surface proof across the clean public and preview-tools routes:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-calendar&control=move_up`
      - `tab-calendar&control=move_down`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added direct `/preview` shell-route proof for:
      - `move_up`
      - `move_down`
- Route coverage comparison after this slice:
  - parity contracts counted: `53`
  - `/app`/`/online` missing count: `0`
  - `/workbench` missing count: `0`
  - `/preview` missing count: `0`
  - any route missing count: `0`
- Practical runtime truth after this slice:
  - all current recursive UI-control parity contracts now have tracked route proof on `/app`/`/online`, `/workbench`, and `/preview`.
  - release posture is still non-flagship in practice; the public-edge postdeploy and Windows visual-audit blockers remain unresolved in the shared handoff.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `420 passed`

## Cross-Codex Refresh (2026-07-06T20:21:28+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Magic/Quality delete actions now have visible wrapper affordances and direct route-surface proof:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added restored-runner PRV links for:
      - `Remove magic item for PRV`
      - `Remove quality for PRV`
    - added wrapper constants for:
      - `magic_delete`
      - `quality_delete`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels and exact hrefs:
      - `workbench?workspace=preview-ws&tab=tab-magician&control=magic_delete`
      - `workbench?workspace=preview-ws&tab=tab-qualities&control=quality_delete`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `magic_delete`
      - `quality_delete`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-magician&control=magic_delete`
      - `tab-qualities&control=quality_delete`
- Practical runtime truth after this slice:
  - Magic delete and Quality delete are no longer recursive-parity-only contracts.
  - both controls now have visible restored-runner browser routes and prove Magic/Qualities shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `414 passed`

## Cross-Codex Refresh (2026-07-06T20:14:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Contacts edit/remove/connection actions now have visible wrapper affordances and direct route-surface proof:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added restored-runner PRV links for:
      - `Edit contact for PRV`
      - `Remove contact for PRV`
      - `Adjust contact connection for PRV`
    - added wrapper constants for:
      - `contact_edit`
      - `contact_remove`
      - `contact_connection`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels and exact hrefs:
      - `workbench?workspace=preview-ws&tab=tab-contacts&control=contact_edit`
      - `workbench?workspace=preview-ws&tab=tab-contacts&control=contact_remove`
      - `workbench?workspace=preview-ws&tab=tab-contacts&control=contact_connection`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `contact_edit`
      - `contact_remove`
      - `contact_connection`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-contacts&control=contact_edit`
      - `tab-contacts&control=contact_remove`
      - `tab-contacts&control=contact_connection`
- Practical runtime truth after this slice:
  - Contacts edit/remove/connection are no longer recursive-parity-only contracts.
  - all three controls now have visible restored-runner browser routes and prove Contacts shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `406 passed`

## Cross-Codex Refresh (2026-07-06T20:07:44+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Vehicle edit/delete/mod actions now have visible wrapper affordances and direct route-surface proof:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added restored-runner PRV links for:
      - `Edit vehicle for PRV`
      - `Remove vehicle for PRV`
      - `Add vehicle mod for PRV`
    - added wrapper constants for:
      - `vehicle_edit`
      - `vehicle_delete`
      - `vehicle_mod_add`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels and exact hrefs:
      - `workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_edit`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_delete`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=vehicle_mod_add`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `vehicle_edit`
      - `vehicle_delete`
      - `vehicle_mod_add`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-gear&control=vehicle_edit`
      - `tab-gear&control=vehicle_delete`
      - `tab-gear&control=vehicle_mod_add`
- Practical runtime truth after this slice:
  - Vehicle edit/delete/mod add are no longer recursive-parity-only contracts.
  - all three controls now have visible restored-runner browser routes and prove Gear shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `394 passed`

## Cross-Codex Refresh (2026-07-06T19:56:21+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Cyberware edit/delete actions now have visible wrapper affordances and direct route-surface proof:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added restored-runner PRV links for:
      - `Edit cyberware for PRV`
      - `Remove cyberware for PRV`
    - added wrapper constants for:
      - `cyberware_edit`
      - `cyberware_delete`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels and exact hrefs:
      - `workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_edit`
      - `workbench?workspace=preview-ws&tab=tab-cyberware&control=cyberware_delete`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `cyberware_edit`
      - `cyberware_delete`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-cyberware&control=cyberware_edit`
      - `tab-cyberware&control=cyberware_delete`
- Practical runtime truth after this slice:
  - Cyberware edit/delete are no longer recursive-parity-only contracts.
  - both controls now have visible restored-runner browser routes and prove Cyberware shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `382 passed`

## Cross-Codex Refresh (2026-07-06T19:48:29+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Combat/Magic actions now have direct route-surface proof:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels:
      - `Reload weapon for PRV`
      - `Review damage track for PRV`
      - `Add general magic item for PRV`
      - `Bind spirit for PRV`
      - `Show magic source for PRV`
    - workbench entrypoint proof now pins exact hrefs for:
      - `workbench?workspace=preview-ws&tab=tab-combat&control=combat_reload`
      - `workbench?workspace=preview-ws&tab=tab-combat&control=combat_damage_track`
      - `workbench?workspace=preview-ws&tab=tab-magician&control=magic_add`
      - `workbench?workspace=preview-ws&tab=tab-magician&control=magic_bind`
      - `workbench?workspace=preview-ws&tab=tab-magician&control=magic_source`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `combat_reload`
      - `combat_damage_track`
      - `magic_add`
      - `magic_bind`
      - `magic_source`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-combat&control=combat_reload`
      - `tab-combat&control=combat_damage_track`
      - `tab-magician&control=magic_add`
      - `tab-magician&control=magic_bind`
      - `tab-magician&control=magic_source`
- Practical runtime truth after this slice:
  - the visible browser Combat reload/damage-track and Magic add/bind/source actions are no longer wrapper-only affordances.
  - all five routes now prove their Combat or Magic shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `374 passed`

## Cross-Codex Refresh (2026-07-06T19:42:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner Gear/Drug inventory actions now have direct route-surface proof:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels:
      - `Edit gear for PRV`
      - `Remove gear for PRV`
      - `Show gear source for PRV`
      - `Mount gear for PRV`
      - `Remove drug for PRV`
    - workbench entrypoint proof now pins exact hrefs for:
      - `workbench?workspace=preview-ws&tab=tab-gear&control=gear_edit`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=gear_delete`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=gear_source`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=gear_mount`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=drug_delete`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `gear_edit`
      - `gear_delete`
      - `gear_mount`
      - `gear_source`
      - `drug_delete`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-gear&control=gear_edit`
      - `tab-gear&control=gear_delete`
      - `tab-gear&control=gear_mount`
      - `tab-gear&control=gear_source`
      - `tab-gear&control=drug_delete`
- Practical runtime truth after this slice:
  - the visible browser Gear/Drug edit, remove, mount, and source actions are no longer wrapper-only affordances.
  - all five routes now prove Gear shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `354 passed`

## Cross-Codex Refresh (2026-07-06T19:36:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The restored-runner identity/license profile actions now have direct route-surface proof:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the visible restored-runner labels:
      - `Add SIN/license for PRV`
      - `Edit SIN/license for PRV`
      - `Remove SIN/license for PRV`
    - workbench entrypoint proof now pins exact hrefs for:
      - `workbench?workspace=preview-ws&tab=tab-info&control=identity_license_add`
      - `workbench?workspace=preview-ws&tab=tab-info&control=identity_license_edit`
      - `workbench?workspace=preview-ws&tab=tab-info&control=identity_license_delete`
    - added direct `/workbench` and `/preview` shell-route proof for:
      - `identity_license_add`
      - `identity_license_edit`
      - `identity_license_delete`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-info&control=identity_license_add`
      - `tab-info&control=identity_license_edit`
      - `tab-info&control=identity_license_delete`
- Practical runtime truth after this slice:
  - the visible browser actions for identity/SIN/license add, edit, and remove are no longer wrapper-only affordances.
  - all three routes now prove Profile shell return behavior across clean public, `/online`, compatibility workbench, and preview tools routes.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
    - warnings/errors: `0 Warning(s)`, `0 Error(s)`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `334 passed`

## Cross-Codex Refresh (2026-07-06T19:27:13+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged `show_source` continuation is now consistently Rules-owned across wrapper routes and recursive UI-control parity:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - moved the restored-runner `Show source for PRV` link from:
      - `tab-info&control=show_source`
    - to:
      - `tab-rules&control=show_source`
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - changed `show_source` return ownership from:
      - `tab-info` / `profile`
    - to:
      - `tab-rules` / `rules`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` route proof for:
      - `tab-rules&control=show_source`
    - public route proof now pins:
      - workflow label: `Rules`
      - shell title: `Rules shell`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins:
      - `Show source for PRV`
      - `workbench?workspace=preview-ws&tab=tab-rules&control=show_source`
    - added direct `/workbench` and `/preview` route proof for:
      - `tab-rules&control=show_source`
- Practical runtime truth after this slice:
  - source lookup no longer splits between a Profile restored-runner route and a Rules context-action route.
  - `show_source` now returns to and renders through the Rules shell on public, compatibility, preview, and recursive parity paths.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `322 passed`

## Cross-Codex Refresh (2026-07-06T19:20:37+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged `toggle_free_paid` continuation is now aligned between the browser wrapper and recursive UI-control parity:
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - changed `toggle_free_paid` return ownership from:
      - `tab-info` / `profile`
    - to:
      - `tab-gear` / `inventory`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` route proof for:
      - `tab-gear&control=toggle_free_paid`
    - public route proof now pins:
      - workflow label: `Gear`
      - shell title: `Gear shell`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins:
      - `Toggle gear free/paid for PRV`
      - `workbench?workspace=preview-ws&tab=tab-gear&control=toggle_free_paid`
    - added direct `/workbench` and `/preview` route proof for:
      - `tab-gear&control=toggle_free_paid`
- Practical runtime truth after this slice:
  - the visible browser action named `Toggle gear free/paid` no longer disagrees with the recursive parity return contract.
  - `toggle_free_paid` now returns to and renders through the Gear shell on public, compatibility, preview, and recursive parity paths.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `318 passed`

## Cross-Codex Refresh (2026-07-06T19:14:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The last unstaged SR6 quick-action roots now have visible PRV wrapper affordances and direct route-surface proof:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added restored-runner PRV links for:
      - `Add weapon for PRV`
      - `Add skill for PRV`
      - `Add vehicle for PRV`
      - `Add drug for PRV`
    - added wrapper constants for:
      - `combat_add_weapon`
      - `skill_add`
      - `vehicle_add`
      - `drug_add`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-route proof for:
      - `tab-combat&control=combat_add_weapon`
      - `tab-skills&control=skill_add`
      - `tab-gear&control=vehicle_add`
      - `tab-gear&control=drug_add`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now pins the new visible PRV affordances and exact hrefs
    - added direct `/workbench` shell-route proof for:
      - `combat_add_weapon`
      - `skill_add`
      - `vehicle_add`
      - `drug_add`
    - added direct `/preview` shell-route proof for the same four controls
- Practical runtime truth after this slice:
  - every current SR6 quick-action root from `WorkflowParityGateTests` is now present in `Preview.razor`, `AppRouteSurfaceTests.cs`, and `PublicPreviewSurfaceTests.cs`.
  - the four previously missing roots are no longer parity-only contracts; they are visible browser routes with public, compatibility, and preview shell proof.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `313 passed`

## Cross-Codex Refresh (2026-07-06T19:05:03+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the latest controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged profile notes continuation now has the same route-proof depth as the other committed-result browser continuations:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` control-route proof for:
      - `tab-info&control=open_notes`
    - added direct `/app` and `/online` committed-result route proof for:
      - `tab-info&control=open_notes&dialog_action=save`
    - public route proof now pins:
      - workflow label: `Profile`
      - shell title: `Profile shell`
      - committed result:
        - `Notes saved.`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now explicitly asserts the surfaced PRV affordances for:
      - `Save dossier notes for PRV`
      - `Edit dossier notes for PRV`
      - plus their exact `tab-info&control=open_notes` hrefs
    - added direct `/workbench` control-route proof for:
      - `open_notes`
    - added direct `/workbench` committed-result route proof for:
      - `open_notes&dialog_action=save`
    - added direct `/preview` control-route proof for:
      - `open_notes`
    - added direct `/preview` committed-result route proof for:
      - `open_notes&dialog_action=save`
- Practical runtime truth after this slice:
  - profile notes no longer rely on a visible affordance and the generic profile-tab shell proof alone; the routed notes editor and the save continuation are both directly pinned on public, compatibility, and preview surfaces.
  - the wrapper already had the `open_notes` save/result mapping; the gap was route-surface proof, not runtime copy or workflow ownership.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `297 passed`

## Cross-Codex Refresh (2026-07-06T18:55:29+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `public_guide_convergence`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The remaining staged SR6 quick-action control routes now have direct route-surface proof instead of relying on tab-only shell coverage or bootstrap-only tests:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` control-route proof for:
      - `tab-gear&control=gear_add`
      - `tab-combat&control=combat_add_armor`
      - `tab-qualities&control=quality_add`
      - `tab-adept&control=adept_power_add`
      - `tab-magician&control=spirit_add`
      - `tab-technomancer&control=matrix_program_add`
    - public route proof now pins the named shell for each of those controls:
      - `Gear`
      - `Combat`
      - `Qualities`
      - `Adept`
      - `Magic`
      - `Matrix`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench entrypoint proof now explicitly asserts the surfaced PRV affordances for:
      - `Add armor for PRV`
      - `Add adept power for PRV`
      - `Add spirit for PRV`
      - `Add Matrix program for PRV`
      - `Add gear for PRV`
      - plus the exposed quality filter route:
        - `workbench?tab=tab-qualities&control=quality_add`
    - added direct `/workbench` control-route proof for:
      - `gear_add`
      - `combat_add_armor`
      - `quality_add`
      - `adept_power_add`
      - `spirit_add`
      - `matrix_program_add`
    - added direct `/preview` control-route proof for the same six controls
- Practical runtime truth after this slice:
  - the browser wrapper now has direct shell-presence proof for every currently staged SR6 quick-action root that already exposes a control route without a committed-result dialog action.
  - `gear_add`, `combat_add_armor`, `quality_add`, `adept_power_add`, `spirit_add`, and `matrix_program_add` no longer depend on indirect inference from lane copy, bootstrap-only tests, or parity contracts to show that the wrapper keeps the correct shell active.
  - release posture is still non-flagship in practice; `public_guide_convergence` and the Windows visual-audit blocker remain unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `289 passed`

## Cross-Codex Refresh (2026-07-06T18:48:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `public_guide_convergence`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged Adept and Cyberware add continuations now have the same route-surface proof depth as the already-covered spell, critter, and complex-form add flows:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - expanded direct `/app` and `/online` committed-result route proof for:
      - `tab-adept&control=initiation_add&dialog_action=add`
      - `tab-cyberware&control=cyberware_add&dialog_action=add`
    - public route proof now pins:
      - workflow labels: `Adept`, `Cyberware`
      - shell titles: `Adept shell`, `Cyberware shell`
      - committed results:
        - `Initiation/submersion reward 'Masking' added.`
        - `Cyberware 'Wired Reflexes 2' added.`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - expanded direct `/workbench` committed-result route proof for:
      - `tab-adept&control=initiation_add&dialog_action=add`
      - `tab-cyberware&control=cyberware_add&dialog_action=add`
    - expanded direct `/preview` committed-result route proof for:
      - `tab-adept&control=initiation_add&dialog_action=add`
      - `tab-cyberware&control=cyberware_add&dialog_action=add`
- Practical runtime truth after this slice:
  - Adept and Cyberware add continuations were already staged in the browser wrapper, but they no longer rely on indirect bootstrap-only proof.
  - public, compatibility, and preview surfaces now prove those add routes stay attached to the named shell and surface the expected committed-result cue.
  - release posture is still non-flagship in practice; `public_guide_convergence` and the Windows visual-audit blocker remain unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `265 passed`

## Cross-Codex Refresh (2026-07-06T18:41:44+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `public_guide_convergence`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while any root blocker remains, especially while `release_truth:windows_installer_visual_audit` is unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged Skills browser lane now has explicit wrapper workflow ownership instead of falling through to generic dossier metadata:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added `IsSkillsWorkflow`
    - `tab-skills` now resolves to:
      - workflow status: `Skills`
      - workflow data key: `skills`
  - `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
    - added a loaded-workspace proof that `SelectTabAsync("tab-skills")` lands on:
      - active tab `tab-skills`
      - section `skills`
      - action `tab-skills.skills`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - expanded direct `/app` and `/online` shell-copy proof for:
      - `tab-skills`
      - `tab-qualities`
      - `tab-combat`
      - `tab-contacts`
    - added direct `/app` and `/online` control-route proof for:
      - `tab-skills&control=skill_specialize`
      - `tab-skills&control=skill_remove`
      - `tab-skills&control=skill_group`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added workbench source-affordance proof for:
      - `Specialize skill for PRV`
      - `Remove skill for PRV`
      - `Edit skill group for PRV`
    - added explicit workbench href proof for:
      - `tab-skills`
      - `tab-combat`
      - `tab-skills&control=skill_specialize`
      - `tab-skills&control=skill_remove`
      - `tab-skills&control=skill_group`
    - expanded direct `/workbench` and `/preview` shell-copy proof for:
      - `tab-skills`
      - `tab-qualities`
      - `tab-combat`
      - `tab-contacts`
    - added direct `/workbench` and `/preview` control-route proof for:
      - `skill_specialize`
      - `skill_remove`
      - `skill_group`
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - quick-action root classification is green with `create_entry` explicitly flagged as a root
- Practical runtime truth after this slice:
  - `tab-skills` wrapper routes now publish the shared Skills shell instead of dossier fallback metadata.
  - direct shell-copy coverage now spans the remaining staged Skills-adjacent lanes across public, workbench, and preview surfaces.
  - the quick-action root parity gate currently matches the discovered section-root set; there is no remaining root-classification mismatch in the focused lane.
  - release posture is still non-flagship in practice; `public_guide_convergence` and the Windows visual-audit blocker remain unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages -p:UseSharedCompilation=false`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_skills_tab|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Quick_action_roots_are_exhaustively_classified|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `260 passed`

## Cross-Codex Refresh (2026-07-06T18:05:31+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged Career browser lane now has explicit compatibility-tab backing instead of relying on the `tab-info` fallback:
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - added dedicated compatibility tab:
      - `tab-calendar`
    - added compatibility action ownership:
      - `tab-calendar.calendar`
      - label: `Career Log`
    - section backing uses the real `calendar` section token rather than another profile-backed placeholder
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - now pins the dedicated career tab and its action inventory directly
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - `create_entry`
    - `edit_entry`
    - `delete_entry`
    - `move_up`
    - `move_down`
    - now return to `tab-calendar` / `calendar` during recursive parity checks
  - `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
    - added a loaded-workspace proof that `SelectTabAsync("tab-calendar")` lands on:
      - active tab `tab-calendar`
      - section `calendar`
      - action `tab-calendar.calendar`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-copy proof for:
      - `tab-calendar`
    - added direct `/app` and `/online` committed-result proof for:
      - `tab-calendar&control=create_entry&dialog_action=add`
      - `tab-calendar&control=edit_entry&dialog_action=apply`
      - `tab-calendar&control=delete_entry&dialog_action=delete`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added workbench source-affordance proof for:
      - `Resume PRV on career log`
      - `Add and keep career entry`
      - `Apply career entry edit`
      - `Remove and keep career entry result`
      - `Move career entry up`
      - `Move career entry down`
    - added direct `/workbench` and `/preview` shell-copy proof for:
      - `tab-calendar`
    - added direct `/workbench` and `/preview` committed-result proof for:
      - `create_entry`
      - `edit_entry`
      - `delete_entry`
    - added direct workbench bootstrap proof for:
      - `create_entry`
      - `edit_entry`
      - `delete_entry`
      - `move_up`
      - `move_down`
      - plus their routed dialog actions
- Practical runtime truth after this slice:
  - the staged career controls are no longer split between `tab-calendar` wrapper URLs and `tab-info` parity contracts
  - the browser wrapper now has direct route-proof coverage for the Career shell on public, compatibility, and preview surfaces
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_critter_tab|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_stats_tab|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_career_tab|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Quick_action_roots_are_exhaustively_classified|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `212 passed`

## Cross-Codex Refresh (2026-07-06T17:54:27+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged Runner Intelligence browser lane now has explicit compatibility-tab backing instead of relying on the `tab-info` fallback:
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - added dedicated compatibility tab:
      - `tab-stats`
    - added compatibility action ownership:
      - `tab-stats.profile`
      - label: `Runner Intelligence`
    - section backing remains the existing safe `profile` section because there is still no standalone runtime `statistics` section token
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - now pins the dedicated stats tab and its action inventory directly
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - `runner_benchmark`
    - `runner_what_if`
    - `runner_cohort_privacy`
    - now return to `tab-stats` / `profile` during recursive parity checks
  - `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
    - added a loaded-workspace proof that `SelectTabAsync("tab-stats")` lands on:
      - active tab `tab-stats`
      - section `profile`
      - action `tab-stats.profile`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-copy proof for:
      - `tab-stats`
    - added direct `/app` and `/online` control-route proof for:
      - `tab-stats&control=runner_benchmark`
      - `tab-stats&control=runner_what_if`
      - `tab-stats&control=runner_cohort_privacy`
    - those public routes now prove the shared Stats shell stays active while the staged control id reaches the presenter
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added workbench source-affordance proof for:
      - `Open Runner Intelligence benchmarks for PRV`
      - `Model Increase Initiative and inventory what-if stack for PRV`
      - `Review Runner Intelligence privacy cohorts for PRV`
    - added direct `/workbench` and `/preview` shell-copy proof for:
      - `tab-stats`
    - added direct `/workbench` and `/preview` control-route proof for:
      - `runner_benchmark`
      - `runner_what_if`
      - `runner_cohort_privacy`
- Practical runtime truth after this slice:
  - the staged Runner Intelligence controls are no longer split between `tab-stats` wrapper URLs and `tab-info` parity contracts
  - the browser wrapper now has direct route-proof coverage for stats shell entry on public, compatibility, and preview surfaces
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_critter_tab|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_stats_tab|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Quick_action_roots_are_exhaustively_classified|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `195 passed`

## Cross-Codex Refresh (2026-07-06T17:41:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The staged critter browser lane is tighter now across the wrapper route, compatibility resolver, and recursive UI-control parity:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added an explicit `Add and keep critter power` source route for:
      - `tab-critter&control=critter_power_add&dialog_action=add`
    - added committed-result mapping:
      - `Critter power 'Natural Weapon' added.`
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - added a dedicated compatibility tab:
      - `tab-critter`
    - added compatibility action ownership:
      - `tab-critter.critterpowers`
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - now pins the dedicated critter tab and its action inventory directly
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - `critter_power_add` now returns to `tab-critter` / `critterpowers` during recursive parity checks
  - `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
    - added a loaded-workspace proof that `SelectTabAsync("tab-critter")` lands on:
      - active tab `tab-critter`
      - section `critterpowers`
      - action `tab-critter.critterpowers`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-copy proof for:
      - `tab-critter`
    - added direct `/app` and `/online` committed-result proof for:
      - `tab-critter&control=critter_power_add&dialog_action=add`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added source-affordance proof for the critter add-and-keep route
    - added direct `/workbench` and `/preview` shell-copy proof for:
      - `tab-critter`
    - added direct `/workbench` and `/preview` committed-result proof for:
      - `critter_power_add&dialog_action=add`
    - added direct workbench bootstrap proof for:
      - `critter_power_add`
      - `critter_power_add&dialog_action=add`
- Practical runtime truth after this slice:
  - the browser wrapper’s dedicated critter lane is no longer just staged copy; it is now directly pinned through route tests and compatibility-tab contract coverage.
  - critter add continuations no longer look like silent state changes when routed with `dialog_action=add`; they surface an explicit committed-result cue like the other proven add flows.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_critter_tab|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Quick_action_roots_are_exhaustively_classified|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity" --output Normal`
    - result: `195 passed`

## Cross-Codex Refresh (2026-07-06T17:27:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The compatibility shell and parity gate now agree on SR6 technomancer ownership for Matrix quick-add flows:
  - `Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs`
    - added a dedicated `tab-technomancer` compatibility navigation tab
    - moved compatibility action ownership for:
      - `complexforms`
      - `aiprograms`
      - plus `sprites`
      from the `tab-adept` bucket into `tab-technomancer`
  - `Chummer.Tests/Presentation/CatalogOnlyRulesetShellCatalogResolverTests.cs`
    - now pins the dedicated technomancer tab and its action inventory directly
  - `Chummer.Tests/Presentation/WorkflowParityGateTests.cs`
    - `complex_form_add` and `matrix_program_add` now return to `tab-technomancer` during recursive UI-control parity checks
  - `Chummer.Tests/Presentation/CharacterOverviewPresenterTests.cs`
    - added a loaded-workspace proof that `SelectTabAsync("tab-technomancer")` lands on:
      - active tab `tab-technomancer`
      - section `complexforms`
      - action `tab-technomancer.complexforms`
  - `Chummer.Tests/RulesetSeamContractsTests.cs`
    - fixed the stale SR6 seam assertion that still expected `tab-adept.complexforms`
    - it now matches the real SR6 technomancer catalog shape
- Practical runtime truth after this slice:
  - SR6 Matrix/technomancer continuations no longer rely on an adept-owned compatibility fallback when the presenter or workflow parity harness resolves return tabs.
  - the browser-route Matrix shell proof from the previous slice now lines up with the compatibility resolver and the local seam contract instead of only the route wrapper copy.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.CatalogOnlyRulesetShellCatalogResolverTests|FullyQualifiedName~Chummer.Tests.Presentation.CharacterOverviewPresenterTests.SelectTabAsync_uses_compatibility_catalog_for_sr6_technomancer_tab|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Quick_action_roots_are_exhaustively_classified|FullyQualifiedName~Chummer.Tests.Presentation.WorkflowParityGateTests.Legacy_ui_controls_keep_recursive_parity|FullyQualifiedName~Chummer.Tests.RulesetSeamContractsTests.Build_lab_create_tab_and_action_are_exposed_across_ruleset_catalogs" --output Normal`
    - result: `7 passed`

## Cross-Codex Refresh (2026-07-06T17:15:38+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The browser-shell route proof is tighter now for Magic and Matrix continuations, and the previously missing contact-add committed-result slice is no longer absent from local handoff history:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` shell-copy proof for:
      - `tab-magician` -> `Magic`
      - `tab-technomancer` -> `Matrix`
    - added direct `/app` and `/online` committed-result proof for:
      - `/app?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add`
      - `/online?workspace=preview-ws&tab=tab-magician&control=spell_add&dialog_action=add`
      - `/app?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add`
      - `/online?workspace=preview-ws&tab=tab-technomancer&control=complex_form_add&dialog_action=add`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added direct `/workbench` and `/preview` shell-copy proof for:
      - `tab-magician` -> `Magic`
      - `tab-technomancer` -> `Matrix`
    - added direct `/workbench` and `/preview` committed-result proof for:
      - `spell_add&dialog_action=add`
      - `complex_form_add&dialog_action=add`
- Practical runtime truth after this slice:
  - `tab-magician` and `tab-technomancer` no longer rely on indirect coverage to prove named-shell posture on app, online, workbench, and preview routes.
  - spell-add and complex-form-add continuations now have direct proof that the shared shell keeps a visible committed-result cue instead of looking like a silent state change.
  - contact-add committed-result coverage is now represented in the local handoff sequence again.
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved.
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `206 passed`

## Cross-Codex Refresh (2026-07-06T17:01:32+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- More routed tab families now graduate from generic dossier fallback into named workflow shells:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - promoted named workflow recognition for existing routed tabs including:
      - `Cyberware`
      - `Adept`
      - plus the remaining named tab families needed to avoid generic fallback on known routes
    - practical effect:
      - `/app?workspace=preview-ws&tab=tab-cyberware` now surfaces `Continue the shared Cyberware shell.`
      - `/app?workspace=preview-ws&tab=tab-adept` now surfaces `Continue the shared Adept shell.`
      - `/workbench?workspace=preview-ws&tab=tab-cyberware` now surfaces `Cyberware` / `Cyberware shell`
      - `/preview?workspace=preview-ws&tab=tab-adept` now surfaces `Adept` / `Adept shell`
- Route proof is stronger now:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` proof for cyberware and adept tab continuations
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added direct `/workbench` and `/preview` proof for cyberware and adept shell titles and route-aware summaries
- Practical runtime truth after this slice:
  - known routed tabs such as cyberware and adept no longer present themselves as generic dossier continuations
  - the browser shell’s named-workflow posture is now more complete across app, preview, and compatibility surfaces
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `186 passed`
- Practical effect for the next Codex:
  - if you keep promoting tab workflows, use the routed links already present in preview/workbench cards as the first proof targets
  - keep workflow-recognition and user-facing shell-copy changes in the same slice; one without the other leaves fallback gaps or unproven behavior

## Cross-Codex Refresh (2026-07-06T16:56:35+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Non-output workspace/tab continuations now expose specific workflow shells instead of collapsing back to a generic dossier shell:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `tab-rules` is now recognized as a first-class workflow instead of falling through to dossier fallback
    - app, preview, and compatibility shells now publish workflow-specific shell titles and summaries for non-output tabs such as:
      - `Gear shell`
      - `Rules shell`
    - practical effect:
      - `/app?workspace=preview-ws&tab=tab-gear` now surfaces `Continue the shared Gear shell.`
      - `/app?workspace=preview-ws&tab=tab-rules` now surfaces `Continue the shared Rules shell.`
      - `/workbench?workspace=preview-ws&tab=tab-gear` now surfaces `Gear` / `Gear shell` across classic chrome and shared frame
      - `/preview?fixture=blue&tab=tab-rules` now surfaces `Rules` / `Rules shell` instead of generic dossier framing
- Route proof is stronger now:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct `/app` and `/online` proof for gear and rules tab continuations, including alias-aware copy on `/online`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - added direct `/workbench` and `/preview` proof that gear and rules tab continuations now expose workflow-specific shell titles and route-aware summaries
- Practical runtime truth after this slice:
  - app, preview, and compatibility routes now describe tab-driven runner contexts more honestly instead of flattening them into a dossier fallback
  - the browser shell now treats rules navigation as a named workflow instead of an unlabeled generic continuation
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `178 passed`
- Practical effect for the next Codex:
  - if you keep tightening tab-driven shells, check named workflow coverage before adding copy; missing workflow recognition causes fallback regressions that copy-only tests will miss
  - keep direct proof across app, preview, and compatibility surfaces when a workflow graduates from generic dossier fallback to a named shell

## Cross-Codex Refresh (2026-07-06T16:49:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The public app shell now keeps `/online` alias copy honest instead of reusing `/app` wording that claimed everything was happening on the generic clean public route:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added alias-aware public app surface phrasing:
      - `the clean public route`
      - `the clean public /online alias`
    - practical effect:
      - `/online?workspace=preview-ws&command=save_character_as&dialog_action=download` now says the runner download is ready on the clean public `/online` alias
      - `/online?workspace=preview-ws&command=export_character&dialog_action=download` now says the export package download is ready on the clean public `/online` alias
      - `/online?workspace=preview-ws&command=print_preview` now says print preview opens on the clean public `/online` alias
      - `/online?workspace=preview-ws&command=open_for_export` now says export staging stays attached to the clean public `/online` alias
      - non-output startup shells on `/online` now match too:
        - Build Lab stays on the clean public `/online` alias
        - the shared import dialog opens from the clean public `/online` alias
    - `/app` keeps the existing clean public route wording
- Route proof is stronger now:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `/online?...` output-route proof now directly checks the alias-specific panel summary and shared-frame copy
    - `/online?workspace=preview-ws&tab=tab-create` and `/online?workspace=preview-ws&command=open_character` now directly prove the build-lab and open-dossier summaries mention the clean public `/online` alias
- Practical runtime truth after this slice:
  - `/online` no longer presents itself as the generic clean public route when the user is explicitly on the alias path
  - `/app`, `/online`, `/preview`, and `/workbench` are now closer on route-surface honesty as well as command-state specificity
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `170 passed`
- Practical effect for the next Codex:
  - if you keep tightening public-app copy, verify `/app` and `/online` separately; route-family parity is no longer just a canonical-route question
  - keep route-surface honesty checks close to the user-facing startup shell strings, not just route metadata

## Cross-Codex Refresh (2026-07-06T16:43:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Preview and compatibility routes now keep the shared frame summary honest about which route surface is actually carrying the continuation:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - shared frame continuation summaries now use a route-aware surface phrase:
      - `the clean public route`
      - `the compatibility route`
      - `the preview tools route`
    - practical effect:
      - `/workbench?command=save_character_as&dialog_action=download` now says the browser download continuation is opening from the compatibility route
      - `/workbench?command=export_character&dialog_action=download` now says the export package download continuation is opening from the compatibility route
      - `/preview?fixture=blue&command=print_preview` now says the print preview continuation is opening from the preview tools route
      - `/preview?command=open_for_export` now says the export staging workflow is opening from the preview tools route
    - `/app` keeps the existing clean public route wording
- Route proof is stronger now:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - workbench output-route proof now directly checks that the shared frame header text includes `from the compatibility route.`
    - preview result-route proof now directly checks that the shared frame header text includes `from the preview tools route.`
- Practical runtime truth after this slice:
  - preview and compatibility routes no longer claim they are opening explicit continuations from the clean public route when they are not
  - app, preview, and compatibility surfaces are now closer on both specificity and route honesty in the shared shell header
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `170 passed`
- Practical effect for the next Codex:
  - if you keep tightening shared frame copy, check route-surface honesty as well as command specificity
  - keep direct `/preview` and `/workbench` proof whenever shared frame summaries mention route posture

## Cross-Codex Refresh (2026-07-06T16:38:22+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Preview and compatibility routes now keep the shared frame kicker aligned with explicit route state instead of falling back to the generic `Chummer Online shell` label during continuations:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - the shared frame kicker now uses:
      - `StartupCommandDisplayLabel` when the route carries a command
      - the active workflow label when the route carries non-command payload
      - the generic `Chummer Online shell` label only when no route state is selected
    - practical effect:
      - `/preview?fixture=blue&command=save_character_as&dialog_action=download` now surfaces `Download Runner` above `Download Runner shell`
      - `/preview?fixture=blue&command=export_character&dialog_action=download` now surfaces `Download Export Package` above `Download package shell`
      - `/preview?fixture=blue&command=print_preview` now surfaces `Open Print Preview` above `Print preview shell`
      - `/preview?command=open_for_export` now surfaces `Open Export Staging` above `Export staging shell`
      - the same specific kicker now appears on `/workbench?command=...` output routes too
- Route proof is stronger now:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - preview result-route proof now directly checks the shared frame kicker as well as the shared frame title
    - workbench output-route proof now checks the frame kicker alongside the classic titlebar, footer workflow label, and frame title while `data-active-workflow` stays category-level
- Practical runtime truth after this slice:
  - preview and compatibility routes no longer present a generic top line above the shared frame title when the route is explicitly in a download, print-preview, or export-staging continuation
  - public app, preview, and compatibility shells are tighter on user-facing state specificity across both frame header lines
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `170 passed`
- Practical effect for the next Codex:
  - if you keep tightening shared frame copy, verify the kicker, title, and summary together rather than fixing only one line
  - keep direct `/preview` and `/workbench` route proof for any shared frame-header change

## Cross-Codex Refresh (2026-07-06T16:34:12+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Preview and compatibility route frame headers now surface the real shell state instead of the generic `Open Chummer Online` heading during explicit continuations:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `RouteOpenTitle` is now route-state aware for:
      - Origin Dossier
      - Character Roster
      - Build Lab
      - Open Dossier
      - save/download continuations
      - print staging/preview continuations
      - export staging/download continuations
    - practical effect:
      - `/preview?fixture=blue&command=save_character_as&dialog_action=download` now surfaces `Download Runner shell`
      - `/preview?fixture=blue&command=export_character&dialog_action=download` now surfaces `Download package shell`
      - `/preview?fixture=blue&command=print_preview` now surfaces `Print preview shell`
      - `/preview?command=open_for_export` now surfaces `Export staging shell`
      - the same specific shell titles now appear on `/workbench?command=...` routes too
- Route proof is stronger now:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - preview result-route proof now directly checks the shared frame `<h2>` for those explicit output states
    - workbench output-route proof now checks the shared frame `<h2>` alongside the classic top and bottom chrome labels while `data-active-workflow` stays category-level
- Practical runtime truth after this slice:
  - preview and compatibility routes no longer present a generic shell heading when the route is explicitly in a download, print-preview, or export-staging continuation
  - public app, preview, and compatibility shells are closer on user-facing shell-title specificity, while internal workflow tokens stay category-stable
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `170 passed`
- Practical effect for the next Codex:
  - if you keep tightening preview/workbench copy, check the shared frame header directly; the startup-label paragraph alone is not enough
  - keep route-proof coverage on both `/preview` and `/workbench` when shared shell headings change

## Cross-Codex Refresh (2026-07-06T16:28:52+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The compatibility shell now keeps its classic status footer aligned with the already-specific workbench titlebar for output continuations:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - the shared classic-shell display label is now used in both:
      - the window titlebar
      - the `Workflow` cell in the classic footer
    - practical effect:
      - `save_character_as&dialog_action=download` now surfaces `Download Runner` in both top and bottom chrome
      - `export_character&dialog_action=download` now surfaces `Download Export Package` in both places
      - `print_preview` now surfaces `Open Print Preview` in both places
      - `open_for_export` now surfaces `Open Export Staging` in both places
    - workflow category state remains intentionally coarse:
      - `data-active-workflow`
      - `data-output-workflow`
      - related save/print/export route tokens
- Compatibility-route proof is stronger now:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - `/workbench?command=...` now directly proves the classic footer `Workflow` value matches the specific output-state label while `data-active-workflow` stays on the broader category token
- Practical runtime truth after this slice:
  - `/workbench` no longer falls back to generic `Save`/`Print`/`Export` wording in the bottom status chrome after the top titlebar became specific
  - public app, preview, and compatibility shells are tighter on user-facing output-state language, while internal workflow tokens stay category-stable
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `170 passed`
- Practical effect for the next Codex:
  - if you keep tightening workbench output copy, check both the top and bottom chrome together instead of fixing one and leaving the other generic
  - keep direct `/workbench` proof whenever the user-facing chrome changes; route metadata alone is not enough

## Cross-Codex Refresh (2026-07-06T16:24:03+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The compatibility shell now matches the public app shell more closely at the top-level output heading layer instead of collapsing output continuations back to coarse `Save`/`Print`/`Export` wording:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - the classic workbench titlebar now uses a specific output-state label for save/print/export continuations
    - practical effect:
      - `save_character_as&dialog_action=download` now surfaces `Download Runner`
      - `export_character&dialog_action=download` now surfaces `Download Export Package`
      - `print_preview` now surfaces `Open Print Preview`
      - `open_for_export` now surfaces `Open Export Staging`
    - workflow category state remains intentionally coarse:
      - `data-active-workflow`
      - `data-output-workflow`
      - related save/print/export route tokens
- Compatibility-route proof is stronger now:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - `/workbench?command=...` now directly proves the titlebar ends with the specific output-state label while `data-active-workflow` stays on the broader category token
- Practical runtime truth after this slice:
  - `/workbench` no longer hides download/preview/staging intent behind a generic titlebar when the route is explicitly in an output continuation
  - `/app`, `/online`, `/preview`, and `/workbench` are now closer on user-facing output-state language, while internal workflow tokens stay category-stable
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `170 passed`
- Practical effect for the next Codex:
  - if you keep refining workbench-route copy, preserve the distinction between heading-level output wording and the broader workflow-category tokens
  - verify `/workbench` directly when you tighten public-route copy; alias or shared-shell parity is not enough on its own

## Cross-Codex Refresh (2026-07-06T16:17:04+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The top-level app shell heading layer is now aligned with the already-specific output-route copy instead of falling back to coarse workflow labels:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - app-route surfaces now use `StartupCommandDisplayLabel` for:
      - `PageTitleText`
      - `AppRouteAriaLabel`
      - `AppRouteTitle`
      - `AppStartupPanelKicker`
    - practical effect:
      - `save_character_as` now surfaces `Prepare Runner Download` instead of a generic `Save`
      - `save_character_as&dialog_action=download` now surfaces `Download Runner`
      - other output-route headings stay explicit about staging/preview/export-package state instead of collapsing to plain `Print` or `Export`
    - data-workflow metadata remains unchanged:
      - `data-active-workflow`, `data-output-workflow`, and related parity tokens still stay on the broader save/print/export categories
- Alias parity proof is stronger now:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `/online?workspace=preview-ws&command=...` now directly proves the same specific output-copy posture already pinned on clean `/app`
    - the older broad route-matrix rows were also updated so save-as routes now expect download-facing copy instead of stale generic `Save` strings
- Practical runtime truth after this slice:
  - the public app shell now gives users specific output-state headings at the very top of the route, not just in deeper panel copy or committed-result banners
  - `/app` and `/online` are now better aligned on that heading-level output-state language
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `166 passed`
- Practical effect for the next Codex:
  - if you keep refining output routes, preserve the distinction between workflow metadata and user-facing headings; the shell can stay category-stable internally while still being explicit to users
  - when you add app-shell copy refinements, pin `/online` directly instead of assuming alias parity will stay covered by `/app` tests alone

## Cross-Codex Refresh (2026-07-06T16:10:00+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Output-route startup copy is now more specific about what the browser shell is actually doing instead of flattening everything into generic save/print/export wording:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added explicit command/dialog-state distinctions for:
      - runner save
      - browser download preparation
      - runner download ready
      - print staging
      - print preview opened
      - export staging
      - export package preparation
      - export package download ready
    - the refinements land across the user-visible shell copy that surrounds the existing committed-result banners:
      - `StartupCommandDisplayLabel`
      - `AppStartupSummary`
      - `AppStartupPanelTitle`
      - `AppStartupPanelSummary`
      - `AppRouteFrameTitle`
      - `RouteOpenSummary`
    - practical effect:
      - routes like `save_character_as&dialog_action=download` no longer read like a generic “prepare the shared save path”
      - routes like `open_for_export` and `print_preview` now say staging/preview explicitly instead of collapsing into broader export/print wording
- Route-proof coverage now pins those more truthful output-copy distinctions:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `/app?workspace=preview-ws&command=...` now directly proves the specific output copy for:
      - save-as download ready
      - export download ready
      - print preview
      - export staging
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - `/preview?...` now directly proves the same specific output-copy posture on the preview/result-check surface
- Practical runtime truth after this slice:
  - output continuations now communicate preparation vs staging vs ready-to-download more honestly on both:
    - the clean public app route
    - the preview/result-check surface
  - result banners remain in place; this pass makes the surrounding shell copy align with the already-hardened committed-result cues
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `162 passed`
- Practical effect for the next Codex:
  - if you keep refining output routes, preserve the distinction between staging, preparation, preview, and ready-to-download states instead of drifting back to generic workflow labels
  - keep the preview/result-check copy aligned with the public app shell; users should not get more precise result-state wording on one surface than the other

## Cross-Codex Refresh (2026-07-06T16:02:33+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The preview-tools result surface now publishes the same committed-result cue as the public and compatibility shells:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `/preview` now renders `data-preview-route-committed-result` whenever a seeded result-route continuation has concrete committed-result text
    - this closes the user-visible asymmetry where:
      - `/app`
      - `/online`
      - `/workbench`
      already showed an explicit output/result cue, but the dedicated preview result-check route still looked like a generic startup shell
  - `Chummer.Blazor/Components/Pages/Preview.razor.css`
    - the shared committed-result styling now explicitly covers the preview-tools surface too
- Route-proof coverage now pins the preview-side result cue directly:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - `/preview?fixture=blue&command=...` now directly proves committed-result banner text for:
      - `save_character`
      - `save_character_as`
      - `save_character_as&dialog_action=download`
      - `export_character`
      - `export_character&dialog_action=download`
      - `print_character`
      - `print_preview`
    - the earlier preview-runtime bootstrap proof remains intact; this new pass adds the missing user-visible result cue proof on top
- Practical runtime truth after this slice:
  - all three browser-facing result surfaces now publish explicit committed-result cues:
    - `/preview`
    - `/app` and `/online`
    - `/workbench`
  - the preview route now behaves more honestly as a result-state check surface instead of only a startup-command harness
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `154 passed`
- Practical effect for the next Codex:
  - if you keep hardening output/result flows, preserve committed-result parity across preview, public, and compatibility surfaces
  - do not let `/preview` regress back into a route that only boots the right command while hiding the actual result-state truth from the user

## Cross-Codex Refresh (2026-07-06T15:58:26+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Output continuations now publish an explicit user-visible committed-result cue instead of relying only on route metadata and generic shell copy:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - app-route startup shells now render `data-app-route-committed-result` when a routed continuation has a concrete output result message
    - the compatibility shell keeps `data-workbench-committed-result`, but the result text logic now also covers output commands in addition to control-dialog actions
    - committed-result text now explicitly distinguishes:
      - runner save
      - browser download prepared
      - runner download ready
      - export package prepared
      - export package download ready
      - print preview prepared/opened
  - `Chummer.Blazor/Components/Pages/Preview.razor.css`
    - added shared banner styling for the new app-route and workbench committed-result surfaces
- Route-proof coverage now pins the output result cue on both public and compatibility shells:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `/app?workspace=preview-ws&command=...` now directly proves committed-result banner text for:
      - `save_character`
      - `save_character_as`
      - `save_character_as&dialog_action=download`
      - `export_character`
      - `export_character&dialog_action=download`
      - `print_character`
      - `print_preview`
    - the same parity proof now exists on `/online?...`
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - `/workbench?command=...` now directly proves the committed-result banner for the same output/result command family, including download variants
- Practical runtime truth after this slice:
  - output continuations no longer look like generic startup shells only; users now get a concrete result-ready cue on:
    - `/app`
    - `/online`
    - `/workbench`
  - route metadata and dialog-action preservation remain unchanged; this was a UI-polish and proof-layer hardening pass, not a route-contract rewrite
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `147 passed`
- Practical effect for the next Codex:
  - when you harden output continuations, verify both the route contract and the user-visible result cue; metadata-only proof is not enough for this shell lane
  - if you extend committed-result text again, keep app-route and workbench parity aligned instead of letting one surface regress into generic startup copy

## Cross-Codex Refresh (2026-07-06T15:50:45+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- Fixture-driven browser output routes now have stronger runtime proof for download continuations instead of only string-level href coverage:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - fixture runtime coverage on `/app` now explicitly proves the public shared shell preserves:
      - `presenter.ExecutedDialogActionId`
      - `data-dialog-action`
      - both plain and `dialog_action=download` result routes for:
        - `save_character_as`
        - `export_character`
    - the same proof shape now exists on `/online`
    - this closes a real verification gap from the earlier fixture-route expansion, where download rows existed but the tests still were not asserting that the dialog action survived runtime bootstrap
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - preview-runtime coverage now also proves the seeded fixture result routes on `/preview?fixture=blue...` forward:
      - the expected startup command
      - the expected dialog action
    - important nuance:
      - `/preview` does not publish the `classic-chummer-shell` workbench metadata container
      - the truthful proof there is startup-command plus dialog-action bootstrap into the shared desktop shell, not fake workbench-style route metadata
- Practical runtime truth after this slice:
  - fixture download continuations are now better pinned across all three browser-facing route surfaces:
    - `/preview`
    - `/app`
    - `/online`
  - clean public surfaces now directly prove download dialog-action preservation for seeded save-as and export result routes, not just command/output workflow posture
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `126 passed`
- Practical effect for the next Codex:
  - when you add fixture-driven download continuations, require runtime proof of dialog-action forwarding on the public surfaces instead of stopping at href presence
  - do not assume `/preview` exposes the same shell metadata container as `/workbench`; prove what that surface actually publishes

## Cross-Codex Refresh (2026-07-06T15:44:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The seeded save-as result path is now pinned more explicitly on the preview surface and the clean public routes:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - the seeded `Open Save As Result` proof card now targets:
      - `preview?fixture=blue&command=save_character_as&dialog_action=download`
    - this keeps the explicit result-check card aligned with the already-advertised browser download continuation, without changing the broader generic save-as links that intentionally still open the plain `save_character_as` route
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - now directly proves the seeded preview surface emits:
      - `preview?fixture=blue&command=save_character_as&dialog_action=download`
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - fixture-driven runtime coverage now also includes:
      - `/app?fixture=blue&command=save_character_as&dialog_action=download`
      - `/online?fixture=blue&command=save_character_as&dialog_action=download`
- Practical runtime truth after this slice:
  - the explicit seeded save-as result route promoted from the preview surface now has direct proof on both the preview side and the clean `/app` plus `/online` shared-shell runtime side
  - the generic seeded save-as shell links still intentionally point at the non-download `save_character_as` route; only the explicit result-check card now carries `dialog_action=download`
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `119 passed`
- Practical effect for the next Codex:
  - when a seeded preview proof card is meant to represent the committed browser result, pin the exact dialog-action variant on that card instead of assuming the generic shell link shape is sufficient
  - the next likely parity wins are other specific dialog/control continuation routes or UI-polish regressions, not this seeded save-as download result path again

## Cross-Codex Refresh (2026-07-06T15:38:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The seeded export-download continuation is now better pinned at both the preview-link and runtime layers:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - the seeded preview surface now explicitly proves:
      - `preview?fixture=blue&command=export_character&dialog_action=download`
    - this is the right place for that assertion because the seeded preview cards advertise `Open Export Result`, while the restored-workspace workbench surface intentionally points export links at workspace-bound `/app?...` continuations instead
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - fixture-driven runtime coverage now also includes:
      - `/app?fixture=blue&command=export_character&dialog_action=download`
      - `/online?fixture=blue&command=export_character&dialog_action=download`
    - those routes are now directly pinned to the same shared-shell `export` / `download-package` posture already expected from the non-download fixture route family
- Practical runtime truth after this slice:
  - the seeded export result path advertised from the preview surface now has explicit route proof on both the compatibility/preview side and the clean public route side
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `117 passed`
- Practical effect for the next Codex:
  - when you harden preview-advertised continuations, keep the assertion on the surface that actually emits the route; seeded preview and restored workbench do not always share the same href shape
  - the next likely parity wins are more specific dialog/control continuation paths or UI-polish regressions, not the main seeded export/download route anymore

## Cross-Codex Refresh (2026-07-06T15:32:30+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- A narrower dialog-action parity gap is now closed in the public route matrix:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct runtime coverage for:
      - `/app?workspace=preview-ws&command=save_character_as&dialog_action=download`
      - `/online?workspace=preview-ws&command=save_character_as&dialog_action=download`
    - this matters because the preview surface already advertises the workspace-bound browser download continuation for `save_character_as`, but earlier route/runtime proofs only pinned the non-download save-as route and the export-download variant
- Practical runtime truth after this slice:
  - the shared-shell route matrix now directly proves the save-as browser-download continuation preserves:
    - the loaded workspace
    - the `save` workflow metadata
    - `data-dialog-action="download"`
    - non-roster shell behavior
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `79 passed`
- Practical effect for the next Codex:
  - the preview-advertised save/export download continuations are now better aligned with direct runtime proof
  - the next likely wins are deeper control/dialog continuation parity or UI-polish regressions rather than the remaining top-level output-route variants

## Cross-Codex Refresh (2026-07-06T15:29:21+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- A deeper public-route hardening pass exposed a real seeded-fixture bootstrap fault and then closed it:
  - new runtime coverage in `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs` now directly pins the fixture-driven public routes exposed from the preview surface:
    - `/app?fixture=blue&command=save_character`
    - `/app?fixture=blue&command=save_character_as`
    - `/app?fixture=blue&command=export_character`
    - `/app?fixture=blue&command=print_character`
    - `/app?fixture=blue&command=print_preview`
    - and the same family on `/online?...`
  - the first verification run failed truthfully:
    - fixture routes were throwing `FileNotFoundException` for `BLUE.chum5`
    - failure source was `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
    - `ResolveDemoFixturePath(...)` only accepted `AppContext.BaseDirectory/Fixtures/BLUE.chum5`
    - the MSTest host actually provides `Chummer.Tests/bin/.../TestFiles/BLUE.chum5`
- Runtime fix that landed:
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
    - `ResolveDemoFixturePath(...)` now accepts both known output layouts for the seeded browser demo fixture:
      - `AppContext.BaseDirectory/Fixtures/BLUE.chum5`
      - `AppContext.BaseDirectory/TestFiles/BLUE.chum5`
    - if neither exists, the exception is now more truthful about the searched locations instead of reporting only one hardcoded path
- Practical runtime truth after this slice:
  - the seeded public browser output routes promoted from the preview surface now boot cleanly in the test host instead of crashing during demo workspace bootstrap
  - route coverage now directly pins both `/app` and `/online` fixture-driven save/print/export flows in addition to the previously hardened command-only and workspace-bound continuations
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `77 passed`
- Practical effect for the next Codex:
  - do not assume seeded fixture links are “just coverage” anymore; they exposed a real bootstrap path dependency
  - the main public route families now have materially stronger runtime proof, so the next likely wins are deeper dialog/control continuation parity or other UI-polish regressions rather than the top-level app/alias command surface

## Cross-Codex Refresh (2026-07-06T15:16:02+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The public `/online` alias now has direct runtime coverage for the workspace-bound shared-shell continuations that were already pinned on clean `/app`:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct alias coverage for:
      - `/online?workspace=preview-ws&command=open_character`
      - `/online?workspace=preview-ws&tab=tab-create`
      - `/online?workspace=preview-ws&command=save_character`
      - `/online?workspace=preview-ws&command=save_character_as`
      - `/online?workspace=preview-ws&command=open_for_printing`
      - `/online?workspace=preview-ws&command=print_character`
      - `/online?workspace=preview-ws&command=open_for_export`
      - `/online?workspace=preview-ws&command=export_character`
      - `/online?workspace=preview-ws&command=export_character&dialog_action=download`
    - the new proofs pin the alias to the same shared-shell behavior already expected from `/app`:
      - workspace load of `preview-ws`
      - canonical route metadata remains `app` while the visible route segment stays `online`
      - the expected active workflow / output workflow / output target tuple is preserved
      - dialog action metadata is preserved where applicable
      - no silent fallback to the generic roster body
- Practical runtime truth after this slice:
  - the clean `/app` and `/online` public entrypoints now have direct runtime coverage parity for the main workspace-bound open/build/save/print/export continuation family
  - alias drift is now less likely both for command-only startup routes and for restored-workspace continuations
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `67 passed`
- Practical effect for the next Codex:
  - the main `/online` alias parity holes for the public browser shell are now mostly covered at runtime
  - the next aligned route/shell wins are more likely to involve specific dialog/control continuations or UI-polish behavior rather than the top-level app/alias command matrix

## Cross-Codex Refresh (2026-07-06T15:12:08+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The public `/online` alias matrix is now closer to `/app` parity for the supported shared-shell output workflows:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct runtime coverage for:
      - `save_character`
      - `save_character_as`
      - `open_for_printing`
      - `print_preview`
      - `print_character`
      - `open_for_export`
      - `export_character`
    - the new alias proofs pin each `/online?command=...` route to the same shared-shell metadata already expected from clean `/app`:
      - `data-route-segment="online"`
      - `data-canonical-route="app"`
      - `data-route-alias="online"`
      - `data-route-family="online-alias"`
      - the expected active workflow and output workflow/target tuple for each command
      - no silent fallback to the generic roster body
- Practical runtime truth after this slice:
  - the public alias route family now has direct runtime coverage for the main startup/output browser workflows, not only roster/open/build/origin
  - `/online` is less likely to drift behind `/app` for save/print/export behaviors while the broader shell-parity lane continues
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `58 passed`
- Practical effect for the next Codex:
  - the obvious `/online` alias coverage gap for the shared-shell command family is now closed
  - the next aligned wins are more likely to be deeper UI/dialog parity or another unverified route family rather than the main clean `/app` output-command alias set

## Cross-Codex Refresh (2026-07-06T15:09:26+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- After the workbench continuation cleanup, the next useful parity hardening turned out to be the public `/online` alias matrix:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct runtime coverage for:
      - `/online?command=new_character`
    - the new proof pins the alias to the same shared-shell Build Lab behavior already expected from clean `/app`:
      - `data-route-segment="online"`
      - `data-canonical-route="app"`
      - `data-route-alias="online"`
      - `data-route-family="online-alias"`
      - `data-active-workflow="build-lab"`
      - no silent fallback to the generic roster body
- Practical runtime truth after this slice:
  - the public alias route family now has direct runtime coverage for:
    - Character Roster
    - Open Dossier
    - Build Lab
    - Origin Dossier
  - this makes the `/online` alias less likely to drift behind `/app` for the main browser-owned startup workflows while workbench/SSR cleanup continues
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `51 passed`
- Practical effect for the next Codex:
  - if alias parity is the next lane, keep extending direct runtime coverage from `/app` to `/online` for any remaining shared-shell command families before assuming the alias behavior is already pinned

## Cross-Codex Refresh (2026-07-06T15:06:18+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The supported clean `/app` command family is now more complete from the hosted `/workbench` SSR fallback side:
  - `Chummer.Blazor/Components/App.razor`
    - `new_character` now emits:
      - `Continue Build Lab on Chummer Online.`
      - a clean workspace-free `/app?command=new_character` continuation route
    - `character_roster` now emits:
      - `Continue Character Roster on Chummer Online.`
      - a clean workspace-free `/app?command=character_roster` continuation route
    - both routes keep their legacy fallback dialogs visible beside the clean continuation link so SSR still explains the compatibility posture without pretending the clean browser shell does not exist
- SSR fallback coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - the supported-dialog rendered-markup matrix now directly proves hosted `/blazor/workbench?command=new_character` and `/blazor/workbench?command=character_roster` pages show:
      - the clean continuation copy
      - the exact `/app?...` route
      - the preserved legacy dialog payload
    - direct fallback identity coverage now also proves:
      - `new_character -> ResultRouteHref = /app?command=new_character`
      - `character_roster -> ResultRouteHref = /app?command=character_roster`
- Practical runtime truth after this slice:
  - the main clean public browser entrypoints for starting Build Lab and opening Character Roster are now exposed from hosted SSR fallback just like the previously hardened Origin/open/save/print/export flows
  - the workspace-free route shape is intentional here because these commands start or open browser-owned public surfaces rather than resuming a specific hosted workspace
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `50 passed`
- Practical effect for the next Codex:
  - the remaining route-parity wins are less likely to be in the main supported command family now; look next for more specific dialog/UI polish or deeper shell-state parity rather than the obvious clean `/app` continuation holes

## Cross-Codex Refresh (2026-07-06T15:01:02+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- A short parity audit immediately after the Origin Dossier slice exposed one remaining shared-command gap in the hosted SSR helper:
  - `save_character` was already a supported clean `/app` workflow and already linked from the preview/workbench browser surfaces
  - but `Chummer.Blazor/Components/App.razor` still treated it as the only save-family command with no SSR fallback continuation route
- The hosted `/workbench` SSR fallback now gives `save_character` the same clean public continuation treatment as the rest of the supported save/print/export family:
  - `Chummer.Blazor/Components/App.razor`
    - `save_character` now emits:
      - `Continue save workflow for BLUE.`
      - a clean `/app?workspace=blue-workspace&command=save_character` continuation route
      - the standard result-panel/link/code treatment used by the other non-dialog save/export/print continuations
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - direct fallback identity coverage now expects:
      - `ResultText = "Continue save workflow for BLUE."`
      - `ResultRouteHref = /app?workspace=blue-workspace&command=save_character`
    - rendered-markup coverage now directly proves hosted `/blazor/workbench?command=save_character` shows:
      - the save continuation copy
      - the clean `/app?...` link
      - the exact route code block
      - no legacy dialog fallback
- Practical runtime truth after this slice:
  - the shared save family is now internally consistent between the clean `/app` shell and the hosted `/workbench` SSR fallback
  - `save_character` no longer stands out as a null-route exception inside the SSR fallback continuation matrix
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `48 passed`
- Practical effect for the next Codex:
  - do a quick command-matrix comparison whenever the clean `/app` route table grows; the SSR fallback helper can drift even when preview/app route links already look correct
  - the remaining parity work, if any, is more likely to be in un-audited shared commands than in the now-covered Origin/save/print/export/open continuations

## Cross-Codex Refresh (2026-07-06T14:56:53+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now gives the Origin Dossier entrypoint a clean public continuation instead of leaving it as dialog-only compatibility copy:
  - `Chummer.Blazor/Components/App.razor`
    - `new_character_origin` now emits:
      - `Continue Origin Dossier on Chummer Online.`
      - a workspace-free `/app?command=new_character_origin` continuation route
      - the standard result-panel/link/code treatment alongside the preserved Origin Dossier dialog payload
    - `BuildPublicAppHref(...)` now accepts routes with or without a `workspace`, so the shared helper matches both the workspace-bound save/open/print/export commands and the story-first Origin entrypoint
- SSR fallback coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - direct `BuildWorkbenchFallback()` identity coverage for `new_character_origin` now proves:
      - `ResultText = "Continue Origin Dossier on Chummer Online."`
      - `ResultRouteHref = /app?command=new_character_origin`
    - rendered-markup coverage now directly proves hosted `/blazor/workbench?command=new_character_origin` shows:
      - the Origin continuation copy
      - the clean `/app?command=new_character_origin` link
      - the exact route code block
      - the preserved Origin Dossier wizard dialog
- Practical runtime truth after this slice:
  - the story-first Origin route now follows the same SSR fallback continuation posture as the other supported clean `/app` workflows
  - the clean Origin continuation intentionally stays workspace-free because the public browser shell owns the new story-first session start
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `Chummer.Tests/bin/Debug/net10.0/Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `47 passed`
- Practical effect for the next Codex:
  - when a clean `/app` command starts a new browser-owned workflow instead of resuming a hosted workspace, keep the SSR continuation route workspace-free instead of forcing legacy compatibility state into the URL
  - the next likely parity wins are remaining supported clean app commands whose hosted SSR fallback still lacks this continuation pattern, if any remain

## Cross-Codex Refresh (2026-07-06T14:49:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback now gives `open_character` the same clean public continuation treatment as the other supported shared commands:
  - `Chummer.Blazor/Components/App.razor`
    - `open_character` now emits:
      - truthful continuation copy
      - a clean `/app?workspace=blue-workspace&command=open_character` route
      - the standard result-panel/link/code treatment
    - the legacy `Open Dossier` dialog remains visible beside the continuation route so SSR still explains the hosted-compatibility posture without trapping the user there
- Clean app-route coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - added direct workspace-bound coverage for:
      - `/app?workspace=preview-ws&command=open_character`
    - this now proves the clean route can both:
      - keep the restored workspace loaded
      - open the shared import workflow without collapsing back to the generic roster body
- SSR fallback coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - workbench fallback identity coverage now expects:
      - `ResultText = "Continue local dossier import while BLUE stays loaded."`
      - `ResultRouteHref = /app?workspace=blue-workspace&command=open_character`
    - rendered-markup coverage now directly proves the hosted `/blazor/workbench?command=open_character` page shows:
      - the clean continuation link
      - the exact `/app?...` route code
      - the preserved `Open Dossier` dialog payload
- Practical runtime truth after this slice:
  - the supported shared-command continuation family exposed from SSR fallback now also includes `open_character`, not only save/print/export flows
  - `/workbench` fallback is now more internally consistent for supported browser-owned workflows that already have a real clean `/app` entrypoint
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `45 passed`
- Practical effect for the next Codex:
  - when a workflow is already a supported shared app command, SSR fallback should prefer offering the clean `/app` continuation instead of behaving like the compatibility dialog is the only truthful endpoint
  - the next likely parity wins are the remaining supported shared commands whose SSR fallback still lacks the clean-route continuation pattern, if any remain after this slice

## Cross-Codex Refresh (2026-07-06T14:44:00+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The hosted `/workbench` SSR fallback no longer strands the supported preparation commands `open_for_printing` and `open_for_export` at compatibility-only dialogs:
  - `Chummer.Blazor/Components/App.razor`
    - fallback result panels now treat `open_for_printing` and `open_for_export` as real clean-route continuations
    - each now emits:
      - truthful continuation copy instead of “prepared” language
      - a clean `/app?workspace=...&command=open_for_printing` or `/app?workspace=...&command=open_for_export` route
      - the same result-panel/link/code treatment already used by the save/export/print result routes
    - the classic fallback dialog remains visible beside the continuation link so SSR still explains the legacy posture while offering the clean public browser shell
- Clean app-route coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - command-only shared-shell coverage now includes:
      - `open_for_printing`
      - `open_for_export`
    - workspace-bound shared-shell coverage now includes:
      - `/app?workspace=preview-ws&command=open_for_printing`
      - `/app?workspace=preview-ws&command=open_for_export`
  - these routes are now directly pinned to the same print/export workflow metadata and non-roster behavior as the rest of the supported app-shell output family
- SSR fallback coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - workbench fallback identity coverage now expects clean continuation routes for:
      - `open_for_printing`
      - `open_for_export`
    - rendered-markup coverage now directly proves each hosted `/blazor/workbench?command=...` page shows:
      - the result-panel continuation link
      - the exact clean `/app?...` route
      - the preserved legacy dialog payload
- Practical runtime truth after this slice:
  - supported preparation commands no longer stop at a compatibility-only SSR explanation when the clean `/app` route can continue the real workflow
  - the workbench fallback is now more consistent across:
    - `save_character_as`
    - `open_for_printing`
    - `print_preview`
    - `print_character`
    - `open_for_export`
    - `export_character`
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `39 passed`
- Practical effect for the next Codex:
  - for supported shared commands, prefer giving `/workbench` SSR fallback users a real clean-route continuation instead of a compatibility-only explanatory dialog
  - the next likely parity wins are other supported command routes that still have asymmetric clean-route continuation treatment between the live browser shell and the hosted SSR fallback

## Cross-Codex Refresh (2026-07-06T14:37:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The compatibility shell’s `print_preview` affordance is now a real clean-route continuation instead of a dead or dropped command:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `print_preview` is now treated as a print workflow for app/workbench shell metadata and startup copy
    - the print-layout `Preview` action now targets the clean public `/app?fixture=blue&command=print_preview` route
  - `Chummer.Presentation/Overview/OverviewCommandPolicy.cs`
    - `print_preview` is now classified as a known shared command, so the browser shell will no longer strip it during startup-command normalization
  - `Chummer.Presentation/Overview/OverviewCommandDispatcher.cs`
    - `print_preview` now dispatches through the same shared print path as `print_character`
  - `Chummer.Blazor/Components/App.razor`
    - the SSR/workbench fallback now treats `print_preview` as part of the print workflow family for active-workflow labels, summaries, result text, and clean `/app?...` continuation links
- Route and policy coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `print_preview` is now included in app-route shared-shell command coverage
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - the compatibility-shell browser entry test now pins the clean `app?fixture=blue&command=print_preview` link
    - the workbench command-only metadata test now verifies `print_preview` publishes print workflow metadata instead of falling back to generic dossier posture
  - `Chummer.Tests/Presentation/OverviewCommandPolicyTests.cs`
    - now directly pins `print_preview` as a known shared command
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - SSR fallback contract now directly covers `print_preview` as a print workflow with:
      - print section heading
      - print-preview summary/result text
      - clean `/app?workspace=blue-workspace&command=print_preview` continuation route
- Practical runtime truth after this slice:
  - the print-layout preview affordance no longer points at a command that the shared browser shell silently discards
  - `/workbench` and SSR fallback both now describe `print_preview` as part of the real print workflow instead of an unclassified startup command
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.OverviewCommandPolicyTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `87 passed`
- Practical effect for the next Codex:
  - when moving compatibility-surface links to `/app`, verify the command is both classified and dispatched by the shared presenter before treating the route as done
  - the next likely parity wins remain the output/share/export subcommands that still point at preview-only detours or still lack shared-command classification

## Cross-Codex Refresh (2026-07-06T14:25:01+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The `/workbench` compatibility surface no longer leaves the plain `save_character` workflow behind on preview/workbench detours after the prior save-as/export/print cleanup:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - the seeded `Save Runner` compatibility action now targets the clean public `/app?fixture=blue&command=save_character` route
    - the restored-workspace `Save @runner in browser` continuation now targets `/app?workspace=...&command=save_character`
    - this keeps ordinary browser save posture aligned with the same clean app shell already used for the adjacent output-result flows
- Route coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `save_character` is now included in app-route command-workflow shared-shell coverage
    - `/app?workspace=preview-ws&command=save_character` is now directly covered as a workspace-bound save continuation that must not fall back to the generic roster surface
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - the `/workbench` browser entry test now pins both clean save links:
      - `app?workspace=preview-ws&command=save_character`
      - `app?fixture=blue&command=save_character`
- Practical runtime truth after this slice:
  - save, save-as, export, and print now all follow the same clean `/app` continuation posture from the compatibility surface
  - the compatibility route still stays in place for live editing, tabs, dialogs, and other non-output continuity
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests" --output Normal`
    - result: `50 passed`
- Practical effect for the next Codex:
  - treat plain browser save as part of the clean app-shell continuation family, not as a preview-only exception
  - the next likely parity wins are other compatibility-surface share/export subcommands that still know the clean browser shell exists but point at preview-only detours

## Cross-Codex Refresh (2026-07-06T14:19:14+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- Do not claim stable or flagship-ready while `release_truth:windows_installer_visual_audit` remains unresolved for digest `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`.
- The `/workbench` compatibility surface now routes browser output continuations into the clean public `/app` shell instead of drifting through compatibility or preview detours:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - added `BuildAppHref(...)` and `BuildAppCommandHref(...)` so the workbench surface can target `/app?...` explicitly without overloading the existing preview/workbench route helper
    - switched the workbench save/export/print continuation helpers and direct output affordances to `/app?...` for:
      - restored-workspace output links
      - seeded-fallback output links
      - workbench status/activity/layout/shortcut output affordances
    - kept non-output edit/lane continuity on `workbench?...` so the compatibility route still behaves like the classic desktop shell for live editing and tab recovery
- Route coverage expanded accordingly:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - the `/workbench` browser entry test now pins clean `/app` output links for:
      - restored workspace continuations
      - explicit export-download continuations
      - seeded fixture output fallbacks
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - workspace-bound `/app` output continuations now directly cover:
      - `save_character_as`
      - `print_character`
      - `export_character`
      - `export_character` with `dialog_action=download`
    - the app-route assertions now also pin `data-dialog-action` when a compatibility-surface continuation carries an explicit output action
- Practical runtime truth after this slice:
  - `/workbench` can keep acting as a compatibility entrypoint without trapping output users in another compatibility-only hop
  - browser output continuations now land in the clean public app shell whether they started from a restored workspace or the seeded fallback runner
  - release posture is still non-flagship in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests" --output Normal`
    - result: `50 passed`
- Practical effect for the next Codex:
  - preserve the current split intentionally:
    - output continuations from the compatibility shell should target clean `/app` routes
    - live edit, dialog, and tab continuity can stay on `workbench?...` until there is stronger evidence to move them
  - the next likely wins are other compatibility-surface affordances that still know the correct clean route or committed result but present a legacy detour instead

## Cross-Codex Refresh (2026-07-06T14:07:50+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- The main in-shell `ResultPanel` now exposes the same output retry affordances that the shell notice surface already had:
  - `Chummer.Blazor/Components/Shell/ResultPanel.razor`
    - pending download/export/print receipts now render:
      - bounded file-name code text
      - retry buttons for `download`, `export`, and `print` when the hosting shell supplies retry callbacks
    - the old dead-end receipt summaries are now actionable from the main content surface, not only from the transient shell notice
  - `Chummer.Blazor/Components/Shell/SectionPane.razor`
    - now passes explicit retry callbacks through to `ResultPanel`
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor`
    - now wires those `ResultPanel` callbacks to the existing retry dispatch methods that re-use the current pending receipt without advancing the handled-version guard
- Direct verification expanded accordingly:
  - `Chummer.Tests/Presentation/DesktopShellDownloadDispatchTests.cs`
    - now directly pins result-panel retry behavior for:
      - pending download
      - pending export
      - pending print
    - each button click proves the existing pending receipt dispatches a second time through the expected JS surface
  - existing `BlazorShellComponentTests` result-panel coverage remains green after the new action affordances were added
- Practical runtime truth after this slice:
  - users no longer need the top shell notice specifically in order to retry browser output actions; the dedicated results surface can do it too
  - output retry behavior is now more internally consistent across the shared shell notice layer and the main result-panel layer
  - release posture is still non-flagship / preview-only in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellDownloadDispatchTests|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.CommandPanel_and_ResultPanel_render_ruleset_specific_headings_and_fallback_copy|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.ResultPanel_renders_last_portability_activity_details" --output Normal`
    - result: `13 passed`
- Practical effect for the next Codex:
  - preserve parity between the shell-notice output affordances and the in-panel result affordances
  - the next likely wins are other dialog or result surfaces that already know the safe next action or route but still present it as inert copy only

## Cross-Codex Refresh (2026-07-06T14:00:27+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth remains aligned with the controller handoff:
  - `release_posture:non_flagship_channel`
  - `release_truth:windows_installer_visual_audit`
- The hosted `/workbench` SSR fallback no longer leaves output-result routes as dead prose:
  - `Chummer.Blazor/Components/App.razor`
    - `save_character_as`, `print_character`, and `export_character` fallback result panels now render:
      - the existing result text
      - a direct continuation link to the clean public `/app` route for the same workspace/command
      - the exact `/app?...` continuation route as bounded code text
    - the fallback model now carries `ResultRouteHref` only for those output-result commands
  - `Chummer.Blazor/wwwroot/app.css`
    - added bounded result-panel layout so the new continuation link and route code remain readable inside the classic shell fallback
- Route/base-href coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - now directly pins the hosted `/blazor/workbench?command=save_character_as` markup to the clean `/app?workspace=blue-workspace&command=save_character_as` continuation route
    - the workbench fallback contract now also pins the clean `/app` continuation route for:
      - `save_character_as`
      - `print_character`
      - `export_character`
    - static helper coverage now includes the sanitized `BuildPublicAppHref(...)` output
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - `/app?command=print_character` and `/app?command=export_character` are now directly covered as shared-shell command routes
    - `/app?workspace=preview-ws&command=save_character_as` is now directly covered as a workspace-bound output continuation that still opens the shared shell instead of collapsing back to roster
- Practical runtime truth after this slice:
  - hosted compatibility output routes now give users a real path into the clean browser shell instead of a one-line static result banner only
  - the new SSR continuation links are backed by direct clean-route coverage, not just markup assumptions
  - release posture is still non-flagship / preview-only in practice; the Windows visual-audit blocker remains unresolved
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `34 passed`
- Practical effect for the next Codex:
  - preserve the clean `/app` continuation-link pattern on hosted SSR fallback routes that cannot directly perform the live browser action themselves
  - the next likely shell/UI parity wins are remaining fallback or dialog result surfaces that still know a safe route or receipt but only render inert copy

## Cross-Codex Refresh (2026-07-06T13:52:15+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The shared desktop shell now makes file-output result notices actionable instead of one-shot prose:
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor`
    - download, export, and print notices now render dedicated shell-notice variants with retry buttons and the receipt file name as bounded code text
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
    - the shell now binds those notice variants only when the exact presenter notice prefixes match the corresponding structured pending receipt
  - `Chummer.Blazor/Components/Layout/DesktopShell.Downloads.cs`
    - auto-dispatch still fires only once per pending-version
    - manual retry buttons now re-dispatch the current pending download/export/print receipt without mutating the handled-version guard
  - `Chummer.Blazor/wwwroot/app.css`
    - shell notice action styling now covers both links and buttons so the new retry affordances stay legible
- Direct shell coverage expanded accordingly:
  - `Chummer.Tests/Presentation/DesktopShellDownloadDispatchTests.cs`
    - now directly pins specialized notice rendering for download/export/print outputs
    - clicking each retry button now proves the existing pending receipt is dispatched a second time through the expected JS surface
- Practical runtime truth after this slice:
  - browser-safe output flows no longer leave users with only static success text after the initial auto-download/open action
  - the shell keeps the original auto-dispatch once-per-version contract while adding explicit retry affordances for download/export/print results
  - release posture is still preview-only; the Windows visual-audit blocker is unchanged
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellDownloadDispatchTests|FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests" --output Normal`
    - result: `15 passed`
- Practical effect for the next Codex:
  - preserve the distinction between automatic once-per-version dispatch and manual retry dispatch for file outputs
  - if shell/UI polish continues, the next likely wins are other result surfaces that still expose structured receipts or routes as inert text only

## Cross-Codex Refresh (2026-07-06T13:43:07+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The shell notice path now treats the Origin Dossier handoff notice as an actionable route instead of inert text:
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor`
    - when `State.Notice` is the `Origin Dossier link: ...` notice, the shell now renders:
      - a direct action link to reopen Origin Dossier on the clean `/app` route
      - the exact route as a code block for copy/review
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
    - the shell now parses the specific Origin Dossier notice prefix and fail-closes to the old plain-text rendering for everything else
  - `Chummer.Blazor/wwwroot/app.css`
    - added notice route styling so the new shell affordance remains legible and bounded
- Direct shell coverage expanded accordingly:
  - `Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs`
    - now directly pins the actionable Origin Dossier notice rendering:
      - `data-shell-notice-kind="origin-dossier-link"`
      - expected clean-route `href`
      - expected visible route code text
- Practical runtime truth after this slice:
  - the `show_origin_dossier_link` path no longer leaves users with only raw notice prose after they ask for the route
  - the shared shell now matches the earlier dialog-handshake work better by making the clean public Origin route usable at both the dialog and shell-notice layers
  - release posture is still preview-only; the Windows visual-audit blocker is unchanged
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests" --output Normal`
    - result: `6 passed`
- Practical effect for the next Codex:
  - keep the Origin Dossier route actionable anywhere the shell explicitly surfaces it
  - if you continue shell/UI polish, the next likely wins are other result or notice states that still surface browser-safe routes or files as raw text only

## Cross-Codex Refresh (2026-07-06T13:39:27+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The stale legacy workspace warning in the shared desktop shell is now actionable instead of being a dead alert:
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor`
    - the `ws-1` stale-workspace warning now renders recovery links for:
      - seeded clean public route: `/app?fixture=blue&tab=tab-create`
      - seeded compatibility shell: `/workbench?fixture=blue&tab=tab-create`
  - `Chummer.Blazor/Components/Layout/DesktopShell.razor.cs`
    - recovery hrefs are now explicit shell constants instead of being implicit in the warning prose
  - `Chummer.Blazor/wwwroot/app.css`
    - added bounded warning action styling so the recovery links remain readable inside the shell notice region
- Direct shell-bootstrap coverage expanded accordingly:
  - `Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs`
    - `DemoWorkspaceId_legacy_seed_alias_skips_backend_load_and_warns` now directly pins:
      - no backend load for stale `ws-1`
      - clean-route recovery link href
      - compatibility-route recovery link href
- Practical runtime truth after this slice:
  - stale legacy workspace links still fail closed, but the shell now offers immediate recovery routes that mint a fresh workspace instead of leaving the user with only warning text
  - the clean public route stays the primary recovery path; the compatibility shell remains available as a secondary escape hatch
  - release posture is still preview-only; the Windows visual-audit blocker is unchanged
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests" --output Normal`
    - result: `6 passed`
- Practical effect for the next Codex:
  - keep stale-workspace recovery actionable; do not regress it back to a raw warning paragraph
  - if you continue shell parity work, the next likely wins are other shell notices or recovery states that still describe the fix without linking to it

## Cross-Codex Refresh (2026-07-06T13:34:31+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The Origin Build handoff pane now exposes the clean public dossier route as an actual browser affordance instead of a plain readonly text box:
  - `Chummer.Blazor/Components/Shell/DialogHost.razor`
    - the specialized `newCharacterOriginDossierLink` field now renders:
      - a real anchor to the clean `/app?command=new_character_origin...` route
      - the exact route as a code block for copy/review
    - practical effect: the build-handoff UI now matches its own copy about reopening Origin Dossier from the clean public route
  - `Chummer.Blazor/wwwroot/app.css`
  - `Chummer.Blazor/Components/Pages/Preview.razor.css`
    - added route-link styling so the handoff link remains legible on both the shared shell and preview-hosted surfaces
- Direct render coverage expanded accordingly:
  - `Chummer.Tests/Presentation/BlazorShellComponentTests.cs`
    - `DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes` now directly pins:
      - exact clean dossier route on the specialized build pane
      - matching anchor `href`
      - visible privacy note that story text stays local
- Practical runtime truth after this slice:
  - the Origin Build dialog no longer tells users to use a route while only showing an inert readonly field
  - the build handoff continues to preserve the no-story-in-URL contract while making the clean route more actionable
  - release posture is still preview-only; the Windows visual-audit blocker is unchanged
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_" --output Normal`
    - result: `12 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_renders_origin_story_and_build_surfaces_with_specialized_browser_panes|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests.BuildNewCharacterOriginBuildDialog_translates_origin_into_alice_guided_build_summary|FullyQualifiedName~Chummer.Tests.Presentation.DialogCoordinatorTests.CoordinateAsync_origin_wizard_generates_alice_build_translation_and_handoff" --output Normal`
    - result: `3 passed`
- Practical effect for the next Codex:
  - preserve the clean `/app` dossier-link contract on the Origin Build handoff surface
  - if you continue dialog/UI polish, the next likely wins are other specialized dialog affordances that still present workflow routes or outputs as inert raw fields

## Cross-Codex Refresh (2026-07-06T13:21:42+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The clean public browser routes now stop collapsing explicit continuation queries back to the generic roster landing surface:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `/app` and `/online` now render the shared `DesktopShell` whenever the route carries explicit continuation state:
      - non-roster command routes such as `new_character`, `open_character`, `save_character_as`, print/export flows
      - workspace / fixture / tab continuation routes such as `/app?workspace=preview-ws&tab=tab-create`
    - app-route root metadata now also publishes:
      - `data-output-workflow`
      - `data-output-state`
      - `data-output-target`
    - `new_character` is now treated as `build-lab` workflow identity on the public app routes too, not only on `/workbench`
- Direct route coverage expanded again:
  - `Chummer.Tests/Presentation/AppRouteSurfaceTests.cs`
    - now directly pins `/app` shared-shell behavior for:
      - `new_character`
      - `open_character`
      - `save_character_as`
      - `workspace=preview-ws&tab=tab-create`
    - now directly pins `/online?command=open_character` as an alias route that still opens the shared shell instead of the roster landing view
- Practical runtime truth after this slice:
  - clean public route links such as `New runner`, `Import`, `Open example`, and build-lab continuations no longer quietly degrade to the roster page when they stay on `/app`
  - `/app` and `/online` now expose more coherent workflow metadata for save/print/export continuations, closer to the already-hardened compatibility shell
  - release posture is still preview-only; the Windows visual-audit blocker is unchanged
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `66 passed`
- Practical effect for the next Codex:
  - preserve the distinction between the roster landing surface and explicit continuation queries on `/app` and `/online`
  - if you keep pushing browser-route parity, the next good target is remaining copy or dialog parity on the public app shell, not re-breaking it back into a roster-only surface

## Cross-Codex Refresh (2026-07-06T13:08:16+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- `open_character` no longer falls through to generic dossier/profile semantics on the browser compatibility routes:
  - `Chummer.Blazor/Components/App.razor`
    - SSR fallback now maps `open_character` to:
      - `ActiveWorkflow = open-dossier`
      - `SectionHeading = Open Dossier`
      - `SectionSummary = Open a local runner dossier.`
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - interactive `/workbench?command=open_character` now publishes:
      - `data-active-workflow="open-dossier"`
      - title/status copy `Open Dossier`
- Direct route coverage expanded again:
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - interactive command-only `/workbench` metadata now also directly pins `open_character` as `open-dossier`
    - the previously added command-only route matrix remains green for:
      - `new_character`
      - `new_character_origin`
      - `character_roster`
      - `open_character`
      - `save_character`
      - `save_character_as`
      - `open_for_printing`
      - `print_character`
      - `open_for_export`
      - `export_character`
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - now directly renders hosted `/blazor/workbench?command=open_character`
    - now also pins reflection-level fallback identity for `open_character`
- Practical runtime truth after this slice:
  - both the SSR fallback shell and the interactive compatibility shell now treat `open_character` as an explicit open-dossier workflow, not as generic dossier/profile state
  - the command-only route model is more internally coherent across new/open/roster/output flows
  - release posture is still preview-only; the Windows visual-audit blocker is unchanged
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `35 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `26 passed`
- Practical effect for the next Codex:
  - if you keep pushing browser-route parity, preserve the explicit workflow identity for `open_character`; do not let it regress to generic dossier/profile metadata
  - the next likely wins are other user-visible shell or dialog parity gaps, not more re-auditing of the now-green command-route matrix

## Cross-Codex Refresh (2026-07-06T13:02:45+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- Interactive `/workbench` command-route parity is now tighter and directly verified:
  - `Chummer.Blazor/Components/Pages/Preview.razor`
    - `IsBuildLabWorkflow` now treats `/workbench?command=new_character` as a real Build Lab route even without an explicit `tab=tab-create`
    - practical effect: the interactive compatibility shell no longer reports generic `dossier` workflow state for the new-runner command route
  - `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs`
    - now directly pins interactive command-only `/workbench` shell metadata for:
      - `new_character`
      - `new_character_origin`
      - `character_roster`
      - `save_character`
      - `save_character_as`
      - `open_for_printing`
      - `print_character`
      - `open_for_export`
      - `export_character`
    - runtime expectations now covered:
      - startup command dispatch still fires
      - `data-tab="none"` on command-only workbench routes
      - workflow/output metadata matches the command route
      - roster route still marks the roster surface as active
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - fallback identity coverage now also directly pins the remaining save/print/export command variants:
      - `save_character`
      - `save_character_as`
      - `open_for_printing`
      - `print_character`
      - `open_for_export`
      - `export_character`
- Practical runtime truth after this slice:
  - both the SSR fallback shell and the interactive compatibility shell now agree that command-only new-runner routes are Build Lab routes, not generic dossier routes
  - save/print/export alias commands are now better locked on both sides of the browser-route split
  - no release-lane truth changed; the unresolved Windows visual-audit blocker still controls the stable/flagship posture
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `34 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `24 passed`
- Practical effect for the next Codex:
  - if you touch command-only `/workbench` routing again, keep the SSR fallback and the interactive compatibility shell aligned on workflow identity first, then on copy/details
  - command aliases for save/print/export are now directly covered; do not reintroduce a split where only the “main” command variant is tested

## Cross-Codex Refresh (2026-07-06T12:54:37+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- `App.razor` SSR workbench fallback semantics are now closer to the interactive compatibility shell for command-driven routes:
  - command-only fallback routes no longer synthesize `tab-info` / `tab-create` metadata
  - fallback `data-tab` now resolves to `none` when the route did not actually provide a tab
  - command-driven fallback workflows now publish explicit workflow identities instead of generic `workbench` where applicable:
    - `new_character` -> `build-lab`
    - `new_character_origin` -> `origin-dossier`
    - `character_roster` -> `character-roster`
    - `save_character` / `save_character_as` -> `save`
    - `print_character` / `open_for_printing` -> `print`
    - `export_character` / `open_for_export` -> `export`
  - fallback headings/summaries are also command-specific now, for example:
    - `Character Roster` with `Group runners into your own folders.`
    - `Save` with `Browser download prepared for BLUE.`
    - `Export` / `Print` summaries that no longer read like a generic profile/workbench surface
- Direct route coverage expanded accordingly:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - now pins hosted `/blazor/workbench?command=save_character_as` document output:
      - `data-tab="none"`
      - `data-active-workflow="save"`
      - save-result summary/result panel
      - no fallback dialog section for that output route
    - now explicitly pins reflection-level fallback identity for `character_roster`
    - updated origin/new-runner expectations to the command-only `data-tab="none"` model
- Practical runtime truth after this slice:
  - the fallback shell is less misleading for saved compatibility links that open into roster or output workflows
  - command-driven SSR fallback metadata now better matches the already-green interactive workbench shell instead of pretending those routes are generic profile/workbench states
  - no release-lane posture changed; the Windows visual-audit blocker is still the hard stop
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `12 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `30 passed`
- Practical effect for the next Codex:
  - if you touch command-driven workbench fallbacks again, preserve the `data-tab="none"` command-route model unless you deliberately migrate the interactive shell too
  - keep workflow metadata/copy aligned across both the SSR fallback shell and the interactive compatibility shell, especially for roster and output routes

## Cross-Codex Refresh (2026-07-06T12:41:08+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- `App.razor` browser-route hardening now has direct document-render coverage instead of only helper/source assertions:
  - `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs`
    - now renders the full `App` document on hosted `/blazor/app` and `/blazor/workbench`
    - directly pins:
      - emitted `<base href>`
      - hosted static asset URLs
      - service-worker bootstrap paths
      - SSR workbench fallback shell markup
      - fallback observer script presence
  - the hosted `/blazor/workbench?command=new_character_origin` fallback no longer reports generic profile/workbench copy
    - `Chummer.Blazor/Components/App.razor` now maps `new_character_origin` fallback state to:
      - `ActiveWorkflow = origin-dossier`
      - `SectionHeading = Origin Dossier`
      - `SectionSummary = Start the story-first character path for BLUE.`
- Practical runtime truth after this slice:
  - the hosted app document still emits `/blazor/` base/static asset paths and does not render the SSR workbench fallback section on `/blazor/app`
  - the hosted workbench document now renders an Origin-specific SSR fallback workflow/copy that better matches the interactive compatibility shell instead of pretending the route is a generic profile/workbench state
  - the surrounding browser-route pack remains green after the fallback parity fix
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off -m:1 --no-restore -p:BuildInParallel=false -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `10 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `30 passed`
- Practical effect for the next Codex:
  - if you touch `App.razor` route bootstrap, static asset paths, or SSR workbench fallback semantics, update the direct render tests in `AppShellBaseHrefTests.cs`, not only the reflection/helper assertions
  - keep the Origin Dossier fallback workflow/copy aligned with the interactive compatibility shell; do not let it drift back to generic profile/workbench metadata

## Cross-Codex Refresh (2026-07-06T12:08:56+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The currently active worktree slices now have a broader integrated regression receipt, not only per-slice spot checks:
  - browser-route and hosted-base contracts are still green together:
    - `PublicPreviewSurfaceTests`
    - `AppRouteSurfaceTests`
    - `PortalAppRouteContractTests`
    - `AppShellBaseHrefTests`
  - Origin Dossier / desktop-shell polish is still green together:
    - `BlazorShellComponentTests.DialogHost_*`
    - `DesktopWindowContrastTests.Origin_dossier_*`
    - `DesktopDialogFactoryTests`
    - `DesktopLocalizationCatalogTests`
    - `DesktopShellStartupSyncTests`
  - repo-local portability / release-lane guardrails are still green together:
    - `MigrationComplianceTests` desktop exit-gate subset
    - `test_desktop_exit_gate_bash_portability.py`
    - `test_release_shell_array_portability.py`
    - `test_startup_smoke_bash_portability.py`
    - `test_desktop_downloads_local_release_policy.py`
    - `test_blazor_portal_route_probe_contract.py`
    - `test_portal_release_shelf_runtime.py`
- Practical runtime truth after this pass:
  - the clean public browser entry remains `/app`, with `/online` as the alias and `/blazor/app` as the hosted app target
  - the SSR/workbench shell metadata and the visible `Runner` -> `Dossier` copy changes remain covered and green after the newer route/base-href work
  - no new evidence clears the preview-only posture or the outstanding Windows installer visual-audit blocker
- Focused verification completed for this integrated pass:
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests|FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_|FullyQualifiedName~Chummer.Tests.Presentation.DesktopWindowContrastTests.Origin_dossier_|FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests|FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests|FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Macos_exit_gate_prefers_registry_release_truth_with_repo_local_fallback_and_accepts_dmg_media|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Linux_exit_gate_defaults_to_promoted_release_tuple_when_overrides_are_missing|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Windows_exit_gate_requires_startup_smoke_receipt_integrity_for_promoted_installer_bytes" --output Normal`
    - result: `180 passed`
  - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_desktop_downloads_local_release_policy.py tests/test_blazor_portal_route_probe_contract.py tests/test_portal_release_shelf_runtime.py`
    - result: `41 passed`
- Practical effect for the next Codex:
  - the next aligned slice can move forward on new SR6 shell parity or dialog polish work without first re-proving these currently green route, metadata, copy, and portability lanes
  - do not soften the blocker language: this repo is still preview-only until the digest-bound Windows visual proof bundle lands and clears `release_truth:windows_installer_visual_audit`

## Cross-Codex Refresh (2026-07-06T12:03:20+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- Remaining `DesktopShell` and dossier-copy deltas in the worktree are now pinned by direct repo-local tests instead of only by source inspection:
  - `Chummer.Tests/Presentation/DesktopShellStartupSyncTests.cs`
    - now proves the rendered `DesktopShell` root publishes:
      - `data-tab`
      - `data-ruleset`
      - `data-active-workflow`
      - `data-route-segment`
      - `data-active-runner`
      - `data-legacy-runner`
    - current verified shell-root runtime truth for the fixture route is:
      - `data-tab="tab-create"`
      - `data-ruleset="sr5"`
      - `data-active-workflow="build-lab"`
      - `data-route-segment="workbench"`
      - `data-active-runner="NOVA"`
  - `Chummer.Tests/Presentation/DesktopDialogFactoryTests.cs`
    - now explicitly pins the `about` dual-head preview dialog to use the field label `Dossier` for the `workspace` field
  - `Chummer.Tests/Presentation/DesktopLocalizationCatalogTests.cs`
    - now explicitly pins `desktop.dialog.character_settings.notice.updated` to:
      - English: `Dossier settings updated.`
      - German: `Dossier-Einstellungen wurden aktualisiert.`
- Practical effect:
  - the shell metadata added in `DesktopShell.razor` / `DesktopShell.razor.cs` is now covered at render time instead of only through static source-contract tests
  - the visible “Runner” -> “Dossier” copy changes in the about dialog and character-settings notice now have direct repo-local assertions
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopShellStartupSyncTests" --output Normal`
    - result: `6 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopLocalizationCatalogTests" --output Normal`
    - result: `12 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests" --output Normal`
    - result: `100 passed`
- Practical effect for the next Codex:
  - if you touch `DesktopShell` metadata or “Dossier” copy again, update these direct runtime/unit tests rather than relying only on static contract coverage
  - the next likely aligned slice is broader SR6 shell parity or additional dialog polish, not rechecking these now-green metadata/copy changes

## Cross-Codex Refresh (2026-07-06T11:54:40+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- Repo-local release-script portability contracts are now aligned with the already-patched bash3-safe desktop exit-gate scripts:
  - new repo-local static portability test:
    - `tests/test_desktop_exit_gate_bash_portability.py`
    - pins the local `materialize-{macos,linux,windows}-desktop-exit-gate.sh` scripts to the `RELEASE_PROMOTED_TUPLE=()` plus `while IFS= read -r tuple_value; do ... done` collector pattern
    - explicitly forbids `mapfile -t RELEASE_PROMOTED_TUPLE`
  - updated repo-local MSTest compliance lock:
    - `Chummer.Tests/Compliance/MigrationComplianceTests.cs`
    - the macOS, Linux, and Windows exit-gate assertions now expect the bash3-safe tuple collector loop instead of the stale bash4-only `mapfile` contract
    - the Windows exit-gate migration test also now matches the current Python argv layout for:
      - `windows_installer_visual_proof_path`
      - `repo_root`
      - `hub_registry_root_arg`
- Practical effect:
  - this repo no longer carries an internal contract split where the scripts are bash3-safe but the repo-local compliance tests still demand the old bash4 collector
  - future local portability work on the desktop exit-gate scripts now has both Python and MSTest coverage in this repo, not only upstream coverage from `chummer.run-services`
- Focused verification completed for this slice:
  - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_exit_gate_bash_portability.py tests/test_release_shell_array_portability.py tests/test_startup_smoke_bash_portability.py tests/test_desktop_downloads_local_release_policy.py`
    - result: `29 passed`
  - `bash -n scripts/materialize-macos-desktop-exit-gate.sh scripts/materialize-linux-desktop-exit-gate.sh scripts/materialize-windows-desktop-exit-gate.sh`
    - result: parse clean
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Macos_exit_gate_prefers_registry_release_truth_with_repo_local_fallback_and_accepts_dmg_media|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Linux_exit_gate_defaults_to_promoted_release_tuple_when_overrides_are_missing|FullyQualifiedName~Chummer.Tests.Compliance.MigrationComplianceTests.Windows_exit_gate_requires_startup_smoke_receipt_integrity_for_promoted_installer_bytes" --output Normal`
    - result: `3 passed`
- Practical effect for the next Codex:
  - do not reintroduce `mapfile -t RELEASE_PROMOTED_TUPLE` into the repo-local desktop exit-gate scripts or their compliance tests
  - if you continue the release-script portability lane, prefer repo-local tests alongside upstream `chummer.run-services` coverage whenever this repo’s own compliance files have drifted

## Cross-Codex Refresh (2026-07-06T11:47:55+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- The next verified origin-dialog polish slice landed in the desktop restore lane:
  - `Chummer.Avalonia/DesktopDialogWindow.axaml.cs` now makes the active combo interaction anchor the explicit first-class path inside `PrimePreferredScrollOffsetForDialogRebind(...)`
  - this keeps the source contract aligned with the intended desktop behavior during Origin Dossier combo-driven refreshes: try the active combo anchor first, then the advanced-panel viewport anchor, and only then fall back to the raw scroll offset
  - the change was small, but it closed a real regression proof gap in the new Avalonia combo-refresh hardening
- Current verified dialog/UI truth for this slice:
  - Blazor `DialogHost` keeps Origin Dossier advanced controls open across rerenders, parent remounts, and shared scroll-restore flows
  - Avalonia Origin Dossier dark-mode preview and combo-refresh proofs are green again
  - `DesktopDialogFactory` still keeps the broader dialog surface inventory green after the desktop restore tweak
- Focused verification completed for this slice:
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.BlazorShellComponentTests.DialogHost_" --output Normal`
    - result: `12 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopWindowContrastTests.Origin_dossier_" --output Normal`
    - result: `9 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.DesktopDialogFactoryTests" --output Normal`
    - result: `99 passed`
- Practical effect for the next Codex:
  - if you keep pushing Origin Dossier polish, preserve the combo-anchor-first ordering in `DesktopDialogWindow.axaml.cs`; do not let future refactors slide back to raw-offset-first priming
  - the more likely next wins are broader SR6 shell parity or additional user-visible dialog polish, not another re-audit of the now-green combo-refresh restore path

## Cross-Codex Refresh (2026-07-06T12:01:54+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- Browser-route hardening for the `/app` and `/online` public entrypoints moved from source-only intent to verified runtime truth:
  - `Chummer.Portal/Program.cs` no longer double-registers slash variants for `/app/` and `/online/`
  - the previous `app.MapGet("/app")` + `app.MapGet("/app/")` and `app.MapGet("/online")` + `app.MapGet("/online/")` combination was causing real `AmbiguousMatchException` 500s in the local portal runtime
  - the portal now keeps one clean public route mapping for `/app` and one for `/online`, while still preserving query-string redirects into `/blazor/app`
  - `Chummer.Tests/Presentation/PortalAppRouteContractTests.cs` now explicitly forbids reintroducing `PublicAppSlash` / `PublicOnlineSlash` route constants and slash-specific `MapGet(...)` registrations
- Portal runtime coverage is broader and more truthful now:
  - `tests/test_portal_release_shelf_runtime.py` was aligned with the current downloads page copy
  - the same runtime module now proves both `/app` and `/online` return redirect headers to `/blazor/app?...` for slash and non-slash query variants
  - local portal-only runtime tests intentionally stop at redirect/OpenAPI verification because this harness does not boot the Blazor upstream; `<base href="/blazor/">` proof remains in the full-stack `scripts/e2e-portal.cjs` lane instead
- Focused verification completed for this slice:
  - `node --check /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/scripts/e2e-portal.cjs`
  - `python3 -m pytest -q --import-mode=importlib tests/test_blazor_portal_route_probe_contract.py tests/test_portal_release_shelf_runtime.py`
    - result: `12 passed`
  - `DOTNET_CLI_UI_LANGUAGE=en dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
    - result: `Build succeeded`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests|FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests|FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `14 passed`
- Practical effect for the next Codex:
  - do not put slash-specific `/app/` or `/online/` `MapGet` endpoints back into `Chummer.Portal/Program.cs`; they are a real runtime regression, not harmless duplication
  - if you need `<base href>` proof for `/app` or `/online`, use the full-stack portal probe rather than the portal-only runtime harness

## Cross-Codex Refresh (2026-07-06T11:30:19+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Shared blocker truth is still unchanged:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- Recent browser-route hardening in this repo now lands in a clearer contract:
  - the clean public Chummer Online route remains `/app`
  - `/online` remains a public alias of `/app`
  - the hosted app path remains `/blazor/app`
  - `/blazor/workbench` remains the proof-compatible hosted route
  - do not re-promote `/blazor/online` as a first-class hosted app path unless the docs, route proofs, and portal contracts are deliberately migrated together
- What changed in the current verified slice:
  - fixed the real preview proof-card href bug in `Chummer.Blazor/Components/Pages/Preview.razor` where positional `BuildPreviewHref(...)` calls were emitting `workspace=tab-*` instead of `tab=tab-*`
  - modernized `Chummer.Tests/Presentation/PublicPreviewSurfaceTests.cs` so preview/workbench route assertions match current copy and relative href shape
  - rewired the workbench query-bootstrap tests to start from a no-active-workspace presenter state when they need to prove real `workspace`/`control`/`dialog_action` bootstrap behavior
  - changed `Chummer.Portal/Program.cs` so `/online` now redirects through the same hosted `/blazor/app` contract as `/app`
  - removed the remaining stale hosted `/blazor/online` expectation from `Chummer.Tests/Presentation/AppShellBaseHrefTests.cs` and replaced it with explicit `/blazor/workbench` hosted-base coverage
- Focused verification completed for this browser-route slice:
  - `dotnet build /docker/chummercomplete/chummer-presentation-sr6-origin-dialog-clean/Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PublicPreviewSurfaceTests" --output Normal`
    - result: `24 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppRouteSurfaceTests" --output Normal`
    - result: `4 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.PortalAppRouteContractTests" --output Normal`
    - result: `2 passed`
  - `./Chummer.Tests --filter "FullyQualifiedName~Chummer.Tests.Presentation.AppShellBaseHrefTests" --output Normal`
    - result: `8 passed`
- Practical effect for the next Codex:
  - do not spend another turn debating whether `/online` should canonicalize to `/blazor/online`; that question is resolved in favor of `/blazor/app`
  - if you keep pushing the browser route lane, look for broader runtime/e2e coverage or other shell-parity drifts, not the now-removed `/blazor/online` contract

## Cross-Codex Refresh (2026-07-06T05:13:39+02:00)

- Read `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md` before touching any publish-lane, route-proof, or blocker-receipt work from this repo.
- Current verified live alias truth is:
  - `/player -> 302 /mobile/player`
  - `/gm -> 302 /mobile/gm`
  - `/observer -> 302 /mobile/observer`
- Older `/play?role=...` route notes are historical drift, not current runtime truth. Do not forward them into fresh handoffs.
- Do not globally remove `/play?role=...` strings from the broader workspace. Some tests and compatibility paths intentionally keep them as negative fixtures.
- Shared blocker truth is still:
  - release posture is preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
- User-reported Windows installer success does not clear that blocker until the promoted-digest proof bundle is imported into canonical release evidence.
- For this repo, stay on shell parity, UI polish, and release-script portability. Do not re-audit flagship blocker receipts here unless your change actually affects their evidence inputs.

## Cross-Codex Refresh (2026-07-06T05:00:45+02:00)

- Current shared flagship truth:
  - the live release lane is still preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for:
    - `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
  - canonical live operator truth is still:
    - `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md`

- What changed in this repo during the broader follow-through pass:
  - the release-script hardening was expanded beyond the first three files and now also covers:
    - `scripts/publish-download-bundle.sh`
    - `scripts/publish-download-bundle-s3.sh`
    - `scripts/verify-releases-manifest.sh`
    - `scripts/build-desktop-installer.sh`
  - combined with the earlier pass, this repo is now normalized across seven release-lane scripts:
    - `scripts/run-desktop-startup-smoke.sh`
    - `scripts/publish-download-bundle-http.sh`
    - `scripts/generate-releases-manifest.sh`
    - `scripts/publish-download-bundle.sh`
    - `scripts/publish-download-bundle-s3.sh`
    - `scripts/verify-releases-manifest.sh`
    - `scripts/build-desktop-installer.sh`

- Validation for this repo slice:
  - `bash -n` passed for the four newly touched scripts above
  - shared central coverage:
    - `python3 -m pytest -q /docker/chummercomplete/chummer.run-services/tests/test_desktop_startup_smoke_bash_compat.py /docker/chummercomplete/chummer.run-services/tests/test_publish_download_bundle_http_bash_portability.py /docker/chummercomplete/chummer.run-services/tests/test_release_shell_array_portability.py`
    - result: `4 passed in 0.40s`
  - repo-local coverage:
    - `python3 -m pytest -q --import-mode=importlib tests/test_desktop_downloads_local_release_policy.py tests/test_release_shell_array_portability.py`
    - result: `26 passed in 0.29s`

- Important:
  - if you run pytest across multiple SR6 companion repos in one command, use `--import-mode=importlib`
  - both repos carry same-basename test modules, so plain collection reports import-file mismatch

## Cross-Codex Refresh (2026-07-06T04:41:32+02:00)

- Current shared flagship truth:
  - the real remaining launch blocker is not desktop startup anymore
  - the live release lane is still preview-only
  - the hard external blocker is still the missing promoted-digest Windows visual proof bundle for:
    - `80655fd79a096cd7714910d7b38f7741eea01f82ada96dc6a2a097951997d91a`
  - canonical live operator truth is in:
    - `/docker/chummercomplete/chummer.run-services/NEXT_SESSION_HANDOFF.md`

- What changed in this repo during the latest pass:
  - shell-portability drift was fixed in the SR6 companion release scripts:
    - `scripts/run-desktop-startup-smoke.sh`
    - `scripts/publish-download-bundle-http.sh`
    - `scripts/generate-releases-manifest.sh`
  - these copies now match the bash3 / nounset-safe helper posture already used in the main release-lane scripts:
    - nounset-safe `array_count`
    - no raw `${#windows_payload_gate_args[@]}`
    - no raw `${#upload_files[@]}`
    - no raw `${#promoted_file_names[@]}`
    - no raw `${#portal_artifacts[@]}`
    - no older empty-array `eval ... +` helper in startup smoke

- Validation for this repo slice:
  - `bash -n scripts/run-desktop-startup-smoke.sh scripts/publish-download-bundle-http.sh scripts/generate-releases-manifest.sh`
  - upstream regression coverage that now pins this repo path too:
    - `python3 -m pytest -q /docker/chummercomplete/chummer.run-services/tests/test_desktop_startup_smoke_bash_compat.py /docker/chummercomplete/chummer.run-services/tests/test_publish_download_bundle_http_bash_portability.py /docker/chummercomplete/chummer.run-services/tests/test_release_shell_array_portability.py`
    - result: `4 passed in 0.09s`

- What another codex should not waste time re-auditing:
  - the old `bad substitution` / empty-array warning class for these three SR6 scripts
  - those copies are now normalized and parse-clean

- What another codex should do next in this repo instead:
  - keep SR6 and SR5 user-facing shell behavior aligned
  - preserve the current shell portability helper pattern if touching release scripts again
  - read the run-services handoff before doing any publish-lane or Windows-proof work

- Important:
  - the older April baseline sections below are archival context, not a live operator checklist
  - do not rerun the old recommit / repush commands for `Populate classic menu roots`
  - use current repo state plus the run-services handoff as the authoritative starting point

## Scope

Drive Chummer6 desktop toward hard Chummer5a-style parity:

- classic menu-first shell, not a dashboard
- dense left rail and runner dossier posture
- startup-safe commands visible and usable on first launch
- Avalonia and Blazor kept in lockstep where the same shell affordance exists
- release builds must ship the current pushed UI snapshot, not a stale head

## Last pushed baseline (Historical 2026-04-17 snapshot)

- Branch: `safe-push-fix-windows-installer-payload-20260401`
- Pushed refs:
  - `origin/safe-push-fix-windows-installer-payload-20260401`
  - `origin/fleet/ui`
  - `origin/main`
- Last pushed UI commit: `68edf04c`
  - message: `Populate classic menu roots`

## Current uncommitted slice

Files changed locally:

- `docs/WORKBENCH_SESSION_HANDOFF.md`

What this slice changes:

- `docs/WORKBENCH_SESSION_HANDOFF.md`
  - rolls the baseline forward after the pushed menu-fidelity slice and records the next parity target for crash/OOM recovery

## Validation status

What passed:

- pushed commit `68edf04c` to `safe-push-fix-windows-installer-payload-20260401`, `fleet/ui`, and `main`
- `git diff --check Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs`
- `dotnet test --project Chummer.Tests/Chummer.Tests.csproj -v minimal -tl:off --no-restore -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages --filter "FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Chummer5a_layout_hard_gate_is_wired_into_release_proofs_and_classic_shell_markers|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_special_and_windows_menus_surface_real_commands|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus|FullyQualifiedName~Chummer.Tests.Presentation.AvaloniaFlagshipUiGateTests.Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters"`
  - result: `Passed 4/4`
- `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
- `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
  - result: `Build succeeded`

What is still flaky in this repo in general:

- a stale restore graph can still fall back into `NETSDK1064` for `Microsoft.Extensions.DependencyInjection 10.0.0`
- stable recovery is to rerun restore immediately before the build with the shared package cache:
  - `dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages`
  - then rerun the same build command

Important note:

- Ctrl-C during these builds can emit bogus `MSB3202` project-not-found noise. Ignore those if they appear immediately after a manual cancel.

## Next exact commands (Historical; superseded)

Run from repo root:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
git diff --check Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
DOTNET_CLI_UI_LANGUAGE=en dotnet restore Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
DOTNET_CLI_UI_LANGUAGE=en dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -v minimal --no-restore -tl:off -p:UseChummerEngineContractsLocalFeed=false -p:RestorePackagesPath=/home/tibor/.nuget/packages
git add Chummer.Avalonia/MainWindow.ShellFrameProjector.cs
git add Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs
git add Chummer.Presentation/UiKit/ShellChromeBoundary.cs
git add Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs
git add docs/WORKBENCH_SESSION_HANDOFF.md
git commit -m "Populate classic menu roots"
git push origin HEAD:safe-push-fix-windows-installer-payload-20260401
git push origin HEAD:fleet/ui
git push origin HEAD:main
```

## Immediate next slices after this commit

1. Rebuild and inspect the live mac desktop preview to confirm the shipped Avalonia head now exposes `Special` and `Windows` commands instead of dead roots.
2. Continue the Chummer5a parity pass on remaining first-glance drifts:
   - menu/toolstrip density
   - icon correctness
   - startup shell posture
   - runner dossier spacing
3. Keep release-train correctness tight so the next mac bootstrap pulls the just-pushed UI head.

## Resume after interruption

If the session dies from OOM, pruning, or host restart, resume exactly here:

```bash
cd /docker/chummercomplete/chummer6-ui
git status --short Chummer.Avalonia/MainWindow.ShellFrameProjector.cs Chummer.Presentation/Shell/CatalogOnlyRulesetShellCatalogResolver.cs Chummer.Presentation/UiKit/ShellChromeBoundary.cs Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs docs/WORKBENCH_SESSION_HANDOFF.md
sed -n '1,220p' docs/WORKBENCH_SESSION_HANDOFF.md
```

Then continue immediately with the next parity slice instead of re-auditing older work:

- audit icon packaging for Avalonia/Windows/macOS
- verify the startup shell still has no dead chrome after the menu-root fix
- keep the next handoff update current before any long-running build/release pass

## Non-negotiables

- Do not commit unrelated dirty or generated files.
- Keep using `RestorePackagesPath=/home/tibor/.nuget/packages` with `UseChummerEngineContractsLocalFeed=false` for restore/build/test work.
- The user wants Chummer5a as the layout reference. If a visible drift stays, either fix it or document a real user-facing reason.
