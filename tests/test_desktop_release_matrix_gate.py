from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
VERIFY_DESKTOP_RELEASE_MATRIX = REPO_ROOT / "scripts" / "release" / "verify_desktop_release_matrix.sh"
DESKTOP_RUNTIME_CSPROJ = REPO_ROOT / "Chummer.Desktop.Runtime" / "Chummer.Desktop.Runtime.csproj"


def test_desktop_release_matrix_verifies_windows_installer_payloads_against_public_downloads_tree() -> None:
    text = VERIFY_DESKTOP_RELEASE_MATRIX.read_text(encoding="utf-8")
    assert 'repo_root="/docker/chummercomplete/chummer-presentation"' in text
    assert 'cd "$repo_root"' in text
    assert 'hub_proof_public_base="${CHUMMER_PUBLIC_BASE_URL:-https://chummer.run}"' in text
    assert "python3 scripts/materialize_hub_local_release_proof.py \\" in text
    assert ".codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json \\" in text
    assert '"$hub_proof_public_base" \\' in text
    assert "docker-compose.yml \\" in text
    assert "python3 /docker/chummercomplete/chummer.run-services/scripts/verify_desktop_native_trust_receipts.py >/dev/null" in text
    assert "dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:RunDesktopReleaseMatrixTestsOnly=true" in text
    assert "dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll" in text
    assert "--no-restore" in text
    assert "CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar" in text
    assert "BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata" in text
    assert "StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" in text
    assert "verify-windows-installer-payloads.py" in text
    assert 'public_downloads_root="/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads"' in text
    assert '--files-dir "$public_downloads_root/files"' in text
    assert '--manifest "$public_downloads_root/releases.json"' in text
    assert "--require-embedded-bootstrap-metadata" in text
    assert "payload_gate_args+=(--require-manifest-row)" in text
    assert "payload_gate_args+=(--allow-empty)" in text
    assert 'if python3 - "$public_downloads_root/releases.json"' in text
    assert "external_blockers" in text
    assert "windows review" in text
    assert "blocking mode" in text
    assert "blockedByExternalConstraintsOnly" in text
    assert "blocking_mode == 'external_only'" in text
    assert "local_count == 0" in text
    assert "external findings" in text
    assert "local findings" in text


def test_desktop_release_matrix_test_project_skips_unrelated_ui_and_coverage_packages() -> None:
    text = (REPO_ROOT / "Chummer.Tests" / "Chummer.Tests.csproj").read_text(encoding="utf-8")

    assert "<RunDesktopReleaseMatrixTestsOnly Condition=\"'$(RunDesktopReleaseMatrixTestsOnly)' == ''\">false</RunDesktopReleaseMatrixTestsOnly>" in text
    assert "<Compile Include=\"DesktopReleaseMatrixTestBootstrap.cs\" />" in text
    assert "<Compile Include=\"DesktopReleaseMatrixRuntimeTests.cs\" />" in text
    assert "<ProjectReference Include=\"..\\Chummer.Desktop.Runtime\\Chummer.Desktop.Runtime.csproj\" />" in text
    assert "<ItemGroup Condition=\"'$(RunDesktopReleaseMatrixTestsOnly)' != 'true' and '$(RunBlazorShellComponentTestsOnly)' != 'true'\">" in text
    assert "<PackageReference Include=\"Avalonia.Headless\" Version=\"11.3.7\" />" in text
    assert "<PackageReference Include=\"Avalonia.Fonts.Inter\" Version=\"11.3.7\" />" in text
    assert "<PackageReference Include=\"Avalonia.Skia\" Version=\"11.3.7\" />" in text
    assert "<ItemGroup Condition=\"'$(RunDesktopReleaseMatrixTestsOnly)' != 'true'\">" in text
    assert "<PackageReference Include=\"bunit\" Version=\"2.5.3\" />" in text
    assert "<PackageReference Include=\"Microsoft.AspNetCore.TestHost\" Version=\"10.0.0\" />" in text
    assert "<PackageReference Include=\"System.Configuration.ConfigurationManager\" Version=\"10.0.0\" />" in text
    assert "<PackageReference Include=\"XMLUnit.Core\" Version=\"2.11.1\" />" in text
    assert "<PackageReference Include=\"coverlet.collector\" Version=\"6.0.4\">" in text
    assert "<PackageReference Include=\"Microsoft.CodeAnalysis.CSharp\" Version=\"5.0.0\" />" in text
    assert "\"'$(TargetFramework)' == 'net10.0' and '$(RunDesktopReleaseMatrixTestsOnly)' != 'true' and '$(RunBlazorShellComponentTestsOnly)' != 'true'\"" in text
    assert "\"'$(TargetFramework)' == 'net10.0' and '$(RunDesktopReleaseMatrixTestsOnly)' != 'true' and '$(RunBlazorShellComponentTestsOnly)' != 'true' and '@(_DesktopSurfaceAssembly)' != ''\"" in text


def test_desktop_release_matrix_runtime_suite_isolated_to_bootstrap_payload_handoff_cases() -> None:
    text = (REPO_ROOT / "Chummer.Tests" / "DesktopReleaseMatrixRuntimeTests.cs").read_text(encoding="utf-8")

    assert "public sealed class DesktopReleaseMatrixRuntimeTests" in text
    assert "CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar" in text
    assert "BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata" in text
    assert "StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" in text
    assert "DesktopSurfacePostureText_uses_plain_user_language" not in text
    assert "TestProcessPathOverrideScope" not in text


def test_desktop_release_matrix_reduced_mode_trims_desktop_runtime_graph() -> None:
    text = DESKTOP_RUNTIME_CSPROJ.read_text(encoding="utf-8")
    matrix_blocks = text.split('<ItemGroup Condition="\'$(RunDesktopReleaseMatrixTestsOnly)\' == \'true\'">')
    assert len(matrix_blocks) >= 2
    matrix_block = matrix_blocks[-1].split("</ItemGroup>", 1)[0]

    assert '<Compile Remove="**\\*.cs" />' in matrix_block
    assert '<Compile Include="DesktopPreferenceRuntime.cs" />' in matrix_block
    assert '<Compile Include="DesktopPublicPortalRuntime.cs" />' in matrix_block
    assert '<Compile Include="DesktopStateRootResolver.cs" />' in matrix_block
    assert '<Compile Include="DesktopUpdateClientStatus.cs" />' in matrix_block
    assert '<Compile Include="DesktopUpdateManifest.cs" />' in matrix_block
    assert '<Compile Include="DesktopUpdateRuntime.cs" />' in matrix_block

    full_graph_block = text.split('<ItemGroup Condition="\'$(RunBlazorShellComponentTestsOnly)\' != \'true\' and \'$(RunDesktopReleaseMatrixTestsOnly)\' != \'true\'">')[1].split("</ItemGroup>", 1)[0]
    assert "Chummer.Application" in full_graph_block
    assert "Chummer.Infrastructure" in full_graph_block
    assert "Chummer.Rulesets.Sr6" in full_graph_block
