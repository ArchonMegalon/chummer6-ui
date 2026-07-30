from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path("/docker/chummercomplete/chummer-presentation")
TEST_BLAZOR_COMPONENTS = REPO_ROOT / "scripts" / "test-blazor-components.sh"
CHUMMER_TESTS_CSPROJ = REPO_ROOT / "Chummer.Tests" / "Chummer.Tests.csproj"
DESKTOP_RUNTIME_CSPROJ = REPO_ROOT / "Chummer.Desktop.Runtime" / "Chummer.Desktop.Runtime.csproj"
BLAZOR_CSPROJ = REPO_ROOT / "Chummer.Blazor" / "Chummer.Blazor.csproj"


def test_blazor_component_gate_uses_reduced_shell_only_build_mode() -> None:
    text = TEST_BLAZOR_COMPONENTS.read_text(encoding="utf-8")
    assert 'CHUMMER_PACKAGE_PLANE_LOCK_WAIT_SECONDS:=120' in text
    assert 'bash scripts/ai/test.sh Chummer.Tests/Chummer.Tests.csproj' in text
    assert '--filter "FullyQualifiedName~BlazorShellComponentTests"' in text
    assert '-p:RunBlazorShellComponentTestsOnly=true' in text


def test_blazor_shell_reduced_mode_skips_avalonia_test_runtime_packages() -> None:
    text = CHUMMER_TESTS_CSPROJ.read_text(encoding="utf-8")
    assert '<PackageReference Include="Avalonia.Headless" Version="11.3.7" />' in text
    assert "'$(RunBlazorShellComponentTestsOnly)' != 'true'" in text
    assert '<PackageReference Include="coverlet.collector" Version="6.0.4">' in text
    assert '<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />' in text
    assert 'CopyDesktopSurfaceAssembliesForTestDiscovery' in text


def test_blazor_shell_reduced_mode_does_not_pull_desktop_runtime_project() -> None:
    text = CHUMMER_TESTS_CSPROJ.read_text(encoding="utf-8")
    blocks = text.split('<ItemGroup Condition="\'$(RunBlazorShellComponentTestsOnly)\' == \'true\'">')
    assert len(blocks) >= 3
    block = blocks[2].split("</ItemGroup>", 1)[0]

    assert '<ProjectReference Include="..\\Chummer.Blazor\\Chummer.Blazor.csproj" />' in block
    assert '<ProjectReference Include="..\\Chummer.Presentation\\Chummer.Presentation.csproj" />' in block
    assert '<ProjectReference Include="..\\Chummer.Desktop.Runtime\\Chummer.Desktop.Runtime.csproj" />' not in block
    assert "Chummer.Rulesets.Sr5" in block


def test_blazor_shell_reduced_mode_trims_desktop_runtime_graph() -> None:
    text = DESKTOP_RUNTIME_CSPROJ.read_text(encoding="utf-8")
    blocks = text.split('<ItemGroup Condition="\'$(RunBlazorShellComponentTestsOnly)\' == \'true\'">')
    assert len(blocks) >= 2
    compile_block = blocks[-1].split("</ItemGroup>", 1)[0]

    assert '<Compile Remove="**\\*.cs" />' in compile_block
    assert '<Compile Include="DesktopCrashReport.cs" />' in compile_block
    assert '<Compile Include="DesktopCrashRuntime.cs" />' in compile_block
    assert '<Compile Include="DesktopInstallLinkingRuntime.cs" />' in compile_block
    assert '<Compile Include="DesktopPublicPortalRuntime.cs" />' in compile_block
    assert '<Compile Include="DesktopRepoRootLocator.cs" />' in compile_block
    assert '<Compile Include="DesktopStateRootResolver.cs" />' in compile_block
    assert '<Compile Include="DesktopTrustReceiptComposer.cs" />' in compile_block
    assert '<Compile Include="DesktopUpdateClientStatus.cs" />' in compile_block

    full_graph_block = text.split('<ItemGroup Condition="\'$(RunBlazorShellComponentTestsOnly)\' != \'true\' and \'$(RunDesktopReleaseMatrixTestsOnly)\' != \'true\'">')[1].split("</ItemGroup>", 1)[0]
    assert "Chummer.Application" in full_graph_block
    assert "Chummer.Infrastructure" in full_graph_block
    assert "Chummer.Rulesets.Hosting" in full_graph_block


def test_blazor_shell_reduced_mode_keeps_loader_owned_ruleset_authority() -> None:
    project_text = (REPO_ROOT / "Chummer.Presentation" / "Chummer.Presentation.csproj").read_text(encoding="utf-8")
    loader_text = (
        REPO_ROOT / "Chummer.Presentation" / "Overview" / "WorkspaceOverviewLoader.cs"
    ).read_text(encoding="utf-8")
    assert "<RunBlazorShellComponentTestsOnly Condition=\"'$(RunBlazorShellComponentTestsOnly)' == ''\">false</RunBlazorShellComponentTestsOnly>" in project_text
    # Recovery validation is a production security boundary and must exercise
    # the composition-owned resolver instead of constructing concrete codecs
    # inside the presentation layer or compiling a weaker test-only authority.
    assert "Chummer.Application" in project_text
    assert "Chummer.Rulesets.Hosting" in project_text
    assert "Chummer.Infrastructure" not in project_text
    assert "Chummer.Rulesets.Sr4" not in project_text
    assert "Chummer.Rulesets.Sr5" not in project_text
    assert "Chummer.Rulesets.Sr6" not in project_text
    assert "IRulesetWorkspaceCodecResolver workspaceCodecResolver" in loader_text
    assert "_canonicalAuthority = new CanonicalDocumentAuthority(" in loader_text


def test_blazor_shell_reduced_mode_excludes_program_entrypoint_from_blazor_project() -> None:
    text = BLAZOR_CSPROJ.read_text(encoding="utf-8")
    assert "<RunBlazorShellComponentTestsOnly Condition=\"'$(RunBlazorShellComponentTestsOnly)' == ''\">false</RunBlazorShellComponentTestsOnly>" in text
    assert "<OutputType Condition=\"'$(RunBlazorShellComponentTestsOnly)' == 'true'\">Library</OutputType>" in text
    assert "<ItemGroup Condition=\"'$(RunBlazorShellComponentTestsOnly)' == 'true'\">" in text
    block = text.split("<ItemGroup Condition=\"'$(RunBlazorShellComponentTestsOnly)' == 'true'\">", 1)[1].split("</ItemGroup>", 1)[0]
    assert "<Compile Remove=\"Program.cs\" />" in block
