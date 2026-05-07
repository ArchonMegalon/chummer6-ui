#nullable enable annotations

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

/// <summary>
/// Guards the character roster multi-character flow structural parity against Chummer5a oracle.
/// Chummer5a CharacterRoster uses:
/// - treCharacterList (TreeView) for multi-character tree selection
/// - tabCharacterText with 5 tabs: Description, Concept, Background, Character Notes, Game Notes
/// - Character metadata labels: Name, Metatype, Career Karma, Player, Alias, Essence, File Name, Settings
/// </summary>
[TestClass]
public sealed class CharacterRosterStructureParityGuardTests
{
    [TestMethod]
    public void Chummer6_roster_implementation_must_reference_treCharacterList_equivalent_tree_structure()
    {
        string repoRoot = FindRepoRoot();

        string[] rosterFiles =
        {
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterControl.axaml"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterControl.axaml.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "CharacterRosterDataBinder.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.StateRefresh.cs")
        };

        bool foundTreeOrListStructure = false;
        foreach (string file in rosterFiles)
        {
            if (File.Exists(file))
            {
                string content = File.ReadAllText(file);
                if (content.Contains("TreeView", StringComparison.Ordinal)
                    || content.Contains("treCharacterList", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("RosterItems", StringComparison.Ordinal)
                    || content.Contains("CharacterRosterNode", StringComparison.Ordinal))
                {
                    foundTreeOrListStructure = true;
                    break;
                }
            }
        }

        Assert.IsTrue(foundTreeOrListStructure, 
            "Chummer6 roster implementation must have tree/list-based multi-character structure equivalent to Chummer5a treCharacterList");
    }

    [TestMethod]
    public void Chummer6_roster_must_have_character_profile_tabs_equivalent()
    {
        string repoRoot = FindRepoRoot();
        string stateFile = Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "CharacterOverviewState.cs");
        string dialogFactoryFile = Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs");

        Assert.IsTrue(File.Exists(stateFile), "CharacterOverviewState.cs must exist for classic roster tab parity.");
        Assert.IsTrue(File.Exists(dialogFactoryFile), "DesktopDialogFactory.cs must exist for classic roster dialog parity.");

        string stateContent = File.ReadAllText(stateFile);
        string dialogFactoryContent = File.ReadAllText(dialogFactoryFile);
        string[] expectedTabLabels = { "Description", "Concept", "Background", "Character Notes", "Game Notes" };

        foreach (string label in expectedTabLabels)
        {
            StringAssert.Contains(stateContent, label, $"CharacterOverviewState must declare classic roster tab label '{label}'.");
            StringAssert.Contains(dialogFactoryContent, label, $"Desktop roster dialog must expose classic roster tab label '{label}'.");
        }

        StringAssert.Contains(dialogFactoryContent, "\"rosterDetailTabs\"", "Desktop roster dialog must keep the classic runner-page tab strip.");
    }

    [TestMethod]
    public void Chummer6_roster_must_have_character_metadata_labels()
    {
        string repoRoot = FindRepoRoot();
        string dialogFactoryFile = Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs");

        Assert.IsTrue(File.Exists(dialogFactoryFile), "DesktopDialogFactory.cs must exist for roster metadata parity.");

        string content = File.ReadAllText(dialogFactoryFile);
        foreach (string label in new[] { "Character Name", "Alias", "File Path", "Settings", "Background / Concept", "Bio / Concept / Notes" })
        {
            StringAssert.Contains(content, label, $"Character Roster dialog must expose metadata label '{label}'.");
        }
    }

    [TestMethod]
    public void Chummer6_roster_pane_must_remain_visible_when_navigator_is_hidden()
    {
        string repoRoot = FindRepoRoot();
        string refreshFile = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.StateRefresh.cs");

        Assert.IsTrue(File.Exists(refreshFile), "MainWindow.StateRefresh.cs must exist for desktop roster visibility checks.");

        string content = File.ReadAllText(refreshFile);
        StringAssert.Contains(content, "bool showRosterPane = !showNavigatorPane;");
        StringAssert.Contains(content, "RosterPaneRegion.IsVisible = showRosterPane;");
        StringAssert.Contains(content, "RosterPaneRegion.IsHitTestVisible = showRosterPane;");
        StringAssert.Contains(content, "ContentRegion.ColumnDefinitions[0].Width = showRosterPane || showNavigatorPane");
    }

    [TestMethod]
    public void Chummer6_roster_binding_must_bind_session_open_workspaces_into_roster_items()
    {
        string repoRoot = FindRepoRoot();
        string refreshFile = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.StateRefresh.cs");
        string bindingFile = Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.ControlBinding.cs");

        Assert.IsTrue(File.Exists(refreshFile), "MainWindow.StateRefresh.cs must exist for roster binding checks.");
        Assert.IsTrue(File.Exists(bindingFile), "MainWindow.ControlBinding.cs must exist for roster selection wiring checks.");

        string refreshContent = File.ReadAllText(refreshFile);
        string bindingContent = File.ReadAllText(bindingFile);

        StringAssert.Contains(refreshContent, "ResolveRosterWorkspaces(state);");
        StringAssert.Contains(refreshContent, "CharacterRosterDataBinder.CreateRosterNodes(rosterWorkspaces);");
        StringAssert.Contains(refreshContent, "CharacterRosterControl.RosterItems = rosterNodes;");
        StringAssert.Contains(refreshContent, "state.Session.OpenWorkspaces.Count > 0");
        StringAssert.Contains(refreshContent, "state.Session.OpenWorkspaces");
        StringAssert.Contains(refreshContent, ": state.OpenWorkspaces;");
        StringAssert.Contains(refreshContent, "CharacterRosterControl.SelectedWorkspaceId =");
        StringAssert.Contains(bindingContent, "characterRoster.SelectionChanged +=");
        StringAssert.Contains(bindingContent, "onRosterWorkspaceSelected(characterRoster, args.SelectedNode.Id)");
    }

    [TestMethod]
    public void Chummer6_roster_structure_guard_is_wired_into_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        
        if (File.Exists(verifyScript))
        {
            string content = File.ReadAllText(verifyScript);
            StringAssert.Contains(content, "CharacterRosterStructureParityGuardTests");
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        Assert.Fail("Could not locate repository root.");
        return string.Empty;
    }
}
