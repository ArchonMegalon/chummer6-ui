#!/usr/bin/env python3
from __future__ import annotations

from desktop_hardware_wide_common import PUBLISHED, ensure_completion_root, load_json, utc_now, write_json


OUTPUT = "DESKTOP_HARDWARE_MATRIX.generated.json"


def row(category: str, target: str, status: str, evidence: str, detail: str) -> dict[str, str]:
    return {
        "category": category,
        "target": target,
        "status": status,
        "evidence": evidence,
        "detail": detail,
    }


def main() -> int:
    windows = load_json(PUBLISHED / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json")
    linux = load_json(PUBLISHED / "UI_LINUX_DESKTOP_EXIT_GATE.generated.json")
    macos = load_json(PUBLISHED / "UI_MACOS_AVALONIA_OSX_ARM64_DESKTOP_EXIT_GATE.generated.json")

    rows = [
        row("platform", "windows_10_11", "pass", str(PUBLISHED / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"), str(windows.get("summary") or windows.get("reason"))),
        row("platform", "linux_desktop", "pass", str(PUBLISHED / "UI_LINUX_DESKTOP_EXIT_GATE.generated.json"), str(linux.get("summary") or linux.get("reason"))),
        row("platform", "macos_public_route", "fail", str(PUBLISHED / "UI_MACOS_AVALONIA_OSX_ARM64_DESKTOP_EXIT_GATE.generated.json"), str(macos.get("summary") or macos.get("reason"))),
        row("dpi", "100_125_150_200", "missing", str(PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json"), "Current receipts do not provide a complete per-DPI hardware-wide matrix." ),
        row("resolution", "1366x768_1440x900_1920x1080_2560x1440_3840x2160", "missing", str(PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json"), "Current receipts do not provide a complete per-resolution hardware-wide matrix."),
        row("theme", "light_dark", "missing", str(PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json"), "Visual parity proof is present, but not as a full light/dark hardware matrix."),
        row("input", "keyboard_only", "partial", str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"), "Keyboard route proof exists, but no full hardware-wide keyboard-only matrix was found."),
        row("accessibility", "high_contrast", "missing", str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"), "No dedicated high-contrast hardware-wide receipt was found."),
        row("accessibility", "screen_reader_smoke", "missing", str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"), "No dedicated screen-reader smoke receipt was found."),
        row("display_topology", "multi_monitor_mixed_dpi", "missing", str(PUBLISHED / "DESKTOP_VISUAL_PARITY_AUDIT.generated.json"), "No explicit multi-monitor or mixed-DPI matrix receipt was found."),
    ]

    payload = {
        "generatedAt": utc_now(),
        "contract_name": "chummer6-ui.desktop_hardware_matrix",
        "status": "not_ready",
        "summary": "Windows and Linux desktop exit gates are strong, but the hardware-wide flagship matrix is still incomplete and macOS is neither fresh nor public.",
        "matrix": rows,
        "blockingFindings": [
            "No complete cross-DPI, cross-resolution, and mixed-display matrix exists yet.",
            "No dedicated high-contrast or screen-reader smoke proof bundle exists in the current desktop receipt set.",
            "macOS is still not publicly shipped and the checked receipt is stale/failed.",
        ],
    }

    out = ensure_completion_root() / OUTPUT
    write_json(out, payload)
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
