#nullable enable annotations

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class ApiSmokeScriptComplianceTests
{
    [TestMethod]
    public void E2e_auth_script_exercises_public_auth_handoff_and_current_workspace_surface()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "e2e-auth.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "/api/health");
        StringAssert.Contains(scriptText, "/api/info");
        StringAssert.Contains(scriptText, "/api/commands");
        StringAssert.Contains(scriptText, "/api/navigation-tabs");
        StringAssert.Contains(scriptText, "/api/shell/bootstrap");
        StringAssert.Contains(scriptText, "/api/workspaces");
        StringAssert.Contains(scriptText, "/auth/google/start?next=");
        StringAssert.Contains(scriptText, "redirect_uri=https%3A%2F%2Fchummer.run%2Fauth%2Fgoogle%2Fcallback");
        StringAssert.DoesNotContain(scriptText, "/api/content/overlays");
        StringAssert.Contains(scriptText, "public handoff and unauthenticated protections are green");
    }

    [TestMethod]
    public void E2e_live_script_exercises_workspace_import_roundtrip_instead_of_legacy_character_posts()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "e2e-live.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "/api/workspaces/import");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/summary");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/validate");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/profile");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/skills");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/rules");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/build");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/export");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id/print");
        StringAssert.Contains(scriptText, "/api/workspaces/$workspace_id");
        StringAssert.Contains(scriptText, "workspace live E2E completed");
        StringAssert.DoesNotContain(scriptText, "/api/characters/summary");
        StringAssert.DoesNotContain(scriptText, "/api/content/overlays");
        StringAssert.DoesNotContain(scriptText, "/api/lifemodules/stages");
    }

    private static string FindRepoRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate Chummer.sln from the current test directory.");
        return string.Empty;
    }
}
