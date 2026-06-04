#!/usr/bin/env python3
from __future__ import annotations

import json
from urllib.parse import unquote, urlparse
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
RECEIPT = PUBLISHED / "PIXEFY_CHUMMER5A_SCREENSHOT_COMPARISON_GATE.generated.json"
DEFAULT_SCREENSHOT_DIR = PUBLISHED / "ui-flagship-release-gate-screenshots"
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


def main() -> int:
    PUBLISHED.mkdir(parents=True, exist_ok=True)
    reasons: list[str] = []
    warnings: list[str] = []

    pixefy_targets_path = PUBLISHED / "PUBLIC_SURFACE_QA_TARGETS.generated.json"
    screenshot_review_path = PUBLISHED / "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"
    contact_sheets_path = PUBLISHED / "CHUMMER5A_SIDE_BY_SIDE_CONTACT_SHEETS.generated.json"
    flagship_gate_path = PUBLISHED / "UI_FLAGSHIP_RELEASE_GATE.generated.json"
    screenshot_control_evidence_path = PUBLISHED / "ui-flagship-release-gate-screenshots" / "SCREENSHOT_CONTROL_EVIDENCE.generated.json"
    screenshot_matrix_path = PUBLISHED / "CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json"

    for path in [
        pixefy_targets_path,
        screenshot_review_path,
        contact_sheets_path,
        flagship_gate_path,
        screenshot_control_evidence_path,
        screenshot_matrix_path,
    ]:
        if not path.is_file():
            reasons.append(f"missing required receipt: {_receipt_path(path)}")

    pixefy_targets = _load_json(pixefy_targets_path) if pixefy_targets_path.is_file() else {}
    screenshot_review = _load_json(screenshot_review_path) if screenshot_review_path.is_file() else {}
    contact_sheets = _load_json(contact_sheets_path) if contact_sheets_path.is_file() else {}
    flagship_gate = _load_json(flagship_gate_path) if flagship_gate_path.is_file() else {}
    screenshot_control_evidence = _load_json(screenshot_control_evidence_path) if screenshot_control_evidence_path.is_file() else {}
    screenshot_matrix = _load_json(screenshot_matrix_path) if screenshot_matrix_path.is_file() else {}

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
    if not _status_is_pass(flagship_gate):
        reasons.append("UI_FLAGSHIP_RELEASE_GATE.generated.json is not passing.")
    if str(screenshot_control_evidence.get("generatedAt") or "").strip() == "":
        reasons.append("SCREENSHOT_CONTROL_EVIDENCE.generated.json is missing generatedAt.")
    if not _status_is_pass(screenshot_matrix):
        reasons.append("CHUMMER5A_HUMAN_PARITY_SCREENSHOT_MATRIX.generated.json is not passing.")

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
        str(entry.get("screenshot") or "").strip()
        for entry in control_entries
        if isinstance(entry, dict) and str(entry.get("screenshot") or "").strip()
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
        "contract_name": "chummer6-ui.pixefy_chummer5a_screenshot_comparison_gate",
        "generated_at": _now_iso(),
        "status": status,
        "provider": "Pixefy",
        "comparison_baseline": "Chummer5a screenshot/contact-sheet receipts",
        "screenshot_directory": _receipt_path(screenshot_directory),
        "screenshot_count": len(screenshot_files),
        "required_screenshots": sorted(required_new_gate_screenshots),
        "found_required_screenshots": sorted(set(required_new_gate_screenshots).intersection(set(screenshot_files))),
        "contact_sheet_rows": len(rows),
        "missing_required_count": len(missing_required),
        "current_ref_count": len(current_refs),
        "current_ref_unique_count": len(set(current_refs)),
        "receipts": {
            "pixefy_targets": _receipt_path(pixefy_targets_path),
            "screenshot_review": _receipt_path(screenshot_review_path),
            "side_by_side_contact_sheets": _receipt_path(contact_sheets_path),
            "flagship_gate": _receipt_path(flagship_gate_path),
            "screenshot_control_evidence": _receipt_path(screenshot_control_evidence_path),
            "human_parity_screenshot_matrix": _receipt_path(screenshot_matrix_path),
        },
        "reasons": reasons,
    }
    if warnings:
        payload["warnings"] = sorted(set(warnings))

    RECEIPT.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        raise SystemExit("; ".join(reasons))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
