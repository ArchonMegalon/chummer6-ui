#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

registry_path="${CHUMMER_NEXT90_REGISTRY_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml}"
queue_path="${CHUMMER_NEXT90_QUEUE_PATH:-/docker/fleet/.codex-studio/published/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
design_queue_path="${CHUMMER_NEXT90_DESIGN_QUEUE_PATH:-/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml}"
receipt_path="${CHUMMER_NEXT90_M103_UI_RECEIPT_PATH:-$repo_root/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json}"
release_channel_path="${CHUMMER_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
authority_repo_root="${CHUMMER_NEXT90_M103_AUTHORITY_REPO_ROOT:-/docker/chummercomplete/chummer6-ui-finish}"
test_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
compliance_guard_path="$repo_root/Chummer.Tests/Compliance/Next90M103VeteranCertificationGuardTests.cs"
verify_script_path="$repo_root/scripts/ai/verify.sh"
toolstrip_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml"
menu_path="$repo_root/Chummer.Avalonia/Controls/ShellMenuBarControl.axaml"
event_handlers_path="$repo_root/Chummer.Avalonia/MainWindow.EventHandlers.cs"
published_screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
published_screenshot_review_markdown_path="$repo_root/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION_REVIEW.generated.md"
legacy_chummer5a_root="${CHUMMER5A_ROOT:-/docker/chummer5a}"

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$registry_path" "$queue_path" "$design_queue_path" "$receipt_path" "$release_channel_path" "$test_path" "$compliance_guard_path" "$verify_script_path" "$toolstrip_path" "$menu_path" "$event_handlers_path" "$published_screenshot_dir" "$published_screenshot_review_markdown_path" "$legacy_chummer5a_root" "$repo_root" "$authority_repo_root"
from __future__ import annotations

import json
import base64
import binascii
import gzip
import hashlib
import html
import re
import struct
import subprocess
import sys
import urllib.parse
import zlib
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

(
    registry_path_text,
    queue_path_text,
    design_queue_path_text,
    receipt_path_text,
    release_channel_path_text,
    test_path_text,
    compliance_guard_path_text,
    verify_script_path_text,
    toolstrip_path_text,
    menu_path_text,
    event_handlers_path_text,
    screenshot_dir_text,
    screenshot_review_markdown_path_text,
    legacy_chummer5a_root_text,
    source_repo_root_text,
    authority_repo_root_text,
) = sys.argv[1:]

registry_path = Path(registry_path_text)
queue_path = Path(queue_path_text)
design_queue_path = Path(design_queue_path_text)
receipt_path = Path(receipt_path_text)
release_channel_path = Path(release_channel_path_text)
test_path = Path(test_path_text)
compliance_guard_path = Path(compliance_guard_path_text)
verify_script_path = Path(verify_script_path_text)
toolstrip_path = Path(toolstrip_path_text)
menu_path = Path(menu_path_text)
event_handlers_path = Path(event_handlers_path_text)
screenshot_dir = Path(screenshot_dir_text)
screenshot_review_markdown_path = Path(screenshot_review_markdown_path_text)
legacy_chummer5a_root = Path(legacy_chummer5a_root_text)
source_repo_root = Path(source_repo_root_text)
authority_repo_root = Path(authority_repo_root_text)
git_repo_root = authority_repo_root if (authority_repo_root / ".git").is_dir() else source_repo_root

PACKAGE_ID = "next90-m103-ui-veteran-certification"
FRONTIER_ID = 2257965187
MILESTONE_ID = 103
WAVE = "W7"
EXPECTED_ALLOWED_PATHS = [
    "Chummer.Avalonia",
    "Chummer.Blazor",
    "Chummer.Tests",
    "scripts",
]
EXPECTED_SURFACES = [
    "veteran_migration_certification",
    "screenshot_parity:desktop",
]
EXPECTED_LANDED_COMMIT = "a8e4f92c"
EXPECTED_COMPLETION_ACTION = "verify_closed_package_only"
EXPECTED_DO_NOT_REOPEN_REASON = "M103 chummer6-ui veteran certification is complete; future shards must verify this receipt, registry row, design queue row, Fleet queue row, and direct proof command instead of recapturing promoted-head Chummer5a screenshot parity."
EXPECTED_PROOF_COMMITS = {
    "fb6eb62e": "Tighten next90 M103 veteran proof guard",
    "3bba8754": "Tighten M103 veteran verify proof",
    "6eafef39": "Tighten M103 veteran queue source proof",
    "1e5557f9": "Pin M103 design queue source proof",
    "9e8d494b": "Tighten M103 successor registry guard",
    "e796c016": "Pin M103 successor proof commits",
    "cb84c37b": "Tighten M103 veteran successor proof guard",
    "809a91d0": "Pin M103 veteran successor guard",
    "0d2e357e": "Pin M103 veteran proof successor commit",
    "b42416b8": "Pin M103 veteran proof hardening commit",
    "5d9f1c86": "Pin M103 queue frontier proof",
    "df06b668": "Pin M103 frontier proof anchor",
    "8ea486f6": "Bind M103 queue proof anchors",
    "11a0882e": "Pin M103 veteran proof anchor",
    "2bfc7338": "Pin M103 veteran queue proof anchors",
    "243062ac": "Pin M103 veteran queue proof commit",
    "b8de3f95": "Tighten M103 active-run proof guard",
    "258fed08": "Tighten M103 successor queue header proof",
    "6c3c93e9": "Pin M103 queue header proof commit",
    "22758a4c": "Pin latest M103 queue proof guard",
    "fd93bb8a": "Tighten M103 queue mirror proof guard",
    "e9f92d0d": "Pin M103 queue mirror proof guard",
    "a4d93e27": "Pin M103 current queue mirror proof",
    "15e4d474": "Pin M103 current queue mirror proof",
    "075b292b": "Tighten M103 registry proof guard",
    "d9bfcff5": "Pin M103 current veteran proof floor",
    "783ac00b": "Pin M103 veteran proof floor",
    "827d3546": "Pin M103 veteran certification proof floor",
    "89ab0ec0": "Pin M103 veteran certification proof floor",
    "bb825994": "Pin M103 current veteran proof floor",
    "594aa3cd": "Pin M103 current veteran proof floor guard",
    "eb11fde4": "Bind M103 veteran proof guard to queue anchor",
    "fa07f2bb": "Pin M103 queue anchor proof floor",
    "680deb43": "Pin M103 veteran certification proof floor",
    "10d65c5f": "Pin M103 veteran certification proof floor",
    "ba914e9a": "Pin current M103 veteran proof floor",
    "de5837b9": "Tighten M103 veteran proof token guard",
    "762aaedb": "Pin M103 veteran proof token guard",
    "8ce865c4": "Tighten M103 veteran no-reopen proof",
    "55f2efca": "Tighten M103 active-run proof guard",
    "f649825d": "Pin M103 active-run proof floor",
    "585ccc78": "Pin M103 active-run proof floor guard",
    "9a6ebd38": "Pin M103 veteran certification proof floor",
    "b0f424aa": "Tighten M103 scoped proof guard",
    "b8c0b19d": "Tighten M103 closed queue action guard",
    "136ff501": "test(next90-m103): tighten veteran review pack proof",
    "b40a6556": "Tighten M103 desktop veteran parity proof",
    "653adf49": "Tighten M103 veteran review packet proof",
    "a5dff485": "Tighten M103 review packet proof binding",
}
EXPECTED_PROOF_RECEIPT = "/docker/chummercomplete/chummer6-ui-finish/.codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json"
EXPECTED_PROOF_SCRIPT = "/docker/chummercomplete/chummer6-ui-finish/scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh"
EXPECTED_PROOF_GUARD = "/docker/chummercomplete/chummer6-ui-finish/Chummer.Tests/Compliance/Next90M103VeteranCertificationGuardTests.cs"
EXPECTED_PROOF_COMMAND = "bash scripts/ai/milestones/next90-m103-ui-veteran-certification-check.sh"
EXPECTED_DESIGN_QUEUE_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml"
EXPECTED_REGISTRY_PATH = "/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_PRODUCT_ADVANCE_REGISTRY.yaml"
EXPECTED_QUEUE_HEADER = {
    "mode": "append",
    "program_wave": "next_90_day_product_advance",
    "status": "live_parallel_successor",
    "source_registry_path": EXPECTED_REGISTRY_PATH,
    "source_queue_fingerprint": "next90-staging-20260415-next-big-wins-widening",
}
EXPECTED_PROOF_COMMIT_ITEMS = [
    "/docker/chummercomplete/chummer6-ui-finish commit fb6eb62e",
    "/docker/chummercomplete/chummer6-ui-finish commit 3bba8754 tightens the M103 verifier receipt and standard-verify proof alignment.",
    "/docker/chummercomplete/chummer6-ui-finish commit 6eafef39 tightens the M103 verifier against design-owned queue source drift.",
    "/docker/chummercomplete/chummer6-ui-finish commit 1e5557f9 pins the M103 design queue source proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 9e8d494b tightens the M103 successor registry guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit e796c016 pins M103 successor proof commits.",
    "/docker/chummercomplete/chummer6-ui-finish commit cb84c37b tightens the M103 veteran successor proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 809a91d0 pins the M103 veteran successor guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 0d2e357e pins the M103 veteran proof successor commit.",
    "/docker/chummercomplete/chummer6-ui-finish commit b42416b8 pins the M103 veteran proof hardening commit.",
    "/docker/chummercomplete/chummer6-ui-finish commit 5d9f1c86 pins M103 queue frontier proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit df06b668 pins M103 frontier proof anchor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 8ea486f6 binds M103 queue proof anchors.",
    "/docker/chummercomplete/chummer6-ui-finish commit 11a0882e pins M103 veteran proof anchor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 2bfc7338 pins M103 veteran queue proof anchors.",
    "/docker/chummercomplete/chummer6-ui-finish commit 243062ac pins M103 veteran queue proof commit.",
    "/docker/chummercomplete/chummer6-ui-finish commit b8de3f95 tightens the M103 active-run proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 258fed08 tightens M103 successor queue header proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 6c3c93e9 pins M103 queue-header proof commit.",
    "/docker/chummercomplete/chummer6-ui-finish commit 22758a4c pins the latest M103 queue proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit fd93bb8a tightens M103 queue mirror proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit e9f92d0d pins M103 queue mirror proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit a4d93e27 pins M103 current queue mirror proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 15e4d474 pins M103 current queue mirror proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 075b292b tightens the M103 registry proof item guard so canonical successor evidence must match completed queue proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit d9bfcff5 pins M103 current veteran proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 783ac00b pins M103 veteran proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 827d3546 pins M103 veteran certification proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 89ab0ec0 pins M103 veteran certification proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit bb825994 pins M103 current veteran proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 594aa3cd pins M103 current veteran proof floor guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit eb11fde4 binds the M103 veteran proof guard to the current queue anchor.",
    "/docker/chummercomplete/chummer6-ui-finish commit fa07f2bb pins M103 queue anchor proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 680deb43 pins M103 veteran certification proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 10d65c5f pins M103 veteran certification proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit ba914e9a pins the current M103 veteran proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit de5837b9 tightens the M103 veteran proof token guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 762aaedb pins the M103 veteran proof token guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 8ce865c4 tightens M103 no-reopen proof posture.",
    "/docker/chummercomplete/chummer6-ui-finish commit 55f2efca tightens M103 active-run state-root proof exclusion.",
    "/docker/chummercomplete/chummer6-ui-finish commit f649825d pins M103 active-run proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit 585ccc78 pins M103 active-run proof floor guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit 9a6ebd38 pins M103 veteran certification proof floor.",
    "/docker/chummercomplete/chummer6-ui-finish commit b0f424aa tightens M103 scoped proof guard.",
    "/docker/chummercomplete/chummer6-ui-finish commit b8c0b19d tightens the M103 completed queue action guard so Fleet and design queue rows must carry verify_closed_package_only plus a package-specific do-not-reopen reason.",
    "/docker/chummercomplete/chummer6-ui-finish commit 136ff501 tightens the M103 veteran review pack proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit b40a6556 tightens the M103 desktop veteran parity proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit 653adf49 tightens the M103 veteran review packet proof.",
    "/docker/chummercomplete/chummer6-ui-finish commit a5dff485 tightens the M103 review packet proof binding.",
]
EXPECTED_VERIFY_BANNER = "checking next-90 M103 Chummer5a veteran certification guard"
DISALLOWED_ACTIVE_RUN_PROOF_TOKENS = [
    "TASK_LOCAL_TELEMETRY.generated.json",
    "ACTIVE_RUN_HANDOFF.generated.md",
    "/var/lib/codex-fleet",
    "active-run helper",
    "active-run helper command",
    "operator telemetry",
    "operator/OODA",
    "run-helper",
]
EXPECTED_PROOF_REPO_PREFIX = "/docker/chummercomplete/chummer6-ui-finish"
EXPECTED_NO_REOPEN_POSTURE = {
    "packageAlreadyComplete": True,
    "futureShardAction": "verify_completed_package_proof_floor",
    "reopenOnlyIf": [
        "canonical_successor_registry_reopens_task_103_2",
        "fleet_or_design_queue_row_drops_complete_status",
        "promoted_desktop_head_binding_or_screenshot_evidence_regresses",
    ],
}
PROMOTED_PRIMARY_HEAD = "avalonia"
REQUIRED_PROMOTED_PLATFORMS = [
    "linux",
    "windows",
    "macos",
]
REQUIRED_SURFACE_EVIDENCE = {
    "menu": {
        "source_markers": ["FileMenuButton", "ToolsMenuButton"],
        "capture_markers": [
            'harness.Click("FileMenuButton")',
            'captured[GetVeteranCertificationReviewStep("menu").ScreenshotFileName] = harness.CaptureScreenshotBytes()',
        ],
        "source_file_markers": [
            {"file": "menu", "markers": ["FileMenuButton", "ToolsMenuButton", 'Tag="file"', 'Header="Tools"']},
            {
                "file": "test",
                "markers": [
                    "VeteranCertificationReviewSteps",
                    'GetVeteranCertificationReviewStep("menu").ScreenshotFileName',
                    "Click FileMenuButton and capture MenuCommandsHost",
                    "Chummer5a ChummerMainForm File/Tools/Windows/Help top menu lineage.",
                ],
            },
        ],
        "screenshot": "02-menu-open-light.png",
    },
    "toolstrip": {
        "source_markers": ["Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions"],
        "capture_markers": [
            "harness.SetTheme(ThemeVariant.Light)",
            'captured[GetVeteranCertificationReviewStep("toolstrip").ScreenshotFileName] = harness.CaptureScreenshotBytes()',
        ],
        "control_markers": ["DesktopHomeButton", "ImportFileButton", "SettingsButton", "ImportRawButton"],
        "source_file_markers": [
            {"file": "toolstrip", "markers": ["DesktopHomeButton", "ImportFileButton", "SettingsButton", "ImportRawButton"]},
            {
                "file": "test",
                "markers": [
                    "VeteranCertificationReviewSteps",
                    'GetVeteranCertificationReviewStep("toolstrip").ScreenshotFileName',
                    "Capture initial promoted Avalonia shell after WaitForReady.",
                    "Chummer5a ChummerMainForm toolStrip New/Open/OpenForPrinting/OpenForExport lineage.",
                ],
            },
        ],
        "screenshot": "01-initial-shell-light.png",
    },
    "roster": {
        "source_markers": ["character_roster", "Character Roster"],
        "capture_markers": [
            'harness.Click("ToolsMenuButton")',
            'harness.ClickMenuCommand("character_roster")',
            'harness.Presenter.ExecuteCommandAsync("character_roster"',
            '"Character Roster"',
            "AssertDialogContainsAll(",
            'GetVeteranCertificationReviewStep("roster").RequiredDialogMarkers',
            'captured[GetVeteranCertificationReviewStep("roster").ScreenshotFileName]',
        ],
        "source_file_markers": [
            {
                "file": "test",
                "markers": [
                    "character_roster",
                    "Character Roster",
                    'GetVeteranCertificationReviewStep("roster").ScreenshotFileName',
                    "Execute character_roster and capture the Character Roster dialog.",
                    "Chummer5a CharacterRoster watch-folder utility lineage.",
                ],
            },
        ],
        "screenshot": "17-character-roster-dialog-light.png",
    },
    "master_index": {
        "source_markers": ["master_index", "Master Index"],
        "capture_markers": [
            'harness.Click("ToolsMenuButton")',
            'harness.ClickMenuCommand("master_index")',
            'harness.Presenter.ExecuteCommandAsync("master_index"',
            '"Master Index"',
            "AssertDialogContainsAll(",
            'GetVeteranCertificationReviewStep("master_index").RequiredDialogMarkers',
            'captured[GetVeteranCertificationReviewStep("master_index").ScreenshotFileName]',
        ],
        "source_file_markers": [
            {
                "file": "test",
                "markers": [
                    "master_index",
                    "Master Index",
                    'GetVeteranCertificationReviewStep("master_index").ScreenshotFileName',
                    "Execute master_index and capture the Master Index dialog.",
                    "Chummer5a MasterIndex search utility lineage.",
                ],
            },
        ],
        "screenshot": "16-master-index-dialog-light.png",
    },
    "settings": {
        "source_markers": ["SettingsButton", "Global Settings"],
        "capture_markers": [
            "harness.PressKey(Key.G, RawInputModifiers.Control)",
            "AssertDialogContainsAll(",
            'GetVeteranCertificationReviewStep("settings").RequiredDialogMarkers',
            'captured[GetVeteranCertificationReviewStep("settings").ScreenshotFileName]',
        ],
        "source_file_markers": [
            {"file": "toolstrip", "markers": ["SettingsButton", "SettingsButton_OnClick"]},
            {"file": "event_handlers", "markers": ["global_settings", "open global settings"]},
            {
                "file": "test",
                "markers": [
                    'GetVeteranCertificationReviewStep("settings").ScreenshotFileName',
                    "Press Ctrl+G and capture the Global Settings dialog.",
                    "Chummer5a EditGlobalSettings Global Options lineage.",
                ],
            },
        ],
        "screenshot": "03-settings-open-light.png",
    },
    "import": {
        "source_markers": ["LoadDemoRunnerButton", "open_character"],
        "capture_markers": [
            'harness.Click("LoadDemoRunnerButton")',
            "harness.Presenter.ImportCalls > 0",
            'harness.Click("FileMenuButton")',
            'harness.ClickMenuCommand("open_character")',
            '"Open Character"',
            'captured[GetVeteranCertificationReviewStep("import").ScreenshotFileName]',
        ],
        "control_markers": ["ImportFileButton", "ImportRawButton"],
        "source_file_markers": [
            {"file": "toolstrip", "markers": ["LoadDemoRunnerButton", "ImportFileButton", "ImportRawButton"]},
            {"file": "event_handlers", "markers": ["OpenImportFileAsync", "OpenBundledDemoRunnerAsync", "ImportAsync"]},
            {
                "file": "test",
                "markers": [
                    'GetVeteranCertificationReviewStep("import").ScreenshotFileName',
                    "Click LoadDemoRunnerButton, then open File > Open Character and capture import familiarity.",
                    "Chummer5a File/Open and Hero Lab Importer import route lineage.",
                ],
            },
        ],
        "screenshot": "18-import-dialog-light.png",
    },
}
VETERAN_WORKFLOW_MAP = {
    "menu": {
        "legacyFamiliarity": "Chummer5a top menu roots remain visible as File, Edit, Special, Tools, Windows, and Help.",
        "promotedHeadTaskProof": "Open the promoted Avalonia head and expand a primary menu to reveal command choices.",
        "parityQuestion": "Can a veteran find the same top-level command geography in the first minute?",
        "screenshotEvidenceRole": "menu-open",
    },
    "toolstrip": {
        "legacyFamiliarity": "Classic flat workbench actions remain immediate toolbar buttons instead of dashboard cards.",
        "promotedHeadTaskProof": "Inspect the initial shell and verify load, import, save, settings, support, and close actions stay in the toolstrip.",
        "parityQuestion": "Can a veteran start normal character work from the same always-visible toolbar posture?",
        "screenshotEvidenceRole": "initial-shell",
    },
    "roster": {
        "legacyFamiliarity": "The Character Roster utility is still a named utility surface, not hidden behind campaign-only navigation.",
        "promotedHeadTaskProof": "Open Character Roster from the promoted desktop command surface.",
        "parityQuestion": "Can a veteran find the familiar roster utility without support instructions?",
        "screenshotEvidenceRole": "character-roster-dialog",
    },
    "master_index": {
        "legacyFamiliarity": "The Master Index utility remains a named searchable reference surface.",
        "promotedHeadTaskProof": "Open Master Index from the promoted desktop command surface.",
        "parityQuestion": "Can a veteran reach the familiar index/search utility from desktop chrome?",
        "screenshotEvidenceRole": "master-index-dialog",
    },
    "settings": {
        "legacyFamiliarity": "Global Settings remains a first-minute settings route with source and roster configuration lineage.",
        "promotedHeadTaskProof": "Open Global Settings from the promoted desktop toolstrip/menu surface.",
        "parityQuestion": "Can a veteran find global setup before editing a character?",
        "screenshotEvidenceRole": "settings-dialog",
    },
    "import": {
        "legacyFamiliarity": "Existing .chum5-era import still starts from the desktop shell and exposes the familiar open-character route.",
        "promotedHeadTaskProof": "Load the bundled legacy runner, then open File > Open Character on the promoted desktop head.",
        "parityQuestion": "Can a veteran find the classic import route after landing in the modern workbench?",
        "screenshotEvidenceRole": "import-dialog",
    },
}
MIN_SCREENSHOT_WIDTH = 1280
MIN_SCREENSHOT_HEIGHT = 800
MIN_SCREENSHOT_DISTINCT_SAMPLE_COLORS = 3
LEGACY_BASELINE_EVIDENCE = {
    "menu": {
        "file": Path("Chummer/Forms/ChummerMainForm.Designer.cs"),
        "markers": [
            'this.fileMenu.Text = "&File";',
            'this.toolsMenu.Text = "&Tools";',
            'this.windowsMenu.Text = "&Windows";',
            'this.helpMenu.Text = "&Help";',
        ],
    },
    "toolstrip": {
        "file": Path("Chummer/Forms/ChummerMainForm.Designer.cs"),
        "markers": [
            'this.toolStrip = new System.Windows.Forms.ToolStrip();',
            'this.tsbNewCharacter.Text = "New";',
            'this.tsbOpen.Text = "Open";',
            'this.tsbOpenForPrinting.Text = "Open for P&rinting";',
            'this.tsbOpenForExport.Text = "Open for E&xport";',
        ],
    },
    "roster": {
        "file": Path("Chummer/Forms/Utility Forms/CharacterRoster.Designer.cs"),
        "markers": [
            'this.Text = "Character Roster";',
            'this.treCharacterList.Name = "treCharacterList";',
            'this.lblSettingsLabel.Text = "Settings File:";',
            'this.lblFilePathLabel.Text = "File Name";',
        ],
    },
    "master_index": {
        "file": Path("Chummer/Forms/Utility Forms/MasterIndex.Designer.cs"),
        "markers": [
            'this.Text = "Master Index";',
            'this.txtSearch.Name = "txtSearch";',
            'this.lblSearch.Text = "Search:";',
        ],
    },
    "settings": {
        "file": Path("Chummer/Forms/EditGlobalSettings.Designer.cs"),
        "markers": [
            'this.Text = "Global Settings";',
            'this.tabGlobal.Text = "Global Options";',
            'this.lblDefaultMasterIndexSetting.Text = "Default Setting for Master Index:";',
            'this.lblCharacterRosterLabel.Text = "Character Roster Watch Folder:";',
        ],
    },
    "import": {
        "file": Path("Chummer/Forms/Utility Forms/HeroLabImporter.Designer.cs"),
        "markers": [
            'this.Text = "Hero Lab Importer";',
            'this.cmdSelectFile.Text = "Select POR File";',
            'this.cmdImport.Text = "Import Character";',
            'this.treCharacterList.Name = "treCharacterList";',
        ],
    },
}
LEGACY_IMPORT_ROUTE_BASELINE_EVIDENCE = {
    "file": Path("Chummer/Forms/ChummerMainForm.Designer.cs"),
    "markers": [
        'this.openToolStripMenuItem.Text = "&Open";',
        'this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenFile);',
        'this.mnuOpenForPrinting.Text = "Open for P&rinting";',
        'this.mnuOpenForExport.Text = "Open for E&xport";',
        'this.mnuHeroLabImporter.Text = "&Hero Lab Importer";',
    ],
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path, reasons: list[str]) -> str:
    if not path.is_file():
        reasons.append(f"Missing required file: {path}")
        return ""
    return path.read_text(encoding="utf-8", errors="replace")


def load_json_object(path: Path, reasons: list[str]) -> dict[str, Any]:
    if not path.is_file():
        reasons.append(f"Missing required JSON file: {path}")
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        reasons.append(f"Required JSON file is unreadable: {path}: {exc}")
        return {}
    if not isinstance(payload, dict):
        reasons.append(f"Required JSON file must contain an object: {path}")
        return {}
    return payload


def run_git(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=git_repo_root,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def scalar_text(value: Any) -> str:
    if value is None:
        return ""
    return str(value).strip()


def parse_simple_yaml_items(text: str) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    current: dict[str, Any] | None = None
    active_list_key: str | None = None
    for raw_line in text.splitlines():
        line = raw_line.rstrip()
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        if line.startswith("  - title: "):
            if current is not None:
                items.append(current)
            current = {"title": stripped.split(": ", 1)[1]}
            active_list_key = None
            continue
        if current is None:
            continue
        if stripped.endswith(":") and not stripped.startswith("- "):
            active_list_key = stripped[:-1]
            current.setdefault(active_list_key, [])
            continue
        if active_list_key and stripped.startswith("- "):
            current.setdefault(active_list_key, []).append(stripped[2:].strip())
            continue
        if ": " in stripped:
            key, value = stripped.split(": ", 1)
            active_list_key = None
            if value.isdigit():
                current[key] = int(value)
            else:
                current[key] = value
    if current is not None:
        items.append(current)
    return items


def parse_top_level_scalars(text: str) -> dict[str, str]:
    scalars: dict[str, str] = {}
    for raw_line in text.splitlines():
        if raw_line.startswith(" ") or raw_line.startswith("-"):
            continue
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("#") or ": " not in stripped:
            continue
        key, value = stripped.split(": ", 1)
        scalars[key] = value.strip()
    return scalars


def validate_queue_header(header: dict[str, str], label: str, reasons: list[str]) -> dict[str, bool]:
    checks: dict[str, bool] = {}
    for key, expected_value in EXPECTED_QUEUE_HEADER.items():
        actual_value = header.get(key)
        check_key = f"{key}_matches"
        checks[check_key] = actual_value == expected_value
        if actual_value != expected_value:
            reasons.append(
                f"{label} successor queue header {key} is {actual_value!r}; expected {expected_value!r}."
            )
    return checks


def queue_alignment_checks(
    queue_item: dict[str, Any],
    design_queue_item: dict[str, Any],
    reasons: list[str],
) -> dict[str, bool]:
    comparable_keys = [
        "title",
        "task",
        "package_id",
        "frontier_id",
        "milestone_id",
        "wave",
        "repo",
        "status",
        "landed_commit",
        "completion_action",
        "do_not_reopen_reason",
        "proof",
        "allowed_paths",
        "owned_surfaces",
    ]
    checks: dict[str, bool] = {}
    for key in comparable_keys:
        fleet_value = queue_item.get(key)
        design_value = design_queue_item.get(key)
        check_key = f"{key}_matches_design_queue"
        checks[check_key] = fleet_value == design_value
        if fleet_value != design_value:
            reasons.append(
                "Fleet successor queue row for M103 no longer matches the design-owned queue row "
                f"for {key}: fleet={fleet_value!r}; design={design_value!r}."
            )
    return checks


def proof_item_checks(
    proof_text: str,
    proof_items: list[str],
    label: str,
    reasons: list[str],
) -> dict[str, bool]:
    checks: dict[str, bool] = {}
    for proof_item in proof_items:
        check_key = f"proof_{proof_item}"
        checks[check_key] = proof_item in proof_text
        if proof_item not in proof_text:
            reasons.append(f"{label} proof is missing required M103 closure evidence: {proof_item}.")
    return checks


def extract_registry_milestone_103(text: str) -> dict[str, Any]:
    lines = text.splitlines()
    start = None
    for index, line in enumerate(lines):
        if line.strip() == "- id: 103":
            start = index
            break
    if start is None:
        return {}
    end = len(lines)
    for index in range(start + 1, len(lines)):
        if lines[index].startswith("  - id: ") and lines[index].strip() != "- id: 103":
            end = index
            break
    block = "\n".join(lines[start:end])
    task_start = None
    for index in range(start, end):
        if lines[index].strip() == "- id: 103.2":
            task_start = index
            break
    task_block = ""
    if task_start is not None:
        task_end = end
        for index in range(task_start + 1, end):
            if lines[index].startswith("      - id: ") and lines[index].strip() != "- id: 103.2":
                task_end = index
                break
        task_block = "\n".join(lines[task_start:task_end])
    return {
        "has_milestone": True,
        "status_in_progress": "status: in_progress" in block,
        "wave_w7": "wave: W7" in block,
        "depends_101": "- 101" in block,
        "depends_102": "- 102" in block,
        "has_task_103_2": "id: 103.2" in block and "Run screenshot-backed parity review" in block,
        "task_103_2_status_complete": "status: complete" in task_block,
        "task_103_2_landed_commit_bound": f"landed_commit: {EXPECTED_LANDED_COMMIT}" in task_block,
        "task_103_2_receipt_proof_bound": EXPECTED_PROOF_RECEIPT in task_block,
        "task_103_2_script_proof_bound": EXPECTED_PROOF_SCRIPT in task_block,
        "task_103_2_command_proof_bound": EXPECTED_PROOF_COMMAND in task_block,
    }


def extract_registry_task_103_2_text(text: str) -> str:
    lines = text.splitlines()
    milestone_start = None
    for index, line in enumerate(lines):
        if line.strip() == "- id: 103":
            milestone_start = index
            break
    if milestone_start is None:
        return ""
    milestone_end = len(lines)
    for index in range(milestone_start + 1, len(lines)):
        if lines[index].startswith("  - id: ") and lines[index].strip() != "- id: 103":
            milestone_end = index
            break
    task_start = None
    for index in range(milestone_start, milestone_end):
        if lines[index].strip() == "- id: 103.2":
            task_start = index
            break
    if task_start is None:
        return ""
    task_end = milestone_end
    for index in range(task_start + 1, milestone_end):
        if lines[index].startswith("      - id: ") and lines[index].strip() != "- id: 103.2":
            task_end = index
            break
    return "\n".join(lines[task_start:task_end])


def inspect_png(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {
            "isPng": False,
            "width": 0,
            "height": 0,
            "sha256": None,
            "bitDepth": 0,
            "colorType": None,
            "contentSampled": False,
            "contentSampleCount": 0,
            "contentDistinctSampleColors": 0,
            "contentNonBlank": False,
        }

    data = path.read_bytes()
    png_signature = b"\x89PNG\r\n\x1a\n"
    if len(data) < 33 or not data.startswith(png_signature) or data[12:16] != b"IHDR":
        return {
            "isPng": False,
            "width": 0,
            "height": 0,
            "sha256": hashlib.sha256(data).hexdigest(),
            "bitDepth": 0,
            "colorType": None,
            "contentSampled": False,
            "contentSampleCount": 0,
            "contentDistinctSampleColors": 0,
            "contentNonBlank": False,
        }

    width, height = struct.unpack(">II", data[16:24])
    bit_depth = data[24]
    color_type = data[25]
    content = inspect_png_content(data, width, height, bit_depth, color_type)
    return {
        "isPng": True,
        "width": width,
        "height": height,
        "sha256": hashlib.sha256(data).hexdigest(),
        "bitDepth": bit_depth,
        "colorType": color_type,
        **content,
    }


def inspect_png_content(
    data: bytes,
    width: int,
    height: int,
    bit_depth: int,
    color_type: int,
) -> dict[str, Any]:
    default = {
        "contentSampled": False,
        "contentSampleCount": 0,
        "contentDistinctSampleColors": 0,
        "contentNonBlank": False,
    }
    if bit_depth != 8 or width <= 0 or height <= 0:
        return default

    channels_by_color_type = {
        0: 1,
        2: 3,
        4: 2,
        6: 4,
    }
    channels = channels_by_color_type.get(color_type)
    if channels is None:
        return default

    idat_chunks: list[bytes] = []
    offset = 8
    while offset + 8 <= len(data):
        chunk_length = struct.unpack(">I", data[offset:offset + 4])[0]
        chunk_type = data[offset + 4:offset + 8]
        chunk_data_start = offset + 8
        chunk_data_end = chunk_data_start + chunk_length
        if chunk_data_end + 4 > len(data):
            return default
        if chunk_type == b"IDAT":
            idat_chunks.append(data[chunk_data_start:chunk_data_end])
        if chunk_type == b"IEND":
            break
        offset = chunk_data_end + 4

    if not idat_chunks:
        return default

    try:
        raw = zlib.decompress(b"".join(idat_chunks))
    except zlib.error:
        return default

    bytes_per_pixel = channels
    row_length = width * channels
    expected_length = (row_length + 1) * height
    if len(raw) < expected_length:
        return default

    previous = bytearray(row_length)
    rows: list[bytes] = []
    cursor = 0
    for _ in range(height):
        filter_type = raw[cursor]
        cursor += 1
        row = bytearray(raw[cursor:cursor + row_length])
        cursor += row_length
        for index in range(row_length):
            left = row[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            above = previous[index]
            upper_left = previous[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            if filter_type == 1:
                row[index] = (row[index] + left) & 0xFF
            elif filter_type == 2:
                row[index] = (row[index] + above) & 0xFF
            elif filter_type == 3:
                row[index] = (row[index] + ((left + above) // 2)) & 0xFF
            elif filter_type == 4:
                row[index] = (row[index] + paeth_predictor(left, above, upper_left)) & 0xFF
            elif filter_type != 0:
                return default
        rows.append(bytes(row))
        previous = row

    sample_x = sorted(set([0, width // 4, width // 2, (width * 3) // 4, width - 1]))
    sample_y = sorted(set([0, height // 4, height // 2, (height * 3) // 4, height - 1]))
    sample_colors: set[tuple[int, ...]] = set()
    for y in sample_y:
        row = rows[y]
        for x in sample_x:
            start = x * channels
            sample_colors.add(tuple(row[start:start + channels]))

    return {
        "contentSampled": True,
        "contentSampleCount": len(sample_x) * len(sample_y),
        "contentDistinctSampleColors": len(sample_colors),
        "contentNonBlank": len(sample_colors) >= MIN_SCREENSHOT_DISTINCT_SAMPLE_COLORS,
    }


def paeth_predictor(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    distance_left = abs(estimate - left)
    distance_above = abs(estimate - above)
    distance_upper_left = abs(estimate - upper_left)
    if distance_left <= distance_above and distance_left <= distance_upper_left:
        return left
    if distance_above <= distance_upper_left:
        return above
    return upper_left


def find_disallowed_active_run_tokens(text: str) -> list[str]:
    lowered = text.lower()
    return [token for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS if token.lower() in lowered]


def iter_encoded_decodes(text: str) -> list[str]:
    candidates: list[str] = [text]
    base64_candidates = sorted(set(re.findall(r"[A-Za-z0-9+/=]{16,}", text)))
    for encoded in base64_candidates:
        try:
            raw_decoded = base64.b64decode(encoded, validate=True)
        except (binascii.Error, ValueError):
            continue
        decoded_values = [raw_decoded]
        for decompress in (gzip.decompress, zlib.decompress):
            try:
                decoded_values.append(decompress(raw_decoded))
            except (OSError, zlib.error, EOFError):
                continue
        for decoded in decoded_values:
            rendered = decoded.decode("utf-8", errors="ignore")
            if rendered:
                candidates.append(rendered)
    return candidates


def block_contains_hex_encoded_ci(block: str, needle: str) -> bool:
    lowered_needle = needle.lower()
    for match in re.finditer(r"(?:[0-9a-fA-F]{2}){8,}", block):
        try:
            decoded = bytes.fromhex(match.group(0)).decode("utf-8", errors="ignore")
        except ValueError:
            continue
        if lowered_needle in decoded.lower():
            return True
    return False


def block_contains_escaped_ci(block: str, needle: str) -> bool:
    lowered_needle = needle.lower()

    def decode_escaped(value: str) -> str:
        def replace_hex(match: re.Match[str]) -> str:
            return chr(int(match.group(1), 16))

        value = re.sub(r"\\x([0-9a-fA-F]{2})", replace_hex, value)
        return bytes(value, "utf-8").decode("unicode_escape", errors="ignore")

    candidates = [block, decode_escaped(block)]
    candidates.extend(
        [
            urllib.parse.unquote(candidate)
            for candidate in list(candidates)
        ]
    )
    candidates.extend(
        [
            html.unescape(candidate)
            for candidate in list(candidates)
        ]
    )
    return any(lowered_needle in candidate.lower() for candidate in candidates)


def find_encoded_active_run_tokens(text: str) -> list[str]:
    hits: set[str] = set()
    encoded_decodes = iter_encoded_decodes(text)
    for token in DISALLOWED_ACTIVE_RUN_PROOF_TOKENS:
        lowered_token = token.lower()
        if any(lowered_token in decoded.lower() for decoded in encoded_decodes):
            hits.add(token)
        if block_contains_hex_encoded_ci(text, token):
            hits.add(token)
        if block_contains_escaped_ci(text, token):
            hits.add(token)
    return sorted(hits)


def find_unscoped_proof_path_refs(text: str) -> list[str]:
    refs: list[str] = []
    for token in text.replace("`", " ").replace(",", " ").split():
        normalized = token.strip().rstrip(".):;")
        if not normalized.startswith("/docker/"):
            continue
        if normalized.startswith(EXPECTED_PROOF_REPO_PREFIX):
            continue
        refs.append(normalized)
    return sorted(set(refs))


reasons: list[str] = []
evidence: dict[str, Any] = {
    "packageId": PACKAGE_ID,
    "frontierId": FRONTIER_ID,
    "milestoneId": MILESTONE_ID,
    "wave": WAVE,
    "registryPath": str(registry_path),
    "queuePath": str(queue_path),
    "designQueuePath": str(design_queue_path),
    "releaseChannelPath": str(release_channel_path),
    "sourceRepoRoot": str(source_repo_root),
    "authorityProofRepoRoot": str(authority_repo_root),
    "gitProofRepoRoot": str(git_repo_root),
    "noReopenPosture": EXPECTED_NO_REOPEN_POSTURE,
}

git_evidence: dict[str, Any] = {
    "expectedLandedCommit": EXPECTED_LANDED_COMMIT,
    "checkedAgainstRef": "HEAD",
}
git_landed = run_git(["rev-parse", "--verify", f"{EXPECTED_LANDED_COMMIT}^{{commit}}"])
if git_landed.returncode == 0:
    git_evidence["landedCommitSha"] = git_landed.stdout.strip()
    git_ancestor = run_git(["merge-base", "--is-ancestor", EXPECTED_LANDED_COMMIT, "HEAD"])
    git_evidence["landedCommitIsAncestorOfHead"] = git_ancestor.returncode == 0
    if git_ancestor.returncode != 0:
        reasons.append(
            f"Expected landed commit {EXPECTED_LANDED_COMMIT} is not an ancestor of the current package HEAD."
        )
else:
    reasons.append(f"Expected landed commit {EXPECTED_LANDED_COMMIT} is not present in this package repo.")
    git_evidence["landedCommitError"] = git_landed.stderr.strip()
evidence["gitLandedCommitProof"] = git_evidence

proof_commit_evidence: list[dict[str, Any]] = []
for proof_commit, proof_title in EXPECTED_PROOF_COMMITS.items():
    proof_evidence: dict[str, Any] = {
        "commit": proof_commit,
        "title": proof_title,
        "checkedAgainstRef": "HEAD",
    }
    git_proof = run_git(["rev-parse", "--verify", f"{proof_commit}^{{commit}}"])
    if git_proof.returncode == 0:
        proof_evidence["sha"] = git_proof.stdout.strip()
        git_ancestor = run_git(["merge-base", "--is-ancestor", proof_commit, "HEAD"])
        proof_evidence["isAncestorOfHead"] = git_ancestor.returncode == 0
        if git_ancestor.returncode != 0:
            reasons.append(
                f"Expected M103 proof-closure commit {proof_commit} is not an ancestor of the current package HEAD."
            )
    else:
        proof_evidence["error"] = git_proof.stderr.strip()
        reasons.append(f"Expected M103 proof-closure commit {proof_commit} is not present in this package repo.")
    proof_commit_evidence.append(proof_evidence)
evidence["gitProofClosureCommits"] = proof_commit_evidence

registry_text = read_text(registry_path, reasons)
queue_text = read_text(queue_path, reasons)
design_queue_text = read_text(design_queue_path, reasons)
registry_task_103_2_text = extract_registry_task_103_2_text(registry_text)
release_channel = load_json_object(release_channel_path, reasons)
test_text = read_text(test_path, reasons)
compliance_guard_text = read_text(compliance_guard_path, reasons)
verify_script_text = read_text(verify_script_path, reasons)
toolstrip_text = read_text(toolstrip_path, reasons)
menu_text = read_text(menu_path, reasons)
event_handlers_text = read_text(event_handlers_path, reasons)
surface_source_texts = {
    "test": test_text,
    "toolstrip": toolstrip_text,
    "menu": menu_text,
    "event_handlers": event_handlers_text,
}
surface_source_paths = {
    "test": str(test_path),
    "toolstrip": str(toolstrip_path),
    "menu": str(menu_path),
    "event_handlers": str(event_handlers_path),
}

release_channel_status = scalar_text(release_channel.get("status")).lower()
release_channel_rollout_state = scalar_text(release_channel.get("rolloutState")).lower()
release_channel_id = scalar_text(release_channel.get("channelId") or release_channel.get("channel"))
release_channel_version = scalar_text(release_channel.get("version") or release_channel.get("releaseVersion"))
desktop_tuple_coverage = release_channel.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    desktop_tuple_coverage = {}
    if release_channel:
        reasons.append("Release channel is missing desktopTupleCoverage for promoted-head parity binding.")

required_heads = desktop_tuple_coverage.get("requiredDesktopHeads")
if not isinstance(required_heads, list) or PROMOTED_PRIMARY_HEAD not in [scalar_text(head) for head in required_heads]:
    reasons.append(
        f"Release channel requiredDesktopHeads does not name the promoted primary head '{PROMOTED_PRIMARY_HEAD}'."
    )

promoted_platform_heads = desktop_tuple_coverage.get("promotedPlatformHeads")
if not isinstance(promoted_platform_heads, dict):
    promoted_platform_heads = {}
    if release_channel:
        reasons.append("Release channel is missing promotedPlatformHeads mapping for M103 promoted-head parity proof.")

missing_promoted_platforms: list[str] = []
for platform in REQUIRED_PROMOTED_PLATFORMS:
    heads = promoted_platform_heads.get(platform)
    normalized_heads = [scalar_text(head) for head in heads] if isinstance(heads, list) else []
    if PROMOTED_PRIMARY_HEAD not in normalized_heads:
        missing_promoted_platforms.append(platform)
if missing_promoted_platforms:
    reasons.append(
        f"Release channel does not promote '{PROMOTED_PRIMARY_HEAD}' on required platform(s): "
        + ", ".join(missing_promoted_platforms)
    )

desktop_route_truth = desktop_tuple_coverage.get("desktopRouteTruth")
if not isinstance(desktop_route_truth, list):
    desktop_route_truth = []
    if release_channel:
        reasons.append("Release channel is missing desktopRouteTruth rows for promoted-head parity proof.")

primary_route_rows: dict[str, dict[str, Any]] = {}
for row in desktop_route_truth:
    if not isinstance(row, dict):
        continue
    if scalar_text(row.get("head")) != PROMOTED_PRIMARY_HEAD:
        continue
    platform = scalar_text(row.get("platform")).lower()
    if platform in REQUIRED_PROMOTED_PLATFORMS:
        primary_route_rows[platform] = row

for platform in REQUIRED_PROMOTED_PLATFORMS:
    row = primary_route_rows.get(platform)
    if not row:
        reasons.append(
            f"Release channel desktopRouteTruth is missing promoted primary route row for {PROMOTED_PRIMARY_HEAD}:{platform}."
        )
        continue
    if scalar_text(row.get("routeRole")).lower() != "primary":
        reasons.append(
            f"Release channel desktopRouteTruth row for {PROMOTED_PRIMARY_HEAD}:{platform} is not routeRole=primary."
        )
    if scalar_text(row.get("promotionState")).lower() != "promoted":
        reasons.append(
            f"Release channel desktopRouteTruth row for {PROMOTED_PRIMARY_HEAD}:{platform} is not promotionState=promoted."
        )
    if scalar_text(row.get("parityPosture")).lower() != "flagship_primary":
        reasons.append(
            f"Release channel desktopRouteTruth row for {PROMOTED_PRIMARY_HEAD}:{platform} is not parityPosture=flagship_primary."
        )
    expected_tuple_id = f"{PROMOTED_PRIMARY_HEAD}:{platform}:"
    tuple_id = scalar_text(row.get("tupleId"))
    artifact_id = scalar_text(row.get("artifactId"))
    public_install_route = scalar_text(row.get("publicInstallRoute"))
    if not tuple_id.startswith(expected_tuple_id):
        reasons.append(
            "Release channel desktopRouteTruth row for "
            f"{PROMOTED_PRIMARY_HEAD}:{platform} has tupleId {tuple_id!r}; expected prefix {expected_tuple_id!r}."
        )
    if not artifact_id:
        reasons.append(
            f"Release channel desktopRouteTruth row for {PROMOTED_PRIMARY_HEAD}:{platform} is missing artifactId."
        )
    if not public_install_route.startswith("/downloads/install/"):
        reasons.append(
            "Release channel desktopRouteTruth row for "
            f"{PROMOTED_PRIMARY_HEAD}:{platform} has publicInstallRoute {public_install_route!r}; "
            "expected a /downloads/install/ route."
        )

promoted_installer_tuples = desktop_tuple_coverage.get("promotedInstallerTuples")
if not isinstance(promoted_installer_tuples, list):
    promoted_installer_tuples = []
    if release_channel:
        reasons.append("Release channel is missing promotedInstallerTuples for promoted-head parity proof.")

promoted_tuple_platforms = {
    scalar_text(row.get("platform")).lower()
    for row in promoted_installer_tuples
    if isinstance(row, dict) and scalar_text(row.get("head")) == PROMOTED_PRIMARY_HEAD
}
missing_installer_platforms = [
    platform for platform in REQUIRED_PROMOTED_PLATFORMS if platform not in promoted_tuple_platforms
]
if missing_installer_platforms:
    reasons.append(
        f"Release channel does not publish promoted installer tuples for '{PROMOTED_PRIMARY_HEAD}' on: "
        + ", ".join(missing_installer_platforms)
    )

if release_channel_status != "published":
    reasons.append(f"Release channel status is {release_channel_status or 'missing'}; expected published.")
if release_channel_rollout_state not in {"promoted_preview", "release_candidate", "public_stable"}:
    reasons.append(
        "Release channel rolloutState is "
        f"{release_channel_rollout_state or 'missing'}; expected promoted_preview, release_candidate, or public_stable."
    )

evidence["promotedDesktopHeadBinding"] = {
    "releaseChannelPath": str(release_channel_path),
    "channelId": release_channel_id,
    "version": release_channel_version,
    "status": release_channel_status,
    "rolloutState": release_channel_rollout_state,
    "primaryHead": PROMOTED_PRIMARY_HEAD,
    "requiredPlatforms": REQUIRED_PROMOTED_PLATFORMS,
    "requiredDesktopHeads": required_heads if isinstance(required_heads, list) else [],
    "promotedPlatformHeads": promoted_platform_heads,
    "promotedInstallerTuplePlatforms": sorted(promoted_tuple_platforms),
    "primaryRouteTruth": {
        platform: {
            "tupleId": scalar_text(row.get("tupleId")),
            "artifactId": scalar_text(row.get("artifactId")),
            "routeRole": scalar_text(row.get("routeRole")),
            "promotionState": scalar_text(row.get("promotionState")),
            "parityPosture": scalar_text(row.get("parityPosture")),
            "publicInstallRoute": scalar_text(row.get("publicInstallRoute")),
            "tupleIdMatchesPromotedHeadAndPlatform": scalar_text(row.get("tupleId")).startswith(
                f"{PROMOTED_PRIMARY_HEAD}:{platform}:"
            ),
            "artifactIdPresent": bool(scalar_text(row.get("artifactId"))),
            "publicInstallRouteIsDownloadRoute": scalar_text(row.get("publicInstallRoute")).startswith(
                "/downloads/install/"
            ),
        }
        for platform, row in sorted(primary_route_rows.items())
    },
}

legacy_baseline_results: dict[str, dict[str, Any]] = {}
legacy_import_route_baseline_results: dict[str, Any] = {}
workflow_map_surfaces = set(VETERAN_WORKFLOW_MAP)
required_surface_names = set(REQUIRED_SURFACE_EVIDENCE)
if workflow_map_surfaces != required_surface_names:
    reasons.append(
        "Veteran workflow map surfaces drifted from required screenshot surfaces: "
        f"missing={sorted(required_surface_names - workflow_map_surfaces)}, "
        f"extra={sorted(workflow_map_surfaces - required_surface_names)}."
    )

for surface, checks in LEGACY_BASELINE_EVIDENCE.items():
    baseline_file = legacy_chummer5a_root / checks["file"]
    baseline_reasons: list[str] = []
    baseline_text = read_text(baseline_file, baseline_reasons)
    missing_markers = [
        marker
        for marker in checks["markers"]
        if marker not in baseline_text
    ]
    if baseline_reasons:
        reasons.extend(baseline_reasons)
    if missing_markers:
        reasons.append(
            f"{surface} Chummer5a baseline is missing markers in {baseline_file}: "
            + ", ".join(missing_markers)
        )
    legacy_baseline_results[surface] = {
        "sourceFile": str(baseline_file),
        "missingMarkers": missing_markers,
        "markerCount": len(checks["markers"]),
    }

legacy_import_route_file = legacy_chummer5a_root / LEGACY_IMPORT_ROUTE_BASELINE_EVIDENCE["file"]
legacy_import_route_reasons: list[str] = []
legacy_import_route_text = read_text(legacy_import_route_file, legacy_import_route_reasons)
legacy_import_route_missing_markers = [
    marker
    for marker in LEGACY_IMPORT_ROUTE_BASELINE_EVIDENCE["markers"]
    if marker not in legacy_import_route_text
]
if legacy_import_route_reasons:
    reasons.extend(legacy_import_route_reasons)
if legacy_import_route_missing_markers:
    reasons.append(
        "import Chummer5a route baseline is missing file-open/import lineage markers in "
        f"{legacy_import_route_file}: " + ", ".join(legacy_import_route_missing_markers)
    )
legacy_import_route_baseline_results = {
    "sourceFile": str(legacy_import_route_file),
    "missingMarkers": legacy_import_route_missing_markers,
    "markerCount": len(LEGACY_IMPORT_ROUTE_BASELINE_EVIDENCE["markers"]),
}

registry_milestone = extract_registry_milestone_103(registry_text)
evidence["registryMilestone103"] = registry_milestone
if not registry_milestone:
    reasons.append("Successor registry is missing milestone 103.")
else:
    for key, ok in registry_milestone.items():
        if key != "has_milestone" and ok is not True:
            reasons.append(f"Successor registry milestone 103 failed check: {key}.")
evidence["registryProofItemChecks"] = proof_item_checks(
    registry_task_103_2_text,
    [
        EXPECTED_PROOF_RECEIPT,
        EXPECTED_PROOF_SCRIPT,
        EXPECTED_PROOF_COMMAND,
        *EXPECTED_PROOF_COMMIT_ITEMS,
    ],
    "Successor registry task 103.2",
    reasons,
)

queue_items = parse_simple_yaml_items(queue_text)
queue_top_level = parse_top_level_scalars(queue_text)
evidence["queueTopLevel"] = queue_top_level
evidence["queueHeaderChecks"] = validate_queue_header(queue_top_level, "Fleet", reasons)

fleet_source_design_queue_path = queue_top_level.get("source_design_queue_path")
if fleet_source_design_queue_path != EXPECTED_DESIGN_QUEUE_PATH:
    reasons.append(
        "Fleet successor queue staging no longer points at the design-owned queue staging source: "
        f"{fleet_source_design_queue_path!r}."
    )
design_queue_items = parse_simple_yaml_items(design_queue_text)
design_queue_top_level = parse_top_level_scalars(design_queue_text)
evidence["designQueueTopLevel"] = design_queue_top_level
evidence["designQueueHeaderChecks"] = validate_queue_header(design_queue_top_level, "Design", reasons)
matching = [item for item in queue_items if item.get("package_id") == PACKAGE_ID]
if len(matching) != 1:
    reasons.append(f"Expected exactly one queue item for {PACKAGE_ID}; found {len(matching)}.")
    queue_item: dict[str, Any] = {}
else:
    queue_item = matching[0]
evidence["queueItem"] = queue_item

design_matching = [item for item in design_queue_items if item.get("package_id") == PACKAGE_ID]
if len(design_matching) != 1:
    reasons.append(f"Expected exactly one design queue item for {PACKAGE_ID}; found {len(design_matching)}.")
    design_queue_item: dict[str, Any] = {}
else:
    design_queue_item = design_matching[0]
evidence["designQueueItem"] = design_queue_item

if queue_item:
    expected = {
        "frontier_id": FRONTIER_ID,
        "milestone_id": MILESTONE_ID,
        "wave": WAVE,
        "repo": "chummer6-ui",
        "status": "complete",
        "landed_commit": EXPECTED_LANDED_COMMIT,
        "completion_action": EXPECTED_COMPLETION_ACTION,
        "do_not_reopen_reason": EXPECTED_DO_NOT_REOPEN_REASON,
        "title": "Run screenshot-backed parity review on the promoted desktop head",
    }
    for key, expected_value in expected.items():
        actual = queue_item.get(key)
        if actual != expected_value:
            reasons.append(f"Queue item {key} is {actual!r}; expected {expected_value!r}.")
    if queue_item.get("allowed_paths") != EXPECTED_ALLOWED_PATHS:
        reasons.append(
            "Queue item allowed_paths drifted: "
            + json.dumps(queue_item.get("allowed_paths"), sort_keys=True)
        )
    if queue_item.get("owned_surfaces") != EXPECTED_SURFACES:
        reasons.append(
            "Queue item owned_surfaces drifted: "
            + json.dumps(queue_item.get("owned_surfaces"), sort_keys=True)
        )
    queue_proof = queue_item.get("proof")
    if not isinstance(queue_proof, list):
        reasons.append("Queue item proof is missing or not a list.")
        queue_proof = []
    for proof_item in [
        EXPECTED_PROOF_RECEIPT,
        EXPECTED_PROOF_SCRIPT,
        EXPECTED_PROOF_GUARD,
        EXPECTED_PROOF_COMMAND,
        *EXPECTED_PROOF_COMMIT_ITEMS,
    ]:
        if proof_item not in queue_proof:
            reasons.append(f"Queue item proof is missing required M103 closure evidence: {proof_item}.")

if design_queue_item:
    design_expected = {
        "frontier_id": FRONTIER_ID,
        "milestone_id": MILESTONE_ID,
        "wave": WAVE,
        "repo": "chummer6-ui",
        "status": "complete",
        "landed_commit": EXPECTED_LANDED_COMMIT,
        "completion_action": EXPECTED_COMPLETION_ACTION,
        "do_not_reopen_reason": EXPECTED_DO_NOT_REOPEN_REASON,
        "title": "Run screenshot-backed parity review on the promoted desktop head",
    }
    for key, expected_value in design_expected.items():
        actual = design_queue_item.get(key)
        if actual != expected_value:
            reasons.append(f"Design queue item {key} is {actual!r}; expected {expected_value!r}.")
    if design_queue_item.get("allowed_paths") != EXPECTED_ALLOWED_PATHS:
        reasons.append(
            "Design queue item allowed_paths drifted: "
            + json.dumps(design_queue_item.get("allowed_paths"), sort_keys=True)
        )
    if design_queue_item.get("owned_surfaces") != EXPECTED_SURFACES:
        reasons.append(
            "Design queue item owned_surfaces drifted: "
            + json.dumps(design_queue_item.get("owned_surfaces"), sort_keys=True)
        )
    design_queue_proof = design_queue_item.get("proof")
    if not isinstance(design_queue_proof, list):
        reasons.append("Design queue item proof is missing or not a list.")
        design_queue_proof = []
    for proof_item in [
        EXPECTED_PROOF_RECEIPT,
        EXPECTED_PROOF_SCRIPT,
        EXPECTED_PROOF_GUARD,
        EXPECTED_PROOF_COMMAND,
        *EXPECTED_PROOF_COMMIT_ITEMS,
    ]:
        if proof_item not in design_queue_proof:
            reasons.append(f"Design queue item proof is missing required M103 closure evidence: {proof_item}.")

if queue_item and design_queue_item:
    evidence["queueMirrorAlignmentChecks"] = queue_alignment_checks(queue_item, design_queue_item, reasons)
else:
    evidence["queueMirrorAlignmentChecks"] = {}

required_proof_text = "\n".join(
    [
        EXPECTED_PROOF_RECEIPT,
        EXPECTED_PROOF_SCRIPT,
        EXPECTED_PROOF_GUARD,
        EXPECTED_PROOF_COMMAND,
        *EXPECTED_PROOF_COMMIT_ITEMS,
    ]
)
queue_proof_text = "\n".join(str(item) for item in (queue_item.get("proof") if queue_item else []) or [])
design_queue_proof_text = "\n".join(
    str(item) for item in (design_queue_item.get("proof") if design_queue_item else []) or []
)
operator_helper_token_hits = {
    "required_proof": find_disallowed_active_run_tokens(required_proof_text),
    "registry_evidence": find_disallowed_active_run_tokens(registry_task_103_2_text),
    "queue_evidence": find_disallowed_active_run_tokens(queue_proof_text),
    "design_queue_evidence": find_disallowed_active_run_tokens(design_queue_proof_text),
}
encoded_operator_helper_token_hits = {
    "required_proof": find_encoded_active_run_tokens(required_proof_text),
    "registry_evidence": find_encoded_active_run_tokens(registry_task_103_2_text),
    "queue_evidence": find_encoded_active_run_tokens(queue_proof_text),
    "design_queue_evidence": find_encoded_active_run_tokens(design_queue_proof_text),
}
operator_helper_proof_checks = {
    "required_proof_avoids_active_run_helpers": not operator_helper_token_hits["required_proof"],
    "registry_evidence_avoids_active_run_helpers": not operator_helper_token_hits["registry_evidence"],
    "queue_evidence_avoids_active_run_helpers": not operator_helper_token_hits["queue_evidence"],
    "design_queue_evidence_avoids_active_run_helpers": not operator_helper_token_hits["design_queue_evidence"],
    "required_proof_avoids_encoded_active_run_helpers": not encoded_operator_helper_token_hits["required_proof"],
    "registry_evidence_avoids_encoded_active_run_helpers": not encoded_operator_helper_token_hits["registry_evidence"],
    "queue_evidence_avoids_encoded_active_run_helpers": not encoded_operator_helper_token_hits["queue_evidence"],
    "design_queue_evidence_avoids_encoded_active_run_helpers": not encoded_operator_helper_token_hits["design_queue_evidence"],
}
for check_name, passed in operator_helper_proof_checks.items():
    if not passed:
        reasons.append(f"operator helper proof check failed: {check_name}")

standard_verify_encoded_blocked_active_run_helper_hits = find_encoded_active_run_tokens(verify_script_text)
standard_verify_checks = {
    "verify_entrypoint_avoids_encoded_active_run_helpers": not standard_verify_encoded_blocked_active_run_helper_hits,
}
for check_name, passed in standard_verify_checks.items():
    if not passed:
        reasons.append(f"M103 standard verify check failed: {check_name}.")

proof_scope_path_hits = {
    "required_proof": find_unscoped_proof_path_refs(required_proof_text),
    "registry_evidence": find_unscoped_proof_path_refs(registry_task_103_2_text),
    "queue_evidence": find_unscoped_proof_path_refs(queue_proof_text),
    "design_queue_evidence": find_unscoped_proof_path_refs(design_queue_proof_text),
}
proof_scope_checks = {
    f"{name}_stays_in_package_repo": not refs
    for name, refs in proof_scope_path_hits.items()
}
for check_name, passed in proof_scope_checks.items():
    if passed:
        continue
    source_name = check_name.removesuffix("_stays_in_package_repo")
    reasons.append(
        "M103 scoped proof check failed: "
        f"{check_name} cites non-package path(s): {', '.join(proof_scope_path_hits[source_name])}."
    )

surface_results: dict[str, dict[str, Any]] = {}
veteran_certification_matrix: list[dict[str, Any]] = []
screenshot_hashes: dict[str, list[str]] = {}
for surface, checks in REQUIRED_SURFACE_EVIDENCE.items():
    missing_markers: list[str] = []
    for marker in checks.get("source_markers", []):
        if marker not in test_text:
            missing_markers.append(marker)
    for marker in checks.get("control_markers", []):
        if marker not in toolstrip_text:
            missing_markers.append(marker)
    source_file_results: list[dict[str, Any]] = []
    for source_check in checks.get("source_file_markers", []):
        source_key = scalar_text(source_check.get("file"))
        source_text = surface_source_texts.get(source_key, "")
        source_missing = [
            marker
            for marker in source_check.get("markers", [])
            if marker not in source_text
        ]
        if source_key not in surface_source_texts:
            source_missing.append(f"unknown source file key: {source_key}")
        if source_missing:
            reasons.append(
                f"{surface} source file proof is missing markers in {source_key}: "
                + ", ".join(source_missing)
            )
        source_file_results.append(
            {
                "sourceKey": source_key,
                "sourceFile": surface_source_paths.get(source_key, ""),
                "missingMarkers": source_missing,
                "markerCount": len(source_check.get("markers", [])),
            }
        )
    screenshot_name = str(checks["screenshot"])
    screenshot_file = screenshot_dir / screenshot_name
    screenshot_in_generator = screenshot_name in test_text
    screenshot_exists = screenshot_file.is_file()
    screenshot_non_empty = screenshot_exists and screenshot_file.stat().st_size > 0
    screenshot_metadata = inspect_png(screenshot_file)
    if not screenshot_in_generator:
        reasons.append(f"{surface} screenshot is not generated by the Avalonia flagship gate: {screenshot_name}.")
    if not screenshot_exists:
        reasons.append(f"{surface} published screenshot is missing: {screenshot_file}.")
    if screenshot_exists and not screenshot_non_empty:
        reasons.append(f"{surface} published screenshot exists but is empty: {screenshot_file}.")
    if screenshot_exists and not screenshot_metadata["isPng"]:
        reasons.append(f"{surface} published screenshot is not a valid PNG: {screenshot_file}.")
    if screenshot_metadata["isPng"] and (
        screenshot_metadata["width"] < MIN_SCREENSHOT_WIDTH
        or screenshot_metadata["height"] < MIN_SCREENSHOT_HEIGHT
    ):
        reasons.append(
            f"{surface} screenshot is undersized: "
            f"{screenshot_metadata['width']}x{screenshot_metadata['height']} "
            f"(minimum {MIN_SCREENSHOT_WIDTH}x{MIN_SCREENSHOT_HEIGHT})."
        )
    if screenshot_metadata["isPng"] and not screenshot_metadata["contentNonBlank"]:
        reasons.append(
            f"{surface} screenshot content sample is blank or too flat: "
            f"{screenshot_metadata['contentDistinctSampleColors']} distinct sampled color(s) "
            f"(minimum {MIN_SCREENSHOT_DISTINCT_SAMPLE_COLORS})."
        )
    screenshot_hash = screenshot_metadata.get("sha256")
    if isinstance(screenshot_hash, str):
        screenshot_hashes.setdefault(screenshot_hash, []).append(surface)
    if missing_markers:
        reasons.append(f"{surface} source proof is missing markers: {', '.join(missing_markers)}.")
    missing_capture_markers = [
        marker for marker in checks.get("capture_markers", [])
        if marker not in test_text
    ]
    if missing_capture_markers:
        reasons.append(
            f"{surface} screenshot capture proof is missing interaction markers: "
            + ", ".join(missing_capture_markers)
            + "."
        )
    legacy_baseline = legacy_baseline_results.get(surface, {})
    matrix_row = {
        "surface": surface,
        "ownedSurface": "screenshot_parity:desktop",
        "promotedHead": PROMOTED_PRIMARY_HEAD,
        "screenshot": screenshot_name,
        "gesture": VETERAN_WORKFLOW_MAP.get(surface, {}).get("promotedHeadTaskProof", ""),
        "chummer5aBaseline": VETERAN_WORKFLOW_MAP.get(surface, {}).get("legacyFamiliarity", ""),
        "chummer5aBaselineFile": legacy_baseline.get("sourceFile", ""),
        "chummer5aMarkerCount": legacy_baseline.get("markerCount", 0),
        "missingChummer5aMarkers": legacy_baseline.get("missingMarkers", []),
        "sourceFileProofCount": len(source_file_results),
        "captureMarkerCount": len(checks.get("capture_markers", [])),
        "screenshotSha256": screenshot_hash,
        "screenshotContentNonBlank": screenshot_metadata["contentNonBlank"],
        "screenshotDistinctSampleColors": screenshot_metadata["contentDistinctSampleColors"],
    }
    matrix_missing = []
    for field in [
        "surface",
        "ownedSurface",
        "promotedHead",
        "screenshot",
        "gesture",
        "chummer5aBaseline",
        "chummer5aBaselineFile",
        "screenshotSha256",
    ]:
        if not matrix_row.get(field):
            matrix_missing.append(field)
    if matrix_row["chummer5aMarkerCount"] <= 0:
        matrix_missing.append("chummer5aMarkerCount")
    if matrix_row["sourceFileProofCount"] <= 0:
        matrix_missing.append("sourceFileProofCount")
    if matrix_row["captureMarkerCount"] <= 0:
        matrix_missing.append("captureMarkerCount")
    if matrix_missing:
        reasons.append(
            f"{surface} veteran certification matrix row is incomplete: "
            + ", ".join(matrix_missing)
            + "."
        )
    veteran_certification_matrix.append(matrix_row)
    surface_results[surface] = {
        "screenshot": screenshot_name,
        "workflowMap": VETERAN_WORKFLOW_MAP.get(surface, {}),
        "certificationMatrixRow": matrix_row,
        "captureMarkerChecks": {
            "requiredMarkers": list(checks.get("capture_markers", [])),
            "missingMarkers": missing_capture_markers,
        },
        "screenshotGeneratedByTest": screenshot_in_generator,
        "publishedScreenshotExists": screenshot_exists,
        "publishedScreenshotNonEmpty": screenshot_non_empty,
        "publishedScreenshotIsPng": screenshot_metadata["isPng"],
        "publishedScreenshotWidth": screenshot_metadata["width"],
        "publishedScreenshotHeight": screenshot_metadata["height"],
        "publishedScreenshotSha256": screenshot_metadata["sha256"],
        "publishedScreenshotContentSampled": screenshot_metadata["contentSampled"],
        "publishedScreenshotContentSampleCount": screenshot_metadata["contentSampleCount"],
        "publishedScreenshotDistinctSampleColors": screenshot_metadata["contentDistinctSampleColors"],
        "publishedScreenshotContentNonBlank": screenshot_metadata["contentNonBlank"],
        "missingMarkers": missing_markers,
        "sourceFileChecks": source_file_results,
    }

for screenshot_hash, surfaces in screenshot_hashes.items():
    if len(surfaces) > 1:
        reasons.append(
            "Veteran certification screenshots must be surface-distinct; duplicate hash "
            f"{screenshot_hash} covers: {', '.join(sorted(surfaces))}."
        )

if "FileMenuButton" not in menu_text:
    reasons.append("Avalonia shell menu source no longer exposes FileMenuButton.")

standard_verify_markers = [
    EXPECTED_VERIFY_BANNER,
    EXPECTED_PROOF_COMMAND,
]
missing_standard_verify_markers = [
    marker for marker in standard_verify_markers if marker not in verify_script_text
]
if missing_standard_verify_markers:
    reasons.append(
        "M103 veteran certification guard is not wired into the standard AI verify path: "
        + ", ".join(missing_standard_verify_markers)
        + "."
    )

required_compliance_guard_markers = [
    "Next90M103VeteranCertificationGuardTests",
    "M103_veteran_certification_guard_pins_completed_queue_proof",
    "M103_veteran_certification_receipt_keeps_surface_distinct_screenshot_proof",
    "next90-m103-ui-veteran-certification",
    "EXPECTED_NO_REOPEN_POSTURE",
    "noReopenPosture",
    "EXPECTED_PROOF_GUARD",
    "promotedDesktopHeadBinding",
    "standardVerifyPath",
    "designQueueItem",
    "designQueueTopLevel",
    "queueHeaderChecks",
    "designQueueHeaderChecks",
    "registryProofItemChecks",
    "queueMirrorAlignmentChecks",
    "legacyBaselineResults",
    "legacyImportRouteBaselineResults",
    "surfaceResults",
    "sourceFileChecks",
    "captureMarkerChecks",
    "eventHandlersSourceFile",
    "DISALLOWED_ACTIVE_RUN_PROOF_TOKENS",
    "operatorHelperProofChecks",
    "proofScopeChecks",
    "proofScopePathHits",
    "veteranCertificationMatrix",
    "certificationMatrixRow",
    "chummer5aBaselineFile",
    "screenshotContentNonBlank",
    "MIN_SCREENSHOT_DISTINCT_SAMPLE_COLORS",
    "publishedScreenshotDistinctSampleColors",
    "publishedScreenshotContentNonBlank",
    "AssertDialogContainsAll",
    "Open Character",
    "Click LoadDemoRunnerButton, then open File > Open Character and capture import familiarity.",
    "18-import-dialog-light.png",
    "operator helper proof check failed",
    "M103 scoped proof check failed",
]
missing_compliance_guard_markers = [
    marker for marker in required_compliance_guard_markers if marker not in compliance_guard_text
]
if missing_compliance_guard_markers:
    reasons.append(
        "M103 veteran certification compliance guard is missing required markers: "
        + ", ".join(missing_compliance_guard_markers)
        + "."
    )

evidence["surfaceResults"] = surface_results
evidence["veteranCertificationMatrix"] = veteran_certification_matrix
evidence["veteranWorkflowMap"] = VETERAN_WORKFLOW_MAP
evidence["legacyChummer5aRoot"] = str(legacy_chummer5a_root)
evidence["legacyBaselineResults"] = legacy_baseline_results
evidence["legacyImportRouteBaselineResults"] = legacy_import_route_baseline_results
evidence["sourceTestFile"] = str(test_path)
evidence["complianceGuardFile"] = str(compliance_guard_path)
evidence["complianceGuardMarkers"] = {
    "required": required_compliance_guard_markers,
    "missing": missing_compliance_guard_markers,
}
evidence["standardVerifyPath"] = {
    "verifyScriptFile": str(verify_script_path),
    "requiredMarkers": standard_verify_markers,
    "missingMarkers": missing_standard_verify_markers,
}
evidence["standardVerifyChecks"] = standard_verify_checks
evidence["operatorHelperProofChecks"] = operator_helper_proof_checks
evidence["disallowedActiveRunProofTokens"] = DISALLOWED_ACTIVE_RUN_PROOF_TOKENS
evidence["operatorHelperTokenHits"] = operator_helper_token_hits
evidence["encodedOperatorHelperTokenHits"] = encoded_operator_helper_token_hits
evidence["standardVerifyEncodedBlockedActiveRunHelperHits"] = standard_verify_encoded_blocked_active_run_helper_hits
evidence["expectedProofRepoPrefix"] = EXPECTED_PROOF_REPO_PREFIX
evidence["proofScopeChecks"] = proof_scope_checks
evidence["proofScopePathHits"] = proof_scope_path_hits
evidence["toolstripSourceFile"] = str(toolstrip_path)
evidence["menuSourceFile"] = str(menu_path)
evidence["eventHandlersSourceFile"] = str(event_handlers_path)
evidence["publishedScreenshotDir"] = str(screenshot_dir)
evidence["publishedScreenshotReviewMarkdownPath"] = str(screenshot_review_markdown_path)

review_rows: list[str] = [
    "# Next90 M103 Veteran Certification Review",
    "",
    f"Receipt: `{receipt_path}`",
    f"Screenshot pack: `{screenshot_dir}`",
    f"Source repo: `{source_repo_root}`",
    f"Authority proof repo: `{authority_repo_root}`",
    "",
    "| Surface | Parity Question | Promoted-Head Proof | Legacy Familiarity | Screenshot | Sample Colors | SHA-256 |",
    "| --- | --- | --- | --- | --- | ---: | --- |",
]
for row in veteran_certification_matrix:
    review_rows.append(
        "| {surface} | {parityQuestion} | {gesture} | {legacy} | {screenshot} | {colors} | {sha} |".format(
            surface=row["surface"],
            parityQuestion=VETERAN_WORKFLOW_MAP.get(row["surface"], {}).get("parityQuestion", ""),
            gesture=row["gesture"],
            legacy=row["chummer5aBaseline"],
            screenshot=row["screenshot"],
            colors=row["screenshotDistinctSampleColors"],
            sha=row["screenshotSha256"],
        )
    )
review_rows.extend(
    [
        "",
        "## Screenshots",
        "",
    ]
)
for row in veteran_certification_matrix:
    review_rows.append(
        f"- `{row['surface']}`: `{screenshot_dir / row['screenshot']}`"
    )
review_markdown_text = "\n".join(review_rows) + "\n"
screenshot_review_markdown_path.write_text(review_markdown_text, encoding="utf-8")

review_markdown_checks = {
    "header_present": "# Next90 M103 Veteran Certification Review" in review_markdown_text,
    "receipt_path_bound": f"Receipt: `{receipt_path}`" in review_markdown_text,
    "screenshot_pack_bound": f"Screenshot pack: `{screenshot_dir}`" in review_markdown_text,
    "source_repo_bound": f"Source repo: `{source_repo_root}`" in review_markdown_text,
    "authority_repo_bound": f"Authority proof repo: `{authority_repo_root}`" in review_markdown_text,
    "table_header_present": "| Surface | Parity Question | Promoted-Head Proof | Legacy Familiarity | Screenshot | Sample Colors | SHA-256 |" in review_markdown_text,
    "surface_row_count_matches": sum(
        1 for line in review_markdown_text.splitlines() if line.startswith("| ") and not line.startswith("| Surface ") and not line.startswith("| --- ")
    ) == len(veteran_certification_matrix),
    "surface_rows_present": all(
        f"| {row['surface']} |" in review_markdown_text and row["screenshot"] in review_markdown_text
        for row in veteran_certification_matrix
    ),
    "screenshot_paths_present": all(
        f"- `{row['surface']}`: `{screenshot_dir / row['screenshot']}`" in review_markdown_text
        for row in veteran_certification_matrix
    ),
}
for key, passed in review_markdown_checks.items():
    if not passed:
        reasons.append(f"M103 review markdown check failed: {key}.")
evidence["reviewMarkdownChecks"] = review_markdown_checks

payload = {
    "contract_name": "chummer6-ui.next90_m103_ui_veteran_certification",
    "packageId": PACKAGE_ID,
    "frontierId": FRONTIER_ID,
    "milestoneId": MILESTONE_ID,
    "wave": WAVE,
    "allowedPaths": EXPECTED_ALLOWED_PATHS,
    "ownedSurfaces": EXPECTED_SURFACES,
    "promotedPrimaryHead": PROMOTED_PRIMARY_HEAD,
    "requiredPromotedPlatforms": REQUIRED_PROMOTED_PLATFORMS,
    "generatedAt": now_iso(),
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Next-90 milestone 103.2 UI veteran certification is backed by package registry truth, "
        "Chummer5a baseline evidence, source-level desktop surface tests, and screenshot-pack coverage."
        if not reasons
        else "Next-90 milestone 103.2 UI veteran certification is missing required proof."
    ),
    "reasons": reasons,
    "evidence": evidence,
}

if receipt_path.is_file():
    try:
        previous_payload = json.loads(receipt_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        previous_payload = None
    if isinstance(previous_payload, dict):
        semantic_payload = dict(payload)
        previous_semantic_payload = dict(previous_payload)
        semantic_payload.pop("generatedAt", None)
        previous_semantic_payload.pop("generatedAt", None)
        if semantic_payload == previous_semantic_payload and isinstance(previous_payload.get("generatedAt"), str):
            payload["generatedAt"] = previous_payload["generatedAt"]

receipt_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if reasons:
    print("[next90-m103-ui-veteran-certification] FAIL")
    for reason in reasons:
        print(f" - {reason}")
    sys.exit(1)

print("[next90-m103-ui-veteran-certification] PASS")
print(str(receipt_path))
PY
