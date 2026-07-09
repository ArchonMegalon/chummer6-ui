#!/usr/bin/env python3
from __future__ import annotations

import json
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def resolve_portal_support_source_root(repo_root: Path) -> Path:
    candidates = (
        repo_root.parent / "chummer.run-services" / "Chummer.Portal" / "downloads",
        repo_root / "Chummer.Portal" / "downloads",
        repo_root / "Docker" / "Downloads",
    )
    for candidate in candidates:
        if (candidate / "RELEASE_CHANNEL.generated.json").is_file() and (candidate / "files").is_dir():
            return candidate
    raise SystemExit("Could not resolve a portal support source root with release-channel artifacts and files.")


def sync_startup_smoke_tree(source_root: Path, output_root: Path) -> None:
    source_dir = source_root / "startup-smoke"
    target_dir = output_root / "startup-smoke"
    if not source_dir.is_dir():
        return
    if source_dir.resolve() == target_dir.resolve(strict=False):
        return
    if target_dir.exists():
        shutil.rmtree(target_dir)
    shutil.copytree(source_dir, target_dir)


def cleanup_manifest_validation_audit(output_dir: Path) -> None:
    audit_dir = output_dir / "manifest-validation-audit"
    if audit_dir.is_dir():
        shutil.rmtree(audit_dir)


def copy_and_verify_mirror(
    repo_root: Path,
    source_manifest: Path,
    compat_source_manifest: Path,
    output_dir: Path,
    *,
    sync_output_startup_smoke_from: Path | None = None,
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_manifest, output_dir / "RELEASE_CHANNEL.generated.json")
    shutil.copy2(compat_source_manifest, output_dir / "releases.json")

    if sync_output_startup_smoke_from is not None:
        sync_startup_smoke_tree(sync_output_startup_smoke_from, output_dir)

    subprocess.run(
        [
            "bash",
            str(repo_root / "scripts" / "verify-releases-manifest.sh"),
            str(output_dir / "RELEASE_CHANNEL.generated.json"),
        ],
        check=True,
    )
    cleanup_manifest_validation_audit(output_dir)
    subprocess.run(
        [
            "bash",
            str(repo_root / "scripts" / "verify-releases-manifest.sh"),
            str(output_dir / "releases.json"),
        ],
        check=True,
    )


def main() -> int:
    repo_root = Path(__file__).absolute().parents[1]
    registry_root = (repo_root / "scripts" / "resolve-hub-registry-root.sh").resolve()
    resolved_registry_root = subprocess.run(
        ["bash", str(registry_root)],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    if not resolved_registry_root:
        raise SystemExit("Could not resolve hub-registry root.")
    registry_root_path = Path(resolved_registry_root)

    canonical_manifest = registry_root_path / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"
    if not canonical_manifest.is_file():
        raise SystemExit(f"Canonical release channel is missing: {canonical_manifest}")

    payload = json.loads(canonical_manifest.read_text(encoding="utf-8-sig"))
    source_generated_at = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
    now = utc_now()
    payload = json.loads(json.dumps(payload))
    payload["verifiedAt"] = now
    payload["verifiedFromPath"] = str(canonical_manifest)
    payload["verifiedFromGeneratedAt"] = source_generated_at

    output_path = repo_root / ".tmp" / "verify-release-channel" / "RELEASE_CHANNEL.generated.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    subprocess.run(
        ["bash", str(repo_root / "scripts" / "verify-releases-manifest.sh"), str(output_path)],
        check=True,
    )

    canonical_compat_manifest = registry_root_path / ".codex-studio" / "published" / "releases.json"
    if not canonical_compat_manifest.is_file():
        raise SystemExit(f"Canonical compat release channel is missing: {canonical_compat_manifest}")
    portal_support_source_root = resolve_portal_support_source_root(repo_root)

    copy_and_verify_mirror(
        repo_root,
        canonical_manifest,
        canonical_compat_manifest,
        repo_root / "Chummer.Portal" / "downloads",
        sync_output_startup_smoke_from=portal_support_source_root,
    )
    copy_and_verify_mirror(
        repo_root,
        canonical_manifest,
        canonical_compat_manifest,
        repo_root / ".codex-studio" / "published" / "portal",
    )

    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
