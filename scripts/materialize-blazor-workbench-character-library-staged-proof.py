#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.generated.json",
    )
)

CHECKS = [
    {
        "id": "product_workbench_character_library",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor",
        "tokens": [
            "Browser-client Character Roster lane",
            "data-workbench-character-library=\"strip\"",
            "Keep Character Roster workflow visible.",
            "compared dossiers",
            "aria-label=\"Character Roster shortcuts\"",
            "Open Dossier",
            "primary Character Roster picker",
            "self-hosted dossier files",
            "Recover Character Roster and dossier import setup.",
            "private const string OpenRecentCommand = \"open_recent\"",
            "private const string PinCharacterCommand = \"pin_character\"",
            "private const string CloneCharacterCommand = \"clone_character\"",
            "private const string ArchiveCharacterCommand = \"archive_character\"",
            "private const string ImportCharacterCommand = \"import_character\"",
            "command: OpenRecentCommand",
            "command: PinCharacterCommand",
            "command: CloneCharacterCommand",
            "command: ArchiveCharacterCommand",
            "command: ImportCharacterCommand",
            "data-workbench-character-library-action=\"open\"",
            "data-workbench-character-library-action=\"recent\"",
            "data-workbench-character-library-action=\"pin\"",
            "data-workbench-character-library-action=\"clone\"",
            "data-workbench-character-library-action=\"archive\"",
            "data-workbench-character-library-action=\"import\"",
            "data-workbench-character-library-action=\"help\"",
            "href=\"@HelpHref\"",
            "private const string HelpHref = \"/help\"",
        ],
    },
    {
        "id": "scoped_character_library_visual_design",
        "path": "Chummer.Blazor/Components/Pages/Preview.razor.css",
        "tokens": [
            ".browser-workbench-character-library",
            ".browser-workbench-character-library-copy",
            ".browser-workbench-character-library-actions",
            "@media (max-width: 720px)",
        ],
    },
    {
        "id": "proof_contract_docs",
        "path": "docs/BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.md",
        "tokens": [
            "Blazor Workbench Character Roster Staged Proof",
            "Character Roster and recent-dossier management affordances",
            "Character Roster recovery stay close to the active dossier",
            "The staged Character Roster lane covers:",
            "a Character Roster strip on the user-facing Chummer Online route",
            "Character Roster source, style, status reporting, and docs agree",
        ],
    },
    {
        "id": "release_truth_docs",
        "path": "docs/WORKBENCH_RELEASE_SIGNOFF.md",
        "tokens": [
            "workbench Character Roster posture",
            "blazor-workbench-character-library-staged-proof-check.sh",
            "not a hosted or Docker browser execution receipt",
        ],
    },
    {
        "id": "parity_goal_docs",
        "path": "docs/BLAZOR_WEB_CLIENT_PARITY_GOAL.md",
        "tokens": [
            "staged hosted workbench character-roster posture",
            "open, recent, pin, clone, archive, import, and help",
            "not yet claiming file-open, roster persistence, clone, archive, import, or portal help runtime parity",
        ],
    },
    {
        "id": "status_utility_reporting",
        "path": "scripts/print_blazor_public_edge_proof_status.py",
        "tokens": [
            "WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF",
            "workbench_character_library_staged_status",
            "workbench_character_library_staged_source_checks",
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
        "contract_name": "chummer6-ui.blazor_workbench_character_library_staged_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "expected_routes": ["/blazor/workbench", "/help"],
        "checks": evaluated_checks,
        "failures": failures,
        "notes": [
            "This receipt only proves that Chummer Online and /blazor/workbench compatibility route Character Roster source, style, status, and docs agree.",
            "It is not a substitute for hosted Playwright execution proof, Docker self-host proof, file-open proof, roster-persistence proof, clone proof, archive proof, import proof, or portal help runtime proof.",
            "Do not use this receipt to claim file-open, roster persistence, clone, archive, import, portal help runtime, or browser execution parity.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_workbench_character_library_staged_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
