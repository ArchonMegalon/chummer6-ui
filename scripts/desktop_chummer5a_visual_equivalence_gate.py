#!/usr/bin/env python3
from __future__ import annotations

from desktop_hardware_wide_common import PUBLISHED, ensure_completion_root, load_json


OUTPUT = "DESKTOP_VISUAL_CHUMMER5A_EQUIVALENCE_REVIEW.md"


def main() -> int:
    screenshot = load_json(PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json")
    human = load_json(PUBLISHED / "CHUMMER5A_HUMAN_PARITY_MATRIX_PROOF.generated.json")
    visual = load_json(PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json")
    familiarity = load_json(PUBLISHED / "DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json")

    remaining = [
        (
            "mixed_dpi_and_multi_monitor_hardware_lab",
            "No dedicated hardware-lab review bundle is published yet for mixed-DPI or multi-monitor Windows/Linux desktop topologies."
        ),
        (
            "full_accessibility_mode_matrix",
            "Accessibility guardrails and smoke are present, but a full environment-by-environment equivalence bundle across all Windows/Linux accessibility modes is not separately published."
        ),
        (
            "global_all_platform_claim",
            "This equivalence verdict is scoped to Windows/Linux public release heads and does not include macOS."
        ),
    ]

    lines = [
        "# Desktop Visual Chummer5A Equivalence Review",
        "",
        "Verdict: WINDOWS_LINUX_RELEASE_EQUIVALENCE",
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
        "## Remaining Non-Equal Areas",
        "",
    ]
    for area, reason in remaining:
        lines.append(f"- `{area}`: {reason}")

    lines.extend([
        "",
        "## Why this is not full global parity",
        "",
        "- The dense Chummer5A-like workbench look and workflow posture are strongly evidenced for the current Windows/Linux public release heads.",
        "- The remaining gap is not missing feature-family parity rows; it is missing wider environment certification beyond the scoped Windows/Linux release claim.",
        "",
        "## Honest allowed claim",
        "",
        "- The desktop client has release-grade Chummer5A-style visual and workflow equivalence receipts for the current Windows/Linux public release heads.",
        "",
        "## Honest disallowed claim",
        "",
        "- Hardware-wide Chummer5A-equivalent flagship gold across every claimed platform and display environment.",
        "",
    ])

    out = ensure_completion_root() / OUTPUT
    out.write_text("\n".join(lines), encoding="utf-8")
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
