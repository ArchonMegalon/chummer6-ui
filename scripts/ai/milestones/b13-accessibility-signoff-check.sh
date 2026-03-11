#!/usr/bin/env bash
set -euo pipefail

echo "[B13] checking post-B6 accessibility signoff guardrails..."

test_project="Chummer.Tests/Chummer.Tests.csproj"
test_filter="(FullyQualifiedName~SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation|FullyQualifiedName~GeneratedAssetReviewPanel_renders_preview_and_emits_attach_approve_archive_actions|FullyQualifiedName~BlazorHome_invalidates_spider_cards_when_session_context_shifts_and_refreshes_them)"
runtime_tests_required="${CHUMMER_B13_TESTS_REQUIRED:-0}"

require_contains() {
  local path="$1"
  local pattern="$2"
  local message="$3"
  if ! rg -q "$pattern" "$path"; then
    echo "$message"
    exit 3
  fi
}

require_contains \
  "Chummer.Blazor/Components/Shell/SectionPane.razor" \
  'role="listbox"' \
  "[B13] FAIL: browse workspace is missing listbox semantics."

require_contains \
  "Chummer.Blazor/Components/Shell/SectionPane.razor" \
  'role="option"' \
  "[B13] FAIL: browse workspace is missing option semantics."

require_contains \
  "Chummer.Blazor/Components/Shell/SectionPane.razor" \
  'aria-selected="@IsBrowseResultActive' \
  "[B13] FAIL: browse workspace is missing active-option aria-selected state."

require_contains \
  "Chummer.Blazor/Components/Shell/SectionPane.razor" \
  'aria-activedescendant=' \
  "[B13] FAIL: browse workspace is missing aria-activedescendant wiring."

require_contains \
  "Chummer.Blazor/wwwroot/app.css" \
  ':focus-visible' \
  "[B13] FAIL: focus-visible styling is missing from shared interactive chrome."

require_contains \
  "Chummer.Blazor/wwwroot/app.css" \
  'outline: 2px solid var\(--ui-kit-focus-ring\)' \
  "[B13] FAIL: focus-visible outline contrast guardrails are missing."

require_contains \
  "Chummer.Blazor/wwwroot/app.css" \
  'box-shadow: 0 0 0 3px rgba\(29, 78, 216, 0.18\)' \
  "[B13] FAIL: focus-visible halo guardrails are missing."

require_contains \
  "Chummer.Blazor/Components/Shared/GmBoardFeed.razor" \
  'data-gm-board-stale-banner' \
  "[B13] FAIL: GM board stale-state banner marker is missing."

require_contains \
  "Chummer.Blazor/Components/Shared/GmBoardFeed.razor" \
  'role="status"' \
  "[B13] FAIL: GM board stale-state banner is missing status semantics."

require_contains \
  "Chummer.Blazor/Components/Shared/GmBoardFeed.razor" \
  'aria-live="polite"' \
  "[B13] FAIL: GM board stale-state banner is missing polite live-region semantics."

require_contains \
  "Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor" \
  'role="tablist"' \
  "[B13] FAIL: generated-asset review is missing tablist semantics."

require_contains \
  "Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor" \
  'role="tab"' \
  "[B13] FAIL: generated-asset review is missing tab semantics."

require_contains \
  "Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor" \
  'role="tabpanel"' \
  "[B13] FAIL: generated-asset review is missing tabpanel semantics."

require_contains \
  "Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor" \
  'aria-controls=' \
  "[B13] FAIL: generated-asset review is missing tab-to-panel wiring."

require_contains \
  "Chummer.Tests/Presentation/BlazorShellComponentTests.cs" \
  'aria-activedescendant' \
  "[B13] FAIL: browse accessibility regression test declaration is missing."

require_contains \
  "Chummer.Tests/Presentation/BlazorShellComponentTests.cs" \
  'aria-controls' \
  "[B13] FAIL: generated-asset accessibility regression test declaration is missing."

require_contains \
  "Chummer.Tests/Presentation/BlazorShellComponentTests.cs" \
  'data-gm-board-stale-banner' \
  "[B13] FAIL: GM board stale-state regression test declaration is missing."

echo "[B13] executing targeted regression tests..."
dotnet_test_args=(
  "$test_project"
  "--filter" "$test_filter"
  --nologo \
  --verbosity quiet
)

has_prebuilt_tests=0
if [[ -f "Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll" || -f "Chummer.Tests/bin/Debug/net10.0-windows/Chummer.Tests.dll" ]]; then
  has_prebuilt_tests=1
fi

is_prebuilt_fresh=0
if [[ "$has_prebuilt_tests" == "1" ]]; then
  newest_test_binary="$(find Chummer.Tests/bin -type f -name 'Chummer.Tests.dll' -print0 | xargs -0 ls -1t 2>/dev/null | head -n 1 || true)"
  if [[ -n "$newest_test_binary" && -f "$newest_test_binary" ]]; then
    newest_input_timestamp="$(
      {
        find Chummer.Blazor -type f \( -name '*.cs' -o -name '*.razor' -o -name '*.css' \) -printf '%T@\n' 2>/dev/null
        find Chummer.Tests/Presentation -type f -name '*.cs' -printf '%T@\n' 2>/dev/null
      } | sort -nr | head -n 1
    )"
    test_binary_timestamp="$(stat -c '%Y' "$newest_test_binary")"
    if [[ -n "$newest_input_timestamp" ]]; then
      newest_input_epoch="${newest_input_timestamp%%.*}"
      if [[ "$test_binary_timestamp" -ge "$newest_input_epoch" ]]; then
        is_prebuilt_fresh=1
      fi
    fi
  fi
fi

if [[ "$is_prebuilt_fresh" == "1" ]]; then
  dotnet_test_args+=(--no-build --no-restore)
  bash scripts/ai/test.sh "${dotnet_test_args[@]}"
else
  if [[ "$has_prebuilt_tests" == "1" ]]; then
    echo "[B13] note: detected stale prebuilt targeted test outputs; running build-backed targeted regression tests."
    bash scripts/ai/test.sh "${dotnet_test_args[@]}"
  else
    if [[ "$runtime_tests_required" == "1" ]]; then
      echo "[B13] note: targeted test outputs are not prebuilt; attempting required no-restore targeted regression tests first."
      no_restore_args=("${dotnet_test_args[@]}" --no-restore)
      if bash scripts/ai/test.sh "${no_restore_args[@]}"; then
        :
      else
        echo "[B13] FAIL: required targeted tests could not run in this offline environment (no prebuilt outputs and no-restore execution failed)."
        echo "[B13] Provide prebuilt Chummer.Tests outputs or run in an environment with package restore access."
        exit 4
      fi
    else
      echo "[B13] note: skipped targeted regression execution because prebuilt test outputs were not found."
      echo "[B13] Set CHUMMER_B13_TESTS_REQUIRED=1 to make missing prebuilt outputs fail."
    fi
  fi
fi

echo "[B13] PASS: post-B6 accessibility guardrails are present."
