# Blazor Workbench Roster Hierarchy Staged Proof

## Purpose

This staged proof keeps Chummer Online and /blazor/workbench sources aligned around the user-facing Character Roster and custom dossier roster hierarchy posture.

It covers visible source posture for custom roster directories and a hierarchy of the user's choosing, user-created virtual folders, nested groups, drag/drop move intent, explicit roster move actions, watched-file virtual links, browser markup/styling, a visible character-library-tree toolbar with organize-ready versus move-pending state, title affordances with row-level keyboard help, aria labels, aria-describedby linkage to dialog-scoped baseline keyboard hint notes and live source feedback hosts, labelled tree containers, vertical tree orientation, tree/treeitem roles, aria-level depth metadata, aria-selected state for selected/source rows, aria-expanded state for folder rows, aria-keyshortcuts for actionable rows, nullable optional ARIA emission for presentation rows, focusability, visible keyboard drag-source state, separate mouse and keyboard source state with mode-specific source badges and live source status instructions, mouse drag-end cleanup for stale mouse sources, atomic live source feedback, visible keyboard operation guidance, hierarchy-status keyboard shortcut summary, scoped Enter/Space/Escape keyboard handling that does not globally suppress focus navigation, and keyboard handling for actionable roster hierarchy rows, editable directory/target fields with option-backed source and target directory pickers, a custom-only source picker for rename/delete/nesting choices, styled visible hierarchy status counts and pending-move disclosure, hidden source-item carriage, a Blazor drag/drop event bridge that fills `rosterTargetFolder`, carries `rosterSourceFolder` for directory drops, carries `rosterSourceItem` for dragged runner/link rows, preserves full runner labels while stripping visual row suffixes, and invokes the same virtual move/reorder actions, non-destructive metadata mutation for create/rename/delete/move/reorder actions including custom-directory nesting, safe custom-directory deletion that moves runner/link items to Inbox and reparents child directories, cycle prevention for directory drops into their own descendants, reset-to-generated-layout recovery that clears only hierarchy metadata, the shared `RosterHierarchyState` contract, shared `RosterHierarchyStateJson` serialization and validation for Avalonia/web reuse, `RosterHierarchyJson` preference staging, staged metadata reuse, `rosterHierarchySource` disclosure for generated versus staged preference metadata, plus hidden global-settings carriage on `/blazor/app?command=character_roster` and compatibility carriage on `/blazor/workbench?command=character_roster`.
Shared roster hierarchy metadata constants live in `RosterHierarchyMetadata`, including `FormatVersion`, `GeneratedSource`, `StagedPreferenceSource`, `ActiveTableFolderId`, `ActiveTableFolderName`, `SavedRunnersFolderId`, `SavedRunnersFolderName`, `InboxFolderId`, `InboxFolderName`, `WatchLinksFolderId`, `WatchLinksFolderName`, `UserDirectoriesLabel`, and `SystemDirectoriesLabel`, so Avalonia and Blazor do not fork persisted hierarchy source or default folder semantics.
The roster dialog factory and mutation coordinator use those constants for generated/staged source disclosure, Active Table, Saved Runners, Inbox, Watch Folder Links, User/System directory labels, and delete notices, so the shared metadata contract is operational rather than docs-only.

The roster command opens as `Character Roster` and tells users they can group dossiers into their own folders, drag dossiers or custom directories through the tree, and keep selected-dossier details close without moving watched files until explicitly confirmed.
System library buckets remain valid drop targets for filing runners and links, but they are not draggable source directories; only user-created custom directories can be nested, renamed, reordered, or deleted. The Blazor tree styles system rows as bucket targets, custom rows as folder targets, runner rows as direct dossier items, and watched-file or shortcut rows as linked roster references.
The Blazor dialog source keeps roster field ids, folder scopes, and drag/drop mutation command ids centralized as DialogHost-local constants, including `RosterCustomFoldersFieldId`, `RosterHierarchyStatusFieldId`, `RosterSystemFolderScope`, `RosterCustomFolderScope`, `RosterMoveRunnerToGroupCommand`, and `RosterReorderTreeCommand`, so drag/drop targeting, source-kind detection, `data-roster-line-kind` and `data-roster-folder-scope` selectors, and bridge command ids do not drift through repeated string literals.

Visible management actions use Roster vocabulary for the same workflow, including `Open Roster Folder`, `Create Roster Folder`, `Configure Roster Folder`, `Create Roster Directory`, `Rename Roster Directory`, `Delete Roster Directory`, and `Roster Entries`, while internal action IDs remain roster-oriented for compatibility.

Command-list and status text follows the same wording, including `Create custom roster directory`, `Undo last roster move`, `Save runner to roster folder`, and `configure a roster folder first`.

The visible Blazor hierarchy uses a polished amber/mint/blue Chummer Online hierarchy treatment: selected rows, drag sources, drop targets, keyboard-source state, pending organization status, and the organize-ready toolbar all remain visibly branded as Chummer Online instead of collapsing into a generic browser tree.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-roster-hierarchy-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF.generated.json
```

## Documentation Index Requirement

The staged proof also checks `docs/BLAZOR_WEB_CLIENT_DOCS_INDEX.md` for this contract document. Roster hierarchy is part of the browser-client parity story, so the contract must stay discoverable from the top-level Blazor/web-client docs map alongside the materializer, milestone wrapper, and example receipt.

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, complete drag/drop UX coverage for every source item type, filesystem moves, directory deletion, watched-file relocation, or external `RosterHierarchyState` storage beyond preference-carried layout metadata.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof plus explicit drag/drop persistence implementation.

The public home hero also carries `data-home-hero-action="explore-chummer-online"`, uses the path-base-safe `app?command=character_roster` link, and shows `Roster entry: /app?command=character_roster` in the route-pills panel so the visible entry path opens the roster organization workflow instead of stopping at a generic app shell. Public copy uses Trust and web-client tour language instead of exposing preview/proof route pills. The home hero keeps the same polished amber/mint/blue route-entry theme with ambient grid texture, reduced-motion-safe public surface reveal, high-contrast affordances, and mobile-softened grid density.
The home page source keeps that roster-first route derived from `AppRoute` and `CharacterRosterCommand` through `RosterRoute` and `PublicRosterRoute`, and keeps the home self-link behind `HomeRoute`, so the visible Chummer Online CTA, brand link, and display pill stay aligned without scattering route or startup command strings.
The public title, brand subtitle, hero headline, promise label, live-client card, and footer use Chummer Online wording while preserving `/app` as the clean route alias for the browser client.
The status strip also presents the clean route as Chummer Online and the compatibility lane as Chummer Online compatibility while keeping stable internal route-family ids such as `chummer_app` and `workbench_compat` for analytics, receipts, and proof tooling.
The startup shell keeps browser-client wording as well: recent entries restore Chummer Online dossier continuations and support commands are grouped under Chummer Utilities, while the underlying roster and master-index command ids stay unchanged.
The Blazor drag/drop bridge carries normalized `SourceItem`, `SourceKind`, `SourceFolderScope`, and `TargetFolderScope` metadata in addition to raw source/target lines, so runtime proof can assert move intent without parsing visual tree glyphs. Roster rows also expose `data-roster-source-item`, `data-roster-drop-action`, and `data-roster-accepts-drop-kinds` so browser probes can inspect the normalized source item, accepted source kinds, and intended virtual action before firing drag/drop.

The Chummer Online shell also marks the startup command with `data-chummer-app-startup-command` and displays roster-specific copy when `command=character_roster` is present, so users can tell the app intentionally opened the Character Roster workflow.
The app shell source keeps the roster startup command centralized as `CharacterRosterCommand` for both the search/filter roster shortcut and the `IsCharacterRosterCommand` comparison, matching the home route's derived roster link posture.
The restored-workspace route source keeps `workspace` centralized as `WorkspaceQueryName`, continues accepting legacy `runner` through `LegacyRunnerQueryName`, resolves `workspace` before the legacy fallback when both are present, generates restored-runner links with `workspace=...`, and marks restored-workspace links plus restored continuation/action cards with `data-workbench-route-query="workspace"` so Chummer Online, `/blazor/workbench`, docs, and public-edge probes do not drift between query keys.

The app shell labels the direct roster shortcut as `Character Roster` with the visible action summary `Find, group, and organize existing dossiers.` so search/filter and startup surfaces use the same dossier-facing vocabulary as the dialog.

The same home surface keeps Blazor-internal navigation such as `home`, `app`, and `showcase` relative while leaving portal routes such as `/downloads/` and `/docs/` absolute.
