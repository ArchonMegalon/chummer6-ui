#!/usr/bin/env bash
set -euo pipefail

echo "[B11] checking post-split session/coach ownership seams..."

if [ -d Chummer.Session.Web ] || [ -d Chummer.Coach.Web ]; then
  echo "[B11] FAIL: shipped /session or /coach hosts reappeared in the presentation repo."
  exit 3
fi

if rg -n 'Chummer\.Session\.Web|Chummer\.Coach\.Web' \
  Chummer.sln \
  Chummer.Presentation.sln \
  Docker/Dockerfile.tests \
  docker-compose.yml \
  scripts/ai/day1-p1-setup.sh >/dev/null; then
  echo "[B11] FAIL: repo wiring still references removed play/mobile hosts."
  exit 4
fi

if ! rg -q "public interface ISessionClient|public sealed class HttpSessionClient" \
  Chummer.Presentation/ISessionClient.cs \
  Chummer.Presentation/HttpSessionClient.cs; then
  echo "[B11] FAIL: shared session API seam is missing from presentation."
  exit 5
fi

if ! rg -q "TryAddSingleton<ISessionClient, HttpSessionClient>|TryAddSingleton<ISessionClient, InProcessSessionClient>" \
  Chummer.Desktop.Runtime/ServiceCollectionDesktopRuntimeExtensions.cs; then
  echo "[B11] FAIL: desktop runtime no longer preserves the shared ISessionClient seam."
  exit 6
fi

if ! rg -q "AiCoachLaunchQuery.BuildRelativeUri" \
  Chummer.Blazor/Components/Layout/DesktopShell.Coach.cs \
  Chummer.Avalonia/MainWindow.CoachSidecar.cs \
  Chummer.Avalonia/MainWindowCoachSidecarProjector.cs; then
  echo "[B11] FAIL: workbench coach sidecars no longer use the shared coach deep-link seam."
  exit 7
fi

if ! rg -q "public interface IWorkbenchCoachApiClient|public interface IAvaloniaCoachSidecarClient" \
  Chummer.Blazor/IWorkbenchCoachApiClient.cs \
  Chummer.Avalonia/IAvaloniaCoachSidecarClient.cs; then
  echo "[B11] FAIL: workbench coach sidecar client seams are missing."
  exit 8
fi

if ! rg -q "CHUMMER_PORTAL_SESSION_PROXY_URL|CHUMMER_PORTAL_COACH_PROXY_URL|CHUMMER_RUN_URL" \
  docker-compose.yml \
  README.md; then
  echo "[B11] FAIL: portal/proxy ownership guidance for external /session and /coach hosts is missing."
  exit 9
fi

if ! rg -q "chummer-play|Chummer.Ui.Kit|shared UI-kit primitives|workbench-side coach sidecars|portal/proxy expectations" \
  README.md; then
  echo "[B11] FAIL: repo guidance no longer documents the post-split ownership map."
  exit 10
fi

echo "[B11] PASS: presentation owns shared seams only; shipped play/mobile hosts stay external."
