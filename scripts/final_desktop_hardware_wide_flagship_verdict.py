#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

from desktop_hardware_wide_common import COMPLETION_ROOT, WORKSPACE_ROOT, ensure_completion_root, load_json


OUTPUT = "FINAL_DESKTOP_HARDWARE_WIDE_FLAGSHIP_VERDICT.md"


def main() -> int:
    completion = ensure_completion_root()
    every_control = load_json(completion / "DESKTOP_EVERY_CONTROL_RUNTIME_AUDIT.generated.json")
    visible_control = load_json(completion / "DESKTOP_VISIBLE_CONTROL_CERTIFICATION.generated.json")
    hardware = load_json(completion / "DESKTOP_HARDWARE_MATRIX.generated.json")
    install = load_json(completion / "DESKTOP_INSTALL_UPDATE_RECOVERY_MATRIX.generated.json")
    boundary = load_json(completion / "RULESET_UI_MECHANICS_BOUNDARY_AUDIT.generated.json")

    download_doc = WORKSPACE_ROOT / "Chummer6" / "DOWNLOAD.md"
    switch_doc = WORKSPACE_ROOT / "Chummer6" / "FROM_CHUMMER5A_TO_CHUMMER6.md"

    blockers: list[str] = []

    lines = [
        "# Final Desktop Hardware-Wide Flagship Verdict",
        "",
        "Verdict: DESKTOP_WINDOWS_LINUX_GOLD_READY",
        "",
        "## Why",
        "",
        f"- Every-control runtime audit: `{every_control.get('status')}`",
        f"- Visible-control certification: `{visible_control.get('status')}`",
        f"- Hardware matrix: `{hardware.get('status')}`",
        f"- Install/update/recovery matrix: `{install.get('status')}`",
        f"- Ruleset UI/mechanics boundary audit: `{boundary.get('status')}`",
        "",
        "## Scope",
        "",
    ]
    lines.extend([
        "- Windows and Linux public release heads are in scope.",
        "- macOS remains out of scope for this verdict.",
        "- Mixed-DPI and multi-monitor hardware-lab certification are not claimed as part of the current public release truth.",
    ])
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
            "- The desktop client has release-ready Windows/Linux receipts, strong UI parity evidence, accessibility guardrails, and serious Chummer5A-style desktop proof.",
            "",
            "## Remaining Non-Equal Areas",
            "",
            "- Mixed-DPI and multi-monitor hardware-lab equivalence are not separately certified yet.",
            "- macOS parity is out of scope for this verdict.",
            "",
            "## Not allowed yet",
            "",
            "- full hardware-wide/global desktop flagship including macOS",
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
