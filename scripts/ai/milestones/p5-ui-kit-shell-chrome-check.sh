#!/usr/bin/env bash
set -euo pipefail

echo "[P5] checking shell chrome ui-kit boundary..."

if ! rg -q "public static class ShellChromeBoundary|PackageId = \"Chummer\\.Ui\\.Kit\"|RootClass = BlazorUiKitAdapter" \
  Chummer.Presentation/UiKit/ShellChromeBoundary.cs; then
  echo "[P5] FAIL: shell chrome boundary shim is missing."
  exit 3
fi

if ! rg -q "ShellChromeBoundary\\.FormatCommandLabel" \
  Chummer.Blazor/Components/Shell/MenuBar.razor \
  Chummer.Blazor/Components/Shell/ToolStrip.razor \
  Chummer.Blazor/Components/Shell/CommandPanel.razor; then
  echo "[P5] FAIL: blazor shell chrome is not consuming the shared shell chrome boundary."
  exit 4
fi

if ! rg -q "ShellChromeBoundary\\.RootClass|UiKitShellChromeAdapterMarker" \
  Chummer.Avalonia/MainWindow.axaml.cs \
  Chummer.Avalonia/MainWindow.ControlBinding.cs; then
  echo "[P5] FAIL: avalonia shell chrome is missing the ui-kit adapter marker seam."
  exit 5
fi

if ! rg -q "DesktopDialogChromeBoundary\\.BuildFailureMessage" \
  Chummer.Avalonia/DesktopDialogWindow.axaml.cs; then
  echo "[P5] FAIL: desktop dialog shell is not routed through the ui-kit dialog boundary."
  exit 6
fi

echo "[P5] PASS: shell chrome consumers are isolated behind the ui-kit boundary shim."
