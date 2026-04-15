#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/SR4_SR6_DESKTOP_PARITY_FRONTIER.generated.json"
sr4_receipt_path="$repo_root/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json"
sr6_receipt_path="$repo_root/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"

mkdir -p "$(dirname "$receipt_path")"

echo "[sr4-sr6-frontier] running SR4 parity gate..."
sr4_exit=0
bash scripts/ai/milestones/sr4-desktop-workflow-parity-check.sh >/dev/null || sr4_exit=$?

echo "[sr4-sr6-frontier] running SR6 parity gate..."
sr6_exit=0
bash scripts/ai/milestones/sr6-desktop-workflow-parity-check.sh >/dev/null || sr6_exit=$?

python3 - <<'PY' "$receipt_path" "$sr4_receipt_path" "$sr6_receipt_path" "$sr4_exit" "$sr6_exit" "$release_channel_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

receipt_path, sr4_receipt_path, sr6_receipt_path = [Path(value) for value in sys.argv[1:4]]
sr4_exit = int(sys.argv[4])
sr6_exit = int(sys.argv[5])
release_channel_path = Path(sys.argv[6])


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def parse_iso(value: object) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "contract_name": "chummer6-ui.sr4_sr6_desktop_parity_frontier",
    "channelId": "",
    "status": "fail",
    "summary": "SR4/SR6 desktop parity frontier closure is not yet proven.",
    "reasons": [],
    "evidence": {
        "sr4ReceiptPath": str(sr4_receipt_path),
        "sr6ReceiptPath": str(sr6_receipt_path),
        "releaseChannelPath": str(release_channel_path),
        "releaseChannelExists": release_channel_path.is_file(),
    },
}

release_channel = {}
if release_channel_path.is_file():
    loaded = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
    if isinstance(loaded, dict):
        release_channel = loaded
release_channel_channel_id = normalize(
    release_channel.get("channelId") if isinstance(release_channel, dict) else ""
)
if not release_channel_channel_id:
    release_channel_channel_id = normalize(
        release_channel.get("channel") if isinstance(release_channel, dict) else ""
    )
release_channel_generated_at_raw = ""
release_channel_generated_at = None
for key in ("generatedAt", "generated_at"):
    if isinstance(release_channel, dict) and key in release_channel:
        release_channel_generated_at_raw = str(release_channel.get(key) or "").strip()
        release_channel_generated_at = parse_iso(release_channel_generated_at_raw)
        break
payload["evidence"]["releaseChannelChannelId"] = release_channel_channel_id
payload["evidence"]["releaseChannelGeneratedAt"] = release_channel_generated_at_raw
if not release_channel_path.is_file():
    payload["reasons"].append(f"Release channel receipt is missing: {release_channel_path}")
elif not isinstance(release_channel, dict) or not release_channel:
    payload["reasons"].append(
        f"Release channel receipt is unreadable or not a JSON object: {release_channel_path}"
    )
if not release_channel_channel_id:
    payload["reasons"].append("Release channel receipt is missing channelId/channel.")
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    payload["reasons"].append(
        "Release channel receipt is missing a valid generatedAt/generated_at timestamp."
    )

for path, label in ((sr4_receipt_path, "SR4"), (sr6_receipt_path, "SR6")):
    if not path.is_file():
        payload["reasons"].append(f"{label} parity receipt is missing: {path}")

if not payload["reasons"]:
    with sr4_receipt_path.open("r", encoding="utf-8") as handle:
        sr4_receipt = json.load(handle)
    with sr6_receipt_path.open("r", encoding="utf-8") as handle:
        sr6_receipt = json.load(handle)

    sr4_status = str(sr4_receipt.get("status") or "").strip().lower()
    sr6_status = str(sr6_receipt.get("status") or "").strip().lower()

    if sr4_status not in {"pass", "passed", "ready"}:
        payload["reasons"].append(
            "SR4 parity receipt is not passing: " + ", ".join(sr4_receipt.get("reasons") or ["missing reason"])
        )
    if sr6_status not in {"pass", "passed", "ready"}:
        payload["reasons"].append(
            "SR6 parity receipt is not passing: " + ", ".join(sr6_receipt.get("reasons") or ["missing reason"])
        )

    payload["evidence"]["sr4Status"] = sr4_status
    payload["evidence"]["sr6Status"] = sr6_status
    payload["evidence"]["sr4ClosureCounts"] = (sr4_receipt.get("evidence") or {}).get("closureCounts")
    payload["evidence"]["sr6ClosureCounts"] = (sr6_receipt.get("evidence") or {}).get("closureCounts")
    sr4_channel_id = normalize(sr4_receipt.get("channelId") or sr4_receipt.get("channel"))
    sr6_channel_id = normalize(sr6_receipt.get("channelId") or sr6_receipt.get("channel"))
    payload["evidence"]["sr4ChannelId"] = sr4_channel_id
    payload["evidence"]["sr6ChannelId"] = sr6_channel_id
    if not sr4_channel_id:
        payload["reasons"].append("SR4 parity receipt is missing channelId/channel.")
    if not sr6_channel_id:
        payload["reasons"].append("SR6 parity receipt is missing channelId/channel.")
    if release_channel_channel_id and sr4_channel_id and sr4_channel_id != release_channel_channel_id:
        payload["reasons"].append("SR4 parity receipt channelId does not match release channel.")
    if release_channel_channel_id and sr6_channel_id and sr6_channel_id != release_channel_channel_id:
        payload["reasons"].append("SR6 parity receipt channelId does not match release channel.")

if sr4_exit != 0:
    payload["reasons"].append(f"SR4 parity gate exited non-zero: {sr4_exit}")
if sr6_exit != 0:
    payload["reasons"].append(f"SR6 parity gate exited non-zero: {sr6_exit}")

payload["channelId"] = release_channel_channel_id
if not payload["reasons"]:
    payload["status"] = "pass"
    payload["summary"] = (
        "SR4 and SR6 desktop parity frontier closure is explicitly proven by executable parity gates with "
        "equivalent-or-not-applicable workflow-family receipts."
    )

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if payload["status"] != "pass":
    raise SystemExit(43)
PY

echo "[sr4-sr6-frontier] PASS"
