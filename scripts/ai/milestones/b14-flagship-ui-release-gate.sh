#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

output_dir="$repo_root/Chummer.Avalonia/bin/Release/net10.0"
sample_path="$output_dir/Samples/Legacy/Soma-Career.chum5"
receipt_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
signoff_path="$repo_root/docs/WORKBENCH_RELEASE_SIGNOFF.md"

mkdir -p "$(dirname "$receipt_path")"
rm -rf "$screenshot_dir"
mkdir -p "$screenshot_dir"

echo "[b14] building Avalonia desktop head..."
bash scripts/ai/build.sh Chummer.Avalonia/Chummer.Avalonia.csproj -c Release -v minimal >/dev/null

if [[ ! -f "$sample_path" ]]; then
  echo "[b14] FAIL: bundled demo runner fixture missing from Release output: $sample_path" >&2
  exit 41
fi

if ! rg -q "b14-flagship-ui-release-gate\\.sh" "$signoff_path"; then
  echo "[b14] FAIL: workbench release signoff does not cite the flagship UI release gate: $signoff_path" >&2
  exit 42
fi

echo "[b14] running flagship Avalonia headless UI gate tests..."
CHUMMER_UI_GATE_SCREENSHOT_DIR="$screenshot_dir" \
bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj \
  --filter "FullyQualifiedName~AvaloniaFlagshipUiGateTests" -v minimal >/dev/null

python3 - <<'PY' "$sample_path" "$receipt_path" "$screenshot_dir" "$signoff_path"
import json
import os
import sys
from datetime import datetime, timezone

sample_path, receipt_path, screenshot_dir, signoff_path = sys.argv[1:5]
expected_screenshots = [
    "01-initial-shell-light.png",
    "02-menu-open-light.png",
    "03-settings-open-light.png",
    "04-loaded-runner-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
]
captured = []
missing = []
for name in expected_screenshots:
    path = os.path.join(screenshot_dir, name)
    if not os.path.isfile(path):
        missing.append(path)
        continue
    captured.append(
        {
            "name": name,
            "path": path,
            "sizeBytes": os.path.getsize(path),
        }
    )

if missing:
    raise SystemExit(
        "[b14] FAIL: missing screenshot evidence: " + ", ".join(missing)
    )

payload = {
    "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    "status": "pass",
    "releaseGate": "b14-flagship-ui-release-gate",
    "desktopHead": "avalonia",
    "artifactPresence": {
        "bundledDemoRunnerPath": sample_path,
        "bundledDemoRunnerPresent": os.path.isfile(sample_path),
    },
    "interactionProof": {
        "testSuite": "AvaloniaFlagshipUiGateTests",
        "menuSurface": "pass",
        "settingsInlineDialog": "pass",
        "demoRunnerDispatch": "pass",
        "keyboardShortcutParity": "pass",
        "legacyFamiliarityBridge": "pass",
    },
    "visualReviewEvidence": {
        "screenshotDirectory": screenshot_dir,
        "expectedScreenshots": expected_screenshots,
        "capturedScreenshots": captured,
    },
    "signoffLane": {
        "workbenchReleaseSignoffPath": signoff_path,
        "citesReleaseGate": True,
    },
}
with open(receipt_path, "w", encoding="utf-8") as handle:
    json.dump(payload, handle, indent=2)
    handle.write("\n")
PY

echo "[b14] PASS"
