#!/usr/bin/env bash
set -euo pipefail

echo "[B2] checking browse workspace virtualization usage..."

if ! rg -q "<Virtualize" Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B2] FAIL: expected <Virtualize> is missing from browse/workspace surfaces."
  exit 3
fi

if ! rg -q "ItemsTagName=\"ul\"|ItemsTagName='ul'|ItemsTagName=\"tbody\"|ItemsTagName='tbody'" Chummer.Blazor/Components/Shell/OpenWorkspaceTree.razor Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B2] FAIL: workspace virtualization lacks semantic container tags for large list rendering."
  exit 4
fi

if ! rg -q '<Virtualize' Chummer.Blazor/Components/Shell/SectionPane.razor || ! rg -q 'Items="@browseWorkspace\.Results"' Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B2] FAIL: browse result rail is not virtualized directly from the projected result window."
  exit 5
fi

if ! rg -q 'ItemSize="56"|ItemSize='"'"'56'"'"'' Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B2] FAIL: browse result virtualization is missing a stable item size guardrail."
  exit 6
fi

if ! rg -q 'data-browse-window-limit|data-browse-window-offset|BuildBrowseResultWindowLabel|Showing \{browseWorkspace\.VisibleResultStart\}-\{browseWorkspace\.VisibleResultEnd\}' Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B2] FAIL: browse shell no longer exposes large-catalog result window metadata."
  exit 7
fi

if rg -q '@foreach \(BrowseWorkspaceResultItemState item in GetBrowseResults\(browseWorkspace\)\)' Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B2] FAIL: browse shell regressed to eager result enumeration."
  exit 8
fi

echo "[B2] PASS: key browse/workspace render paths are virtualized."
