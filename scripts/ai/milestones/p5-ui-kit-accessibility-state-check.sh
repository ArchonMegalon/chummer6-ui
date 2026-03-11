#!/usr/bin/env bash
set -euo pipefail

echo "[P5/B13] checking accessibility/state ui-kit boundary..."

if ! rg -q "public static class AccessibilityPrimitiveBoundary|PackageId = \"Chummer\\.Ui\\.Kit\"|LocalAdapterMarker|BuildStatusAnnouncement|BuildDialogDescriptionId" \
  Chummer.Presentation/UiKit/ShellChromeBoundary.cs; then
  echo "[P5/B13] FAIL: accessibility primitive boundary shim is missing."
  exit 3
fi

if ! rg -q "AccessibilityPrimitiveBoundary\\.DialogRole|AccessibilityPrimitiveBoundary\\.BuildDialogDescriptionId|_dialogRoot\\.FocusAsync" \
  Chummer.Blazor/Components/Shell/DialogHost.razor; then
  echo "[P5/B13] FAIL: blazor dialog host is not consuming the shared focus/aria boundary."
  exit 4
fi

if ! rg -q "AccessibilityPrimitiveBoundary\\.StatusRegionRole|AccessibilityPrimitiveBoundary\\.PoliteAnnouncementMode|AccessibilityPrimitiveBoundary\\.BuildStatusAnnouncement" \
  Chummer.Blazor/Components/Shell/StatusStrip.razor; then
  echo "[P5/B13] FAIL: blazor status strip is not consuming the shared announcement boundary."
  exit 5
fi

if ! rg -q "UiKitAccessibilityAdapterMarker|AccessibilityPrimitiveBoundary\\.LocalAdapterMarker|primaryAction\\.Focus" \
  Chummer.Avalonia/DesktopDialogWindow.axaml.cs; then
  echo "[P5/B13] FAIL: avalonia desktop dialog is missing the shared focus seam."
  exit 6
fi

if ! rg -q "UiKitAccessibilityAdapterMarker|AccessibilityPrimitiveBoundary\\.BuildStatusAnnouncement" \
  Chummer.Avalonia/Controls/StatusStripControl.axaml.cs; then
  echo "[P5/B13] FAIL: avalonia status strip is missing the shared announcement seam."
  exit 7
fi

echo "[P5/B13] PASS: accessibility/state primitives are isolated behind the ui-kit boundary shim."
