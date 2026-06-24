# Chummer5A UI Element Parity Audit

Generated at: 2026-06-24T02:30:32.301857Z

## Scope
This matrix covers every parity-tracked visible surface and currently-present disallowed extra represented in the Chummer5A oracle, screenshot review gate, visual familiarity gate, workflow execution gate, and veteran workflow packs.

## Summary
- Total audited elements: 84
- Visual parity yes/no: 82/2
- Behavioral parity yes/no: 82/2
- Chummer6-only extras present: 0
- Removable extras present: 0
- Active/productive/nonproductive shard runs: 1/0/0

## Top findings
- [HIGH] readiness_gap: Flagship readiness still contains open coverage keys outside the surface-level desktop parity matrix. desktop_client: Executable desktop exit gate proof is missing or not passed. Desktop shell/install/support liveliness must be proven from shipped artifacts., Executable gate blocker: flagship UI release gate proof is stale (238147s old)., Executable gate blocker: Desktop visual familiarity exit gate is missing or not passing., Executable gate blocker: Desktop workflow execution gate is missing or not passing., Executable gate blocker: Windows desktop exit gate requires a Windows-capable host; current host cannot run promoted Windows installer smoke., Executable gate blocker: Windows gate reason: Published Windows installer is missing a recognizable desktop payload marker., Executable gate blocker: Windows gate reason: Published Windows installer is missing the bundled sample-character marker., Executable gate blocker: Windows gate reason: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host., Executable gate blocker: Windows gate reason: Chummer5a desktop workflow parity proof is missing or not passed., Executable gate blocker: Windows installer receipt does not confirm a recognizable payload marker for promoted tuple avalonia:win-x64., Executable gate blocker: Windows installer receipt does not confirm bundled demo sample marker for promoted tuple avalonia:win-x64., Executable gate blocker: linux desktop exit gate proof for avalonia:linux-x64 is stale (237149s old)., Executable gate blocker: Linux desktop exit gate receipt checks.release_channel_version does not match release channel version for promoted head 'avalonia'., Executable gate blocker: Linux desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head 'avalonia'., Executable gate blocker: Linux installer startup smoke receipt version does not match release channel version for promoted head 'avalonia'., Executable gate blocker: Linux gate embedded release_channel_linux_artifact version/releaseVersion does not match promoted release channel version., Executable gate blocker: Linux gate embedded release_channel_linux_artifact sha256 does not match promoted release channel., Executable gate blocker: Linux gate embedded release_channel_linux_artifact sizeBytes does not match promoted release channel., Desktop visual familiarity exit gate proof is missing or not passed. Workflow parity without familiar theme/layout/dialog posture does not pass., Windows desktop exit gate proof is missing, not passed, or lacks payload/sample integrity proof., Chummer5a desktop workflow parity proof is missing or not passed. Representative shell parity is not enough., Release channel publishes Linux installer media, but executable-gate evidence is missing passing Linux startup-smoke tuple proof., Release channel publishes Windows installer media, but executable-gate evidence is missing passing Windows startup-smoke tuple proof., Chummer5A UI element parity audit still has unresolved release-blocking rows: Hero Lab importer route (no/no), Legacy And Adjacent Import Oracles (no/no)., Chummer5A UI element parity audit still reports open parity gaps: visual_no_count=2, behavioral_no_count=2.
- [HIGH] ui_parity_gap: Hero Lab importer route is not directly parity-proven. Current parity artifacts do not directly prove the Hero Lab importer route with runtime/dialog coverage.
- [HIGH] ui_parity_gap: Legacy And Adjacent Import Oracles is not directly parity-proven. At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:hero_lab_importer', 'workflow:import_oracle'].

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
| Hero Lab importer route | legacy_source_anchor | no | no | yes | no | Current parity artifacts do not directly prove the Hero Lab importer route with runtime/dialog coverage. |
| Master Index dialog title | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Master Index source click reminder | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Master index route | legacy_source_anchor | yes | yes | yes | no | Master Index and Character Roster runtime-backed route proofs are passing. |
| Open for export route | legacy_source_anchor | yes | yes | yes | no | Runtime-backed open-for-export route parity is directly covered by the current file-menu proof. |
| Open route | legacy_source_anchor | yes | yes | yes | no | Runtime-backed open-route parity is directly covered by the current file-menu proof. |
| Tools menu | legacy_source_anchor | yes | yes | yes | no | Runtime-backed menu-bar label and clickability proofs are passing. |
| Translator route | legacy_source_anchor | yes | yes | yes | no | Catalog, presenter, dialog-factory, and dual-head acceptance proofs directly cover the Translator route. |
| Windows menu | legacy_source_anchor | yes | yes | yes | no | Runtime-backed menu-bar label and clickability proofs are passing. |
| XML amendment editor route | legacy_source_anchor | yes | yes | yes | no | Catalog, presenter, dialog-factory, and dual-head acceptance proofs directly cover the XML Amendment Editor route. |
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
| Custom Data Xml And Translator Bridge | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['menu:translator', 'menu:xml_editor']. |
| Dense Builder And Career Workflows | workflow_family | yes | yes | yes | no | Route-local dense workbench proof cites ruleset tabs, workspace actions, screenshot review, and the published flagship workflow receipts directly. |
| Dice Initiative And Table Utilities | workflow_family | yes | yes | yes | no | Route-local dice and initiative proof cites the generated dice dialog parity, runboard initiative route, and current workflow receipts directly. |
| Identity Contacts Lifestyles History | workflow_family | yes | yes | yes | no | Route-local contacts, lifestyles, and history proof cites section-host parity, current contacts and diary screenshots, and current workflow-state receipts directly. |
| Legacy And Adjacent Import Oracles | workflow_family | no | no | yes | no | At least one declared compare artifact for this Chummer5A family lacks direct parity proof: ['menu:hero_lab_importer', 'workflow:import_oracle']. |
| Roster Dashboards And Multi Character Ops | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['baseline:character_roster_multi_character_flow', 'workflow:multi_character']. |
| Settings And Rules Environment Authoring | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['workflow:rules', 'workflow:sources', 'baseline:menu_tools_settings_masterindex_roster']. |
| Sheet Export Print Viewer And Exchange | workflow_family | yes | yes | yes | no | Route-local print, export, and exchange proof cites menu parity, screenshot review markers, and deterministic workspace-exchange receipts directly. |
| Shell Workbench Orientation | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['baseline:first_launch_workbench_or_restore', 'baseline:menu_windows_help_liveness']. |
| Sourcebooks Reference And Master Index | workflow_family | yes | yes | yes | no | All declared compare artifacts for this Chummer5A family are directly backed by current parity proof: ['baseline:master_index_dense_reference_flow', 'workflow:sources']. |
| Sr6 Supplements Designers And House Rules | workflow_family | yes | yes | yes | no | Route-local SR6 supplement and house-rule proof cites screenshot review markers, rule studio surface proof, and deterministic successor-lane receipts directly. |
