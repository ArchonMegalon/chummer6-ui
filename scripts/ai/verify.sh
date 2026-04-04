#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

test -f docs/COMPATIBILITY_CARGO.md
test -f docs/WORKBENCH_RELEASE_SIGNOFF.md

if [ "${CHUMMER_VERIFY_CROSS_REPO_BUILDS:-0}" = "1" ]; then
  echo "[verify] running opt-in cross-repo contract builds..."

  if [ -f "$repo_root/../chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj" ]; then
    dotnet build "$repo_root/../chummer-hub-registry/Chummer.Hub.Registry.Contracts/Chummer.Hub.Registry.Contracts.csproj" --nologo -m:1 >/dev/null
  else
    echo "[verify] WARN: skipping hub-registry contracts build (sibling repo not present)."
  fi

  if [ -f "$repo_root/../chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj" ]; then
    dotnet build "$repo_root/../chummer.run-services/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj" --nologo -m:1 >/dev/null
  else
    echo "[verify] WARN: skipping run-services contracts build (sibling repo not present)."
  fi
fi

echo "[verify] checking contract package consumption..."
bash scripts/ai/milestones/p5-contract-package-boundary-check.sh

echo "[verify] checking desktop runtime resilience regression guard..."
bash scripts/ai/test.sh Chummer.Desktop.Runtime.Tests/Chummer.Desktop.Runtime.Tests.csproj -v minimal

if ! rg -n '<ChummerUseLocalCompatibilityTree Condition="'\''\$\(ChummerUseLocalCompatibilityTree\)'\'' == '\'''\''">false</ChummerUseLocalCompatibilityTree>' \
  Directory.Build.props >/dev/null; then
  echo "[verify] FAIL: the local compatibility tree must be opt-in instead of the ambient default."
  exit 20
fi

if ! rg -n 'ChummerRunContractsPackageId|ChummerRunContractsPackageVersion|ChummerHubRegistryContractsPackageId|ChummerHubRegistryContractsPackageVersion|ChummerLocalHubRegistryContractsProject' \
  Directory.Build.props >/dev/null; then
  echo "[verify] FAIL: run-service and hub-registry contract package-plane properties are missing from Directory.Build.props."
  exit 21
fi

if ! rg -n 'with-package-plane\.sh' \
  scripts/ai/build.sh scripts/ai/test.sh scripts/ai/restore.sh \
  scripts/ai/milestones/b13-accessibility-signoff-check.sh \
  scripts/test-blazor-components.sh scripts/build-desktop-installer.sh >/dev/null; then
  echo "[verify] FAIL: repo-local build, test, restore, and installer flows must route through the package-plane helper."
  exit 22
fi

if ! rg -n 'PackageReference Include="\$\(ChummerHubRegistryContractsPackageId\)" Version="\$\(ChummerHubRegistryContractsPackageVersion\)"' \
  Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj >/dev/null; then
  echo "[verify] FAIL: desktop runtime must consume hub-registry contracts through the package plane fallback."
  exit 23
fi

if ! rg -n 'PackageReference Include="\$\(ChummerRunContractsPackageId\)" Version="\$\(ChummerRunContractsPackageVersion\)"' \
  Chummer.Presentation/Chummer.Presentation.csproj \
  Chummer.Blazor/Chummer.Blazor.csproj \
  Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj \
  Chummer.Avalonia/Chummer.Avalonia.csproj \
  Chummer.Tests/Chummer.Tests.csproj >/dev/null; then
  echo "[verify] FAIL: workbench projects must consume run contracts through the package plane fallback."
  exit 25
fi

if rg -n 'HintPath>.*chummer-hub-registry' Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj >/dev/null; then
  echo "[verify] FAIL: desktop runtime must not depend on sibling hub-registry build artifacts."
  exit 24
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

echo "[verify] checking ruleset-specific workbench adaptation guard..."
bash scripts/ai/milestones/ruleset-ui-adaptation-check.sh

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

echo "[verify] checking MIG-095 workspace benchmark ownership guard..."
bash scripts/ai/milestones/mig-095-benchmark-ownership-check.sh

if ! rg -n 'ChummerCampaignContractsPackageId|ChummerCampaignContractsPackageVersion|ChummerLocalCampaignContractsProject' \
  Directory.Build.props >/dev/null; then
  echo "[verify] FAIL: campaign-contract package plane properties are missing from Directory.Build.props."
  exit 14
fi

if ! rg -n 'PackageReference Include="\$\(ChummerCampaignContractsPackageId\)" Version="\$\(ChummerCampaignContractsPackageVersion\)"' \
  Chummer.Blazor/Chummer.Blazor.csproj Chummer.Tests/Chummer.Tests.csproj >/dev/null; then
  echo "[verify] FAIL: Blazor and test projects must consume Chummer.Campaign.Contracts through the package plane fallback."
  exit 15
fi

if rg -n 'namespace Chummer\.Campaign\.Contracts' \
  Chummer.Blazor Chummer.Presentation Chummer.Avalonia Chummer.Tests -g '*.cs' >/dev/null; then
  echo "[verify] FAIL: campaign contract shadows were reintroduced in the UI repo."
  exit 16
fi

if rg -n '\b(class|record)\s+(TokenCanon|ThemeCompiler|ShellChrome|AccessibilityState|Banner|StaleStateBadge|ApprovalChip|OfflineBanner|DenseTableHeader|DenseRowMetadata|ExplainChip|SpiderStatusCard|ArtifactStatusCard)\b|\b(static\s+)?UiAdapterPayload\s+Adapt(ShellChrome|AccessibilityState|Banner|StaleStateBadge|ApprovalChip|OfflineBanner|DenseTableHeader|DenseRowMetadata|ExplainChip|SpiderStatusCard|ArtifactStatusCard)\s*\(' \
  Chummer.Presentation Chummer.Blazor Chummer.Avalonia Chummer.Tests -g '*.cs' >/dev/null; then
  echo "[verify] FAIL: source-copied ui-kit token/theme/shell/accessibility primitives were reintroduced."
  exit 10
fi

if ! rg -n '^# Compatibility Cargo$|`Chummer/`|`ChummerDataViewer/`|`TextblockConverter/`|`Translator/`' \
  docs/COMPATIBILITY_CARGO.md >/dev/null; then
  echo "[verify] FAIL: compatibility cargo inventory must explicitly document retained legacy roots." >&2
  exit 12
fi

if ! rg -n 'b3-build-lab-check\.sh|b10-contact-network-check\.sh|b9-campaign-journal-check\.sh|b8-runtime-inspector-check\.sh|b12-generated-asset-dispatch-check\.sh|b11-npc-persona-studio-check\.sh|b4-gm-board-spider-feed-check\.sh|b13-accessibility-signoff-check\.sh|b14-flagship-ui-release-gate\.sh|materialize-desktop-executable-exit-gate\.sh|b7-browser-isolation-check\.sh|b2-browse-virtualization-check\.sh|RulesetExplainRenderer\.cs' \
  docs/WORKBENCH_RELEASE_SIGNOFF.md >/dev/null; then
  echo "[verify] FAIL: workbench release signoff must keep E0/F0 evidence explicit." >&2
  exit 13
fi

if ! rg -n 'b15-localization-release-gate\.sh' docs/WORKBENCH_RELEASE_SIGNOFF.md >/dev/null; then
  echo "[verify] FAIL: workbench release signoff must keep localization release evidence explicit." >&2
  exit 26
fi

echo "[verify] checking B13 accessibility signoff guard..."
CHUMMER_B13_TESTS_REQUIRED=1 bash scripts/ai/milestones/b13-accessibility-signoff-check.sh

echo "[verify] checking B14 flagship UI release gate..."
bash scripts/ai/milestones/b14-flagship-ui-release-gate.sh

echo "[verify] checking W1 desktop executable exit gate..."
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh

echo "[verify] checking W1 desktop executable gate blocking findings aliases..."
python3 - <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

receipt_path = Path(".codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json")
payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))

reasons = payload.get("reasons")
blocking_findings = payload.get("blockingFindings")
blocking_findings_alias = payload.get("blocking_findings")
blocking_findings_count = payload.get("blockingFindingsCount")
blocking_findings_count_alias = payload.get("blocking_findings_count")

if not isinstance(reasons, list):
    raise SystemExit("verify gate failed: desktop executable gate payload is missing reasons list.")
if not isinstance(blocking_findings, list):
    raise SystemExit("verify gate failed: desktop executable gate payload is missing blockingFindings list.")
if not isinstance(blocking_findings_alias, list):
    raise SystemExit("verify gate failed: desktop executable gate payload is missing blocking_findings list.")
if blocking_findings != reasons or blocking_findings_alias != reasons:
    raise SystemExit(
        "verify gate failed: desktop executable gate payload carries blocking-findings alias drift between reasons/blockingFindings/blocking_findings."
    )
if int(blocking_findings_count or -1) != len(reasons):
    raise SystemExit("verify gate failed: desktop executable gate payload blockingFindingsCount does not match reasons count.")
if int(blocking_findings_count_alias or -1) != len(reasons):
    raise SystemExit("verify gate failed: desktop executable gate payload blocking_findings_count does not match reasons count.")
PY

echo "[verify] checking W1 desktop executable gate fail-close mutation for unexpected desktopTupleCoverage keys..."
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi

desktop_tuple_mutation_release_channel="$(mktemp)"
desktop_tuple_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$desktop_tuple_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    desktop_tuple_coverage = {}
payload["desktopTupleCoverage"] = desktop_tuple_coverage
desktop_tuple_coverage["bonus_noncanonical_tuple_coverage_key"] = "unexpected"
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$desktop_tuple_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$desktop_tuple_mutation_output" 2>&1
desktop_tuple_mutation_exit=$?
set -e

if [[ "$desktop_tuple_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject unexpected desktopTupleCoverage keys."
  cat "$desktop_tuple_mutation_output"
  rm -f "$desktop_tuple_mutation_release_channel" "$desktop_tuple_mutation_output"
  exit 27
fi

if ! rg -F "Release channel desktopTupleCoverage has unexpected keys:" "$desktop_tuple_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit unexpected desktopTupleCoverage key marker."
  cat "$desktop_tuple_mutation_output"
  rm -f "$desktop_tuple_mutation_release_channel" "$desktop_tuple_mutation_output"
  exit 28
fi

rm -f "$desktop_tuple_mutation_release_channel" "$desktop_tuple_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for unexpected desktop install artifact keys..."
desktop_artifact_key_mutation_release_channel="$(mktemp)"
desktop_artifact_key_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$desktop_artifact_key_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
artifacts = payload.get("artifacts")
if not isinstance(artifacts, list):
    artifacts = []
    payload["artifacts"] = artifacts

desktop_artifact = None
for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    platform = str(artifact.get("platform") or "").strip().lower()
    kind = str(artifact.get("kind") or "").strip().lower()
    if platform not in {"windows", "macos", "linux"}:
        continue
    if platform == "macos":
        if kind not in {"installer", "dmg", "pkg"}:
            continue
    elif kind != "installer":
        continue
    desktop_artifact = artifact
    break

if desktop_artifact is None:
    desktop_artifact = {
        "artifactId": "mutation-desktop-artifact",
        "platform": "linux",
        "kind": "installer",
        "head": "avalonia",
        "rid": "linux-x64",
        "fileName": "mutation-desktop-artifact.deb",
        "channelId": str(payload.get("channelId") or payload.get("channel") or "").strip(),
        "version": str(payload.get("version") or payload.get("releaseVersion") or "").strip(),
        "generated_at": str(payload.get("generated_at") or payload.get("generatedAt") or "").strip(),
    }
    artifacts.append(desktop_artifact)

desktop_artifact["bonus_noncanonical_install_artifact_key"] = "unexpected"
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$desktop_artifact_key_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$desktop_artifact_key_mutation_output" 2>&1
desktop_artifact_key_mutation_exit=$?
set -e

if [[ "$desktop_artifact_key_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject unexpected desktop install artifact keys."
  cat "$desktop_artifact_key_mutation_output"
  rm -f "$desktop_artifact_key_mutation_release_channel" "$desktop_artifact_key_mutation_output"
  exit 29
fi

if ! rg -F "Release channel desktop install artifact(s) have unexpected keys:" "$desktop_artifact_key_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit unexpected desktop install artifact key marker."
  cat "$desktop_artifact_key_mutation_output"
  rm -f "$desktop_artifact_key_mutation_release_channel" "$desktop_artifact_key_mutation_output"
  exit 30
fi

rm -f "$desktop_artifact_key_mutation_release_channel" "$desktop_artifact_key_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for promotedInstallerTuples artifact metadata drift..."
promoted_tuple_row_mutation_release_channel="$(mktemp)"
promoted_tuple_row_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$promoted_tuple_row_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("promotedInstallerTuples")
if not isinstance(rows, list) or not rows:
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.promotedInstallerTuples rows in release channel fixture.")
first_row = rows[0]
if not isinstance(first_row, dict):
    raise SystemExit("verify gate failed: expected promotedInstallerTuples rows to be object values.")
first_row["artifactId"] = "tampered-promoted-installer-artifact-id"
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$promoted_tuple_row_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$promoted_tuple_row_mutation_output" 2>&1
promoted_tuple_row_mutation_exit=$?
set -e

if [[ "$promoted_tuple_row_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject promotedInstallerTuples artifact metadata drift."
  cat "$promoted_tuple_row_mutation_output"
  rm -f "$promoted_tuple_row_mutation_release_channel" "$promoted_tuple_row_mutation_output"
  exit 31
fi

if ! rg -F "Release channel desktopTupleCoverage.promotedInstallerTuples object rows do not match promoted installer artifact metadata." "$promoted_tuple_row_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit promotedInstallerTuples metadata drift marker."
  cat "$promoted_tuple_row_mutation_output"
  rm -f "$promoted_tuple_row_mutation_release_channel" "$promoted_tuple_row_mutation_output"
  exit 32
fi

rm -f "$promoted_tuple_row_mutation_release_channel" "$promoted_tuple_row_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for promotedPlatformHeadRidTuples inventory drift..."
promoted_platform_head_rid_tuple_mutation_release_channel="$(mktemp)"
promoted_platform_head_rid_tuple_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$promoted_platform_head_rid_tuple_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("promotedPlatformHeadRidTuples")
if not isinstance(rows, list) or not rows:
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.promotedPlatformHeadRidTuples rows in release channel fixture.")
rows[0] = "tampered-head:tampered-rid:windows"
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$promoted_platform_head_rid_tuple_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$promoted_platform_head_rid_tuple_mutation_output" 2>&1
promoted_platform_head_rid_tuple_mutation_exit=$?
set -e

if [[ "$promoted_platform_head_rid_tuple_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject promotedPlatformHeadRidTuples inventory drift."
  cat "$promoted_platform_head_rid_tuple_mutation_output"
  rm -f "$promoted_platform_head_rid_tuple_mutation_release_channel" "$promoted_platform_head_rid_tuple_mutation_output"
  exit 33
fi

if ! rg -F "Release channel desktopTupleCoverage promotedPlatformHeadRidTuples inventory does not match promoted installer tuples." "$promoted_platform_head_rid_tuple_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit promotedPlatformHeadRidTuples inventory drift marker."
  cat "$promoted_platform_head_rid_tuple_mutation_output"
  rm -f "$promoted_platform_head_rid_tuple_mutation_release_channel" "$promoted_platform_head_rid_tuple_mutation_output"
  exit 34
fi

rm -f "$promoted_platform_head_rid_tuple_mutation_release_channel" "$promoted_platform_head_rid_tuple_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for missingRequiredPlatformHeadRidTuples inventory drift..."
missing_required_platform_head_rid_tuple_mutation_release_channel="$(mktemp)"
missing_required_platform_head_rid_tuple_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$missing_required_platform_head_rid_tuple_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("missingRequiredPlatformHeadRidTuples")
if not isinstance(rows, list):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.missingRequiredPlatformHeadRidTuples list in release channel fixture.")
rows.append("tampered-head:tampered-rid:windows")
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$missing_required_platform_head_rid_tuple_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$missing_required_platform_head_rid_tuple_mutation_output" 2>&1
missing_required_platform_head_rid_tuple_mutation_exit=$?
set -e

if [[ "$missing_required_platform_head_rid_tuple_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject missingRequiredPlatformHeadRidTuples inventory drift."
  cat "$missing_required_platform_head_rid_tuple_mutation_output"
  rm -f "$missing_required_platform_head_rid_tuple_mutation_release_channel" "$missing_required_platform_head_rid_tuple_mutation_output"
  exit 35
fi

if ! rg -F "Release channel desktopTupleCoverage missingRequiredPlatformHeadRidTuples inventory does not match promoted installer tuples." "$missing_required_platform_head_rid_tuple_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit missingRequiredPlatformHeadRidTuples inventory drift marker."
  cat "$missing_required_platform_head_rid_tuple_mutation_output"
  rm -f "$missing_required_platform_head_rid_tuple_mutation_release_channel" "$missing_required_platform_head_rid_tuple_mutation_output"
  exit 36
fi

rm -f "$missing_required_platform_head_rid_tuple_mutation_release_channel" "$missing_required_platform_head_rid_tuple_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for missingRequiredPlatformHeadPairs inventory drift..."
missing_required_platform_head_pairs_mutation_release_channel="$(mktemp)"
missing_required_platform_head_pairs_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$missing_required_platform_head_pairs_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("missingRequiredPlatformHeadPairs")
if not isinstance(rows, list):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.missingRequiredPlatformHeadPairs list in release channel fixture.")
rows.append("tampered-head:tampered-platform")
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$missing_required_platform_head_pairs_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$missing_required_platform_head_pairs_mutation_output" 2>&1
missing_required_platform_head_pairs_mutation_exit=$?
set -e

if [[ "$missing_required_platform_head_pairs_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject missingRequiredPlatformHeadPairs inventory drift."
  cat "$missing_required_platform_head_pairs_mutation_output"
  rm -f "$missing_required_platform_head_pairs_mutation_release_channel" "$missing_required_platform_head_pairs_mutation_output"
  exit 37
fi

if ! rg -F "Release channel desktopTupleCoverage missingRequiredPlatformHeadPairs inventory does not match promoted installer tuples." "$missing_required_platform_head_pairs_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit missingRequiredPlatformHeadPairs inventory drift marker."
  cat "$missing_required_platform_head_pairs_mutation_output"
  rm -f "$missing_required_platform_head_pairs_mutation_release_channel" "$missing_required_platform_head_pairs_mutation_output"
  exit 38
fi

rm -f "$missing_required_platform_head_pairs_mutation_release_channel" "$missing_required_platform_head_pairs_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for missingRequiredPlatforms inventory drift..."
missing_required_platforms_mutation_release_channel="$(mktemp)"
missing_required_platforms_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$missing_required_platforms_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("missingRequiredPlatforms")
if not isinstance(rows, list):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.missingRequiredPlatforms list in release channel fixture.")
rows.append("tampered-platform")
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$missing_required_platforms_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$missing_required_platforms_mutation_output" 2>&1
missing_required_platforms_mutation_exit=$?
set -e

if [[ "$missing_required_platforms_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject missingRequiredPlatforms inventory drift."
  cat "$missing_required_platforms_mutation_output"
  rm -f "$missing_required_platforms_mutation_release_channel" "$missing_required_platforms_mutation_output"
  exit 39
fi

if ! rg -F "Release channel desktopTupleCoverage missingRequiredPlatforms inventory does not match promoted installer tuples." "$missing_required_platforms_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit missingRequiredPlatforms inventory drift marker."
  cat "$missing_required_platforms_mutation_output"
  rm -f "$missing_required_platforms_mutation_release_channel" "$missing_required_platforms_mutation_output"
  exit 40
fi

rm -f "$missing_required_platforms_mutation_release_channel" "$missing_required_platforms_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for missingRequiredHeads inventory drift..."
missing_required_heads_mutation_release_channel="$(mktemp)"
missing_required_heads_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$missing_required_heads_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("missingRequiredHeads")
if not isinstance(rows, list):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.missingRequiredHeads list in release channel fixture.")
rows.append("tampered-head")
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$missing_required_heads_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$missing_required_heads_mutation_output" 2>&1
missing_required_heads_mutation_exit=$?
set -e

if [[ "$missing_required_heads_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject missingRequiredHeads inventory drift."
  cat "$missing_required_heads_mutation_output"
  rm -f "$missing_required_heads_mutation_release_channel" "$missing_required_heads_mutation_output"
  exit 41
fi

if ! rg -F "Release channel desktopTupleCoverage missingRequiredHeads inventory does not match promoted installer tuples." "$missing_required_heads_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit missingRequiredHeads inventory drift marker."
  cat "$missing_required_heads_mutation_output"
  rm -f "$missing_required_heads_mutation_release_channel" "$missing_required_heads_mutation_output"
  exit 42
fi

rm -f "$missing_required_heads_mutation_release_channel" "$missing_required_heads_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for requiredDesktopPlatformHeadRidTuples missing required platform/head pair coverage..."
required_platform_head_rid_tuples_pair_coverage_mutation_release_channel="$(mktemp)"
required_platform_head_rid_tuples_pair_coverage_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$required_platform_head_rid_tuples_pair_coverage_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("requiredDesktopPlatformHeadRidTuples")
if not isinstance(rows, list) or not rows:
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.requiredDesktopPlatformHeadRidTuples list in release channel fixture.")
desktop_tuple_coverage["requiredDesktopPlatformHeadRidTuples"] = rows[1:]
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$required_platform_head_rid_tuples_pair_coverage_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$required_platform_head_rid_tuples_pair_coverage_mutation_output" 2>&1
required_platform_head_rid_tuples_pair_coverage_mutation_exit=$?
set -e

if [[ "$required_platform_head_rid_tuples_pair_coverage_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject requiredDesktopPlatformHeadRidTuples missing required desktop platform/head pair coverage."
  cat "$required_platform_head_rid_tuples_pair_coverage_mutation_output"
  rm -f "$required_platform_head_rid_tuples_pair_coverage_mutation_release_channel" "$required_platform_head_rid_tuples_pair_coverage_mutation_output"
  exit 43
fi

if ! rg -F "Release channel desktopTupleCoverage requiredDesktopPlatformHeadRidTuples is missing required desktop platform/head pair coverage:" "$required_platform_head_rid_tuples_pair_coverage_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit requiredDesktopPlatformHeadRidTuples missing required desktop platform/head pair coverage marker."
  cat "$required_platform_head_rid_tuples_pair_coverage_mutation_output"
  rm -f "$required_platform_head_rid_tuples_pair_coverage_mutation_release_channel" "$required_platform_head_rid_tuples_pair_coverage_mutation_output"
  exit 44
fi

rm -f "$required_platform_head_rid_tuples_pair_coverage_mutation_release_channel" "$required_platform_head_rid_tuples_pair_coverage_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for requiredDesktopPlatforms missing required policy platform coverage..."
required_desktop_platforms_mutation_release_channel="$(mktemp)"
required_desktop_platforms_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$required_desktop_platforms_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("requiredDesktopPlatforms")
if not isinstance(rows, list) or not rows:
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.requiredDesktopPlatforms list in release channel fixture.")
desktop_tuple_coverage["requiredDesktopPlatforms"] = rows[1:]
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$required_desktop_platforms_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$required_desktop_platforms_mutation_output" 2>&1
required_desktop_platforms_mutation_exit=$?
set -e

if [[ "$required_desktop_platforms_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject requiredDesktopPlatforms missing required policy platform coverage."
  cat "$required_desktop_platforms_mutation_output"
  rm -f "$required_desktop_platforms_mutation_release_channel" "$required_desktop_platforms_mutation_output"
  exit 45
fi

if ! rg -F "Release channel desktopTupleCoverage requiredDesktopPlatforms is missing required policy platform(s):" "$required_desktop_platforms_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit requiredDesktopPlatforms missing required policy platform coverage marker."
  cat "$required_desktop_platforms_mutation_output"
  rm -f "$required_desktop_platforms_mutation_release_channel" "$required_desktop_platforms_mutation_output"
  exit 46
fi

rm -f "$required_desktop_platforms_mutation_release_channel" "$required_desktop_platforms_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for requiredDesktopHeads missing required policy head coverage..."
required_desktop_heads_policy_mutation_release_channel="$(mktemp)"
required_desktop_heads_policy_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$required_desktop_heads_policy_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("requiredDesktopHeads")
if not isinstance(rows, list) or not rows:
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.requiredDesktopHeads list in release channel fixture.")
desktop_tuple_coverage["requiredDesktopHeads"] = rows[1:]
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$required_desktop_heads_policy_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$required_desktop_heads_policy_mutation_output" 2>&1
required_desktop_heads_policy_mutation_exit=$?
set -e

if [[ "$required_desktop_heads_policy_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject requiredDesktopHeads missing required policy head coverage."
  cat "$required_desktop_heads_policy_mutation_output"
  rm -f "$required_desktop_heads_policy_mutation_release_channel" "$required_desktop_heads_policy_mutation_output"
  exit 47
fi

if ! rg -F "Release channel desktopTupleCoverage requiredDesktopHeads is missing required policy head(s):" "$required_desktop_heads_policy_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit requiredDesktopHeads missing required policy head coverage marker."
  cat "$required_desktop_heads_policy_mutation_output"
  rm -f "$required_desktop_heads_policy_mutation_release_channel" "$required_desktop_heads_policy_mutation_output"
  exit 48
fi

rm -f "$required_desktop_heads_policy_mutation_release_channel" "$required_desktop_heads_policy_mutation_output"

echo "[verify] checking W1 desktop executable gate fail-close mutation for requiredDesktopHeads missing canonical required head coverage..."
required_desktop_heads_canonical_mutation_release_channel="$(mktemp)"
required_desktop_heads_canonical_mutation_output="$(mktemp)"
python3 - "$release_channel_path_default" "$required_desktop_heads_canonical_mutation_release_channel" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
desktop_tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(desktop_tuple_coverage, dict):
    raise SystemExit("verify gate failed: expected desktopTupleCoverage object in release channel fixture.")
rows = desktop_tuple_coverage.get("requiredDesktopHeads")
if not isinstance(rows, list) or not rows:
    raise SystemExit("verify gate failed: expected desktopTupleCoverage.requiredDesktopHeads list in release channel fixture.")
desktop_tuple_coverage["requiredDesktopHeads"] = [row for row in rows if str(row).strip().lower() != "blazor-desktop"]
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

set +e
CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE=1 \
CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH="$required_desktop_heads_canonical_mutation_release_channel" \
bash scripts/ai/milestones/materialize-desktop-executable-exit-gate.sh >"$required_desktop_heads_canonical_mutation_output" 2>&1
required_desktop_heads_canonical_mutation_exit=$?
set -e

if [[ "$required_desktop_heads_canonical_mutation_exit" -eq 0 ]]; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate should reject requiredDesktopHeads missing canonical required head coverage."
  cat "$required_desktop_heads_canonical_mutation_output"
  rm -f "$required_desktop_heads_canonical_mutation_release_channel" "$required_desktop_heads_canonical_mutation_output"
  exit 49
fi

if ! rg -F "Release channel desktopTupleCoverage requiredDesktopHeads is missing canonical required head(s):" "$required_desktop_heads_canonical_mutation_output" >/dev/null; then
  echo "[verify] FAIL: verify gate failed: desktop executable gate mutation did not emit requiredDesktopHeads missing canonical required head coverage marker."
  cat "$required_desktop_heads_canonical_mutation_output"
  rm -f "$required_desktop_heads_canonical_mutation_release_channel" "$required_desktop_heads_canonical_mutation_output"
  exit 50
fi

rm -f "$required_desktop_heads_canonical_mutation_release_channel" "$required_desktop_heads_canonical_mutation_output"

echo "[verify] checking B15 localization release gate..."
bash scripts/ai/milestones/b15-localization-release-gate.sh

echo "[verify] checking B7 browser deployment signoff guard..."
CHUMMER_B7_RUNTIME_REQUIRED=1 CHUMMER_B7_ALLOW_RUNTIME_SKIP=0 \
bash scripts/ai/milestones/b7-browser-isolation-check.sh

echo "[verify] checking B12 generated-asset dispatch/review guard..."
bash scripts/ai/milestones/b12-generated-asset-dispatch-check.sh

echo "[verify] checking B9 campaign journal planner/calendar guard..."
bash scripts/ai/milestones/b9-campaign-journal-check.sh

if ! rg -n 'BuildLabHandoffPanel|RulesNavigatorPanel|CreatorPublicationPanel' \
  Chummer.Blazor/Components/Pages/Home.razor >/dev/null; then
  echo "[verify] FAIL: home surface must compose the Build Lab handoff, Rules Navigator, and creator publication panels."
  exit 17
fi

if ! rg -n 'data-build-lab-handoff-showcase|data-rules-navigator-showcase|data-creator-publication-showcase' \
  Chummer.Blazor/Components/Shared/BuildLabHandoffPanel.razor \
  Chummer.Blazor/Components/Shared/RulesNavigatorPanel.razor \
  Chummer.Blazor/Components/Shared/CreatorPublicationPanel.razor >/dev/null; then
  echo "[verify] FAIL: campaign spine showcase panels must keep stable rendered evidence hooks."
  exit 18
fi

if ! rg -n 'CampaignSpineShowcaseComponentTests|BuildLabHandoffPanel_renders_dossier_and_campaign_outputs|RulesNavigatorPanel_renders_grounded_answer_and_reuse_hints|CreatorPublicationPanel_renders_trusted_publication_posture|Home_renders_build_lab_rules_and_creator_showcase_panels' \
  Chummer.Tests/Presentation/CampaignSpineShowcaseComponentTests.cs >/dev/null; then
  echo "[verify] FAIL: campaign spine showcase component tests are missing."
  exit 19
fi

echo "[verify] PASS"
