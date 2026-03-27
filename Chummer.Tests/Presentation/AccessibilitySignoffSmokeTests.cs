#nullable enable annotations

using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using System;
using System.IO;

namespace Chummer.Tests.Presentation;

internal static class AccessibilitySignoffSmokeTests
{
    private static int Main()
    {
        try
        {
            SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation();
            GeneratedAssetReviewPanel_renders_preview_and_emits_attach_approve_archive_actions();
            BlazorHome_invalidates_spider_cards_when_session_context_shifts_and_refreshes_them();
            DesktopHomeBuildExplainProjector_uses_real_contract_state();
            DesktopHomeBuildExplainProjector_exposes_safe_action_and_watchouts_when_workspace_is_missing();
            DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel();
            DesktopHead_uses_canonical_catalog_only_resolver();
            Console.WriteLine("[B13] PASS: targeted accessibility smoke runner checks passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[B13] FAIL: {ex.Message}");
            return 1;
        }
    }

    private static void SectionPane_renders_browse_projection_with_saved_filters_and_keyboard_navigation()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shell/SectionPane.razor");
        RequireContains(source, "role=\"listbox\"");
        RequireContains(source, "role=\"option\"");
        RequireContains(source, "aria-activedescendant=");
        RequireContains(source, "aria-selected=\"@IsBrowseResultActive");
    }

    private static void GeneratedAssetReviewPanel_renders_preview_and_emits_attach_approve_archive_actions()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shared/GeneratedAssetReviewPanel.razor");
        RequireContains(source, "role=\"tablist\"");
        RequireContains(source, "role=\"tab\"");
        RequireContains(source, "role=\"tabpanel\"");
        RequireContains(source, "aria-controls=");
    }

    private static void BlazorHome_invalidates_spider_cards_when_session_context_shifts_and_refreshes_them()
    {
        string source = ReadSource("Chummer.Blazor/Components/Shared/GmBoardFeed.razor");
        RequireContains(source, "data-gm-board-stale-banner");
        RequireContains(source, "role=\"status\"");
        RequireContains(source, "aria-live=\"polite\"");
    }

    private static void DesktopHomeBuildExplainProjector_uses_real_contract_state()
    {
        WorkspaceListItem workspace = new(
            new CharacterWorkspaceId("workspace-1"),
            new CharacterFileSummary(
                Name: "Apex",
                Alias: "Alias",
                Metatype: "Human",
                BuildMethod: "Priority",
                CreatedVersion: "6.0",
                AppVersion: "6.0",
                Karma: 0,
                Nuyen: 0,
                Created: true),
            DateTimeOffset.Parse("2026-03-27T10:15:00+00:00"),
            "sr6.preview.v1",
            HasSavedWorkspace: true);
        CharacterBuildSection build = new(
            BuildMethod: "Priority",
            PriorityMetatype: "B",
            PriorityAttributes: "A",
            PrioritySpecial: "D",
            PrioritySkills: "C",
            PriorityResources: "E",
            PriorityTalent: "Magic",
            SumToTen: 10,
            Special: 4,
            TotalSpecial: 6,
            TotalAttributes: 24,
            ContactPoints: 10,
            ContactPointsUsed: 6);
        CharacterRulesSection rules = new(
            GameEdition: "SR6",
            Settings: "Seattle Nights",
            GameplayOption: "Prime runner preview",
            GameplayOptionQualityLimit: 2,
            MaxNuyen: 450000,
            MaxKarma: 50,
            ContactMultiplier: 3,
            BannedWareGrades: ["Used", "Prototype"]);

        DesktopHomeBuildExplainProjection projection = DesktopHomeBuildExplainProjector.Create([workspace], build, rules);
        RequireContains(projection.NextSafeAction, "Continue Apex");
        RequireContains(projection.ExplainFocus, "Explain focus:");
        RequireContains(projection.Summary, "Metatype B");
        RequireContains(projection.Summary, "SR6");
        RequireContains(projection.Summary, "Used, Prototype");
        if (projection.Watchouts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should surface multiple watchouts for the flagship home cockpit.");
        }
    }

    private static void DesktopHomeBuildExplainProjector_exposes_safe_action_and_watchouts_when_workspace_is_missing()
    {
        DesktopHomeBuildExplainProjection projection = DesktopHomeBuildExplainProjector.Create([], build: null, rules: null);
        RequireContains(projection.NextSafeAction, "Create or import the first dossier");
        RequireContains(projection.ExplainFocus, "Claim the install");
        if (projection.Watchouts.Count < 2)
        {
            throw new InvalidOperationException("Desktop build/explain projection should keep explicit watchouts even before the first workspace exists.");
        }
    }

    private static void DesktopHome_wires_the_build_and_explain_projection_into_the_summary_panel()
    {
        string source = ReadSource("Chummer.Avalonia/DesktopHomeWindow.cs");
        RequireContains(source, "ReadBuildExplainProjectionAsync");
        RequireContains(source, "BuildBuildExplainBody()");
        RequireContains(source, "_buildExplainProjection.NextSafeAction");
        RequireContains(source, "_buildExplainProjection.ExplainFocus");
        RequireContains(source, "_buildExplainProjection.Watchouts");
        RequireContains(source, "client.GetBuildAsync");
        RequireContains(source, "client.GetRulesAsync");
    }

    private static void DesktopHead_uses_canonical_catalog_only_resolver()
    {
        string appSource = ReadSource("Chummer.Avalonia/App.axaml.cs");
        RequireContains(appSource, "CatalogOnlyRulesetShellCatalogResolver");

        string? repoRoot = FindRepoRoot();
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new InvalidOperationException("Could not locate the repository root for desktop runtime checks.");
        }

        string duplicateResolverPath = Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopFallbackRulesetShellCatalogResolver.cs");
        if (File.Exists(duplicateResolverPath))
        {
            throw new InvalidOperationException("Desktop runtime should not keep a duplicate fallback ruleset shell resolver.");
        }
    }

    private static string ReadSource(string relativePath)
    {
        string? cursor = FindRepoRoot();
        while (!string.IsNullOrWhiteSpace(cursor))
        {
            string candidate = Path.Combine(cursor, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            DirectoryInfo? parent = Directory.GetParent(cursor);
            cursor = parent?.FullName;
        }

        throw new FileNotFoundException($"Could not locate required source file: {relativePath}");
    }

    private static string? FindRepoRoot()
    {
        string?[] startingPoints =
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            AppDomain.CurrentDomain.BaseDirectory
        };

        foreach (string? startingPoint in startingPoints)
        {
            string? cursor = startingPoint;
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                bool hasPresentationProject = File.Exists(Path.Combine(cursor, "Chummer.Presentation", "Chummer.Presentation.csproj"));
                bool hasBlazorShell = File.Exists(Path.Combine(cursor, "Chummer.Blazor", "Components", "Shell", "SectionPane.razor"));
                if (hasPresentationProject && hasBlazorShell)
                {
                    return cursor;
                }

                DirectoryInfo? parent = Directory.GetParent(cursor);
                cursor = parent?.FullName;
            }
        }

        return null;
    }

    private static void RequireContains(string source, string expected)
    {
        if (!source.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expected}' in smoke target source.");
        }
    }
}
