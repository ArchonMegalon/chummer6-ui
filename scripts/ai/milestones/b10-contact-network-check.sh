#!/usr/bin/env bash
set -euo pipefail

echo "[B10] checking contact network continuity surface..."

if [[ ! -f Chummer.Blazor/Components/Shared/ContactNetworkPanel.razor ]]; then
  echo "[B10] FAIL: missing ContactNetworkPanel component."
  exit 3
fi

if ! rg -q "ContactRelationshipGraphState|ContactRelationshipGraphProjector|CharacterContactsSection|CharacterContactSummary" Chummer.Blazor/Components/Shared/ContactNetworkPanel.razor Chummer.Blazor/Components/Pages/Home.razor Chummer.Presentation/Overview/ContactRelationshipGraphState.cs; then
  echo "[B10] FAIL: contact network surface is not driven by contact contracts."
  exit 4
fi

if ! rg -q "ContactNetworkPanel" Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B10] FAIL: contact network panel is not composed on the home surface."
  exit 5
fi

if ! rg -q "Faction Status Rail|Heat Rail|Obligation Rail|Unresolved Favor Rail|data-contact-faction-rail|data-contact-heat-rail|data-contact-obligation-rail|data-contact-favor-rail" Chummer.Blazor/Components/Shared/ContactNetworkPanel.razor; then
  echo "[B10] FAIL: relationship graph rails (faction/heat/obligation/favor) are missing."
  exit 6
fi

if ! rg -q "SetContactGraph|ContactGraphBorder|ContactRelationshipGraphState|BuildContactGraph" Chummer.Avalonia/Controls/SectionHostControl.axaml Chummer.Avalonia/Controls/SectionHostControl.axaml.cs Chummer.Avalonia/MainWindow.ShellFrameProjector.cs; then
  echo "[B10] FAIL: Avalonia contact relationship graph rendering seam is missing."
  exit 7
fi

echo "[B10] PASS: contact relationship graph rails are present across Blazor and Avalonia seams."
