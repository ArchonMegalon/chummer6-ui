#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any


PLATFORM_ROW_RE = re.compile(r"^\s{2}-\s+id:\s*(.+?)\s*$")
PLATFORM_FIELD_RE = re.compile(r"^\s{4}([A-Za-z0-9_]+):\s*(.*?)\s*$")
BLOCKED_PROMOTION_STATES = {
    "blocked",
    "hidden",
    "proof_required",
    "quarantined",
    "support_only",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Verify that a public nightly stage contains at least one public-eligible "
            "Windows or Linux installer allowed by the shared desktop platform policy."
        )
    )
    parser.add_argument("--manifest", type=Path, required=True, help="Canonical staged release manifest.")
    parser.add_argument("--files-dir", type=Path, required=True, help="Staged downloads/files directory.")
    parser.add_argument(
        "--platform-policy",
        type=Path,
        required=True,
        help="Shared DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml path.",
    )
    return parser.parse_args()


def normalized(value: object) -> str:
    return str(value or "").strip().lower()


def yaml_scalar(value: str) -> str:
    # The policy fields consumed here are deliberately scalar-only. Keeping this
    # parser in the standard library avoids adding a release-host YAML dependency.
    scalar = value.split(" #", 1)[0].strip()
    if len(scalar) >= 2 and scalar[0] == scalar[-1] and scalar[0] in {"'", '"'}:
        scalar = scalar[1:-1]
    return scalar


def load_promoted_platform_policy(path: Path) -> dict[str, dict[str, str]]:
    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except Exception as exc:
        raise SystemExit(f"failed to read desktop platform acceptance policy {path}: {exc}") from exc

    in_platforms = False
    current: dict[str, str] | None = None
    rows: list[dict[str, str]] = []

    for line in lines:
        if not in_platforms:
            if line.strip() == "platforms:":
                in_platforms = True
            continue

        if line and not line[0].isspace():
            break

        row_match = PLATFORM_ROW_RE.match(line)
        if row_match:
            if current is not None:
                rows.append(current)
            current = {"id": yaml_scalar(row_match.group(1))}
            continue

        field_match = PLATFORM_FIELD_RE.match(line)
        if field_match and current is not None:
            current[field_match.group(1)] = yaml_scalar(field_match.group(2))

    if current is not None:
        rows.append(current)
    if not in_platforms or not rows:
        raise SystemExit(f"desktop platform acceptance policy has no platforms list: {path}")

    promoted: dict[str, dict[str, str]] = {}
    for row in rows:
        platform_id = normalized(row.get("id"))
        if platform_id not in {"windows", "linux"}:
            continue
        if normalized(row.get("public_shelf_status")) != "promoted_release":
            continue
        package_kind = normalized(row.get("primary_package_kind"))
        if not package_kind:
            raise SystemExit(
                f"promoted platform {platform_id!r} is missing primary_package_kind in {path}"
            )
        promoted[platform_id] = {
            "primary_package_kind": package_kind,
            "public_shelf_status": "promoted_release",
        }

    if not promoted:
        raise SystemExit(
            f"desktop platform acceptance policy has no promoted Windows/Linux release platform: {path}"
        )
    return promoted


def load_manifest(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        raise SystemExit(f"failed to read canonical release manifest {path}: {exc}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"canonical release manifest must be a JSON object: {path}")
    return payload


def manifest_rows(payload: dict[str, Any]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    seen: set[tuple[str, str]] = set()
    for key in ("artifacts", "downloads"):
        for row in payload.get(key) or []:
            if not isinstance(row, dict):
                continue
            artifact_id = normalized(row.get("artifactId") or row.get("id"))
            file_name = str(row.get("fileName") or "").strip()
            identity = (artifact_id, file_name)
            if identity in seen:
                continue
            seen.add(identity)
            rows.append(row)
    return rows


def row_matches_package_policy(row: dict[str, Any], package_kind: str, file_name: str) -> bool:
    kind = normalized(row.get("kind") or row.get("flavor"))
    format_name = normalized(row.get("format"))
    suffixes = "".join(Path(file_name).suffixes).lower()

    if kind not in {"installer", "msix", "deb"}:
        return False
    if package_kind == "installer":
        return kind == "installer"
    if package_kind == "deb":
        return format_name == "deb" or kind == "deb" or suffixes.endswith(".deb")
    if package_kind == "msix":
        return format_name == "msix" or kind == "msix" or suffixes.endswith(".msix")
    return package_kind in {kind, format_name}


def eligible_row(
    row: dict[str, Any],
    promoted: dict[str, dict[str, str]],
    files_dir: Path,
) -> tuple[bool, str]:
    artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
    platform_id = normalized(row.get("platformId") or row.get("platform"))
    file_name = str(row.get("fileName") or "").strip()

    if not artifact_id:
        return False, "artifact id is missing"
    if platform_id not in promoted:
        return False, f"platform {platform_id or '<missing>'} is not a promoted Windows/Linux release platform"
    if normalized(row.get("installAccessClass")) != "open_public":
        return False, "installAccessClass is not open_public"
    compatibility_state = normalized(row.get("compatibilityState"))
    if compatibility_state and compatibility_state != "compatible":
        return False, f"compatibilityState is {compatibility_state}"
    promotion_state = normalized(row.get("promotionState"))
    if promotion_state in BLOCKED_PROMOTION_STATES:
        return False, f"promotionState is {promotion_state}"
    if not file_name or Path(file_name).name != file_name:
        return False, "fileName is missing or is not a basename"

    package_kind = promoted[platform_id]["primary_package_kind"]
    if not row_matches_package_policy(row, package_kind, file_name):
        return False, f"artifact does not match primary package kind {package_kind}"
    if not (files_dir / file_name).is_file():
        return False, f"staged artifact bytes are missing: files/{file_name}"
    return True, ""


def main() -> int:
    args = parse_args()
    if not args.files_dir.is_dir():
        print(
            f"public_nightly_installer_eligibility:fail files directory is missing: {args.files_dir}",
            file=sys.stderr,
        )
        return 1

    promoted = load_promoted_platform_policy(args.platform_policy)
    payload = load_manifest(args.manifest)
    rejected: list[str] = []

    for row in manifest_rows(payload):
        accepted, reason = eligible_row(row, promoted, args.files_dir)
        artifact_id = str(row.get("artifactId") or row.get("id") or "<unnamed>").strip()
        if accepted:
            platform_id = normalized(row.get("platformId") or row.get("platform"))
            print(
                "public_nightly_installer_eligibility:ok "
                f"artifact_id={artifact_id} platform={platform_id}"
            )
            return 0
        rejected.append(f"{artifact_id or '<unnamed>'}: {reason}")

    print("public_nightly_installer_eligibility:fail", file=sys.stderr)
    print(
        " - no staged artifact is an open-public installer for a Windows/Linux "
        "platform whose shared public_shelf_status is promoted_release",
        file=sys.stderr,
    )
    for reason in rejected[:8]:
        print(f" - {reason}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
