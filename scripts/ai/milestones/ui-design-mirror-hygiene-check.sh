#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
cd "$repo_root"

echo "[UI-DESIGN-MIRROR] checking canonical UI mirror parity..."

python3 - <<'PY'
from __future__ import annotations

from collections import Counter
import json
from pathlib import Path

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
        relative_source = Path(*parts[2:])
    elif source_path.name in duplicate_basenames:
        relative_source = source_path
    else:
        relative_source = Path(source_path.name)
    return relative_source

expected_rel_paths = [relative_product_target(source_rel) for source_rel in expected_sources]
expected_set = {path.as_posix() for path in expected_rel_paths}
local_files = sorted(
    path.relative_to(product_target).as_posix()
    for path in product_target.rglob("*")
    if path.is_file()
)
local_set = set(local_files)

missing = sorted(expected_set - local_set)
extra = sorted(local_set - expected_set)
if missing or extra:
    problems = []
    if missing:
        problems.append("missing product files: " + ", ".join(missing))
    if extra:
        problems.append("unexpected product files: " + ", ".join(extra))
    raise SystemExit("[UI-DESIGN-MIRROR] FAIL: " + " | ".join(problems))

for source_rel, target_rel in zip(expected_sources, expected_rel_paths):
    source = design_root / source_rel
    target = product_target / target_rel
    if source.read_bytes() != target.read_bytes():
        raise SystemExit(
            f"[UI-DESIGN-MIRROR] FAIL: product mirror drift for {target_rel.as_posix()} relative to {source_rel}."
        )

repo_source_rel = str(mirror.get("repo_source") or "")
repo_target_rel = str(mirror.get("repo_target") or ".codex-design/repo/IMPLEMENTATION_SCOPE.md")
review_source_rel = str(mirror.get("review_source") or "")
review_target_rel = str(mirror.get("review_target") or ".codex-design/review/REVIEW_CONTEXT.md")

if repo_source_rel:
    if (design_root / repo_source_rel).read_bytes() != (repo_root / repo_target_rel).read_bytes():
        raise SystemExit("[UI-DESIGN-MIRROR] FAIL: IMPLEMENTATION_SCOPE mirror drift detected.")

if review_source_rel:
    if (design_root / review_source_rel).read_bytes() != (repo_root / review_target_rel).read_bytes():
        raise SystemExit("[UI-DESIGN-MIRROR] FAIL: REVIEW_CONTEXT mirror drift detected.")

queue_path = repo_root / ".codex-studio" / "published" / "QUEUE.generated.yaml"
queue_payload = yaml.safe_load(queue_path.read_text(encoding="utf-8")) or {}
if not isinstance(queue_payload, dict):
    raise SystemExit("[UI-DESIGN-MIRROR] FAIL: queue overlay is not a YAML object.")

items = queue_payload.get("items") or []
matching_items = [
    item for item in items if isinstance(item, dict) and item.get("package_id") == "audit-task-11708"
]
if len(matching_items) > 1:
    raise SystemExit(
        "[UI-DESIGN-MIRROR] FAIL: queue overlay re-opened audit-task-11708 multiple times instead of keeping one bounded slice."
    )

target_item = matching_items[0] if matching_items else None

worklist_text = (repo_root / "WORKLIST.md").read_text(encoding="utf-8")
wl_214_done = "| WL-214 | done |" in worklist_text
wl_214_active = "Repo-local live queue: active (`WL-214`)" in worklist_text
latest_repeat_marker = "Auditor publication incorporation (2026-04-21 /fast system re-entry, latest 11708 wave):"
latest_repeat_detail = "feedback/2026-04-21-154433-audit-task-11708.md"

if latest_repeat_marker not in worklist_text or latest_repeat_detail not in worklist_text:
    raise SystemExit(
        "[UI-DESIGN-MIRROR] FAIL: WORKLIST.md must record the latest audit-task-11708 publication wave so repeated mirror-drift observations stay closed as bounded hygiene instead of becoming orphaned feedback."
    )

if target_item is not None:
    expected_allowed_paths = [".codex-design"]
    expected_owned_surfaces = ["design_mirror:ui"]
    expected_source_items = [
        "/docker/chummercomplete/chummer-design/products/chummer/README.md",
        "/docker/chummercomplete/chummer-design/products/chummer/sync/sync-manifest.yaml",
        "/docker/chummercomplete/chummer-design/products/chummer/projects/ui.md",
        "/docker/chummercomplete/chummer-design/products/chummer/review/ui.AGENTS.template.md",
    ]

    allowed_paths = target_item.get("allowed_paths")
    owned_surfaces = target_item.get("owned_surfaces")
    source_items = target_item.get("source_items")
    if allowed_paths != expected_allowed_paths:
        raise SystemExit(
            "[UI-DESIGN-MIRROR] FAIL: queue mirror slice allowed_paths drifted: "
            + json.dumps(allowed_paths, sort_keys=True)
        )
    if owned_surfaces != expected_owned_surfaces:
        raise SystemExit(
            "[UI-DESIGN-MIRROR] FAIL: queue mirror slice owned_surfaces drifted: "
            + json.dumps(owned_surfaces, sort_keys=True)
        )
    if source_items != expected_source_items:
        raise SystemExit(
            "[UI-DESIGN-MIRROR] FAIL: queue mirror slice source_items drifted: "
            + json.dumps(source_items, sort_keys=True)
        )

    if wl_214_done and not wl_214_active:
        raise SystemExit(
            "[UI-DESIGN-MIRROR] FAIL: audit-task-11708 is still published in the live queue even though WL-214 is closed and the mirror is already current."
        )

if not wl_214_done and not wl_214_active:
    raise SystemExit(
        "[UI-DESIGN-MIRROR] FAIL: WORKLIST.md must keep the mirror slice explicit by marking WL-214 done or active."
    )

print("[UI-DESIGN-MIRROR] PASS: canonical UI mirror subset and queue slice are aligned.")
PY
