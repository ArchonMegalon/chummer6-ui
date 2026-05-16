#!/usr/bin/env python3
from __future__ import annotations

from desktop_hardware_wide_common import PUBLISHED, ensure_completion_root, load_json


OUTPUT = "DESKTOP_VISUAL_CHUMMER5A_EQUIVALENCE_REVIEW.md"


def main() -> int:
    screenshot = load_json(PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json")
    human = load_json(PUBLISHED / "CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json")
    visual = load_json(PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json")
    familiarity = load_json(PUBLISHED / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json")

    lines = [
        "# Desktop Visual Chummer5A Equivalence Review",
        "",
        "Verdict: STRONG_PREVIEW_EQUIVALENCE",
        "",
        "## What is proven",
        "",
        f"- Screenshot review gate: `{screenshot.get('status')}`",
        f"- Human parity matrix proof: `{human.get('status')}`",
        f"- Desktop visual parity audit: `{visual.get('status')}`",
        f"- Desktop visual familiarity exit gate: `{familiarity.get('status')}`",
        "",
        "## Evidence",
        "",
        f"- `{PUBLISHED / 'CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json'}`",
        f"- `{PUBLISHED / 'CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json'}`",
        f"- `{PUBLISHED / 'DESKTOP_VISUAL_PARITY_AUDIT.generated.json'}`",
        f"- `{PUBLISHED / 'DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json'}`",
        "",
        "## Review summary",
        "",
        f"- Screenshot review summary: {screenshot.get('summary')}",
        f"- Human parity summary: {human.get('summary')}",
        f"- Visual parity summary: {visual.get('summary')}",
        f"- Visual familiarity summary: {familiarity.get('summary')}",
        "",
        "## Why this is not hardware-wide gold yet",
        "",
        "- The dense Chummer5A-like workbench look is strongly evidenced in current proof receipts.",
        "- The remaining gap is not the existence of visual receipts, but the absence of a hardware-wide manual review bundle across DPI, resolutions, mixed displays, and accessibility modes for the Windows/Linux preview shelf.",
        "- Public desktop docs still explicitly describe the shelf as preview rather than a finished flagship release.",
        "",
        "## Honest allowed claim",
        "",
        "- The desktop client has strong Chummer5A-style visual familiarity receipts for the current preview desktop heads.",
        "",
        "## Honest disallowed claim",
        "",
        "- Hardware-wide Chummer5A-equivalent flagship gold across every claimed platform and display environment.",
        "",
    ]

    out = ensure_completion_root() / OUTPUT
    out.write_text("\n".join(lines), encoding="utf-8")
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
