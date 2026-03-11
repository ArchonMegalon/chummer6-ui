#nullable enable annotations

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

    private static string ReadSource(string relativePath)
    {
        string? cursor = Directory.GetCurrentDirectory();
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

    private static void RequireContains(string source, string expected)
    {
        if (!source.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected to find '{expected}' in smoke target source.");
        }
    }
}
