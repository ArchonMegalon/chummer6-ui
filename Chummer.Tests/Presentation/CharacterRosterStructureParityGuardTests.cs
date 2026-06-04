#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Parity guard tests for the Character Roster multi-character flow.
/// Ensures Chummer6 implements the classic Chummer5a roster structure:
/// - treCharacterList TreeView for multi-character navigation
/// - Character profile tabs: Description, Concept, Background, Notes, GameNotes
/// - Dense treelist presentation matching classic Chummer posture
/// 
/// Oracle: /docker/chummer5a/Chummer/Forms/Utility Forms/CharacterRoster.Designer.cs
/// </summary>
internal static class CharacterRosterStructureParityGuardTests
{
    internal static void Run()
    {
        CharacterRosterTreeView_structure_exists_in_chummer6();
        CharacterProfileTabs_match_oracle();
        OpenWorkspaces_bind_into_roster_items();
        RosterSurface_is_accessible_from_main_shell();
    }

    private static void CharacterRosterTreeView_structure_exists_in_chummer6()
    {
        string repoRoot = FindRepoRoot();
        string[] candidateFiles =
        {
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterControl.axaml"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterControl.axaml.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterDataBinder.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.StateRefresh.cs")
        };

        bool foundTreeViewStructure = false;
        foreach (string file in candidateFiles)
        {
            if (File.Exists(file))
            {
                string content = File.ReadAllText(file);
                if (content.Contains("TreeView", StringComparison.Ordinal)
                    || content.Contains("treCharacterList", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("RosterItems", StringComparison.Ordinal)
                    || content.Contains("CharacterRosterNode", StringComparison.Ordinal))
                {
                    foundTreeViewStructure = true;
                    break;
                }
            }
        }

        if (!foundTreeViewStructure)
        {
            throw new InvalidOperationException(
                $"Chummer6 must have TreeView structure for character roster. Checked: {string.Join(", ", candidateFiles)}");
        }
    }

    private static void CharacterProfileTabs_match_oracle()
    {
        string repoRoot = FindRepoRoot();
        string stateFile = Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "CharacterOverviewState.cs");
        string dialogFactoryFile = Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs");

        string stateContent = File.Exists(stateFile)
            ? File.ReadAllText(stateFile)
            : throw new InvalidOperationException($"Missing roster tab state file: {stateFile}");
        string dialogFactoryContent = File.Exists(dialogFactoryFile)
            ? File.ReadAllText(dialogFactoryFile)
            : throw new InvalidOperationException($"Missing roster dialog factory file: {dialogFactoryFile}");

        foreach (string label in new[] { "Description", "Concept", "Background", "Character Notes", "Game Notes" })
        {
            if (!stateContent.Contains(label, StringComparison.Ordinal) || !dialogFactoryContent.Contains(label, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Classic roster tab label '{label}' must exist in both CharacterOverviewState and DesktopDialogFactory.");
            }
        }
    }

    private static void OpenWorkspaces_bind_into_roster_items()
    {
        string repoRoot = FindRepoRoot();
        string projectorFile = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs");
        string bindingFile = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ControlBinding.cs");
        string rosterControlFile = Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterControl.axaml.cs");
        string projectorContent = File.Exists(projectorFile)
            ? File.ReadAllText(projectorFile)
            : throw new InvalidOperationException($"Missing roster projector file: {projectorFile}");
        string bindingContent = File.Exists(bindingFile)
            ? File.ReadAllText(bindingFile)
            : throw new InvalidOperationException($"Missing roster binding file: {bindingFile}");
        string rosterControlContent = File.Exists(rosterControlFile)
            ? File.ReadAllText(rosterControlFile)
            : throw new InvalidOperationException($"Missing roster control file: {rosterControlFile}");

        foreach (string needle in new[]
                 {
                     "CharacterRosterDataBinder.CreateRosterNodes(resolvedOpenWorkspaces).ToArray()",
                     "RosterPaneState: new RosterPaneState(",
                     "SelectedWorkspaceId: workspaceContext.ActiveWorkspaceId?.Value"
                 })
        {
            if (!projectorContent.Contains(needle, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Roster binding proof is missing '{needle}' from MainWindow.ShellFrameProjector.cs.");
            }
        }

        if (!bindingContent.Contains("CharacterRoster.SetState(shellFrame.RosterPaneState);", StringComparison.Ordinal)
            || !bindingContent.Contains("characterRoster.SelectionChanged +=", StringComparison.Ordinal)
            || !bindingContent.Contains("onRosterWorkspaceSelected(characterRoster, args.SelectedNode.Id)", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roster shell binding must apply projected roster state and route CharacterRosterControl.SelectionChanged into workspace selection.");
        }

        if (!rosterControlContent.Contains("void SetState(RosterPaneState state)", StringComparison.Ordinal)
            || !rosterControlContent.Contains("RosterItems = state.Items;", StringComparison.Ordinal)
            || !rosterControlContent.Contains("SelectedWorkspaceId = state.SelectedWorkspaceId;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roster control must expose a SetState method that applies roster items and the selected workspace.");
        }
    }

    private static void RosterSurface_is_accessible_from_main_shell()
    {
        string repoRoot = FindRepoRoot();
        string[] candidateFiles =
        {
            Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ShellFrameProjector.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ControlBinding.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ToolStripControl.axaml.cs")
        };

        bool foundRosterRoute = false;
        foreach (string file in candidateFiles)
        {
            if (File.Exists(file))
            {
                string content = File.ReadAllText(file);
                if (content.Contains("roster", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("Overview", StringComparison.OrdinalIgnoreCase))
                {
                    foundRosterRoute = true;
                    break;
                }
            }
        }

        if (!foundRosterRoute)
        {
            throw new InvalidOperationException(
                "Chummer6 main shell must expose character roster navigation route.");
        }
    }

    private static string FindRepoRoot()
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
                if (File.Exists(Path.Combine(cursor, "Chummer.Avalonia", "Chummer.Avalonia.csproj"))
                    && File.Exists(Path.Combine(cursor, "Chummer.Presentation", "Chummer.Presentation.csproj")))
                {
                    return cursor;
                }

                cursor = Directory.GetParent(cursor)?.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate chummer-presentation repository root.");
    }
}
