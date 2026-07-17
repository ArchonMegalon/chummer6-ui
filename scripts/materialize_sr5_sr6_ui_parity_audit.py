#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parent.parent
PUBLISHED_ROOT = REPO_ROOT / ".codex-studio" / "published"
RECEIPT_PATH = PUBLISHED_ROOT / "SR5_SR6_UI_PARITY_AUDIT.generated.json"
LEGACY_AUDIT_PATH = REPO_ROOT / "Chummer.Tests" / "Presentation" / "LegacySr5DesktopParityAuditTests.cs"
PROVIDER_AUDIT_PATH = REPO_ROOT / "Chummer.Tests" / "Presentation" / "Sr5Sr6RulesetParityAuditTests.cs"
LEGACY_DETAILS_PATH = PUBLISHED_ROOT / "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY_DETAILS.generated.json"
SR6_SHELL_CATALOG_PATH = REPO_ROOT.parent / "chummer-core-engine" / "Chummer.Rulesets.Sr6" / "Sr6ShellCatalogs.cs"
LEGACY_UI_CONTROL_CATALOG_PATH = REPO_ROOT / "Chummer.Presentation" / "Overview" / "LegacyUiControlCatalog.cs"
DESKTOP_DIALOG_FACTORY_PATH = REPO_ROOT / "Chummer.Presentation" / "Overview" / "DesktopDialogFactory.cs"
ATTRIBUTE_WORKBENCH_PROJECTOR_PATH = REPO_ROOT / "Chummer.Presentation" / "Overview" / "AttributeWorkbenchProjector.cs"
WORKSPACE_XML_MUTATION_CATALOG_PATH = REPO_ROOT / "Chummer.Presentation" / "Overview" / "WorkspaceXmlMutationCatalog.cs"
SECTION_HOST_CONTROL_PATH = REPO_ROOT / "Chummer.Avalonia" / "Controls" / "SectionHostControl.axaml.cs"
BLAZOR_ATTRIBUTE_WORKBENCH_PATH = REPO_ROOT / "Chummer.Blazor" / "Components" / "Shell" / "Sr6AttributeWorkbench.razor"
AVALONIA_ROOT = REPO_ROOT / "Chummer.Avalonia"
TEST_PROJECT_PATH = REPO_ROOT / "Chummer.Tests" / "Chummer.Tests.csproj"
TEST_RUNNER_PATH = REPO_ROOT / "Chummer.Tests" / "bin" / "Debug" / "net10.0" / "Chummer.Tests"
TEST_FILTER = "FullyQualifiedName~LegacySr5DesktopParityAuditTests|FullyQualifiedName~Sr5Sr6RulesetParityAuditTests"

EXPECTATION_PATTERN = re.compile(
    r'new LegacySurfaceParityExpectation\("(?P<label>[^"]+)",\s*'
    r'LegacySurfaceParityDisposition\.(?P<disposition>\w+),\s*\[(?P<pendants>[^\]]*)\]\)'
)
PENDANT_PATTERN = re.compile(r'"([^"]+)"')
TEST_METHOD_PATTERN = re.compile(r"public(?:\s+async)?\s+(?:void|Task)\s+(?P<name>\w+)\(")
STRING_LITERAL_PATTERN = re.compile(r'"([^"]+)"')
PENDANT_PREFIXES = (
    "command:",
    "action:",
    "ui:",
    "dialog-action:",
    "current-dynamic:",
    "current-named:",
    "source-marker:",
)


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def read_json(path: Path) -> dict[str, object]:
    return json.loads(read_text(path))


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


def parse_expectations(text: str) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    tabs_marker = "private static readonly IReadOnlyDictionary<string, LegacySurfaceParityExpectation> TabExpectations"
    controls_marker = "private static readonly IReadOnlyDictionary<string, LegacySurfaceParityExpectation> ControlExpectations"

    tabs_start = text.index(tabs_marker)
    controls_start = text.index(controls_marker)
    tabs_section = text[tabs_start:controls_start]
    controls_section = text[controls_start:]

    def parse_section(section: str) -> list[dict[str, object]]:
        rows: list[dict[str, object]] = []
        for match in EXPECTATION_PATTERN.finditer(section):
            rows.append(
                {
                    "label": match.group("label"),
                    "disposition": match.group("disposition"),
                    "modernPendants": PENDANT_PATTERN.findall(match.group("pendants")),
                }
            )
        return rows

    return parse_section(tabs_section), parse_section(controls_section)


def parse_catalog_ids(text: str, start_marker: str, end_marker: str | None, constructor_name: str) -> list[str]:
    start = text.index(start_marker)
    end = text.index(end_marker, start) if end_marker else len(text)
    section = text[start:end]
    return sorted({match.group(1) for match in re.finditer(rf"{constructor_name}\(\"([^\"]+)\"", section)})


def parse_legacy_ui_control_ids(text: str) -> list[str]:
    catalog_marker = "public static IReadOnlyList<string> All { get; } ="
    start = text.index(catalog_marker)
    section = text[start:]
    body_match = re.search(r"\[(?P<body>.*?)\];", section, re.DOTALL)
    if body_match is None:
        raise SystemExit("Unable to locate the LegacyUiControlCatalog.All list.")
    return sorted(set(STRING_LITERAL_PATTERN.findall(body_match.group("body"))))


def parse_dialog_action_ids(text: str) -> list[str]:
    return sorted(set(re.findall(r'new DesktopDialogAction\("([^"]+)"', text)))


def parse_dynamic_inventory_ids(root: Path, type_names: list[str]) -> list[str]:
    discovered: set[str] = set()
    source_files = sorted(list(root.rglob("*.cs")) + list(root.rglob("*.axaml")))

    for path in source_files:
        if any(part in {"bin", "obj"} for part in path.parts):
            continue
        text = read_text(path)
        for type_name in type_names:
            if re.search(rf"\bnew\s+{re.escape(type_name)}\b|<{re.escape(type_name)}\b", text):
                discovered.add(f"current-dynamic:{type_name}")

    return sorted(discovered)


def parse_source_marker_ids(paths: list[Path], marker_values: list[str]) -> list[str]:
    corpus = "\n".join(read_text(path) for path in paths if path.is_file())
    return sorted({f"source-marker:{value}" for value in marker_values if value and value in corpus})


def mapped_id_kind_counts(values: list[str]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for value in values:
        kind = value.split(":", 1)[0] if ":" in value else "<unknown>"
        counts[kind] = counts.get(kind, 0) + 1
    return dict(sorted(counts.items()))


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def is_pendant_id(value: str) -> bool:
    return value.startswith(PENDANT_PREFIXES)


def disposition_counts(rows: list[dict[str, object]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for row in rows:
        disposition = str(row["disposition"])
        counts[disposition] = counts.get(disposition, 0) + 1
    return dict(sorted(counts.items()))


def unique_pendants(rows: list[dict[str, object]]) -> list[str]:
    values = {
        pendant
        for row in rows
        for pendant in row["modernPendants"]
        if isinstance(pendant, str) and pendant
    }
    return sorted(values)


def ensure_test_runner() -> None:
    if TEST_RUNNER_PATH.is_file():
        return

    build_command = [
        "dotnet",
        "build",
        str(TEST_PROJECT_PATH),
        "--nologo",
        "--verbosity",
        "quiet",
        "--ignore-failed-sources",
        "-p:NuGetAudit=false",
    ]
    build = subprocess.run(build_command, cwd=REPO_ROOT, text=True, capture_output=True)
    if build.returncode != 0:
        raise SystemExit(
            "Unable to build test runner for SR5/SR6 UI parity audit:\n"
            + tail_lines((build.stdout or "") + "\n" + (build.stderr or ""))
        )
    if not TEST_RUNNER_PATH.is_file():
        raise SystemExit(f"Test runner was not produced at {TEST_RUNNER_PATH}")


def run_filtered_tests() -> dict[str, object]:
    ensure_test_runner()
    command = [
        str(TEST_RUNNER_PATH),
        "--filter",
        TEST_FILTER,
        "--output",
        "Normal",
        "--no-progress",
    ]
    result = subprocess.run(command, cwd=REPO_ROOT, text=True, capture_output=True)
    combined = (result.stdout or "") + "\n" + (result.stderr or "")
    return {
        "command": command,
        "exitCode": result.returncode,
        "outputTail": tail_lines(combined),
    }


def main() -> None:
    legacy_audit_text = read_text(LEGACY_AUDIT_PATH)
    provider_audit_text = read_text(PROVIDER_AUDIT_PATH)
    legacy_detail_receipt = read_json(LEGACY_DETAILS_PATH)
    sr6_shell_catalog_text = read_text(SR6_SHELL_CATALOG_PATH)
    legacy_ui_control_catalog_text = read_text(LEGACY_UI_CONTROL_CATALOG_PATH)
    desktop_dialog_factory_text = read_text(DESKTOP_DIALOG_FACTORY_PATH)
    legacy_tabs, legacy_controls = parse_expectations(legacy_audit_text)

    partial_tabs = [row for row in legacy_tabs if row["disposition"] == "Partial"]
    missing_tabs = [row for row in legacy_tabs if row["disposition"] == "Missing"]
    partial_controls = [row for row in legacy_controls if row["disposition"] == "Partial"]
    missing_controls = [row for row in legacy_controls if row["disposition"] == "Missing"]
    provider_tests = TEST_METHOD_PATTERN.findall(provider_audit_text)
    test_result = run_filtered_tests()
    legacy_element_dispositions = legacy_detail_receipt.get("legacyElementDispositions") or []
    if not isinstance(legacy_element_dispositions, list):
        raise SystemExit("Chummer5a legacy UI element parity detail receipt does not expose a legacyElementDispositions array.")

    raw_mapped_current_ids = sorted(
        {
            current_id
            for row in legacy_element_dispositions
            if isinstance(row, dict)
            for current_id in row.get("mappedCurrentIds") or []
            if isinstance(current_id, str) and current_id
        }
    )
    mapped_current_ids = [current_id for current_id in raw_mapped_current_ids if is_pendant_id(current_id)]
    non_pendant_mapped_current_ids = [current_id for current_id in raw_mapped_current_ids if not is_pendant_id(current_id)]
    observed_dynamic_types = sorted(
        {
            current_id.split(":", 1)[1]
            for current_id in mapped_current_ids
            if current_id.startswith("current-dynamic:")
        }
    )
    observed_source_markers = sorted(
        {
            current_id.split(":", 1)[1]
            for current_id in mapped_current_ids
            if current_id.startswith("source-marker:")
        }
    )

    sr6_command_ids = [f"command:{value}" for value in parse_catalog_ids(
        sr6_shell_catalog_text,
        "internal static class Sr6AppCommandCatalog",
        "internal static class Sr6NavigationTabCatalog",
        "Sr6")]
    sr6_action_ids = [f"action:{value}" for value in parse_catalog_ids(
        sr6_shell_catalog_text,
        "internal static class Sr6WorkspaceSurfaceActionCatalog",
        None,
        "Sr6")]
    sr6_ui_control_ids = [f"ui:{value}" for value in parse_legacy_ui_control_ids(legacy_ui_control_catalog_text)]
    sr6_dialog_action_ids = [f"dialog-action:{value}" for value in parse_dialog_action_ids(desktop_dialog_factory_text)]
    sr6_dynamic_ids = parse_dynamic_inventory_ids(AVALONIA_ROOT, observed_dynamic_types)
    sr6_source_marker_ids = parse_source_marker_ids(
        [
            ATTRIBUTE_WORKBENCH_PROJECTOR_PATH,
            WORKSPACE_XML_MUTATION_CATALOG_PATH,
            SECTION_HOST_CONTROL_PATH,
            BLAZOR_ATTRIBUTE_WORKBENCH_PATH,
        ],
        observed_source_markers,
    )
    sr6_supported_ids = set(
        sr6_command_ids
        + sr6_action_ids
        + sr6_ui_control_ids
        + sr6_dialog_action_ids
        + sr6_dynamic_ids
        + sr6_source_marker_ids
    )

    unsupported_mapped_current_ids = [current_id for current_id in mapped_current_ids if current_id not in sr6_supported_ids]
    legacy_element_pendant_gaps: list[dict[str, object]] = []
    for row in legacy_element_dispositions:
        if not isinstance(row, dict):
            continue
        mapped_ids = [
            value
            for value in row.get("mappedCurrentIds") or []
            if isinstance(value, str) and value and is_pendant_id(value)
        ]
        missing_sr6_pendants = [value for value in mapped_ids if value not in sr6_supported_ids]
        if missing_sr6_pendants:
            legacy_element_pendant_gaps.append(
                {
                    "legacyElementId": row.get("legacyElementId"),
                    "family": row.get("family"),
                    "mappedCurrentIds": mapped_ids,
                    "missingSr6Pendants": missing_sr6_pendants,
                }
            )

    reasons: list[str] = []
    if partial_tabs:
        reasons.append("SR5 legacy tab parity still contains partial SR6 dispositions.")
    if missing_tabs:
        reasons.append("SR5 legacy tab parity still contains missing SR6 dispositions.")
    if partial_controls:
        reasons.append("SR5 legacy control parity still contains partial SR6 dispositions.")
    if missing_controls:
        reasons.append("SR5 legacy control parity still contains missing SR6 dispositions.")
    if not status_ok(legacy_detail_receipt.get("status")):
        reasons.append("Chummer5a full legacy UI element parity detail receipt is not passing.")
    if int(legacy_detail_receipt.get("missingLegacyElementDispositionCount") or 0) != 0:
        reasons.append("Chummer5a full legacy UI element parity detail receipt still contains missing dispositions.")
    if int(legacy_detail_receipt.get("familyFallbackLegacyElementDispositionCount") or 0) != 0:
        reasons.append("Chummer5a full legacy UI element parity detail receipt still relies on fallback family mappings.")
    if int(legacy_detail_receipt.get("familyReviewsWithUnavailableMappedCurrentIds") or 0) != 0:
        reasons.append("Chummer5a full legacy UI element parity detail receipt still carries unavailable family-level SR6 counterparts.")
    if int(legacy_detail_receipt.get("legacyElementsWithUnavailableMappedCurrentIds") or 0) != 0:
        reasons.append("Chummer5a full legacy UI element parity detail receipt still carries unavailable element-level SR6 counterparts.")
    if non_pendant_mapped_current_ids:
        reasons.append("Some full-spectrum SR5 legacy element mappings still resolve to non-pendant current IDs.")
    if unsupported_mapped_current_ids:
        reasons.append("Some full-spectrum SR5 legacy element mappings still point to current IDs without explicit SR6 backing.")
    if legacy_element_pendant_gaps:
        reasons.append("Some full-spectrum SR5 legacy elements still lack an explicit SR6 pendant.")
    if int(test_result["exitCode"]) != 0:
        reasons.append("Direct SR5/SR6 provider, dialog-contract, workflow, or shared-command execution parity tests failed.")

    payload = {
        "generatedAt": now_iso(),
        "contract_name": "chummer6-ui.sr5_sr6_ui_parity_audit",
        "status": "pass" if not reasons else "fail",
        "summary": (
            "SR5 legacy desktop surfaces, full-spectrum Chummer5a element dispositions, and direct SR5/SR6 provider, "
            "dialog-contract, legacy utility-control dialog, utility-dialog action execution, workflow, and shared-command execution parity tests confirm that every audited SR5 UI element/function has an explicit SR6 pendant."
            if not reasons
            else "SR5/SR6 UI parity audit found unresolved parity gaps."
        ),
        "reasons": reasons,
        "evidence": {
            "legacyAuditSourcePath": str(LEGACY_AUDIT_PATH),
            "providerAuditSourcePath": str(PROVIDER_AUDIT_PATH),
            "legacyDetailReceiptPath": str(LEGACY_DETAILS_PATH),
            "sr6ShellCatalogSourcePath": str(SR6_SHELL_CATALOG_PATH),
            "legacyUiControlCatalogPath": str(LEGACY_UI_CONTROL_CATALOG_PATH),
            "desktopDialogFactoryPath": str(DESKTOP_DIALOG_FACTORY_PATH),
            "testProjectPath": str(TEST_PROJECT_PATH),
            "testRunnerPath": str(TEST_RUNNER_PATH),
            "testFilter": TEST_FILTER,
            "testResult": test_result,
            "legacyTabCount": len(legacy_tabs),
            "legacyControlCount": len(legacy_controls),
            "legacyTabDispositionCounts": disposition_counts(legacy_tabs),
            "legacyControlDispositionCounts": disposition_counts(legacy_controls),
            "partialTabCount": len(partial_tabs),
            "missingTabCount": len(missing_tabs),
            "partialControlCount": len(partial_controls),
            "missingControlCount": len(missing_controls),
            "providerParityTestCount": len(provider_tests),
            "providerParityTests": provider_tests,
            "uniqueTabPendantCount": len(unique_pendants(legacy_tabs)),
            "uniqueControlPendantCount": len(unique_pendants(legacy_controls)),
            "legacyDetailReceiptStatus": legacy_detail_receipt.get("status"),
            "legacyElementDispositionCount": len(legacy_element_dispositions),
            "missingLegacyElementDispositionCount": int(legacy_detail_receipt.get("missingLegacyElementDispositionCount") or 0),
            "familyFallbackLegacyElementDispositionCount": int(legacy_detail_receipt.get("familyFallbackLegacyElementDispositionCount") or 0),
            "familyReviewsWithUnavailableMappedCurrentIds": int(legacy_detail_receipt.get("familyReviewsWithUnavailableMappedCurrentIds") or 0),
            "familyUnavailableMappedCurrentIdCount": int(legacy_detail_receipt.get("familyUnavailableMappedCurrentIdCount") or 0),
            "legacyElementsWithUnavailableMappedCurrentIds": int(legacy_detail_receipt.get("legacyElementsWithUnavailableMappedCurrentIds") or 0),
            "unavailableMappedCurrentIdCount": int(legacy_detail_receipt.get("unavailableMappedCurrentIdCount") or 0),
            "observedLegacyFamilyCount": int(legacy_detail_receipt.get("observedFamilyCount") or 0),
            "observedLegacyFamilies": legacy_detail_receipt.get("observedFamilies") or [],
            "uniqueMappedCurrentIdCount": len(mapped_current_ids),
            "uniqueMappedCurrentIdKindCounts": mapped_id_kind_counts(mapped_current_ids),
            "nonPendantMappedCurrentIdCount": len(non_pendant_mapped_current_ids),
            "nonPendantMappedCurrentIds": non_pendant_mapped_current_ids,
            "sr6CommandCount": len(sr6_command_ids),
            "sr6WorkspaceActionCount": len(sr6_action_ids),
            "sr6UiControlCount": len(sr6_ui_control_ids),
            "sr6DialogActionCount": len(sr6_dialog_action_ids),
            "sr6DynamicTypeCount": len(sr6_dynamic_ids),
            "legacyElementsWithExplicitSr6Pendants": len(legacy_element_dispositions) - len(legacy_element_pendant_gaps),
            "legacyElementsMissingExplicitSr6Pendants": len(legacy_element_pendant_gaps),
            "unsupportedMappedCurrentIdCount": len(unsupported_mapped_current_ids),
            "unsupportedMappedCurrentIds": unsupported_mapped_current_ids,
        },
        "legacyTabs": legacy_tabs,
        "legacyControls": legacy_controls,
        "providerParityTests": provider_tests,
        "partialTabs": partial_tabs,
        "missingTabs": missing_tabs,
        "partialControls": partial_controls,
        "missingControls": missing_controls,
        "legacyElementGapSamples": legacy_element_pendant_gaps[:50],
        "unsupportedMappedCurrentIds": unsupported_mapped_current_ids,
    }

    PUBLISHED_ROOT.mkdir(parents=True, exist_ok=True)
    RECEIPT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        raise SystemExit(48)

    print(f"[sr5-sr6-ui-parity-audit] PASS: {RECEIPT_PATH}")


if __name__ == "__main__":
    main()
