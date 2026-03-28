#!/usr/bin/env bash
set -euo pipefail

echo "[B13] checking post-B6 accessibility signoff guardrails..."

test_project="Chummer.Tests/Chummer.Tests.csproj"
if [[ -f "Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj" ]]; then
  test_project="Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj"
fi
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

echo "[B13] executing targeted regression smoke runner..."
if [[ "$runtime_tests_required" == "1" ]]; then
  if ! bash -lc '
    set -euo pipefail
    scripts/ai/with-package-plane.sh build "'"$test_project"'" --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false
    scripts/ai/with-package-plane.sh run --project "'"$test_project"'" --no-build --nologo --verbosity quiet
  '; then
    echo "[B13] FAIL: required targeted smoke runner execution failed."
    exit 4
  fi
else
  echo "[B13] note: CHUMMER_B13_TESTS_REQUIRED is not 1; executing smoke runner anyway for strict signoff drift protection."
  bash -lc '
    set -euo pipefail
    scripts/ai/with-package-plane.sh build "'"$test_project"'" --nologo --verbosity quiet --ignore-failed-sources -p:NuGetAudit=false
    scripts/ai/with-package-plane.sh run --project "'"$test_project"'" --no-build --nologo --verbosity quiet
  '
fi

echo "[B13] PASS: post-B6 accessibility guardrails are present."
