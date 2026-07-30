#!/usr/bin/env python3
"""Verify promoted desktop artifact sizes against immutable byte budgets."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import tempfile
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = ROOT.parent
DEFAULT_DOWNLOADS_ROOT = WORKSPACE_ROOT / "chummer.run-services" / "Chummer.Portal" / "downloads"
DEFAULT_MANIFEST = DEFAULT_DOWNLOADS_ROOT / "releases.json"
DEFAULT_FILES_DIR = DEFAULT_DOWNLOADS_ROOT / "files"
DEFAULT_OUTPUT = ROOT / ".codex-studio" / "published" / "DESKTOP_ARTIFACT_SIZE_BUDGET.generated.json"
CONTRACT_NAME = "chummer6-ui.desktop_artifact_size_budget.v1"

MIB = 1024 * 1024
ARTIFACT_BUDGETS: dict[str, dict[str, Any]] = {
    "avalonia-linux-x64-installer": {
        "head": "avalonia",
        "platform": "linux",
        "rid": "linux-x64",
        "arch": "x64",
        "installer_max_bytes": 48 * MIB,
        "payload_max_bytes": None,
        "requires_bootstrap_payload": False,
    },
    "avalonia-win-x64-installer": {
        "head": "avalonia",
        "platform": "windows",
        "rid": "win-x64",
        "arch": "x64",
        "installer_max_bytes": 8 * MIB,
        "payload_max_bytes": 64 * MIB,
        "requires_bootstrap_payload": True,
    },
}
AGGREGATE_MAX_BYTES = 112 * MIB
STARTUP_TIME_BUDGET_POSTURE = {
    "status": "not_enforced",
    "reason_code": "receipt_timer_starts_inside_smoke_handler_after_process_entry",
    "source_path": "chummer-presentation/Chummer.Desktop.Runtime/DesktopStartupSmokeRuntime.cs",
    "claim": "Existing startedAtUtc/completedAtUtc fields measure receipt-handler work, not process launch-to-ready latency.",
}


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--files-dir", type=Path, default=DEFAULT_FILES_DIR)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="verify current bytes without mutating the materialized receipt",
    )
    return parser.parse_args()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _positive_int(value: object) -> int | None:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        return None
    return value


def _valid_sha256(value: object) -> str | None:
    normalized = str(value or "").strip().lower()
    if len(normalized) != 64 or any(character not in "0123456789abcdef" for character in normalized):
        return None
    return normalized


def _portable_path(path: Path) -> str:
    resolved = path.resolve()
    try:
        return str(resolved.relative_to(WORKSPACE_ROOT))
    except ValueError:
        return "<external-test-fixture>"


def _resolve_owned_file(files_dir: Path, file_name: object) -> tuple[Path | None, str | None]:
    normalized = str(file_name or "").strip()
    if not normalized:
        return None, "file name is missing"
    if Path(normalized).name != normalized or "/" in normalized or "\\" in normalized:
        return None, f"unsafe artifact file name: {normalized!r}"
    candidate = files_dir / normalized
    try:
        resolved = candidate.resolve(strict=True)
    except (FileNotFoundError, OSError) as exc:
        return None, f"artifact is missing: {normalized} ({exc.__class__.__name__})"
    if candidate.is_symlink() or not resolved.is_relative_to(files_dir):
        return None, f"artifact escapes the owned files directory: {normalized}"
    if not resolved.is_file():
        return None, f"artifact is not a regular file: {normalized}"
    return resolved, None


def _measure_bound_file(
    *,
    files_dir: Path,
    file_name: object,
    expected_size: object,
    expected_sha256: object,
    maximum_bytes: int,
    label: str,
    failures: list[str],
) -> dict[str, object] | None:
    expected_size_value = _positive_int(expected_size)
    expected_digest = _valid_sha256(expected_sha256)
    if expected_size_value is None:
        failures.append(f"{label} manifest size is missing or invalid")
    if expected_digest is None:
        failures.append(f"{label} manifest sha256 is missing or invalid")

    path, path_error = _resolve_owned_file(files_dir, file_name)
    if path_error:
        failures.append(f"{label} {path_error}")
        return None
    assert path is not None
    actual_size = path.stat().st_size
    actual_digest = _sha256_file(path)
    if expected_size_value is not None and actual_size != expected_size_value:
        failures.append(
            f"{label} size {actual_size} does not match manifest {expected_size_value}"
        )
    if expected_digest is not None and actual_digest != expected_digest:
        failures.append(f"{label} sha256 does not match manifest")
    if actual_size > maximum_bytes:
        failures.append(f"{label} size {actual_size} exceeds budget {maximum_bytes}")
    return {
        "file_name": path.name,
        "size_bytes": actual_size,
        "sha256": actual_digest,
        "manifest_size_bytes": expected_size_value,
        "manifest_sha256": expected_digest,
        "maximum_bytes": maximum_bytes,
        "within_budget": actual_size <= maximum_bytes,
        "manifest_identity_matches": (
            expected_size_value == actual_size and expected_digest == actual_digest
        ),
    }


def evaluate(manifest_path: Path, files_dir: Path) -> dict[str, object]:
    manifest_path = manifest_path.resolve()
    files_dir = files_dir.resolve()
    failures: list[str] = []
    artifacts: dict[str, dict[str, object]] = {}
    manifest: dict[str, object] = {}

    if not manifest_path.is_file() or manifest_path.is_symlink():
        failures.append("authoritative release manifest is missing or is not a regular owned file")
    else:
        try:
            loaded = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
            if isinstance(loaded, dict):
                manifest = loaded
            else:
                failures.append("authoritative release manifest root must be an object")
        except (OSError, json.JSONDecodeError) as exc:
            failures.append(f"authoritative release manifest is unreadable: {exc.__class__.__name__}")

    downloads = manifest.get("downloads")
    rows = downloads if isinstance(downloads, list) else []
    if not isinstance(downloads, list):
        failures.append("authoritative release manifest downloads must be a list")

    desktop_rows: dict[str, dict[str, object]] = {}
    duplicate_ids: set[str] = set()
    for row in rows:
        if not isinstance(row, dict):
            failures.append("release manifest downloads contains a non-object row")
            continue
        platform = str(row.get("platform") or row.get("platformId") or "").strip().lower()
        if platform.startswith("linux-"):
            platform = "linux"
        elif platform.startswith("windows-") or platform.startswith("win-"):
            platform = "windows"
        elif platform.startswith("macos-") or platform.startswith("osx-"):
            platform = "macos"
        kind = str(row.get("kind") or "").strip().lower()
        head = str(row.get("head") or "").strip().lower()
        if platform not in {"linux", "windows", "macos"} or kind != "installer" or not head:
            continue
        artifact_id = str(row.get("id") or row.get("artifactId") or "").strip()
        if not artifact_id:
            failures.append("desktop installer row is missing id")
            continue
        if artifact_id in desktop_rows:
            duplicate_ids.add(artifact_id)
        desktop_rows[artifact_id] = row

    if duplicate_ids:
        failures.append(f"duplicate desktop artifact ids: {sorted(duplicate_ids)}")
    actual_ids = set(desktop_rows)
    expected_ids = set(ARTIFACT_BUDGETS)
    if actual_ids != expected_ids:
        failures.append(
            "desktop artifact budget coverage drifted "
            f"(missing={sorted(expected_ids - actual_ids)}, unbudgeted={sorted(actual_ids - expected_ids)})"
        )

    tuple_coverage = manifest.get("desktopTupleCoverage")
    promoted_tuples = (
        tuple_coverage.get("promotedInstallerTuples")
        if isinstance(tuple_coverage, dict)
        else None
    )
    tuple_rows = promoted_tuples if isinstance(promoted_tuples, list) else []
    if not isinstance(promoted_tuples, list):
        failures.append("desktopTupleCoverage.promotedInstallerTuples must be a list")
    tuple_by_artifact: dict[str, dict[str, object]] = {}
    duplicate_tuple_ids: set[str] = set()
    for tuple_row in tuple_rows:
        if not isinstance(tuple_row, dict):
            failures.append("promotedInstallerTuples contains a non-object row")
            continue
        artifact_id = str(tuple_row.get("artifactId") or "").strip()
        if not artifact_id:
            failures.append("promoted installer tuple is missing artifactId")
            continue
        if artifact_id in tuple_by_artifact:
            duplicate_tuple_ids.add(artifact_id)
        tuple_by_artifact[artifact_id] = tuple_row
    if duplicate_tuple_ids:
        failures.append(f"duplicate promoted installer tuple artifact ids: {sorted(duplicate_tuple_ids)}")
    promoted_budget_ids = set(tuple_by_artifact) & expected_ids
    if promoted_budget_ids != expected_ids:
        failures.append(
            "promoted installer tuple coverage is incomplete "
            f"(missing={sorted(expected_ids - promoted_budget_ids)})"
        )

    aggregate_size = 0
    for artifact_id, budget in ARTIFACT_BUDGETS.items():
        row = desktop_rows.get(artifact_id)
        if row is None:
            continue
        for field, expected in {
            "head": budget["head"],
            "arch": budget["arch"],
            "kind": "installer",
        }.items():
            actual = str(row.get(field) or "").strip().lower()
            if actual != expected:
                failures.append(
                    f"{artifact_id} {field} {actual!r} does not match budget tuple {expected!r}"
                )
        row_platform = str(row.get("platform") or row.get("platformId") or "").strip().lower()
        if row_platform.startswith("linux-"):
            row_platform = "linux"
        elif row_platform.startswith("windows-") or row_platform.startswith("win-"):
            row_platform = "windows"
        elif row_platform.startswith("macos-") or row_platform.startswith("osx-"):
            row_platform = "macos"
        if row_platform != budget["platform"]:
            failures.append(
                f"{artifact_id} platform {row_platform!r} "
                f"does not match budget tuple {budget['platform']!r}"
            )
        tuple_row = tuple_by_artifact.get(artifact_id)
        if tuple_row is None:
            failures.append(f"{artifact_id} promoted installer tuple is missing")
        else:
            for field, expected in {
                "head": budget["head"],
                "platform": budget["platform"],
                "rid": budget["rid"],
                "arch": budget["arch"],
            }.items():
                actual = str(tuple_row.get(field) or "").strip().lower()
                if actual != expected:
                    failures.append(
                        f"{artifact_id} promoted tuple {field} {actual!r} "
                        f"does not match budget tuple {expected!r}"
                    )

        installer = _measure_bound_file(
            files_dir=files_dir,
            file_name=row.get("fileName"),
            expected_size=row.get("sizeBytes"),
            expected_sha256=row.get("sha256"),
            maximum_bytes=int(budget["installer_max_bytes"]),
            label=f"{artifact_id} installer",
            failures=failures,
        )
        if installer is not None:
            aggregate_size += int(installer["size_bytes"])

        requires_payload = bool(budget["requires_bootstrap_payload"])
        payload: dict[str, object] | None = None
        if requires_payload:
            if str(row.get("installerMode") or "").strip().lower() != "bootstrap":
                failures.append(f"{artifact_id} must remain a bootstrap installer")
            payload_maximum = budget.get("payload_max_bytes")
            if not isinstance(payload_maximum, int):
                failures.append(f"{artifact_id} payload budget is missing")
            else:
                payload = _measure_bound_file(
                    files_dir=files_dir,
                    file_name=row.get("payloadFileName"),
                    expected_size=row.get("payloadSizeBytes"),
                    expected_sha256=row.get("payloadSha256"),
                    maximum_bytes=payload_maximum,
                    label=f"{artifact_id} payload",
                    failures=failures,
                )
                if payload is not None:
                    aggregate_size += int(payload["size_bytes"])
        elif any(row.get(key) not in {None, ""} for key in ("payloadFileName", "payloadSizeBytes", "payloadSha256")):
            failures.append(f"{artifact_id} has an unbudgeted payload")

        artifacts[artifact_id] = {
            "tuple": {
                "head": budget["head"],
                "platform": budget["platform"],
                "rid": budget["rid"],
                "arch": budget["arch"],
            },
            "installer": installer,
            "payload": payload,
        }

    if aggregate_size > AGGREGATE_MAX_BYTES:
        failures.append(
            f"desktop promoted artifact aggregate {aggregate_size} exceeds budget {AGGREGATE_MAX_BYTES}"
        )

    generated_at = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")
    return {
        "contract_name": CONTRACT_NAME,
        "generated_at_utc": generated_at,
        "status": "pass" if not failures else "fail",
        "source_manifest": _portable_path(manifest_path),
        "source_manifest_sha256": _sha256_file(manifest_path) if manifest_path.is_file() else None,
        "release_version": manifest.get("releaseVersion") or manifest.get("version"),
        "channel": manifest.get("channel"),
        "artifact_budgets": ARTIFACT_BUDGETS,
        "aggregate_max_bytes": AGGREGATE_MAX_BYTES,
        "aggregate_observed_bytes": aggregate_size,
        "artifacts": artifacts,
        "startup_time_budget": STARTUP_TIME_BUDGET_POSTURE,
        "failures": failures,
    }


def _write_json_atomic(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
            json.dump(payload, stream, indent=2, sort_keys=True)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        temporary_path.unlink(missing_ok=True)


def main() -> int:
    args = _parse_args()
    payload = evaluate(args.manifest, args.files_dir)
    if not args.check_only:
        _write_json_atomic(args.output.resolve(), payload)
    print(
        "desktop_artifact_size_budget:"
        f"{payload['status']} bytes={payload['aggregate_observed_bytes']}/"
        f"{payload['aggregate_max_bytes']}"
    )
    for failure in payload["failures"]:
        print(f"- {failure}")
    return 0 if payload["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
