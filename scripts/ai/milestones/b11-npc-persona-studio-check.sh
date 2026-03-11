#!/usr/bin/env bash
set -euo pipefail

echo "[B11-NPC] checking NPC Persona Studio implementation..."

if [[ ! -f Chummer.Blazor/Components/Shared/NpcPersonaStudioPanel.razor ]]; then
  echo "[B11-NPC] FAIL: missing NpcPersonaStudioPanel component."
  exit 3
fi

if ! rg -q "NpcPersonaStudioState|NpcPersonaDescriptorState|NpcPersonaRoutePolicyState|NpcPersonaStudioProjector|defaultPersonaId|routePolicies|personas" Chummer.Presentation/Overview/NpcPersonaStudioState.cs Chummer.Presentation/Overview/WorkspaceSectionRenderer.cs Chummer.Blazor/Components/Shared/NpcPersonaStudioPanel.razor; then
  echo "[B11-NPC] FAIL: NPC Persona Studio path is not contract-driven."
  exit 4
fi

if ! rg -q "NpcPersonaStudioPanel" Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B11-NPC] FAIL: NPC Persona Studio panel is not composed on the home surface."
  exit 5
fi

if ! rg -q "SetNpcPersonaStudio|NpcPersonaStudioBorder|NpcPersonaStudioState|SectionHostState\\(" Chummer.Avalonia/Controls/SectionHostControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Avalonia/MainWindow.ShellFrameProjector.cs; then
  echo "[B11-NPC] FAIL: Avalonia NPC Persona Studio rendering seam is missing."
  exit 6
fi

if ! rg -q "WL-079 \\| done \\| .*Milestone B11: build NPC Persona Studio screens on shared persona descriptor/policy contracts" WORKLIST.md; then
  echo "[B11-NPC] FAIL: WL-079 is not marked done in WORKLIST.md."
  exit 7
fi

echo "[B11-NPC] PASS: NPC Persona Studio surfaces are present and contract-driven."
