#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

test -f docs/COMPATIBILITY_CARGO.md
test -f docs/WORKBENCH_RELEASE_SIGNOFF.md

dotnet build "$repo_root/../chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj" --nologo -m:1 >/dev/null
dotnet build "$repo_root/../chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj" --nologo -m:1 >/dev/null

echo "[verify] checking contract package consumption..."

if rg -n 'ProjectReference Include="..\\Chummer.Contracts\\Chummer.Contracts.csproj"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj >/dev/null; then
  echo "[verify] FAIL: duplicated Chummer.Contracts source project is still referenced."
  exit 3
fi

if ! rg -n 'PackageReference Include="\$\(ChummerContractsPackageId\)" Version="\$\(ChummerContractsPackageVersion\)"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj >/dev/null; then
  echo "[verify] FAIL: authoritative contracts package references are missing."
  exit 4
fi

if rg -n -F 'Chummer.Contracts/Chummer.Contracts.csproj' \
  Chummer.Blazor/Dockerfile \
  Docker/Dockerfile.tests \
  scripts/ai/day1-p1-setup.sh >/dev/null; then
  echo "[verify] FAIL: build scripts still hard-code the duplicated contracts project."
  exit 5
fi

if [ -d Chummer.Contracts ]; then
  echo "[verify] FAIL: duplicated Chummer.Contracts source tree still exists in the UI repo."
  exit 6
fi

if rg -n '^namespace[[:space:]]+Chummer\.Contracts(\.|;|$)' \
  Chummer.Presentation Chummer.Blazor Chummer.Avalonia Chummer.Tests \
  -g '*.cs' -g '!**/obj/**' -g '!**/bin/**' >/dev/null; then
  echo "[verify] FAIL: UI repo still declares local Chummer.Contracts namespaces instead of consuming the package."
  exit 11
fi

if [ -d Chummer.Session.Web ] || [ -d Chummer.Coach.Web ]; then
  echo "[verify] FAIL: play/mobile heads still exist in the presentation repo."
  exit 7
fi

if rg -n 'Chummer\.Session\.Web|Chummer\.Coach\.Web' Chummer.sln Chummer.Presentation.sln Docker/Dockerfile.tests docker-compose.yml scripts/ai/day1-p1-setup.sh >/dev/null; then
  echo "[verify] FAIL: repo build wiring still references removed play/mobile heads."
  exit 8
fi

echo "[verify] checking post-split ownership guard..."
bash scripts/ai/milestones/b11-post-split-ownership-check.sh

echo "[verify] checking NPC Persona Studio backlog mapping guard..."
bash scripts/ai/milestones/b11-npc-persona-studio-check.sh

echo "[verify] checking UI milestone coverage registry guard..."
bash scripts/ai/milestones/ui-milestone-coverage-check.sh

echo "[verify] checking ui-kit shell chrome guard..."
bash scripts/ai/milestones/p5-ui-kit-shell-chrome-check.sh

echo "[verify] checking ui-kit design token/theme queue guard..."
bash scripts/ai/milestones/p5-ui-kit-design-token-check.sh

echo "[verify] checking ui-kit accessibility/state guard..."
bash scripts/ai/milestones/p5-ui-kit-accessibility-state-check.sh

echo "[verify] checking ui-kit package-only boundary guard..."
if ! rg -n 'PackageReference Include="\$\(ChummerUiKitPackageId\)" Version="\$\(ChummerUiKitPackageVersion\)"' \
  Chummer.Presentation/Chummer.Presentation.csproj >/dev/null; then
  echo "[verify] FAIL: Chummer.Presentation must consume Chummer.Ui.Kit as a package reference."
  exit 9
fi

if rg -n '\b(class|record)\s+(TokenCanon|ThemeCompiler|ShellChrome|AccessibilityState|Banner|StaleStateBadge|ApprovalChip|OfflineBanner)\b|\b(static\s+)?UiAdapterPayload\s+Adapt(ShellChrome|AccessibilityState|Banner|StaleStateBadge|ApprovalChip|OfflineBanner)\s*\(' \
  Chummer.Presentation Chummer.Blazor Chummer.Avalonia Chummer.Tests -g '*.cs' >/dev/null; then
  echo "[verify] FAIL: source-copied ui-kit token/theme/shell/accessibility primitives were reintroduced."
  exit 10
fi

if ! rg -n '^# Compatibility Cargo$|`Chummer/`|`ChummerDataViewer/`|`CrashHandler/`|`TextblockConverter/`|`Translator/`' \
  docs/COMPATIBILITY_CARGO.md >/dev/null; then
  echo "[verify] FAIL: compatibility cargo inventory must explicitly document retained legacy roots." >&2
  exit 12
fi

if ! rg -n 'b3-build-lab-check\.sh|b10-contact-network-check\.sh|b9-campaign-journal-check\.sh|b8-runtime-inspector-check\.sh|b12-generated-asset-dispatch-check\.sh|b11-npc-persona-studio-check\.sh|b4-gm-board-spider-feed-check\.sh|b13-accessibility-signoff-check\.sh|b7-browser-isolation-check\.sh|b2-browse-virtualization-check\.sh|RulesetExplainRenderer\.cs' \
  docs/WORKBENCH_RELEASE_SIGNOFF.md >/dev/null; then
  echo "[verify] FAIL: workbench release signoff must keep E0/F0 evidence explicit." >&2
  exit 13
fi

echo "[verify] checking B13 accessibility signoff guard..."
CHUMMER_B13_TESTS_REQUIRED=1 bash scripts/ai/milestones/b13-accessibility-signoff-check.sh

echo "[verify] checking B7 browser deployment signoff guard..."
CHUMMER_B7_RUNTIME_REQUIRED=1 CHUMMER_B7_ALLOW_RUNTIME_SKIP=0 \
bash scripts/ai/milestones/b7-browser-isolation-check.sh

echo "[verify] checking B12 generated-asset dispatch/review guard..."
bash scripts/ai/milestones/b12-generated-asset-dispatch-check.sh

echo "[verify] checking B9 campaign journal planner/calendar guard..."
bash scripts/ai/milestones/b9-campaign-journal-check.sh

echo "[verify] PASS"
