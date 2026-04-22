#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

echo "[B4] checking GM Board / Spider Feed tactical card controls..."

if [[ ! -f Chummer.Blazor/Components/Shared/GmBoardFeed.razor ]]; then
  echo "[B4] FAIL: missing GM Board component."
  exit 3
fi

if ! rg -q "InterruptionBudget|AutonomyLevel|AutonomyLevelChanged|PinRequested|DismissRequested|SnoozeRequested|MuteRequested|MutedUntilLabel" Chummer.Blazor/Components/Shared/GmBoardFeed.razor; then
  echo "[B4] FAIL: GM Board component missing tactical controls (budget/autonomy/pin/dismiss/snooze/mute)."
  exit 4
fi

if ! rg -q "GetSeverityClass|severity" Chummer.Blazor/Components/Shared/GmBoardFeed.razor; then
  echo "[B4] FAIL: GM Board missing severity-driven visual classification."
  exit 5
fi

if ! rg -q "CurrentSessionContext|SessionContextChanged|data-gm-board-stale-banner|data-gm-board-refresh-context|Invalidated by context shift" Chummer.Blazor/Components/Shared/GmBoardFeed.razor; then
  echo "[B4] FAIL: GM Board missing stale-state context-shift invalidation affordances."
  exit 7
fi

if ! rg -q "GmBoardFeed" Chummer.Blazor/Components/Pages/Showcase.razor; then
  echo "[B4] FAIL: GM Board component not composed on showcase surface."
  exit 6
fi

echo "[B4] PASS: GM Board/Spider Feed tactical controls are present."
