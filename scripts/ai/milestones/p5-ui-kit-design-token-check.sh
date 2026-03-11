#!/usr/bin/env bash
set -euo pipefail

echo "[P5] checking design token/theme ui-kit backlog mapping..."

if ! rg -q '^\| WL-087 \| done \| P1 \| Milestone P5: publish the remaining shared token/theme extraction backlog for `Chummer\.Ui\.Kit`\.' WORKLIST.md; then
  echo "[P5] FAIL: WL-087 is not closed with a published completion entry."
  exit 3
fi

if ! rg -q 'WL-087.*Runnable slice command chain: `bash scripts/ai/milestones/p5-ui-kit-design-token-check.sh && bash scripts/ai/verify.sh`' WORKLIST.md; then
  echo "[P5] FAIL: WL-087 is missing the executable closure command chain."
  exit 4
fi

if ! rg -q 'public static class DesignTokenThemeBoundary|PackageId = "Chummer\.Ui\.Kit"|LocalAdapterMarker|--ui-kit-shell-surface|--ui-kit-focus-ring' \
  Chummer.Presentation/UiKit/DesignTokenThemeBoundary.cs; then
  echo "[P5] FAIL: design token/theme boundary shim is missing required ui-kit markers."
  exit 5
fi

if ! rg -q '^:root \{|--ui-kit-shell-surface:|--ui-kit-shell-border:|--ui-kit-panel-surface:|--ui-kit-focus-ring:' \
  Chummer.Blazor/wwwroot/app.css; then
  echo "[P5] FAIL: shared shell/workbench token variables are not published in app.css."
  exit 6
fi

if ! rg -q 'var\(--ui-kit-shell-surface\)|var\(--ui-kit-shell-border\)|var\(--ui-kit-panel-surface\)|var\(--ui-kit-focus-ring\)' \
  Chummer.Blazor/wwwroot/app.css; then
  echo "[P5] FAIL: shell/workbench styles are not consuming ui-kit token variables."
  exit 7
fi

echo "[P5] PASS: token/theme backlog is published with executable closure and ui-kit seam markers."
