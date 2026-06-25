#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "presentation_roster_hierarchy_contract",
        "path": "Chummer.Presentation/Overview/RosterHierarchyState.cs",
        "tokens": [
            "RosterHierarchyState",
            "RosterHierarchyFolderState",
            "RosterHierarchyMoveIntentState",
            "MovesFilesOnlyAfterConfirmation",
            "RosterHierarchyDeletePolicies",
            "move_children_to_inbox_first",
        ],
    },
    {
        "id": "presentation_roster_hierarchy_preferences",
        "path": "Chummer.Presentation/Overview/DesktopPreferenceState.cs",
        "tokens": [
            "RosterHierarchyJson",
            "string RosterHierarchyJson = \"\"",
        ],
    },
    {
        "id": "presentation_roster_hierarchy_preference_normalization",
        "path": "Chummer.Presentation/Overview/DesktopPreferenceStateRuntime.cs",
        "tokens": [
            "RosterHierarchyJson = NormalizeRosterHierarchyJson",
            "NormalizeRosterHierarchyJson",
        ],
    },
    {
        "id": "presentation_character_roster_hierarchy",
        "path": "Chummer.Presentation/Overview/DesktopDialogFactory.cs",
        "tokens": [
            "rosterCustomFolders",
            "rosterHierarchyStatus",
            "BuildRosterHierarchyStatus",
            "Enter/Space select or drop; Escape clears source",
            "rosterFolderName",
            "rosterTargetFolder",
            "BuildRosterFolderOptions",
            "Choose a folder or type a folder id/name for nesting and moves",
            "rosterSourceFolder",
            "Choose a custom folder or type a folder id/name for rename, delete, and nesting",
            "IncludeSystemFolders",
            "rosterSourceItem",
            "Move Runner to Folder",
            "Create Roster Folder",
            "Delete Roster Folder",
            "Reorder Character Tree",
            "Reset Character Layout",
            "Drag runner onto folder: preview move",
            "Keyboard: Enter/Space selects source, Enter/Space on folder drops, Escape clears source",
            "layout metadata follows owner and runner scope",
            "delete custom folder moves runner/link items to Inbox and reparents child folders",
            "folder drops cannot target their own descendants",
            "dragged row wins before selected-runner fallback",
            "globalRosterHierarchyJson",
            "preferences.RosterHierarchyJson",
            "rosterHierarchySource",
            "staged preference metadata",
            "TryDeserializeRosterHierarchyState",
            "RosterHierarchyJson = DesktopDialogFieldValueParser.GetValue",
        ],
    },
    {
        "id": "presentation_roster_command_dispatch",
        "path": "Chummer.Presentation/Overview/DialogCoordinator.cs",
        "tokens": [
            "create_roster_group",
            "rename_roster_group",
            "delete_roster_group",
            "move_runner_to_group",
            "reorder_roster_tree",
            "reset_roster_hierarchy",
            "StageCharacterRosterHierarchyMutation",
            "ResetCharacterRosterHierarchy",
            "Roster layout reset to generated grouping",
            "CreateRosterFolder",
            "DeleteRosterFolder",
            "Moved {movedItemCount} runner/link item(s) to Inbox",
            "MoveRosterRunner",
            "ResolveRosterSourceItem",
            "ExtractRosterLineLabel",
            "RosterHierarchyItemKinds.Workspace",
            "LastIndexOf(\" [\"",
            "Nested roster folder",
            "IsRosterFolderDescendant",
            "cannot be nested under one of its own child folders",
            "PublishCharacterRosterDialog(context, nextPreferences",
            "Created roster folder",
            "RosterHierarchyJson",
        ],
    },
    {
        "id": "blazor_dialog_markup",
        "path": "Chummer.Blazor/Components/Shell/DialogHost.razor",
        "tokens": [
            "data-roster-hierarchy-field",
            "rosterHierarchyStatus",
            "dialog-roster-tree",
            "dialog-roster-list",
            "data-roster-drag-root",
            "@ondragstart",
            "@ondrop",
            "DialogRosterDropIntent",
            "RosterDropRequested",
            "reorder_roster_tree",
            "IsRosterHierarchyField",
            "IsRosterDropTargetLine",
            "is-drop-target",
            "BuildRosterHierarchyLineTitle",
            "aria-label",
            "aria-describedby",
            "dialogRosterKeyboardStatus",
            "BuildRosterHierarchyLineDescriptionId",
            "BuildRosterHierarchyContainerRole",
            "BuildRosterHierarchyContainerLabel",
            "aria-orientation",
            "BuildRosterHierarchyContainerOrientation",
            "BuildRosterHierarchyLineRole",
            "treeitem",
            "aria-level",
            "BuildRosterHierarchyLineLevel",
            "aria-selected",
            "BuildRosterHierarchyLineSelected",
            "return null;",
            "aria-expanded",
            "BuildRosterHierarchyLineExpanded",
            "aria-keyshortcuts",
            "BuildRosterHierarchyLineKeyShortcuts",
            "Enter Space Escape",
            "@onkeydown:preventDefault",
            "ShouldPreventRosterLineKeyDefault",
            "BuildRosterHierarchyLineTabIndex",
            "HandleRosterLineKeyDownAsync",
            "Escape",
            "Enter",
            "is-drag-source",
            "dialog-roster-keyboard-status",
            "Keyboard source selected:",
            "role=\"status\"",
            "aria-atomic=\"true\"",
            "Keyboard: Enter/Space selects this source; Escape clears it.",
            "Keyboard: Enter/Space drops the selected source here.",
            "Moves are virtual metadata only until explicitly committed.",
            "BuildRosterHierarchyLineClass",
        ],
    },
    {
        "id": "blazor_roster_drag_drop_shell_bridge",
        "path": "Chummer.Blazor/Components/Layout/DesktopShell.Dialogs.cs",
        "tokens": [
            "OnDialogRosterDropAsync",
            "SourceFolder",
            "rosterSourceItem",
            "rosterSourceFolder",
            "rosterTargetFolder",
            "ExecuteDialogActionAsync(intent.ActionId",
            "SyncShellWorkspaceContextAsync",
        ],
    },
    {
        "id": "blazor_dialog_style",
        "path": "Chummer.Blazor/wwwroot/app.css",
        "tokens": [
            ".dialog-field--roster-hierarchy",
            "content: \" preview\"",
            ".dialog-visual--roster-hierarchy",
            ".dialog-roster-line.is-draggable",
            ".dialog-roster-line.is-drop-target",
            ".dialog-roster-line.is-drag-source",
            ".dialog-roster-keyboard-status",
            ".dialog-roster-line:focus-visible",
            ".dialog-roster-line.is-selected",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "Character Roster now also carries custom hierarchy posture",
            "non-destructive metadata mutation",
            "`rosterHierarchySource` disclosure",
            "roster-organization affordances",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "character roster custom hierarchy posture",
            "user-created virtual folders, nested groups, drag/drop move intent",
            "non-destructive metadata mutation",
            "Blazor drag/drop event bridge",
            "`rosterHierarchySource` disclosure",
            "before full hosted drag execution is claimed",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_ROSTER_HIERARCHY_STAGED_PROOF",
            "workbench_roster_hierarchy_staged_status",
            "workbench_roster_hierarchy_staged_source_checks",
            "source_alignment_only_not_browser_execution",
        ],
    },
]


def read_text(relative_path: str) -> str:
    return (REPO_ROOT / relative_path).read_text(encoding="utf-8")


def evaluate_check(check: dict) -> dict:
    content = read_text(check["path"])
    missing_tokens = [token for token in check["tokens"] if token not in content]
    return {
        "id": check["id"],
        "path": check["path"],
        "status": "passed" if not missing_tokens else "failed",
        "required_token_count": len(check["tokens"]),
        "missing_tokens": missing_tokens,
    }


def main() -> int:
    evaluated_checks = [evaluate_check(check) for check in CHECKS]
    failures = [check for check in evaluated_checks if check["status"] != "passed"]
    receipt = {
        "contract_name": "chummer6-ui.blazor_workbench_roster_hierarchy_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench?command=character_roster"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that the character roster hierarchy source, styling, status, and docs agree.",
            "It is not a substitute for hosted browser execution, drag/drop mutation, durable layout persistence, filesystem moves, folder deletion, watched-file relocation, or external RosterHierarchyState storage.",
            "Do not use this receipt to claim complete roster persistence or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_roster_hierarchy_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
