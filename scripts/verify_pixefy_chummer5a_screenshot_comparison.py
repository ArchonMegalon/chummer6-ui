#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from urllib.parse import unquote, urlparse
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
DEFAULT_PIXEFY_RECEIPT = PUBLISHED / "PIXEFY_CHUMMER5A_SCREENSHOT_COMPARISON_GATE.generated.json"
LOCAL_ONLY_RECEIPT = PUBLISHED / "CHUMMER5A_LOCAL_SCREENSHOT_COMPARISON_GATE.generated.json"
DEFAULT_SCREENSHOT_DIR = PUBLISHED / "ui-flagship-release-gate-screenshots"
DEFAULT_SCOPE = "pixefy_public_routes_only"
LOCAL_ONLY_SCOPE = "local_only"
_MIN_SCREENSHOT_COUNT = 40


def _receipt_path(path: Path) -> str:
    try:
        return str(path.relative_to(REPO_ROOT))
    except ValueError:
        return str(path)


def _normalize_reference_value(value: Any) -> str:
    if not isinstance(value, str):
        return ""

    trimmed = value.strip().strip('"').strip("'")
    if trimmed.startswith("file://"):
        parsed = urlparse(trimmed)
        if parsed.scheme == "file":
            trimmed = parsed.path
            if parsed.netloc:
                trimmed = f"/{parsed.netloc}{parsed.path}"

    return unquote(trimmed.replace("\\", "/").replace("//", "/"))


def _load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise SystemExit(f"JSON root is not an object: {path}")
    return payload


def _status_is_pass(payload: dict[str, Any]) -> bool:
    return str(payload.get("status") or "").strip().lower() in {"pass", "passed", "ready"}


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _comparison_scope() -> str:
    raw_scope = str(os.environ.get("CHUMMER5A_SCREENSHOT_COMPARISON_SCOPE") or DEFAULT_SCOPE).strip().lower()
    return LOCAL_ONLY_SCOPE if raw_scope == LOCAL_ONLY_SCOPE else DEFAULT_SCOPE


def _output_receipt_path(scope: str) -> Path:
    explicit = str(os.environ.get("CHUMMER5A_SCREENSHOT_COMPARISON_RECEIPT_PATH") or "").strip()
    if explicit:
        path = Path(explicit)
        return path if path.is_absolute() else (REPO_ROOT / path)

    return LOCAL_ONLY_RECEIPT if scope == LOCAL_ONLY_SCOPE else DEFAULT_PIXEFY_RECEIPT


def _invalid_path_value(value: Any) -> str | None:
    if not isinstance(value, str):
        return "value must be a string"

    trimmed = value.strip()
    if not trimmed:
        return "empty path"

    if ".." in Path(trimmed).parts:
        return "path contains traversal segment"

    return None


def _is_within_directory(path: Path, directory: Path) -> bool:
    try:
        resolved_path = path.resolve()
        resolved_directory = directory.resolve()
        resolved_path.relative_to(resolved_directory)
        return True
    except ValueError:
        return False


def _coerce_artifact_path(
    value: Any,
    *,
    screenshot_directory: Path = DEFAULT_SCREENSHOT_DIR,
    strict_screenshot_scope: bool = True,
) -> tuple[Path | None, str | None]:
    error = _invalid_path_value(value)
    if error is not None:
        return None, error

    raw_value = _normalize_reference_value(value)
    candidate = Path(raw_value)

    if ".." in candidate.parts:
        return None, "relative path traversal is not allowed"

    resolved: Path | None = None
    for candidate_path in _artifact_reference_candidates(candidate):
        if candidate_path.is_file():
            resolved = candidate_path
            break

    if resolved is None:
        return None, "file does not exist"

    if strict_screenshot_scope and not _is_within_directory(resolved, screenshot_directory):
        return None, "file resolves outside the screenshot directory"

    if resolved.suffix.lower() != ".png":
        return None, "artifact is not a png file"

    return resolved, None


def _artifact_reference_candidates(candidate: Path) -> list[Path]:
    candidates: list[Path] = []

    # 1) Direct resolution against the repository root or workspace root.
    candidates.append(_resolve_reference(candidate))

    # 2) Workspace-scoped references.
    if candidate.parent != Path("."):
        candidates.append(_resolve_reference(candidate))

    # 2b) Legacy aliases with either repo root or symlink root style.
    if candidate.as_posix().startswith("chummer-presentation/"):
        candidates.append((WORKSPACE_ROOT / candidate).resolve())
    if candidate.as_posix().startswith("chummer6-ui/"):
        candidates.append((WORKSPACE_ROOT.parent / candidate).resolve())

    # 3) Windows drive paths normalized from URL decoding.
    if candidate.as_posix().startswith("/mnt/") and ":" in candidate.as_posix():
        candidates.append(Path(candidate.as_posix()).resolve())

    if candidate.parent == Path(".") and len(candidate.name) > 0 and candidate.suffix.lower() == ".png":
        candidates.append(_search_screenshot_directory(candidate.name))

    # 3) Single-file names from contract artifacts.
    if candidate.parent == Path("."):
        candidates.append((DEFAULT_SCREENSHOT_DIR / candidate).resolve())

    if candidate.parent == Path("."):
        candidates.append((WORKSPACE_ROOT / ".codex-studio" / "published" / "ui-flagship-release-gate-screenshots" / candidate).resolve())

    # 5) Last-resort absolute fallback to keep validation robust in CI when absolute receipts are generated.
    if candidate.is_absolute():
        candidates.append(candidate.resolve())

    # Keep the list deterministic and avoid duplicate filesystem probes.
    deduped: list[Path] = []
    seen: set[str] = set()
    for candidate_path in candidates:
        resolved_candidate = candidate_path.resolve()
        key = str(resolved_candidate)
        if key not in seen:
            seen.add(key)
            deduped.append(resolved_candidate)

    return deduped


def _resolve_reference(candidate: Path) -> Path:
    if candidate.is_absolute():
        return candidate.resolve()

    direct = (REPO_ROOT / candidate).resolve()
    if direct.is_file():
        return direct

    workspace_reference = (WORKSPACE_ROOT / candidate).resolve()
    if workspace_reference.exists():
        return workspace_reference

    return direct


def _search_screenshot_directory(file_name: str) -> Path:
    if not file_name:
        return DEFAULT_SCREENSHOT_DIR / file_name

    if DEFAULT_SCREENSHOT_DIR.is_dir():
        direct_hit = DEFAULT_SCREENSHOT_DIR / file_name
        if direct_hit.is_file():
            return direct_hit

        matches = [path for path in DEFAULT_SCREENSHOT_DIR.glob(f"**/{file_name}") if path.is_file()]
        if matches:
            return matches[0]

    return DEFAULT_SCREENSHOT_DIR / file_name


def _normalize_required_screenshot_list(value: Any) -> tuple[list[str], list[str]]:
    if not isinstance(value, list):
        return [], ["required screenshot list missing or malformed"]

    normalized: list[str] = []
    reasons: list[str] = []
    for candidate in value:
        if not isinstance(candidate, str):
            reasons.append("required screenshot entry is not a string")
            continue
        candidate = candidate.strip()
        if not candidate:
            reasons.append("required screenshot entry is blank")
            continue
        normalized.append(candidate)

    return normalized, reasons


def _ensure_paths_exist(
    references: list[str],
    label: str,
    missing: list[str],
    reasons: list[str],
    *,
    screenshot_directory: Path = DEFAULT_SCREENSHOT_DIR,
) -> None:
    seen: set[str] = set()
    for reference in references:
        resolved, error = _coerce_artifact_path(reference, screenshot_directory=screenshot_directory)
        if error is not None:
            reasons.append(f"{label} reference '{reference}' invalid: {error}.")
            continue

        assert resolved is not None
        if str(resolved) in seen:
            continue
        if not resolved.is_file():
            missing.append(reference)
            continue

        seen.add(str(resolved))


def _coerce_rows(rows: Any, reasons: list[str]) -> list[dict[str, Any]]:
    if not isinstance(rows, list):
        reasons.append("contact sheet rows payload is missing or not a list")
        return []

    normalized: list[dict[str, Any]] = []
    seen_sheet_ids: set[str] = set()
    for index, raw_row in enumerate(rows):
        if not isinstance(raw_row, dict):
            reasons.append(f"contact sheet row {index} must be an object")
            continue

        sheet_id = str(raw_row.get("sheetId", "")).strip()
        if not sheet_id:
            reasons.append(f"contact sheet row {index} is missing sheetId")
            continue

        if sheet_id in seen_sheet_ids:
            reasons.append(f"duplicate contact sheet row id '{sheet_id}'")
        else:
            seen_sheet_ids.add(sheet_id)

        row_status = str(raw_row.get("status") or "").strip().lower()
        if row_status not in {"pass", "passed", "ready"}:
            reasons.append(f"contact sheet row '{sheet_id}' did not pass: {raw_row.get('status')}")

        normalized.append(raw_row)

    return normalized


def _find_screenshot_entry(entries: list[dict[str, Any]], screenshot_name: str) -> dict[str, Any] | None:
    for entry in entries:
        if str(_entry_value(entry, "screenshot", "Screenshot") or "").strip() == screenshot_name:
            return entry
    return None


def _entry_value(entry: dict[str, Any], *keys: str) -> Any:
    for key in keys:
        if key in entry:
            return entry.get(key)
    return None


def _bool_value(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        return value.strip().lower() in {"true", "1", "yes"}
    if isinstance(value, (int, float)):
        return value != 0
    return False


def _float_value(value: Any) -> float | None:
    if isinstance(value, (int, float)):
        return float(value)
    if isinstance(value, str):
        try:
            return float(value.strip())
        except ValueError:
            return None
    return None


def main() -> int:
    PUBLISHED.mkdir(parents=True, exist_ok=True)
    comparison_scope = _comparison_scope()
    comparison_scope_is_local_only = comparison_scope == LOCAL_ONLY_SCOPE
    receipt_path = _output_receipt_path(comparison_scope)
    reasons: list[str] = []
    warnings: list[str] = []

    pixefy_targets_path = PUBLISHED / "PUBLIC_SURFACE_QA_TARGETS.generated.json"
    screenshot_review_path = PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
    contact_sheets_path = PUBLISHED / "CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json"
    flagship_gate_path = PUBLISHED / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
    screenshot_control_evidence_path = PUBLISHED / "ui-flagship-release-gate-screenshots" / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
    screenshot_matrix_path = PUBLISHED / "CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json"
    windows_gate_path = PUBLISHED / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
    startup_smoke_gate_path = PUBLISHED / "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json"
    required_paths = [
        screenshot_review_path,
        contact_sheets_path,
        screenshot_control_evidence_path,
        screenshot_matrix_path,
    ]
    if not comparison_scope_is_local_only:
        required_paths.extend(
            [
                pixefy_targets_path,
                flagship_gate_path,
                windows_gate_path,
                startup_smoke_gate_path,
            ]
        )

    for path in required_paths:
        if not path.is_file():
            reasons.append(f"missing required receipt: {_receipt_path(path)}")

    pixefy_targets = _load_json(pixefy_targets_path) if pixefy_targets_path.is_file() else {}
    screenshot_review = _load_json(screenshot_review_path) if screenshot_review_path.is_file() else {}
    contact_sheets = _load_json(contact_sheets_path) if contact_sheets_path.is_file() else {}
    flagship_gate = _load_json(flagship_gate_path) if flagship_gate_path.is_file() else {}
    screenshot_control_evidence = _load_json(screenshot_control_evidence_path) if screenshot_control_evidence_path.is_file() else {}
    screenshot_matrix = _load_json(screenshot_matrix_path) if screenshot_matrix_path.is_file() else {}
    windows_gate = _load_json(windows_gate_path) if windows_gate_path.is_file() else {}
    startup_smoke_gate = _load_json(startup_smoke_gate_path) if startup_smoke_gate_path.is_file() else {}

    if not comparison_scope_is_local_only:
        if str(pixefy_targets.get("provider") or "").strip().lower() != "pixefy":
            reasons.append("PUBLIC_SURFACE_QA_TARGETS.generated.json must declare provider Pixefy.")
        if str(pixefy_targets.get("scope") or "").strip().lower() != "public_routes_only":
            reasons.append("PUBLIC_SURFACE_QA_TARGETS.generated.json must stay scoped to public_routes_only.")
        if str(pixefy_targets.get("status") or "").strip().lower() != "ready_for_pixefy_capture":
            reasons.append("PUBLIC_SURFACE_QA_TARGETS.generated.json must be ready_for_pixefy_capture.")

    if not _status_is_pass(screenshot_review):
        reasons.append("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json is not passing.")
    if not _status_is_pass(contact_sheets):
        reasons.append("CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json is not passing.")
    if str(screenshot_control_evidence.get("generatedAt") or "").strip() == "":
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing generatedAt.")
    if not _status_is_pass(screenshot_matrix):
        reasons.append("CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json is not passing.")
    if not comparison_scope_is_local_only:
        if not _status_is_pass(flagship_gate):
            reasons.append("UI_FLAGSHIP_RELEASE_GATE.generated.json is not passing.")
        if (
            str(windows_gate.get("contract_name") or "").strip()
            != "chummer6-ui.windows_desktop_exit_gate"
        ):
            reasons.append("UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json has an invalid contract.")
        if not _status_is_pass(windows_gate):
            reasons.append("UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json is not passing.")
        windows_head = windows_gate.get("head")
        if (
            not isinstance(windows_head, dict)
            or str(windows_head.get("platform") or "").strip().lower() != "windows"
        ):
            reasons.append("UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json does not prove a Windows head.")
        if (
            str(startup_smoke_gate.get("contract_name") or "").strip()
            != "chummer6-ui.next90_m144_startup_smoke_and_executable_gate"
        ):
            reasons.append(
                "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json has an invalid contract."
            )
        if not _status_is_pass(startup_smoke_gate):
            reasons.append(
                "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json is not passing."
            )
        startup_smoke_proofs = startup_smoke_gate.get("proofs")
        windows_smoke_proofs = [
            proof
            for proof in startup_smoke_proofs
            if isinstance(proof, dict)
            and str(proof.get("platform") or "").strip().lower() == "windows"
        ] if isinstance(startup_smoke_proofs, list) else []
        if not windows_smoke_proofs:
            reasons.append(
                "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json "
                "is missing Windows startup-smoke evidence."
            )
        elif not any(
            _status_is_pass({"status": proof.get("startupSmokeStatus")})
            and _status_is_pass({"status": proof.get("executableGateStatus")})
            and proof.get("startupSmokeAcceptedAsIncompatibleHostSkip") is False
            and proof.get("startupSmokeVersionMatchesReleaseChannel") is True
            and proof.get("startupSmokeChannelMatchesReleaseChannel") is True
            and proof.get("startupSmokeArtifactDigestMatchesLocalArtifact") is True
            and proof.get("executableGateVersionMatchesReleaseChannel") is True
            and proof.get("executableGateChannelMatchesReleaseChannel") is True
            for proof in windows_smoke_proofs
        ):
            reasons.append(
                "NEXT90_M144_UI_STARTUP_SMOKE_AND_EXECUTABLE_GATE.generated.json "
                "does not contain a passing, release-bound native Windows proof."
            )

    authority = screenshot_control_evidence.get("authority")
    if not isinstance(authority, dict):
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing authority metadata.")
        authority = {}
    if str(authority.get("visualBaseline") or "").strip() != "Chummer5a":
        reasons.append("Screenshot control evidence must pin visualBaseline to Chummer5a.")
    if str(authority.get("designAuthorityPlatform") or "").strip().lower() != "windows":
        reasons.append("Screenshot control evidence must declare Windows as designAuthorityPlatform.")
    if str(authority.get("captureHead") or "").strip().lower() != "avalonia":
        reasons.append("Screenshot control evidence must declare Avalonia as captureHead.")
    if str(authority.get("menuInteractionMode") or "").strip().lower() != "real_menu_items":
        reasons.append("Screenshot control evidence must declare real_menu_items menu interaction mode.")
    if str(authority.get("dialogHostPolicy") or "").strip().lower() != "dedicated_desktop_dialog_window":
        reasons.append("Screenshot control evidence must declare dedicated_desktop_dialog_window dialogHostPolicy.")
    if str(authority.get("forbiddenInlineSurface") or "").strip() != "RightShellRegion":
        reasons.append("Screenshot control evidence must forbid RightShellRegion as inline desktop dialog surface.")

    supporting_proofs = screenshot_control_evidence.get("supportingProofs")
    if not comparison_scope_is_local_only:
        if not isinstance(supporting_proofs, dict):
            reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing supportingProofs metadata.")
            supporting_proofs = {}
        if str(supporting_proofs.get("windowsDesktopExitGate") or "").strip() == "":
            reasons.append("Screenshot control evidence must cite the Windows desktop exit gate.")
        if str(supporting_proofs.get("startupSmokeAndExecutableGate") or "").strip() == "":
            reasons.append("Screenshot control evidence must cite the startup smoke and executable gate.")

    screenshot_asset_review = screenshot_review.get("screenshotAssetReview", {})
    if not isinstance(screenshot_asset_review, dict):
        reasons.append("CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json is missing screenshotAssetReview.")
        screenshot_asset_review = {}

    required_new_gate_screenshots, required_parse_reasons = _normalize_required_screenshot_list(
        screenshot_asset_review.get("requiredScreenshots")
    )
    if required_parse_reasons:
        reasons.extend(required_parse_reasons)

    if screenshot_asset_review.get("missingScreenshots"):
        reasons.append(
            "screenshotAssetReview lists missing screenshots: "
            + ", ".join(str(item) for item in screenshot_asset_review.get("missingScreenshots", []))
        )
    if screenshot_asset_review.get("invalidScreenshots"):
        reasons.append(
            "screenshotAssetReview lists invalid screenshots: "
            + ", ".join(str(item) for item in screenshot_asset_review.get("invalidScreenshots", []))
        )

    if screenshot_asset_review.get("undersizedScreenshots"):
        reasons.append(
            "screenshotAssetReview lists undersized screenshots: "
            + ", ".join(str(item) for item in screenshot_asset_review.get("undersizedScreenshots", []))
        )
    if screenshot_asset_review.get("staleScreenshots"):
        reasons.append(
            "screenshotAssetReview lists stale screenshots: "
            + ", ".join(str(item) for item in screenshot_asset_review.get("staleScreenshots", []))
        )

    screenshot_directory = DEFAULT_SCREENSHOT_DIR
    declared_directory = str(screenshot_review.get("screenshotDirectory") or screenshot_asset_review.get("screenshotDirectory") or "").strip()
    if declared_directory:
        declared_error = _invalid_path_value(declared_directory)
        if declared_error is not None:
            reasons.append(f"screenshot review screenshotDirectory is invalid: {declared_error}.")
        else:
            candidate = Path(declared_directory)
            screenshot_directory = _resolve_reference(candidate)

            if not screenshot_directory.is_dir():
                reasons.append(
                    f"screenshot review declares invalid or missing screenshotDirectory: {declared_directory}"
                )

    screenshot_files = sorted(path.name for path in screenshot_directory.glob("*.png")) if screenshot_directory.is_dir() else []
    if len(screenshot_files) < _MIN_SCREENSHOT_COUNT:
        reasons.append(
            f"expected at least {_MIN_SCREENSHOT_COUNT} promoted screenshots, found {len(screenshot_files)}."
        )

    control_entries = screenshot_control_evidence.get("entries")
    if not isinstance(control_entries, list) or not control_entries:
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing screenshot entries.")
        control_entries = []
    control_screenshot_names = sorted({
        str(_entry_value(entry, "screenshot", "Screenshot") or "").strip()
        for entry in control_entries
        if isinstance(entry, dict) and str(_entry_value(entry, "screenshot", "Screenshot") or "").strip()
    })
    if control_screenshot_names and control_screenshot_names != screenshot_files:
        missing_from_control = sorted(set(screenshot_files) - set(control_screenshot_names))
        missing_from_disk = sorted(set(control_screenshot_names) - set(screenshot_files))
        if missing_from_control:
            reasons.append(
                "published screenshots are missing from screenshot control evidence: "
                + ", ".join(missing_from_control)
            )
        if missing_from_disk:
            reasons.append(
                "screenshot control evidence references screenshots missing on disk: "
                + ", ".join(missing_from_disk)
            )

    workflow_coverage = screenshot_control_evidence.get("workflowCoverage")
    if not isinstance(workflow_coverage, list) or not workflow_coverage:
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing workflowCoverage.")
    else:
        covered_files = [
            str(file_name).strip()
            for row in workflow_coverage
            if isinstance(row, dict)
            for file_name in row.get("screenshotFiles", [])
            if isinstance(file_name, str) and file_name.strip()
        ]
        missing_workflow_files = sorted(set(covered_files) - set(screenshot_files))
        if missing_workflow_files:
            reasons.append(
                "workflowCoverage references screenshots missing on disk: "
                + ", ".join(missing_workflow_files)
            )

    menu_open_entry = _find_screenshot_entry(control_entries, "02-menu-open-light.png")
    if menu_open_entry is None:
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing 02-menu-open-light.png entry.")
    else:
        visible_menu_command_ids = [
            str(value).strip()
            for value in _entry_value(menu_open_entry, "visibleMenuCommandIds", "VisibleMenuCommandIds") or []
            if isinstance(value, str) and value.strip()
        ]
        for required_command in ("new_character", "open_character", "save_character"):
            if required_command not in visible_menu_command_ids:
                reasons.append(
                    f"02-menu-open-light.png must expose '{required_command}' through real visible menu command ids."
                )
        if _bool_value(_entry_value(menu_open_entry, "rightShellVisible", "RightShellVisible")):
            reasons.append("02-menu-open-light.png must not show the inline right shell.")
        right_shell_width = _float_value(_entry_value(menu_open_entry, "rightShellWidth", "RightShellWidth"))
        if right_shell_width is None:
            reasons.append("02-menu-open-light.png is missing rightShellWidth evidence.")
        elif right_shell_width > 1.0:
            reasons.append("02-menu-open-light.png must keep rightShellWidth collapsed.")
        if _bool_value(_entry_value(menu_open_entry, "inlineCommandSurfaceVisible", "InlineCommandSurfaceVisible")):
            reasons.append("02-menu-open-light.png must not render the inline command surface.")

    new_character_entries = [
        entry for entry in control_entries
        if "new-character" in str(_entry_value(entry, "screenshot", "Screenshot") or "").strip()
        or str(_entry_value(entry, "dialogTitle", "DialogTitle") or "").strip() == "Select Build Method"
    ]
    if not new_character_entries:
        reasons.append("Screenshot control evidence is missing a New Character workflow visual proof entry.")
    else:
        for entry in new_character_entries:
            screenshot_name = str(_entry_value(entry, "screenshot", "Screenshot") or "").strip() or "<unknown>"
            if _bool_value(_entry_value(entry, "rightShellVisible", "RightShellVisible")):
                reasons.append(f"{screenshot_name} must not show the inline right shell during New Character.")
            right_shell_width = _float_value(_entry_value(entry, "rightShellWidth", "RightShellWidth"))
            if right_shell_width is None:
                reasons.append(f"{screenshot_name} is missing rightShellWidth evidence.")
            elif right_shell_width > 1.0:
                reasons.append(f"{screenshot_name} must keep rightShellWidth collapsed during New Character.")
            if _bool_value(_entry_value(entry, "inlineCommandSurfaceVisible", "InlineCommandSurfaceVisible")):
                reasons.append(f"{screenshot_name} must not render the inline command surface during New Character.")
            if not _bool_value(_entry_value(entry, "dialogWindowVisible", "DialogWindowVisible")):
                reasons.append(f"{screenshot_name} must prove the dedicated desktop dialog window is visible.")

    rows = _coerce_rows(contact_sheets.get("rows"), reasons)
    if not rows:
        reasons.append("side-by-side contact sheet rows are missing.")

    current_refs: list[str] = []
    current_refs_by_basename: set[str] = set()
    missing_screenshot_refs: list[str] = []

    for row in rows:
        sheet_id = str(row.get("sheetId")).strip()
        sheet_path_raw = row.get("sheetPath")
        sheet_path_error = _invalid_path_value(sheet_path_raw)
        if sheet_path_error is not None:
            reasons.append(f"contact sheet '{sheet_id}' has invalid sheetPath: {sheet_path_error}.")
        else:
            sheet_path = Path(str(sheet_path_raw).strip())
            resolved_sheet = _resolve_reference(sheet_path)
            if not resolved_sheet.is_file():
                reasons.append(f"contact sheet '{sheet_id}' points to missing sheet file: {sheet_path_raw}")

        legacy_refs = row.get("legacyAnchorRefs")
        if isinstance(legacy_refs, list):
            for legacy_ref in legacy_refs:
                if not isinstance(legacy_ref, str) or not legacy_ref.strip():
                    reasons.append(f"contact sheet '{sheet_id}' has invalid legacyAnchorRef entry.")
                    continue
                legacy_error = _invalid_path_value(legacy_ref)
                if legacy_error is not None:
                    reasons.append(f"contact sheet '{sheet_id}' has invalid legacyAnchorRef '{legacy_ref}': {legacy_error}.")
                    continue
                legacy_path = Path(legacy_ref.strip())
                legacy_path = _resolve_reference(legacy_path)
                if legacy_path.is_absolute() and not legacy_path.is_file():
                    warnings.append(
                        f"legacyAnchorRef for '{sheet_id}' does not point to an existing file yet remains documented: {legacy_ref}"
                    )
        else:
            reasons.append(f"contact sheet '{sheet_id}' missing legacyAnchorRefs list.")

        row_current_refs = row.get("currentScreenshotRefs")
        if not isinstance(row_current_refs, list) or not row_current_refs:
            reasons.append(f"contact sheet '{sheet_id}' has missing currentScreenshotRefs.")
            continue

        row_seen: set[str] = set()
        for current_ref in row_current_refs:
            if not isinstance(current_ref, str):
                reasons.append(f"contact sheet '{sheet_id}' has non-string currentScreenshotRef.")
                continue
            current_ref = current_ref.strip()
            if not current_ref:
                reasons.append(f"contact sheet '{sheet_id}' has a blank currentScreenshotRef.")
                continue
            if current_ref in row_seen:
                reasons.append(f"contact sheet '{sheet_id}' repeats screenshot ref '{current_ref}'.")
            row_seen.add(current_ref)
            current_refs.append(current_ref)

            resolved_ref, error = _coerce_artifact_path(current_ref, strict_screenshot_scope=True)
            if error is not None:
                reasons.append(
                    f"contact sheet '{sheet_id}' has invalid currentScreenshotRef '{current_ref}': {error}."
                )
                continue
            if resolved_ref is None:
                missing_screenshot_refs.append(current_ref)
                continue

            if not resolved_ref.exists():
                missing_screenshot_refs.append(current_ref)
            else:
                current_refs_by_basename.add(resolved_ref.name)

    missing_required = sorted(set(required_new_gate_screenshots) - set(screenshot_files))
    if missing_required:
        reasons.append(f"missing required screenshot comparisons: {', '.join(missing_required)}.")
    if not required_new_gate_screenshots:
        reasons.append("required screenshot list in screenshotAssetReview is empty.")

    screenshot_matrix_rows = screenshot_matrix.get("rows")
    if not isinstance(screenshot_matrix_rows, list) or not screenshot_matrix_rows:
        reasons.append("CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json is missing rows.")
    else:
        parity_matrix_screenshots = {
            Path(str(reference).strip()).name
            for row in screenshot_matrix_rows
            if isinstance(row, dict)
            for reference in row.get("screenshot_refs", [])
            if isinstance(reference, str) and Path(reference.strip()).name
        }
        missing_from_disk = sorted(parity_matrix_screenshots - set(screenshot_files))
        if missing_from_disk:
            reasons.append(
                "human parity screenshot matrix references screenshots missing on disk: "
                + ", ".join(missing_from_disk)
            )

    required_screenshot_refs = []
    for required in required_new_gate_screenshots:
        candidate = Path(required.strip())
        if candidate.parent == Path("."):
            required_screenshot_refs.append(str(screenshot_directory / candidate))
        else:
            required_screenshot_refs.append(required)

    required_basename_refs = {
        Path(ref).name if str(ref).strip() else ""
        for ref in required_screenshot_refs
        if Path(ref).name
    }
    missing_required_from_current = sorted(required_basename_refs - current_refs_by_basename)
    if missing_required_from_current:
        reasons.append(
            "required screenshots are not actively referenced by current screenshot refs: "
            + ", ".join(missing_required_from_current)
        )

    if missing_screenshot_refs:
        missing_unique = sorted(set(missing_screenshot_refs))
        reasons.append(f"missing screenshot references in contact sheet rows: {', '.join(missing_unique)}.")

    # Cross-check every referenced screenshot path from contact sheets, and every explicitly required screenshot.
    _ensure_paths_exist(
        references=required_screenshot_refs,
        label="required screenshot",
        missing=missing_screenshot_refs,
        reasons=reasons,
        screenshot_directory=screenshot_directory,
    )

    # Validate all references resolve to real files in the screenshot directory.
    for reference in current_refs:
        if isinstance(reference, str):
            normalized = reference.strip()
            if not normalized:
                continue
            resolved_ref, error = _coerce_artifact_path(
                normalized,
                screenshot_directory=screenshot_directory,
            )
            if error is not None:
                continue
            assert resolved_ref is not None
            if not _is_within_directory(resolved_ref, screenshot_directory):
                reasons.append(f"reference '{normalized}' resolves outside screenshot directory: {resolved_ref}")

    if warnings:
        # Keep warnings visible for gate reviewers; they should not fail coverage by default.
        pass

    status = "pass" if not reasons else "fail"
    payload = {
        "contract_name": (
            "chummer6-ui.chummer5a_local_screenshot_comparison_gate"
            if comparison_scope_is_local_only
            else "chummer6-ui.pixefy_chummer5a_screenshot_comparison_gate"
        ),
        "generated_at": _now_iso(),
        "status": status,
        "provider": "local_authority_receipts" if comparison_scope_is_local_only else "Pixefy",
        "scope": comparison_scope,
        "comparison_baseline": (
            "Chummer5a local screenshot/contact-sheet receipts"
            if comparison_scope_is_local_only
            else "Chummer5a screenshot/contact-sheet receipts"
        ),
        "screenshot_directory": _receipt_path(screenshot_directory),
        "screenshot_count": len(screenshot_files),
        "required_screenshots": sorted(required_new_gate_screenshots),
        "found_required_screenshots": sorted(set(required_new_gate_screenshots).intersection(set(screenshot_files))),
        "contact_sheet_rows": len(rows),
        "missing_required_count": len(missing_required),
        "current_ref_count": len(current_refs),
        "current_ref_unique_count": len(set(current_refs)),
        "receipts": {
            "screenshot_review": _receipt_path(screenshot_review_path),
            "side_by_side_contact_sheets": _receipt_path(contact_sheets_path),
            "screenshot_control_evidence": _receipt_path(screenshot_control_evidence_path),
            "human_parity_screenshot_matrix": _receipt_path(screenshot_matrix_path),
        },
        "reasons": reasons,
    }
    if not comparison_scope_is_local_only:
        payload["receipts"]["pixefy_targets"] = _receipt_path(pixefy_targets_path)
        payload["receipts"]["flagship_gate"] = _receipt_path(flagship_gate_path)
    if warnings:
        payload["warnings"] = sorted(set(warnings))

    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        raise SystemExit("; ".join(reasons))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
