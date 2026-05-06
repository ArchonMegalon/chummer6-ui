#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
cd "$repo_root"

echo "[UI-DESIGN-MIRROR] syncing canonical UI mirror subset..."

python3 - <<'PY'
from __future__ import annotations

from collections import Counter
from pathlib import Path
import shutil

import yaml

repo_root = Path.cwd()
design_root = Path("/docker/chummercomplete/chummer-design")
manifest_path = design_root / "products" / "chummer" / "sync" / "sync-manifest.yaml"

manifest = yaml.safe_load(manifest_path.read_text(encoding="utf-8"))
if not isinstance(manifest, dict):
    raise SystemExit("[UI-DESIGN-MIRROR] FAIL: sync manifest is not a YAML object.")

mirrors = manifest.get("mirrors") or []
mirror = next((item for item in mirrors if isinstance(item, dict) and item.get("repo") == "chummer6-ui"), None)
if mirror is None:
    raise SystemExit("[UI-DESIGN-MIRROR] FAIL: sync manifest does not define the chummer6-ui mirror.")

group_table = manifest.get("product_source_groups") or {}
product_groups = mirror.get("product_groups") or []
product_target = repo_root / str(mirror.get("product_target") or ".codex-design/product")

expected_sources: list[str] = []
for group_name in product_groups:
    group_items = group_table.get(group_name)
    if not isinstance(group_items, list):
        raise SystemExit(f"[UI-DESIGN-MIRROR] FAIL: product group {group_name!r} is missing or invalid.")
    expected_sources.extend(str(item) for item in group_items)

duplicate_basenames = {
    name for name, count in Counter(Path(source).name for source in expected_sources).items() if count > 1
}

def relative_product_target(source_rel: str) -> Path:
    source_path = Path(source_rel)
    parts = list(source_path.parts)
    if len(parts) >= 2 and parts[0] == "products" and parts[1] == "chummer":
        return Path(*parts[2:])
    if source_path.name in duplicate_basenames:
        return source_path
    return Path(source_path.name)

expected_targets = {}
for source_rel in expected_sources:
    expected_targets[relative_product_target(source_rel)] = design_root / source_rel

product_target.mkdir(parents=True, exist_ok=True)
for target_rel, source in expected_targets.items():
    target = product_target / target_rel
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)

for local_path in product_target.rglob("*"):
    if not local_path.is_file():
        continue
    target_rel = local_path.relative_to(product_target)
    if target_rel not in expected_targets:
        local_path.unlink()

for local_dir in sorted(
    (path for path in product_target.rglob("*") if path.is_dir()),
    key=lambda path: len(path.relative_to(product_target).parts),
    reverse=True,
):
    try:
        local_dir.rmdir()
    except OSError:
        pass

for key, default_target in (
    ("repo_source", ".codex-design/repo/IMPLEMENTATION_SCOPE.md"),
    ("review_source", ".codex-design/review/REVIEW_CONTEXT.md"),
):
    source_rel = mirror.get(key)
    if not source_rel:
        continue
    target_key = key.replace("_source", "_target")
    target_rel = Path(str(mirror.get(target_key) or default_target))
    source = design_root / str(source_rel)
    target = repo_root / target_rel
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)

print("[UI-DESIGN-MIRROR] PASS: synced canonical UI mirror subset.")
PY
