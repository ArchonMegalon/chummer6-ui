#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

echo "[B11-NPC] checking NPC Persona Studio implementation..."

if [[ ! -f "$REPO_ROOT/Chummer.Blazor/Components/Shared/NpcPersonaStudioPanel.razor" ]]; then
  echo "[B11-NPC] FAIL: missing NpcPersonaStudioPanel component."
  exit 3
fi

if ! rg -q "NpcPersonaStudioState|NpcPersonaDescriptorState|NpcPersonaRoutePolicyState|NpcPersonaStudioProjector|defaultPersonaId|routePolicies|personas" \
  "$REPO_ROOT/Chummer.Presentation/Overview/NpcPersonaStudioState.cs" \
  "$REPO_ROOT/Chummer.Presentation/Overview/WorkspaceSectionRenderer.cs" \
  "$REPO_ROOT/Chummer.Blazor/Components/Shared/NpcPersonaStudioPanel.razor"; then
  echo "[B11-NPC] FAIL: NPC Persona Studio path is not contract-driven."
  exit 4
fi

if ! rg -q "NpcPersonaStudioPanel" "$REPO_ROOT/Chummer.Blazor/Components/Pages/Home.razor"; then
  echo "[B11-NPC] FAIL: NPC Persona Studio panel is not composed on the home surface."
  exit 5
fi

if ! rg -q "SetNpcPersonaStudio|NpcPersonaStudioBorder|NpcPersonaStudioState|SectionHostState\\(" \
  "$REPO_ROOT/Chummer.Avalonia/Controls/SectionHostControl.axaml" \
  "$REPO_ROOT/Chummer.Avalonia/Controls/SectionHostControl.axaml.cs" \
  "$REPO_ROOT/Chummer.Avalonia/MainWindow.ShellFrameProjector.cs"; then
  echo "[B11-NPC] FAIL: Avalonia NPC Persona Studio rendering seam is missing."
  exit 6
fi

if ! rg -q "WL-079 \\| done \\| .*Milestone B11: build NPC Persona Studio screens on shared persona descriptor/policy contracts" "$REPO_ROOT/WORKLIST.md"; then
  echo "[B11-NPC] FAIL: WL-079 is not marked done in WORKLIST.md."
  exit 7
fi

echo "[B11-NPC] PASS: NPC Persona Studio surfaces are present and contract-driven."
