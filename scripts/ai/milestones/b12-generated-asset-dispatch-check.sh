#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

echo "[B12] checking generated-asset dispatch/review depth..."

if [[ ! -f Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor ]]; then
  echo "[B12] FAIL: missing shared generated-asset review panel."
  exit 3
fi

if ! rg -q "data-generated-asset-coach-routing|data-generated-asset-shadowfeed-rail|data-generated-asset-stale-banner" \
  Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor; then
  echo "[B12] FAIL: shared generated-asset viewer is missing coach routing, shadowfeed dispatch/review, or stale rails."
  exit 4
fi

if ! rg -q "data-generated-portrait-forge|data-generated-portrait-forge-seed|data-generated-portrait-forge-style-options|data-generated-portrait-forge-reroll-timeline|mark_canonical" \
  Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor; then
  echo "[B12] FAIL: shared generated-asset viewer is missing portrait forge prompt/style/reroll/canonical rails."
  exit 7
fi

if ! rg -q "GeneratedAssetActionKindDispatch|GeneratedAssetActionKindReview|GeneratedAssetActionKindRefreshDispatch|GeneratedAssetActionKindMarkCanonical|ApplyDispatchInvalidation|shadowfeedDispatchReceipt|portraitPromptSeed|portraitStyleOptions|canonicalAssetId|coachRouteClass|gpt-5.3-codex" \
  Chummer.Blazor/Components/Pages/Showcase.razor; then
  echo "[B12] FAIL: Blazor showcase surface is missing portrait forge metadata/canonical action or dispatch/review lifecycle wiring."
  exit 5
fi

if ! rg -q "data-generated-asset-coach-routing|data-generated-asset-shadowfeed-rail|data-generated-portrait-forge|mark_canonical|dispatch|refresh_dispatch|BlazorHome_invalidates_shadowfeed_dispatch_after_context_shift_and_allows_refresh|BlazorHome_marks_portrait_candidate_as_canonical_through_shared_action_rail" \
  Chummer.Tests/Presentation/BlazorShellComponentTests.cs; then
  echo "[B12] FAIL: missing shared component coverage for portrait forge/canonical or coach/shadowfeed dispatch-review rails."
  exit 6
fi

echo "[B12] PASS: portrait forge selection/reroll/canonical rails and coach-shadowfeed dispatch-review rails are materialized on shared generated-asset workflows."
