#!/usr/bin/env bash
set -euo pipefail

echo "[B3] checking build lab UI contract posture..."

if rg -q 'ProjectReference Include="..\\Chummer.Contracts\\Chummer.Contracts.csproj"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj; then
  echo "[B3] FAIL: presentation projects still compile against the duplicated Chummer.Contracts source project."
  exit 3
fi

if ! rg -q "PackageReference Include=\"\\$\\(ChummerContractsPackageId\\)\" Version=\"\\$\\(ChummerContractsPackageVersion\\)\"" \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj; then
  echo "[B3] FAIL: authoritative contract package consumption is not wired through the presentation projects."
  exit 4
fi

if [ -d Chummer.Contracts ]; then
  echo "[B3] FAIL: duplicated Chummer.Contracts source tree still exists in the presentation repo."
  exit 10
fi

if ! rg -q "BuildLabConceptIntakeProjector|ActiveBuildLab" Chummer.Presentation/Overview Chummer.Blazor/Components/Shell/SectionPane.razor Chummer.Avalonia/Controls/SectionHostControl.axaml.cs -g"*.cs" -g"*.razor"; then
  echo "[B3] FAIL: Build Lab DTO projection is not wired into shared shell renderers."
  exit 5
fi

if rg -q "RunPlanner|TimelineRecommendation|Street mage with social focus" Chummer.Blazor/Components/Shared/BuildLabPanel.razor; then
  echo "[B3] FAIL: Build Lab demo placeholders are still standing in for the shared shell flow."
  exit 6
fi

if ! rg -q "BuildLabPanel" Chummer.Blazor/Components/Pages/Home.razor; then
  echo "[B3] FAIL: BuildLab panel is not present on the landing page composition."
  exit 7
fi

if ! rg -q "data-build-lab|Build Lab Intake|Explain \\+ Source" Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B3] FAIL: Build Lab intake shell chrome is missing from the Blazor section pane."
  exit 4
fi

if ! rg -q "Variant Comparison|25 / 50 / 100 Karma|data-build-lab-variants|data-build-lab-timelines" Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B3] FAIL: Build Lab compare/timeline shell chrome is missing from the Blazor section pane."
  exit 8
fi

if ! rg -q "Export \\+ Hand-off|data-build-lab-export|data-build-lab-export-target|data-build-lab-export-payload" Chummer.Blazor/Components/Shell/SectionPane.razor; then
  echo "[B3] FAIL: Build Lab export hand-off shell chrome is missing from the Blazor section pane."
  exit 9
fi

echo "[B3] PASS: Build Lab surfaces are present and contract-oriented."
