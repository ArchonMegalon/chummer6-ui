#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

echo "[P5-CONTRACTS] checking contract package consumption boundary..."

if rg -n 'ProjectReference Include="..\\Chummer.Contracts\\Chummer.Contracts.csproj"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj >/dev/null; then
  echo "[P5-CONTRACTS] FAIL: duplicated Chummer.Contracts source project is still referenced."
  exit 3
fi

if ! rg -n 'PackageReference Include="\$\(ChummerContractsPackageId\)" Version="\$\(ChummerContractsPackageVersion\)"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj >/dev/null; then
  echo "[P5-CONTRACTS] FAIL: authoritative contracts package references are missing."
  exit 4
fi

if rg -n -F 'Chummer.Contracts/Chummer.Contracts.csproj' \
  Chummer.Blazor/Dockerfile \
  Docker/Dockerfile.tests \
  scripts/ai/day1-p1-setup.sh >/dev/null; then
  echo "[P5-CONTRACTS] FAIL: build scripts still hard-code the duplicated contracts project."
  exit 5
fi

if [ -d Chummer.Contracts ] || [ -d Chummer.Presentation/Contracts ]; then
  echo "[P5-CONTRACTS] FAIL: duplicated Chummer.Contracts source tree still exists in the UI repo."
  exit 6
fi

if rg -n '^namespace[[:space:]]+Chummer\.Contracts(\.|;|$)' \
  Chummer.Presentation Chummer.Blazor Chummer.Avalonia Chummer.Tests \
  -g '*.cs' -g '!**/obj/**' -g '!**/bin/**' >/dev/null; then
  echo "[P5-CONTRACTS] FAIL: UI repo still declares local Chummer.Contracts namespaces instead of consuming the package."
  exit 11
fi

echo "[P5-CONTRACTS] PASS: UI consumes contracts via package-only boundary."
