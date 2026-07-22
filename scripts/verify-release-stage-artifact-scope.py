#!/usr/bin/env python3
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import subprocess
import sys
from pathlib import Path
from typing import Any


DESKTOP_ARTIFACT_FILE_RE = re.compile(
    r"^chummer-.*(?:\.exe|\.zip|\.tar\.gz|\.deb|\.pkg|\.dmg|\.msix|\.zip\.json)$",
    re.IGNORECASE,
)


def load_publication_scope_module():
    helper_path = Path(__file__).resolve().with_name(
        "preview_nightly_publication_scope.py"
    )
    spec = importlib.util.spec_from_file_location(
        "preview_nightly_publication_scope_artifact_gate", helper_path
    )
    if spec is None or spec.loader is None:
        raise SystemExit("could not load Windows-only publication-scope helper")
    module = importlib.util.module_from_spec(spec)
    sys.modules.setdefault(spec.name, module)
    spec.loader.exec_module(module)
    return module


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Verify that a staged release files directory contains only desktop artifacts "
            "declared by the staged release manifest, plus declared Windows bootstrap payload sidecars."
        )
    )
    parser.add_argument(
        "--manifest",
        action="append",
        type=Path,
        default=[],
        help="Release manifest to use as artifact truth. Can be passed more than once.",
    )
    parser.add_argument("--files-dir", type=Path, required=True, help="Staged downloads/files directory.")
    parser.add_argument(
        "--startup-smoke-dir",
        type=Path,
        help="Optional startup-smoke receipt directory to scope to manifest artifacts.",
    )
    parser.add_argument("--publication-scope", type=Path)
    parser.add_argument("--publication-proposal", type=Path)
    parser.add_argument("--publication-dir", type=Path)
    parser.add_argument("--sealed-stage-root", type=Path)
    parser.add_argument(
        "--require-windows-only-publication-scope", action="store_true"
    )
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        raise SystemExit(f"failed to read JSON manifest {path}: {exc}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"manifest must be a JSON object: {path}")
    return payload


def basename_from_row(row: dict[str, Any]) -> str:
    file_name = str(row.get("fileName") or "").strip()
    if file_name:
        return Path(file_name).name
    for key in ("downloadUrl", "url"):
        value = str(row.get(key) or "").strip()
        if value:
            return Path(value).name
    return ""


def payload_names_for_row(row: dict[str, Any], artifact_file_name: str) -> set[str]:
    names: set[str] = set()
    explicit = str(row.get("payloadFileName") or "").strip()
    if explicit:
        names.add(Path(explicit).name)

    if artifact_file_name.lower().endswith("-installer.exe"):
        names.add(f"{artifact_file_name[:-len('-installer.exe')]}-payload.zip")

    return {name for name in names if name}


def manifest_scope(manifest_paths: list[Path]) -> tuple[set[str], set[str]]:
    artifact_names: set[str] = set()
    allowed_file_names: set[str] = set()
    seen_manifest = False

    for manifest_path in manifest_paths:
        if not manifest_path.is_file():
            continue
        seen_manifest = True
        payload = load_json(manifest_path)
        rows: list[dict[str, Any]] = []
        for key in ("artifacts", "downloads"):
            rows.extend(row for row in payload.get(key) or [] if isinstance(row, dict))

        for row in rows:
            artifact_file_name = basename_from_row(row)
            if not artifact_file_name:
                continue
            artifact_names.add(artifact_file_name)
            allowed_file_names.add(artifact_file_name)
            for payload_name in payload_names_for_row(row, artifact_file_name):
                allowed_file_names.add(payload_name)
                allowed_file_names.add(f"{payload_name}.json")

    if not seen_manifest:
        manifest_text = ", ".join(str(path) for path in manifest_paths) or "<none>"
        raise SystemExit(f"no readable release manifest was provided: {manifest_text}")

    return artifact_names, allowed_file_names


def is_desktop_artifact_like(file_name: str) -> bool:
    return DESKTOP_ARTIFACT_FILE_RE.fullmatch(file_name) is not None


def receipt_artifact_file_name(receipt: dict[str, Any]) -> str:
    for key in ("artifactFileName", "fileName", "artifact_file_name", "file_name"):
        value = str(receipt.get(key) or "").strip()
        if value:
            return Path(value).name
    for key in ("artifactPath", "artifact_path", "artifactRelativePath", "artifact_relative_path"):
        value = str(receipt.get(key) or "").strip()
        if value:
            return Path(value).name
    return ""


def verify_files_dir(files_dir: Path, allowed_file_names: set[str]) -> list[str]:
    failures: list[str] = []
    if not files_dir.is_dir():
        return [f"files directory is missing: {files_dir}"]

    for path in sorted(files_dir.iterdir()):
        if not path.is_file():
            continue
        file_name = path.name
        if not is_desktop_artifact_like(file_name):
            continue
        if file_name not in allowed_file_names:
            failures.append(f"unmanifested staged desktop artifact: files/{file_name}")

    return failures


def verify_startup_smoke_dir(startup_smoke_dir: Path | None, artifact_names: set[str]) -> list[str]:
    failures: list[str] = []
    if startup_smoke_dir is None or not startup_smoke_dir.exists():
        return failures
    if not startup_smoke_dir.is_dir():
        return [f"startup-smoke path is not a directory: {startup_smoke_dir}"]

    for receipt_path in sorted(startup_smoke_dir.glob("startup-smoke-*.receipt.json")):
        try:
            receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
        except Exception as exc:
            failures.append(f"startup-smoke receipt is unreadable: {receipt_path.name}: {exc}")
            continue
        if not isinstance(receipt, dict):
            failures.append(f"startup-smoke receipt is not a JSON object: {receipt_path.name}")
            continue
        artifact_file_name = receipt_artifact_file_name(receipt)
        if artifact_file_name and artifact_file_name not in artifact_names:
            failures.append(
                f"startup-smoke receipt references an unmanifested artifact: "
                f"{receipt_path.name} -> {artifact_file_name}"
            )

    return failures


def main() -> int:
    args = parse_args()
    scope_requested = args.require_windows_only_publication_scope or any(
        value is not None
        for value in (
            args.publication_scope,
            args.publication_proposal,
            args.publication_dir,
            args.sealed_stage_root,
        )
    )
    if scope_requested:
        if any(
            value is None
            for value in (
                args.publication_scope,
                args.publication_proposal,
                args.publication_dir,
                args.sealed_stage_root,
            )
        ):
            print(
                "release_stage_artifact_scope:fail\n"
                " - Windows-only publication requires scope, proposal, and publication directory",
                file=sys.stderr,
            )
            return 1
        stage_root = args.sealed_stage_root.resolve()
        if stage_root != args.publication_dir.resolve().parent:
            print("release_stage_artifact_scope:fail", file=sys.stderr)
            print(
                " - sealed stage root is not the parent of the composed publication shelf",
                file=sys.stderr,
            )
            return 1
        stage_helper = Path(__file__).resolve().with_name(
            "preview_nightly_stage_contract.py"
        )
        sealed = subprocess.run(
            [
                sys.executable,
                str(stage_helper),
                "verify",
                "--stage-dir",
                str(stage_root),
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        if sealed.returncode != 0:
            print("release_stage_artifact_scope:fail", file=sys.stderr)
            print(" - sealed stage verification failed", file=sys.stderr)
            if sealed.stderr.strip():
                print(f" - {sealed.stderr.strip()}", file=sys.stderr)
            return 1
        scope = load_publication_scope_module()
        try:
            scope.verify_scope(
                argparse.Namespace(
                    scope=args.publication_scope,
                    proposal=args.publication_proposal,
                    publication_dir=args.publication_dir,
                    evidence_root=stage_root,
                )
            )
        except (OSError, scope.ScopeError) as exc:
            print("release_stage_artifact_scope:fail", file=sys.stderr)
            print(f" - invalid Windows-only publication shelf: {exc}", file=sys.stderr)
            return 1
        expected_manifests = {
            (args.publication_dir / scope.CANONICAL_MANIFEST_NAME).resolve(),
            (args.publication_dir / scope.COMPATIBILITY_MANIFEST_NAME).resolve(),
        }
        supplied_manifests = {
            path.resolve() for path in args.manifest if path.is_file()
        }
        if (
            supplied_manifests != expected_manifests
            or args.files_dir.resolve()
            != (args.publication_dir / "files").resolve()
        ):
            print("release_stage_artifact_scope:fail", file=sys.stderr)
            print(
                " - artifact gate inputs are not the exact composed publication shelf",
                file=sys.stderr,
            )
            return 1
    artifact_names, allowed_file_names = manifest_scope(args.manifest)
    failures = verify_files_dir(args.files_dir, allowed_file_names)
    failures.extend(verify_startup_smoke_dir(args.startup_smoke_dir, artifact_names))

    if failures:
        print("release_stage_artifact_scope:fail", file=sys.stderr)
        for failure in failures:
            print(f" - {failure}", file=sys.stderr)
        return 1

    checked_files = sum(
        1
        for path in args.files_dir.iterdir()
        if path.is_file() and is_desktop_artifact_like(path.name)
    )
    checked_receipts = (
        sum(1 for _ in args.startup_smoke_dir.glob("startup-smoke-*.receipt.json"))
        if args.startup_smoke_dir is not None and args.startup_smoke_dir.is_dir()
        else 0
    )
    print(f"release_stage_artifact_scope:ok checked_files={checked_files} checked_receipts={checked_receipts}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
