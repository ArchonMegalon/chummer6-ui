#!/usr/bin/env bash
set -euo pipefail

echo "[B8] checking runtime inspector shared surface..."

if [[ ! -f Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor ]]; then
  echo "[B8] FAIL: missing RuntimeInspectorPanel component."
  exit 3
fi

if ! rg -q "RuntimeInspectorProjection|ResolvedRulePacks|ProviderBindings|CompatibilityDiagnostics|Warnings|MigrationPreview|CapabilityDescriptors|Rule Profile Diagnostics|Rule Pack Diagnostics|Hub Client Diagnostics|Review State|Stale/Invalidation|runtime-hub-diagnostics" Chummer.Blazor/Components/Shared/RuntimeInspectorPanel.razor; then
  echo "[B8] FAIL: runtime inspector panel is not rendering the projection contract."
  exit 4
fi

if ! rg -q "runtimeHubClientDiagnostics|Hub Client Diagnostics" Chummer.Presentation/Overview/DesktopDialogFactory.cs; then
  echo "[B8] FAIL: desktop runtime inspector dialog is missing hub-client diagnostics rails."
  exit 6
fi

if ! rg -q "RuntimeInspectorPanel" Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B8] FAIL: runtime inspector panel is not composed on the home surface."
  exit 5
fi

echo "[B8] PASS: runtime inspector hub-client rails are present and contract-driven."
