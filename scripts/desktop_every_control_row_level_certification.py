#!/usr/bin/env python3
from __future__ import annotations

from desktop_hardware_wide_common import PUBLISHED, ensure_completion_root, is_pass_status, load_json, utc_now, write_json


OUTPUT = "DESKTOP_VISIBLE_CONTROL_CERTIFICATION.generated.json"


def flatten_group(group: str, tests: dict[str, object]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for name, passed in sorted(tests.items()):
        rows.append(
            {
                "group": group,
                "controlOrRoute": name,
                "status": "pass" if bool(passed) else "fail",
            }
        )
    return rows


def main() -> int:
    interactive = load_json(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json")

    evidence = interactive.get("evidence", {})
    standalone = evidence.get("standaloneControlTests", {})
    main_window = evidence.get("mainWindowTests", {})
    blazor_keyboard = evidence.get("blazorKeyboardTests", {})
    shortcut_catalog = evidence.get("shortcutCatalogTests", {})

    rows: list[dict[str, object]] = []
    if isinstance(standalone, dict):
        rows.extend(flatten_group("standalone_controls", standalone))
    if isinstance(main_window, dict):
        rows.extend(flatten_group("main_window_routes", main_window))
    if isinstance(blazor_keyboard, dict):
        rows.extend(flatten_group("keyboard_navigation", blazor_keyboard))
    if isinstance(shortcut_catalog, dict):
        rows.extend(flatten_group("shortcut_catalog", shortcut_catalog))

    passed = sum(1 for row in rows if row["status"] == "pass")
    failed = sum(1 for row in rows if row["status"] == "fail")

    payload = {
        "generatedAt": utc_now(),
        "contract_name": "chummer6-ui.desktop_visible_control_certification",
        "scope": "windows_linux_preview_only",
        "status": "pass" if failed == 0 and rows else "fail",
        "summary": "Flattened row-level certification artifact derived from the interactive control inventory receipt for the active Windows/Linux desktop preview heads.",
        "sourceReceipt": str(PUBLISHED / "INTERACTIVE_CONTROL_INVENTORY.generated.json"),
        "sourceStatus": "pass" if is_pass_status(interactive) else "fail",
        "rowCount": len(rows),
        "passedCount": passed,
        "failedCount": failed,
        "rows": rows,
    }

    out = ensure_completion_root() / OUTPUT
    write_json(out, payload)
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
