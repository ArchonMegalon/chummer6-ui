# Chummer5A UI Element Parity Audit

Generated at: 2026-04-30T13:12:23.172019Z

## Scope
This matrix covers every parity-tracked visible surface and currently-present disallowed extra represented in the Chummer5A oracle, screenshot review gate, visual familiarity gate, workflow execution gate, and veteran workflow packs.

## Summary
- Total audited elements: 84
- Visual parity yes/no: 74/10
- Behavioral parity yes/no: 74/10
- Chummer6-only extras present: 0
- Removable extras present: 0
- Active/productive/nonproductive shard runs: 12/11/1

## Top findings
- [HIGH] ui_parity_gap: Custom Data Xml And Translator Bridge is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:translator', 'menu:xml_editor'].
- [HIGH] ui_parity_gap: Dense Builder And Career Workflows is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['oracle:tabs', 'oracle:workspace_actions', 'workflow:build_explain_publish'].
- [HIGH] ui_parity_gap: Dice Initiative And Table Utilities is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:dice_roller', 'workflow:initiative'].
- [HIGH] readiness_gap: Flagship readiness still contains open coverage keys outside the surface-level desktop parity matrix. desktop_client: Executable desktop exit gate proof is missing or not passed. Desktop shell/install/support liveliness must be proven from shipped artifacts., Executable gate blocker: Release channel does not publish desktop install media for required platform 'linux'., Executable gate blocker: Release channel does not publish desktop install media for required platform 'macos'., Executable gate blocker: Desktop downloads shelf contains installer artifact(s) not promoted in release-channel truth: chummer-avalonia-linux-x64-installer.deb, chummer-avalonia-osx-arm64-installer.dmg, chummer-blazor-desktop-linux-x64-installer.deb, chummer-blazor-desktop-osx-arm64-installer.dmg., Executable gate blocker: Release channel is missing required desktop platform/head installer tuple pair(s): avalonia:linux, avalonia:macos., Executable gate blocker: Release channel is missing required desktop platform/head/rid installer tuple(s): avalonia:linux-x64:linux, avalonia:osx-arm64:macos., Executable gate blocker: Release channel status cannot be publishable while required desktop tuple coverage is incomplete., Executable gate blocker: Windows desktop exit gate is missing or not passing., Executable gate blocker: Windows gate reason: Windows startup smoke receipt artifactDigest does not match promoted installer bytes., Executable gate blocker: Windows gate reason: Windows startup smoke receipt version does not match release channel run-20260430-130506., Executable gate blocker: Windows gate reason: Windows startup smoke receipt is stale (1355841s old)., Executable gate blocker: Windows startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes., Executable gate blocker: Windows startup smoke receipt version does not match release channel version for promoted installer bytes., Executable gate blocker: Windows startup smoke receipt is stale for promoted installer bytes (1355842s old)., Executable gate blocker: Linux desktop exit gate receipt checks.release_channel_version does not match release channel version for promoted head 'avalonia'., Executable gate blocker: Linux desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head 'avalonia'., Executable gate blocker: Linux installer startup smoke receipt version does not match release channel version for promoted head 'avalonia'., Executable gate blocker: Linux installer startup smoke receipt carries conflicting version/releaseVersion alias values for promoted head 'avalonia'., Executable gate blocker: Linux gate embedded release_channel_linux_artifact version/releaseVersion does not match promoted release channel version., Executable gate blocker: macOS desktop exit gate receipt checks.release_channel_version does not match release channel version for promoted head 'avalonia' (osx-arm64)., Executable gate blocker: macOS desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head 'avalonia' (osx-arm64)., Executable gate blocker: macOS startup smoke receipt version does not match release channel version for promoted head 'avalonia' (osx-arm64)., Windows desktop exit gate proof is missing, not passed, or lacks embedded payload/sample integrity proof., Release channel does not publish any promoted Linux installer media., Release channel is missing required desktop platform/head installer tuple pair(s): avalonia:linux, avalonia:macos., Desktop shelf contains installer artifacts not represented in release-channel promoted tuples: chummer-avalonia-linux-x64-installer.deb, chummer-avalonia-osx-arm64-installer.dmg, chummer-blazor-desktop-linux-x64-installer.deb, chummer-blazor-desktop-osx-arm64-installer.dmg., Executable gate reports stale passing platform gate receipts for non-promoted desktop tuples: linux:blazor-desktop:linux-x64, macos:blazor-desktop:osx-arm64., Release channel publishes Windows installer media, but executable-gate evidence is missing passing Windows startup-smoke tuple proof. ; mobile_play_shell: Recover-from-sync-conflict journey is blocked, not ready. ; ui_kit_and_flagship_polish: Build/explain/publish journey is blocked, not ready. ; media_artifacts: Build/explain/publish journey is blocked, not ready.
- [HIGH] ui_parity_gap: Hero Lab importer route is not directly parity-proven. Current parity artifacts do not directly prove the Hero Lab Importer route with screenshot-backed runtime coverage.
- [HIGH] ui_parity_gap: Identity Contacts Lifestyles History is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['workflow:contacts', 'workflow:lifestyles', 'workflow:notes'].
- [HIGH] ui_parity_gap: Legacy And Adjacent Import Oracles is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:hero_lab_importer', 'workflow:import_oracle'].
- [HIGH] ui_parity_gap: Sheet Export Print Viewer And Exchange is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:open_for_printing', 'menu:open_for_export', 'menu:file_print_multiple'].
- [HIGH] ui_parity_gap: Sr6 Supplements Designers And House Rules is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['workflow:sr6_supplements', 'workflow:house_rules'].
- [HIGH] ui_parity_gap: Translator route is not directly parity-proven. Current parity artifacts do not directly prove the Translator route with screenshot-backed runtime coverage.
- [HIGH] ui_parity_gap: XML amendment editor route is not directly parity-proven. Current parity artifacts do not directly prove the XML Amendment Editor route with screenshot-backed runtime coverage.

## Element matrix

| Element | Category | Visual parity | Behavioral parity | In Chummer5A | Removable without workflow degradation | Reason |
| --- | --- | --- | --- | --- | --- | --- |
| Character Roster Multi Character Flow | baseline_surface | yes | yes | yes | no | The screenshot baseline and matching runtime interaction proof are both present. |
| First Launch Workbench Or Restore | baseline_surface | yes | yes | yes | no | The screenshot baseline and matching runtime interaction proof are both present. |
| Master Index Dense Reference Flow | baseline_surface | yes | yes | yes | no | The screenshot baseline and matching runtime interaction proof are both present. |
| Menu File Open Save Import | baseline_surface | yes | yes | yes | no | The screenshot baseline and matching runtime interaction proof are both present. |
| Menu Tools Settings Masterindex Roster | baseline_surface | yes | yes | yes | no | The screenshot baseline and matching runtime interaction proof are both present. |
| Menu Windows Help Liveness | baseline_surface | yes | yes | yes | no | The screenshot baseline and matching runtime interaction proof are both present. |
| Advancement dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Character Roster dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Character creation section | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Contacts section | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Cyberware dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Dense builder section (dark) | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Dense builder section (light) | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Diary dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| File/open/import menu surface | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Import dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Initial workbench shell | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Loaded runner tab strip | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Loaded runner workbench | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Magic dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Master Index dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Matrix dialog | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Settings surface | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| Vehicles and drones section | captured_surface | yes | yes | yes | no | Required screenshot is present and the matching runtime-backed interaction proof is passing. |
| StatusStrip | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| pgbProgress | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| tabCharacterTabs | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| tabInfo | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| treArmor | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| treCyberware | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| treGear | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| treQualities | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| treVehicles | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| treWeapons | dense_workbench_landmark | yes | yes | yes | no | This Chummer5A dense-workbench landmark is still present in the successor workbench proof. |
| Character Roster dialog title | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Character Roster tree | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Character roster route | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| File menu | legacy_source_anchor | yes | yes | yes | no | Runtime-backed menu-bar label and clickability proofs are passing. |
| Global settings route | legacy_source_anchor | yes | yes | yes | no | Runtime-backed settings-route parity is directly covered by the current file/settings proof. |
| Help menu | legacy_source_anchor | yes | yes | yes | no | Runtime-backed menu-bar label and clickability proofs are passing. |
| Hero Lab importer route | legacy_source_anchor | no | no | yes | no | Current parity artifacts do not directly prove the Hero Lab Importer route with screenshot-backed runtime coverage. |
| Master Index dialog title | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Master Index source click reminder | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Master index route | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Open for export route | legacy_source_anchor | yes | yes | yes | no | Runtime-backed open-for-export route parity is directly covered by the current file-menu proof. |
| Open route | legacy_source_anchor | yes | yes | yes | no | Runtime-backed open-route parity is directly covered by the current file-menu proof. |
| Tools menu | legacy_source_anchor | yes | yes | yes | no | Runtime-backed menu-bar label and clickability proofs are passing. |
| Translator route | legacy_source_anchor | no | no | yes | no | Current parity artifacts do not directly prove the Translator route with screenshot-backed runtime coverage. |
| Windows menu | legacy_source_anchor | yes | yes | yes | no | Runtime-backed menu-bar label and clickability proofs are passing. |
| XML amendment editor route | legacy_source_anchor | no | no | yes | no | Current parity artifacts do not directly prove the XML Amendment Editor route with screenshot-backed runtime coverage. |
| Character Roster is first-class | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| Claim restore is in-app or installer-backed | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| File menu stays live | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| Guided product installer happy path | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| Master Index is first-class | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| No browser-only claim code ritual | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| No generic shell or dashboard first | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| Startup lands in workbench or restore | non_negotiable | yes | yes | yes | no | This Chummer5A non-negotiable is directly backed by the current screenshot/runtime parity evidence. |
| Bottom status strip | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Character roster route | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| File menu | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Help menu | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Immediate toolstrip | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Import route | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Master index route | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Save or open route | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Settings route | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Tools menu | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Windows menu | required_landmark | yes | yes | yes | no | This required Chummer5A landmark is directly backed by current screenshot/runtime proof. |
| Locate Master Index And Roster | veteran_task | yes | yes | yes | no | Required baseline captures and the matching veteran workflow interaction proof are present. |
| Locate Save Import Settings | veteran_task | yes | yes | yes | no | Required baseline captures and the matching veteran workflow interaction proof are present. |
| Reach Real Workbench | veteran_task | yes | yes | yes | no | Required baseline captures and the matching veteran workflow interaction proof are present. |
| Recover Section Rhythm | veteran_task | yes | yes | yes | no | Required baseline captures and the matching veteran workflow interaction proof are present. |
| Custom Data Xml And Translator Bridge | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:translator', 'menu:xml_editor']. |
| Dense Builder And Career Workflows | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['oracle:tabs', 'oracle:workspace_actions', 'workflow:build_explain_publish']. |
| Dice Initiative And Table Utilities | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:dice_roller', 'workflow:initiative']. |
| Identity Contacts Lifestyles History | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['workflow:contacts', 'workflow:lifestyles', 'workflow:notes']. |
| Legacy And Adjacent Import Oracles | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:hero_lab_importer', 'workflow:import_oracle']. |
| Roster Dashboards And Multi Character Ops | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['baseline:character_roster_multi_character_flow', 'workflow:multi_character']. |
| Settings And Rules Environment Authoring | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['workflow:rules', 'workflow:sources', 'baseline:menu_tools_settings_masterindex_roster']. |
| Sheet Export Print Viewer And Exchange | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:open_for_printing', 'menu:open_for_export', 'menu:file_print_multiple']. |
| Shell Workbench Orientation | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['baseline:first_launch_workbench_or_restore', 'baseline:menu_windows_help_liveness']. |
| Sourcebooks Reference And Master Index | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['baseline:master_index_dense_reference_flow', 'workflow:sources']. |
| Sr6 Supplements Designers And House Rules | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['workflow:sr6_supplements', 'workflow:house_rules']. |
