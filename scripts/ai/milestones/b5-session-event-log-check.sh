#!/usr/bin/env bash
set -euo pipefail

echo "[B5] checking play-shell repo boundary requirements..."

if [ -d Chummer.Session.Web ] || [ -d Chummer.Coach.Web ]; then
  echo "[B5] FAIL: play/mobile heads still live in the presentation repo."
  exit 3
fi

if ! rg -q "public interface ISessionClient|public sealed class HttpSessionClient" \
  Chummer.Presentation/ISessionClient.cs \
  Chummer.Presentation/HttpSessionClient.cs; then
  echo "[B5] FAIL: shared session API seam is missing from the presentation layer."
  exit 4
fi

if ! rg -q "chummer-play|Chummer.Ui.Kit|play/mobile heads now live outside this repo" \
  README.md instructions.md; then
  echo "[B5] FAIL: repo guidance does not point play/mobile ownership at the dedicated repos."
  exit 5
fi

if rg -q 'ProjectReference Include="..\\Chummer.Contracts\\Chummer.Contracts.csproj"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj; then
  echo "[B5] FAIL: shared session seams still compile against the duplicated Chummer.Contracts source project."
  exit 6
fi

if ! rg -q "PackageReference Include=\"\\$\\(ChummerContractsPackageId\\)\" Version=\"\\$\\(ChummerContractsPackageVersion\\)\"" \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Tests/Chummer.Tests.csproj; then
  echo "[B5] FAIL: authoritative contracts package consumption is not wired through the shared session seams."
  exit 7
fi

if [ -d Chummer.Contracts ]; then
  echo "[B5] FAIL: duplicated Chummer.Contracts source tree still exists in the presentation repo."
  exit 10
fi

echo "[B5] PASS: play/mobile repo ownership is externalized and the shared session seam remains."
