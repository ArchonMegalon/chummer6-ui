#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

echo "[B6] checking generated asset preview and approval workflow..."

if [[ ! -f Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor ]]; then
  echo "[B6] FAIL: missing generated-asset review component."
  exit 3
fi

if rg -q 'ProjectReference Include="..\\Chummer.Contracts\\Chummer.Contracts.csproj"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj; then
  echo "[B6] FAIL: generated-asset flow still compiles against the duplicated Chummer.Contracts source project."
  exit 4
fi

if [ -d Chummer.Contracts ]; then
  echo "[B6] FAIL: duplicated Chummer.Contracts source tree still exists in the presentation repo."
  exit 10
fi

if ! rg -q "GeneratedAssetActionKinds.Attach|GeneratedAssetActionKinds.Approve|GeneratedAssetActionKinds.Archive|data-generated-asset-preview|AttachmentTargets|GeneratedAssetActionRequest" \
  Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor; then
  echo "[B6] FAIL: generated-asset viewer is missing attach/approve/archive workflow seams."
  exit 5
fi

if ! rg -q "GeneratedAssetComparisonSlot|GeneratedAssetPreviewSection|GeneratedAssetComparisonRoles" \
  Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor \
  Chummer.Blazor/Components/Pages/Showcase.razor \
  Chummer.Tests/Presentation/BlazorShellComponentTests.cs; then
  echo "[B6] FAIL: generated-asset compare or dossier preview flows are not wired through the shared presentation seam."
  exit 6
fi

if ! rg -q "data-generated-asset-compare|data-generated-asset-preview-section" \
  Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor; then
  echo "[B6] FAIL: shared viewer is missing portrait compare or dossier preview surfaces."
  exit 7
fi

if ! rg -q "data-generated-asset-video-viewer|data-generated-asset-video-card|Route recap clip|Sixth World News Card|GeneratedAssetPreviewKinds.Video" \
  Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor \
  Chummer.Blazor/Components/Pages/Showcase.razor; then
  echo "[B6] FAIL: shared viewer is missing route-video recap/news card coverage."
  exit 9
fi

if ! rg -q "GeneratedAssetReviewPanel|GeneratedAssetProjection|OnAssetActionRequested" Chummer.Blazor/Components/Pages/Showcase.razor; then
  echo "[B6] FAIL: generated-asset workflow not composed into the showcase surface."
  exit 8
fi

echo "[B6] PASS: generated asset review flow is present."
