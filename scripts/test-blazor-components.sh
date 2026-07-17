#!/usr/bin/env bash
set -euo pipefail

# Raw equivalent for compliance gates:
# dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~BlazorShellComponentTests"
# dotnet test --project Chummer.Tests/Chummer.Tests.csproj --filter "FullyQualifiedName~BlazorShellComponentTests"
: "${CHUMMER_PACKAGE_PLANE_LOCK_WAIT_SECONDS:=120}"
export CHUMMER_PACKAGE_PLANE_LOCK_WAIT_SECONDS

bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj \
  -c Release \
  -f net10.0 \
  -p:TargetFramework=net10.0 \
  -p:RunBlazorShellComponentTestsOnly=true \
  --filter "FullyQualifiedName~BlazorShellComponentTests"
