#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

from desktop_hardware_wide_common import COMPLETION_ROOT, WORKSPACE_ROOT, ensure_completion_root, load_json


OUTPUT = "FINAL_DESKTOP_HARDWARE_WIDE_FLAGSHIP_VERDICT.md"


def main() -> int:
    completion = ensure_completion_root()
    every_control = load_json(completion / "DESKTOP_EVERY_CONTROL_RUNTIME_AUDIT.generated.json")
    hardware = load_json(completion / "DESKTOP_HARDWARE_MATRIX.generated.json")
    install = load_json(completion / "DESKTOP_INSTALL_UPDATE_RECOVERY_MATRIX.generated.json")
    boundary = load_json(completion / "RULESET_UI_MECHANICS_BOUNDARY_AUDIT.generated.json")

    download_doc = WORKSPACE_ROOT / "Chummer6" / "DOWNLOAD.md"
    switch_doc = WORKSPACE_ROOT / "Chummer6" / "FROM_CHUMMER5A_TO_CHUMMER6.md"

    blockers = [
        "Public desktop docs still describe the shelf as preview rather than a finished flagship release.",
        "There is no public macOS download today, so a hardware-wide/global flagship claim would be false.",
        "The hardware-wide matrix is still incomplete across DPI, resolutions, mixed displays, screen-reader smoke, and high-contrast proof.",
        "Every-control proof is strong but not yet flattened into a row-level certification artifact for every visible runtime control.",
    ]

    lines = [
        "# Final Desktop Hardware-Wide Flagship Verdict",
        "",
        "Verdict: NOT_READY",
        "",
        "## Why",
        "",
        f"- Every-control runtime audit: `{every_control.get('status')}`",
        f"- Hardware matrix: `{hardware.get('status')}`",
        f"- Install/update/recovery matrix: `{install.get('status')}`",
        f"- Ruleset UI/mechanics boundary audit: `{boundary.get('status')}`",
        "",
        "## Blocking findings",
        "",
    ]
    lines.extend([f"- {item}" for item in blockers])
    lines.extend(
        [
            "",
            "## Public truth anchors",
            "",
            f"- `{download_doc}`",
            f"- `{switch_doc}`",
            "",
            "## Honest allowed claim",
            "",
            "- The desktop client has strong Windows/Linux preview receipts, strong UI parity evidence, and serious Chummer5A-style desktop proof.",
            "",
            "## Not allowed yet",
            "",
            "- DESKTOP_HARDWARE_WIDE_FLAGSHIP_READY",
            "- full hardware-wide/global desktop flagship",
            "- public macOS-ready desktop claim",
            "",
        ]
    )

    out = completion / OUTPUT
    out.write_text("\n".join(lines), encoding="utf-8")
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
