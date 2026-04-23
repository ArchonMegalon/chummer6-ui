#nullable enable annotations

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Chummer.Contracts.Api;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class DialogCoordinatorTests
{
    [TestMethod]
    public async Task CoordinateAsync_save_global_settings_updates_preferences_and_closes_dialog()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.global_settings",
                Title: "Global Settings",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("globalUiScale", "UI Scale", "125", "100"),
                    new DesktopDialogField("globalTheme", "Theme", "dark-steel", "chummer"),
                    new DesktopDialogField("globalLanguage", "Language", "de-de", "en-us"),
                    new DesktopDialogField("globalSheetLanguage", "Sheet Language", "fr-fr", "en-us"),
                    new DesktopDialogField("globalCompactMode", "Compact", "true", "false"),
                    new DesktopDialogField("globalHideMasterIndex", "Hide Master Index", "true", "false", InputType: "checkbox")
                ],
                Actions:
                [
                    new DesktopDialogAction("save", "Save", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("save", context, CancellationToken.None);

        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual(125, published.Preferences.UiScalePercent);
        Assert.AreEqual("dark-steel", published.Preferences.Theme);
        Assert.AreEqual("de-de", published.Preferences.Language);
        Assert.AreEqual("fr-fr", published.Preferences.SheetLanguage);
        Assert.IsTrue(published.Preferences.CompactMode);
        Assert.IsTrue(published.Preferences.HideMasterIndex);
        StringAssert.Contains(published.Notice ?? string.Empty, "Restart the desktop head to fully apply");
    }

    [TestMethod]
    public async Task CoordinateAsync_save_global_settings_falls_back_to_english_for_unsupported_locale()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.global_settings",
                Title: "Global Settings",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("globalUiScale", "UI Scale", "100", "100"),
                    new DesktopDialogField("globalTheme", "Theme", "classic", "classic"),
                    new DesktopDialogField("globalLanguage", "Language", "es-es", "en-us"),
                    new DesktopDialogField("globalCompactMode", "Compact", "false", "false")
                ],
                Actions:
                [
                    new DesktopDialogAction("save", "Save", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("save", context, CancellationToken.None);

        Assert.AreEqual(DesktopLocalizationCatalog.DefaultLanguage, published.Preferences.Language);
    }

    [TestMethod]
    public async Task CoordinateAsync_apply_metadata_calls_update_delegate_and_closes_dialog_on_success()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.workspace.metadata",
                Title: "Metadata",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("metadataName", "Name", "  Neo  ", string.Empty),
                    new DesktopDialogField("metadataAlias", "Alias", "  One  ", string.Empty),
                    new DesktopDialogField("metadataNotes", "Notes", "Runner", string.Empty, IsMultiline: true)
                ],
                Actions:
                [
                    new DesktopDialogAction("apply_metadata", "Apply", true)
                ])
        };

        UpdateWorkspaceMetadata? captured = null;
        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: (command, _) =>
            {
                captured = command;
                published = published with { Error = null };
                return Task.CompletedTask;
            },
            GetState: () => published);

        await coordinator.CoordinateAsync("apply_metadata", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual("Neo", captured!.Name);
        Assert.AreEqual("One", captured.Alias);
        Assert.AreEqual("Runner", captured.Notes);
        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Metadata updated.", published.Notice);
    }

    [TestMethod]
    public async Task CoordinateAsync_apply_metadata_keeps_dialog_open_when_update_sets_error()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.workspace.metadata",
                Title: "Metadata",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("metadataName", "Name", "Neo", string.Empty)
                ],
                Actions:
                [
                    new DesktopDialogAction("apply_metadata", "Apply", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: (_, _) =>
            {
                published = published with { Error = "boom" };
                return Task.CompletedTask;
            },
            GetState: () => published);

        await coordinator.CoordinateAsync("apply_metadata", context, CancellationToken.None);

        Assert.AreEqual("boom", published.Error);
        Assert.IsNotNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_roll_adds_result_field_to_dice_dialog()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.dice_roller",
                Title: "Dice Roller",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("diceMethod", "Method", "Standard", "Standard", InputType: "select", Options:
                    [
                        new DesktopDialogFieldOption("Standard", "Standard"),
                        new DesktopDialogFieldOption("Large", "Large"),
                        new DesktopDialogFieldOption("ReallyLarge", "Really Large")
                    ]),
                    new DesktopDialogField("diceCount", "Dice", "3", "1"),
                    new DesktopDialogField("diceThreshold", "Threshold", "2", "0"),
                    new DesktopDialogField("diceGremlins", "Gremlins", "0", "0"),
                    new DesktopDialogField("diceRuleOf6", "Rule of 6", "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("diceCinematicGameplay", "Cinematic Gameplay", "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("diceRushJob", "Rush Job", "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("diceVariableGlitch", "Variable Glitch", "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("diceBubbleDie", "Bubble Die", "false", "false", InputType: "checkbox"),
                    new DesktopDialogField("diceResultsSummary", "Results", "Roll dice to see hits, glitches, and the summed total.", "Roll dice to see hits, glitches, and the summed total.", IsReadOnly: true),
                    new DesktopDialogField("diceResultsList", "Roll History", "No rolls yet.", "No rolls yet.", IsReadOnly: true),
                    new DesktopDialogField("diceLastRollState", "Last Roll State", string.Empty, string.Empty, IsReadOnly: true)
                ],
                Actions:
                [
                    new DesktopDialogAction("roll", "Roll", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("roll", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreNotEqual(
            "Roll dice to see hits, glitches, and the summed total.",
            DesktopDialogFieldValueParser.GetValue(published.ActiveDialog!, "diceResultsSummary"));
        StringAssert.Contains(
            DesktopDialogFieldValueParser.GetValue(published.ActiveDialog!, "diceResultsList"),
            "Die 1:");
        Assert.IsFalse(string.IsNullOrWhiteSpace(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog!, "diceLastRollState")));
    }

    [TestMethod]
    public async Task CoordinateAsync_create_character_imports_workspace_and_closes_dialog_on_success()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.new_character",
                Title: "New Character",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("newCharacterRulesetId", "Ruleset", "sr6", "sr5", InputType: "select", Options:
                    [
                        new DesktopDialogFieldOption("sr5", "SR5"),
                        new DesktopDialogFieldOption("sr6", "SR6")
                    ]),
                    new DesktopDialogField("newCharacterBuildMethod", "Build Method", "Karma", "Priority", InputType: "select", Options:
                    [
                        new DesktopDialogFieldOption("Priority", "Priority"),
                        new DesktopDialogFieldOption("Karma", "Karma")
                    ])
                ],
                Actions:
                [
                    new DesktopDialogAction("create_character", "Create", true)
                ])
        };

        WorkspaceImportDocument? imported = null;
        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: (document, _) =>
            {
                imported = document;
                published = published with
                {
                    Error = null,
                    WorkspaceId = new CharacterWorkspaceId("ws-created")
                };
                return Task.CompletedTask;
            },
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("create_character", context, CancellationToken.None);

        Assert.IsNotNull(imported);
        Assert.AreEqual("sr6", imported!.RulesetId);
        StringAssert.Contains(imported.Content, "<name>New Character</name>");
        StringAssert.Contains(imported.Content, "<alias>Runner</alias>");
        StringAssert.Contains(imported.Content, "<buildmethod>Karma</buildmethod>");
        StringAssert.Contains(imported.Content, "<attributes>");
        StringAssert.Contains(imported.Content, "<newskills>");
        StringAssert.Contains(imported.Content, "<qualities>");
        StringAssert.Contains(imported.Content, "<contacts>");
        StringAssert.Contains(imported.Content, "<gears>");
        StringAssert.Contains(imported.Content, "<weapons>");
        Assert.IsNull(published.ActiveDialog);
        StringAssert.Contains(published.Notice ?? string.Empty, "Created 'New Character' (Karma, SR6).");
    }

    [TestMethod]
    public async Task CoordinateAsync_reroll_misses_requires_previous_roll_state()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.dice_roller",
                Title: "Dice Roller",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("diceLastRollState", "Last Roll State", string.Empty, string.Empty, IsReadOnly: true)
                ],
                Actions:
                [
                    new DesktopDialogAction("reroll_misses", "Reroll Misses", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("reroll_misses", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.dice_roller", published.ActiveDialog!.Id);
        Assert.AreEqual("Roll the dice first before rerolling misses.", published.Error);
    }

    [TestMethod]
    public async Task CoordinateAsync_import_imports_workspace_and_closes_dialog_on_success()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.open_character",
                Title: "Open Character",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("importRulesetId", "Ruleset", " SR6 ", "sr5"),
                    new DesktopDialogField("openCharacterXml", "Character XML", "<character><name>Runner</name></character>", "<character />", true)
                ],
                Actions:
                [
                    new DesktopDialogAction("import", "Import", true)
                ])
        };

        WorkspaceImportDocument? imported = null;
        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: (document, _) =>
            {
                imported = document;
                published = published with
                {
                    Error = null,
                    WorkspaceId = new CharacterWorkspaceId("ws-imported")
                };
                return Task.CompletedTask;
            },
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("import", context, CancellationToken.None);

        Assert.IsNotNull(imported);
        StringAssert.Contains(imported!.Content, "<character>");
        Assert.AreEqual("sr6", imported.RulesetId);
        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Character imported.", published.Notice);
        Assert.AreEqual("ws-imported", published.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_more_gear_keeps_dialog_open_and_rebuilds_preview()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            ActiveDialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default) with
            {
                Fields = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default).Fields
                    .Select(field => field.Id switch
                    {
                        "uiGearMarkup" => field with { Value = "15" },
                        "uiGearBlackMarketDiscount" => field with { Value = "true" },
                        _ => field
                    })
                    .ToArray()
            }
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("add_more", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.ui.gear_add", published.ActiveDialog!.Id);
        Assert.AreEqual("0", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearMarkup"));
        StringAssert.Contains(published.ActiveDialog.Message ?? string.Empty, "Add & More keeps the classic selector open");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearSelectionDetails"), "Cost | ¥653");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearLiveRecalc"), "Add Again | Stays open");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearLiveRecalc"), "Black Market | Yes");
        StringAssert.Contains(published.Notice ?? string.Empty, "Gear 'Ares Predator V' added.");
    }

    [TestMethod]
    public async Task CoordinateAsync_add_more_gear_uses_sr4_authored_notice_when_state_is_sr4()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            WorkspaceId = new CharacterWorkspaceId("ws-sr4"),
            OpenWorkspaces =
            [
                new OpenWorkspaceState(new CharacterWorkspaceId("ws-sr4"), "Nyx", "NYX", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr4, true)
            ],
            ActiveDialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default)
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("add_more", context, CancellationToken.None);

        StringAssert.Contains(published.Notice ?? string.Empty, "SR4 import tools:");
        StringAssert.Contains(published.Notice ?? string.Empty, "Gear 'Ares Predator V' added.");
    }

    [TestMethod]
    public async Task CoordinateAsync_focus_category_for_gear_add_toggles_to_selected_branch()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            ActiveDialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default)
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("focus_category", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("Pistols", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearCategory"));
        Assert.AreEqual("Show All Categories", published.ActiveDialog.Actions.Single(action => string.Equals(action.Id, "focus_category", StringComparison.Ordinal)).Label);
        StringAssert.Contains(published.Notice ?? string.Empty, "Pistols");
    }

    [TestMethod]
    public async Task CoordinateAsync_toggle_search_scope_for_gear_add_rebuilds_dialog()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            ActiveDialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default)
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("toggle_search_scope", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("false", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearSearchInCategoryOnly"));
        Assert.AreEqual("Search Current Category", published.ActiveDialog.Actions.Single(action => string.Equals(action.Id, "toggle_search_scope", StringComparison.Ordinal)).Label);
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearSelectionTrail"), "Search Scope | all categories");
        StringAssert.Contains(published.Notice ?? string.Empty, "all categories");
    }

    [TestMethod]
    public async Task CoordinateAsync_add_more_cyberware_keeps_dialog_open_and_rebuilds_preview()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            ActiveDialog = factory.CreateUiControlDialog("cyberware_add", DesktopPreferenceState.Default) with
            {
                Fields = factory.CreateUiControlDialog("cyberware_add", DesktopPreferenceState.Default).Fields
                    .Select(field => field.Id switch
                    {
                        "uiCyberwareGrade" => field with { Value = "Alpha" },
                        "uiCyberwareMarkup" => field with { Value = "10" },
                        "uiCyberwareBlackMarketDiscount" => field with { Value = "true" },
                        _ => field
                    })
                    .ToArray()
            }
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("add_more", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.ui.cyberware_add", published.ActiveDialog!.Id);
        Assert.AreEqual("0", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiCyberwareMarkup"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiCyberwareSelectionDetails"), "Grade | Alpha");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiCyberwareSelectionDetails"), "Cost | ¥160,920");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiCyberwareLiveRecalc"), "Black Market | Yes");
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiCyberwareLiveRecalc"), "Recalculated Cost | ¥160,920");
        StringAssert.Contains(published.Notice ?? string.Empty, "Cyberware 'Wired Reflexes 2' added.");
    }

    [TestMethod]
    public async Task CoordinateAsync_apply_ruleset_calls_delegate_and_closes_dialog_on_success()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.switch_ruleset",
                Title: "Switch Ruleset",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("preferredRulesetId", "Ruleset", " SR6 ", RulesetDefaults.Sr5)
                ],
                Actions:
                [
                    new DesktopDialogAction("apply_ruleset", "Apply", true)
                ])
        };

        string? preferredRulesetId = null;
        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            SetPreferredRulesetAsync: (rulesetId, _) =>
            {
                preferredRulesetId = rulesetId;
                published = published with { Error = null };
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("apply_ruleset", context, CancellationToken.None);

        Assert.AreEqual("sr6", preferredRulesetId);
        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Preferred ruleset set to 'sr6'.", published.Notice);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_matrix_program_uses_sr6_authored_notice_when_state_is_sr6()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            WorkspaceId = new CharacterWorkspaceId("ws-sr6"),
            OpenWorkspaces =
            [
                new OpenWorkspaceState(new CharacterWorkspaceId("ws-sr6"), "Apex", "APX", DateTimeOffset.Parse("2026-04-21T08:05:00+00:00"), RulesetDefaults.Sr6, true)
            ],
            ActiveDialog = factory.CreateUiControlDialog("matrix_program_add", DesktopPreferenceState.Default)
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        StringAssert.Contains(published.Notice ?? string.Empty, "SR6 setup tools:");
        StringAssert.Contains(published.Notice ?? string.Empty, "Program 'Armor' added.");
    }

    [TestMethod]
    public async Task CoordinateAsync_apply_ruleset_rejects_blank_ruleset()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.switch_ruleset",
                Title: "Switch Ruleset",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("preferredRulesetId", "Ruleset", "   ", string.Empty)
                ],
                Actions:
                [
                    new DesktopDialogAction("apply_ruleset", "Apply", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("apply_ruleset", context, CancellationToken.None);

        Assert.AreEqual("Preferred ruleset is required.", published.Error);
        Assert.IsNotNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_import_rejects_blank_ruleset()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.open_character",
                Title: "Open Character",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("importRulesetId", "Ruleset", "   ", string.Empty),
                    new DesktopDialogField("openCharacterXml", "Character XML", "<character><name>Runner</name></character>", "<character />", true)
                ],
                Actions:
                [
                    new DesktopDialogAction("import", "Import", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("import", context, CancellationToken.None);

        Assert.AreEqual("Ruleset is required.", published.Error);
        Assert.IsNotNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_hero_lab_import_imports_workspace_and_sets_compat_notice()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.hero_lab_importer",
                Title: "Hero Lab Importer",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("importRulesetId", "Ruleset", " sr6 ", string.Empty),
                    new DesktopDialogField("heroLabXml", "Hero Lab XML", "<character><name>Hero</name></character>", "<character />", true)
                ],
                Actions:
                [
                    new DesktopDialogAction("import", "Import", true)
                ])
        };

        WorkspaceImportDocument? imported = null;
        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: (document, _) =>
            {
                imported = document;
                published = published with
                {
                    Error = null,
                    WorkspaceId = new CharacterWorkspaceId("ws-hero")
                };
                return Task.CompletedTask;
            },
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("import", context, CancellationToken.None);

        Assert.IsNotNull(imported);
        StringAssert.Contains(imported!.Content, "<character>");
        Assert.AreEqual("sr6", imported.RulesetId);
        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Hero Lab XML imported.", published.Notice);
        Assert.AreEqual("ws-hero", published.WorkspaceId?.Value);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_gear_adds_item_and_closes_dialog()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.gear_add",
                Title: "Add Gear",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiGearName", "Gear Name", "Ares Alpha", "Ares Predator")
                ],
                Actions:
                [
                    new DesktopDialogAction("add", "Add", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Gear 'Ares Alpha' added.", published.Notice);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_gear_invokes_quick_add_delegate_when_available()
    {
        DialogCoordinator coordinator = new();
        WorkspaceQuickAddRequest? captured = null;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.gear_add",
                Title: "Add Gear",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiGearName", "Gear Name", "Fake SIN", "Ares Predator"),
                    new DesktopDialogField("uiGearCategory", "Category", "Identity", "Gear"),
                    new DesktopDialogField("uiGearSource", "Source", "Run Faster", "Desktop Quick Add"),
                    new DesktopDialogField("uiGearCost", "Cost", "2500", "0"),
                    new DesktopDialogField("uiGearRating", "Rating", "1", "0"),
                    new DesktopDialogField("uiGearQuantity", "Quantity", "2", "1")
                ],
                Actions:
                [
                    new DesktopDialogAction("add", "Add", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            ApplyQuickAddAsync: (request, _) =>
            {
                captured = request;
                published = published with { ActiveSectionId = "gear" };
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(WorkspaceQuickAddKinds.Gear, captured!.Kind);
        Assert.AreEqual("Fake SIN", captured.Name);
        Assert.AreEqual("Identity", captured.Category);
        Assert.AreEqual("Run Faster", captured.Source);
        Assert.AreEqual("2500", captured.Cost);
        Assert.AreEqual(1, captured.Rating);
        Assert.AreEqual(2, captured.Quantity);
        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Gear 'Fake SIN' added.", published.Notice);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_more_gear_invokes_quick_add_delegate_and_keeps_dialog_open()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        WorkspaceQuickAddRequest? captured = null;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            Preferences = DesktopPreferenceState.Default,
            ActiveDialog = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default) with
            {
                Fields = factory.CreateUiControlDialog("gear_add", DesktopPreferenceState.Default).Fields
                    .Select(field => field.Id switch
                    {
                        "uiGearName" => field with { Value = "Certified Credstick" },
                        "uiGearQuantity" => field with { Value = "3" },
                        "uiGearMarkup" => field with { Value = "15" },
                        "uiGearBlackMarketDiscount" => field with { Value = "true" },
                        _ => field
                    })
                    .ToArray()
            }
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            ApplyQuickAddAsync: (request, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("add_more", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(WorkspaceQuickAddKinds.Gear, captured!.Kind);
        Assert.AreEqual("Certified Credstick", captured.Name);
        Assert.AreEqual(3, captured.Quantity);
        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.ui.gear_add", published.ActiveDialog!.Id);
        Assert.AreEqual("0", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "uiGearMarkup"));
        StringAssert.Contains(published.Notice ?? string.Empty, "Gear 'Certified Credstick' added.");
    }

    [TestMethod]
    public async Task CoordinateAsync_add_cyberware_invokes_quick_add_delegate_when_available()
    {
        DialogCoordinator coordinator = new();
        WorkspaceQuickAddRequest? captured = null;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.cyberware_add",
                Title: "Add Cyberware",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiCyberwareName", "Cyberware", "Wired Reflexes 2", "Wired Reflexes 2"),
                    new DesktopDialogField("uiCyberwareCategory", "Category", "Bodyware", "Show All"),
                    new DesktopDialogField("uiCyberwareSelectedBranch", "Selected Branch", "Bodyware", "Bodyware"),
                    new DesktopDialogField("uiCyberwareSource", "Source", "Core Rulebook p. 461", "Desktop Quick Add"),
                    new DesktopDialogField("uiCyberwareCost", "Cost", "149000", "0"),
                    new DesktopDialogField("uiCyberwareRating", "Rating", "2", "0"),
                    new DesktopDialogField("uiCyberwareGrade", "Grade", "Alpha", "Standard"),
                    new DesktopDialogField("uiCyberwareEssence", "Essence", "2.70", "0.00"),
                    new DesktopDialogField("uiCyberwareCapacity", "Capacity", "n/a", "n/a"),
                    new DesktopDialogField("uiCyberwareSlot", "Location", "Body", "Body")
                ],
                Actions:
                [
                    new DesktopDialogAction("add", "Add", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            ApplyQuickAddAsync: (request, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(WorkspaceQuickAddKinds.Cyberware, captured!.Kind);
        Assert.AreEqual("Wired Reflexes 2", captured.Name);
        Assert.AreEqual("Bodyware", captured.Category);
        Assert.AreEqual("149000", captured.Cost);
        Assert.AreEqual("Alpha", captured.Grade);
        Assert.AreEqual("2.70", captured.Essence);
        Assert.IsNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_spell_invokes_quick_add_delegate_when_available()
    {
        DialogCoordinator coordinator = new();
        WorkspaceQuickAddRequest? captured = null;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.spell_add",
                Title: "Add Spell",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiSpellName", "Spell", "Heal", "Stunbolt"),
                    new DesktopDialogField("uiSpellCategory", "Category", "Health", "Combat"),
                    new DesktopDialogField("uiSpellSource", "Source", "Core Rulebook p. 289", "Desktop Quick Add")
                ],
                Actions:
                [
                    new DesktopDialogAction("add", "Add", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            ApplyQuickAddAsync: (request, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(WorkspaceQuickAddKinds.Spell, captured!.Kind);
        Assert.AreEqual("Heal", captured.Name);
        Assert.AreEqual("Health", captured.Category);
        Assert.AreEqual("Mana", captured.Type);
        Assert.AreEqual("F-3", captured.DrainValue);
        Assert.IsNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_matrix_program_invokes_quick_add_delegate_when_available()
    {
        DialogCoordinator coordinator = new();
        WorkspaceQuickAddRequest? captured = null;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.matrix_program_add",
                Title: "Add Program / Cyberdeck Item",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiMatrixProgramName", "Program", "Baby Monitor", "Armor"),
                    new DesktopDialogField("uiMatrixProgramSlot", "Slot", "Hacking", "Common"),
                    new DesktopDialogField("uiMatrixProgramSource", "Source", "Data Trails p. 61", "Desktop Quick Add")
                ],
                Actions:
                [
                    new DesktopDialogAction("add", "Add", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            ApplyQuickAddAsync: (request, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(WorkspaceQuickAddKinds.MatrixProgram, captured!.Kind);
        Assert.AreEqual("Baby Monitor", captured.Name);
        Assert.AreEqual("Hacking", captured.Slot);
        Assert.AreEqual("Data Trails p. 61", captured.Source);
        Assert.IsNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_add_initiation_invokes_quick_add_delegate_when_available()
    {
        DialogCoordinator coordinator = new();
        WorkspaceQuickAddRequest? captured = null;
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.initiation_add",
                Title: "Add Initiation / Submersion",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiInitiationTrack", "Track", "Submersion", "Initiation"),
                    new DesktopDialogField("uiInitiationGrade", "Grade", "2", "1"),
                    new DesktopDialogField("uiInitiationReward", "Reward", "Overclocking", "Masking")
                ],
                Actions:
                [
                    new DesktopDialogAction("add", "Add", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published,
            ApplyQuickAddAsync: (request, _) =>
            {
                captured = request;
                return Task.CompletedTask;
            });

        await coordinator.CoordinateAsync("add", context, CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual(WorkspaceQuickAddKinds.InitiationGrade, captured!.Kind);
        Assert.AreEqual("Overclocking", captured.Name);
        Assert.AreEqual(2, captured.Rating);
        Assert.IsTrue(captured.Res);
        Assert.IsNull(published.ActiveDialog);
    }

    [TestMethod]
    public async Task CoordinateAsync_apply_contact_edit_updates_notice_and_closes_dialog()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.ui.contact_edit",
                Title: "Edit Contact",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("uiContactEditName", "Name", "Nines", "Selected Contact")
                ],
                Actions:
                [
                    new DesktopDialogAction("apply", "Apply", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("apply", context, CancellationToken.None);

        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual("Contact renamed to 'Nines'.", published.Notice);
    }

    [DataTestMethod]
    [DataRow("delete_entry", "Entry 'Current Entry' removed.")]
    [DataRow("gear_delete", "Gear 'Armor Jacket' removed.")]
    [DataRow("cyberware_delete", "Cyberware 'Cybereyes Rating 4' removed.")]
    [DataRow("drug_delete", "Drug 'Jazz' removed.")]
    [DataRow("magic_delete", "Spell/power 'Stunbolt' removed.")]
    [DataRow("skill_remove", "Skill 'Perception' removed.")]
    [DataRow("vehicle_delete", "Vehicle 'GMC Roadmaster' removed.")]
    [DataRow("contact_remove", "Contact 'Mr. Johnson' removed.")]
    [DataRow("quality_delete", "Quality 'First Impression' removed.")]
    public async Task CoordinateAsync_delete_legacy_utility_dialogs_close_with_targeted_notice(string controlId, string expectedNotice)
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = factory.CreateUiControlDialog(controlId, DesktopPreferenceState.Default)
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("delete", context, CancellationToken.None);

        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual(expectedNotice, published.Notice);
    }

    [TestMethod]
    public async Task CoordinateAsync_refresh_watch_folder_character_roster_creates_missing_folder_and_rebuilds_dialog()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-refresh-{Guid.NewGuid():N}");
        CharacterWorkspaceId workspaceId = new("ghost-runner");
        OpenWorkspaceState[] openWorkspaces =
        [
            new OpenWorkspaceState(workspaceId, "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr5, true)
        ];
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterRosterPath = rosterPath
        };

        try
        {
            CharacterOverviewState published = CharacterOverviewState.Empty with
            {
                Profile = CreateProfile("Ghost Runner", "GST"),
                Preferences = preferences,
                WorkspaceId = workspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveDialog = factory.CreateCommandDialog(
                    "character_roster",
                    CreateProfile("Ghost Runner", "GST"),
                    preferences,
                    activeSectionJson: null,
                    currentWorkspace: workspaceId,
                    rulesetId: RulesetDefaults.Sr5,
                    openWorkspaces: openWorkspaces)
            };

            DialogCoordinationContext context = new(
                State: published,
                Publish: state => published = state,
                ImportAsync: static (_, _) => Task.CompletedTask,
                UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
                GetState: () => published);

            await coordinator.CoordinateAsync("refresh_watch_folder", context, CancellationToken.None);

            Assert.IsTrue(Directory.Exists(rosterPath));
            Assert.IsNotNull(published.ActiveDialog);
            Assert.AreEqual("dialog.character_roster", published.ActiveDialog!.Id);
            StringAssert.Contains(published.Notice ?? string.Empty, "created and refreshed");
            StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "rosterWatchFolderStatus"), "Watcher | FileSystemWatcher active");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CoordinateAsync_open_runner_character_roster_switches_workspace_and_closes_dialog()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterWorkspaceId workspaceOne = new("ghost-runner");
        CharacterWorkspaceId workspaceTwo = new("apex-runner");
        OpenWorkspaceState[] openWorkspaces =
        [
            new OpenWorkspaceState(workspaceOne, "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr5, true),
            new OpenWorkspaceState(workspaceTwo, "Apex", "APX", DateTimeOffset.Parse("2026-04-21T09:00:00+00:00"), RulesetDefaults.Sr6, true)
        ];
        DesktopDialogState rosterDialog = factory.CreateCommandDialog(
            "character_roster",
            CreateProfile("Ghost Runner", "GST"),
            DesktopPreferenceState.Default,
            activeSectionJson: null,
            currentWorkspace: workspaceOne,
            rulesetId: RulesetDefaults.Sr5,
            openWorkspaces: openWorkspaces) with
        {
            Fields = factory.CreateCommandDialog(
                    "character_roster",
                    CreateProfile("Ghost Runner", "GST"),
                    DesktopPreferenceState.Default,
                    activeSectionJson: null,
                    currentWorkspace: workspaceOne,
                    rulesetId: RulesetDefaults.Sr5,
                    openWorkspaces: openWorkspaces)
                .Fields
                .Select(field => field.Id switch
                {
                    "rosterSelectedRunnerId" => field with { Value = workspaceTwo.Value },
                    "rosterSelectedRunnerAlias" => field with { Value = "APX" },
                    _ => field
                })
                .ToArray()
        };

        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            WorkspaceId = workspaceOne,
            OpenWorkspaces = openWorkspaces,
            ActiveDialog = rosterDialog
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("open_runner", context, CancellationToken.None);

        Assert.IsNull(published.ActiveDialog);
        Assert.AreEqual(workspaceTwo, published.WorkspaceId);
        StringAssert.Contains(published.Notice ?? string.Empty, "SR6 setup tools:");
        StringAssert.Contains(published.Notice ?? string.Empty, "Runner 'APX' opened from roster.");
    }

    [TestMethod]
    public async Task CoordinateAsync_open_watch_file_character_roster_keeps_dialog_open_with_notice()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-watch-{Guid.NewGuid():N}");
        string nestedPath = Path.Combine(rosterPath, "campaign-a");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "ghost-runner.chum5"), "runner");
        CharacterWorkspaceId workspaceId = new("ghost-runner");
        OpenWorkspaceState[] openWorkspaces =
        [
            new OpenWorkspaceState(workspaceId, "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr5, true)
        ];
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterRosterPath = rosterPath
        };

        try
        {
            CharacterOverviewState published = CharacterOverviewState.Empty with
            {
                Profile = CreateProfile("Ghost Runner", "GST"),
                Preferences = preferences,
                WorkspaceId = workspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveDialog = factory.CreateCommandDialog(
                    "character_roster",
                    CreateProfile("Ghost Runner", "GST"),
                    preferences,
                    activeSectionJson: null,
                    currentWorkspace: workspaceId,
                    rulesetId: RulesetDefaults.Sr5,
                    openWorkspaces: openWorkspaces)
            };

            DialogCoordinationContext context = new(
                State: published,
                Publish: state => published = state,
                ImportAsync: static (_, _) => Task.CompletedTask,
                UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
                GetState: () => published);

            await coordinator.CoordinateAsync("open_watch_file", context, CancellationToken.None);

            Assert.IsNotNull(published.ActiveDialog);
            Assert.AreEqual("dialog.character_roster", published.ActiveDialog!.Id);
            StringAssert.Contains(published.Notice ?? string.Empty, "already aligned");
            StringAssert.Contains(published.Notice ?? string.Empty, "GST");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CoordinateAsync_open_watch_file_character_roster_switches_workspace_when_watch_file_matches_another_runner()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-watch-switch-{Guid.NewGuid():N}");
        string nestedPath = Path.Combine(rosterPath, "campaign-a");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "apex-runner.chum5"), "runner");
        CharacterWorkspaceId workspaceOne = new("ghost-runner");
        CharacterWorkspaceId workspaceTwo = new("apex-runner");
        OpenWorkspaceState[] openWorkspaces =
        [
            new OpenWorkspaceState(workspaceOne, "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr5, true),
            new OpenWorkspaceState(workspaceTwo, "Apex", "APX", DateTimeOffset.Parse("2026-04-21T09:00:00+00:00"), RulesetDefaults.Sr6, true)
        ];
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterRosterPath = rosterPath
        };

        try
        {
            DesktopDialogState rosterDialog = WithFieldValues(
                factory.CreateCommandDialog(
                    "character_roster",
                    CreateProfile("Ghost Runner", "GST"),
                    preferences,
                    activeSectionJson: null,
                    currentWorkspace: workspaceOne,
                    rulesetId: RulesetDefaults.Sr5,
                    openWorkspaces: openWorkspaces),
                ("rosterSelectedWatchFile", "campaign-a/apex-runner.chum5"));

            CharacterOverviewState published = CharacterOverviewState.Empty with
            {
                Profile = CreateProfile("Ghost Runner", "GST"),
                Preferences = preferences,
                WorkspaceId = workspaceOne,
                OpenWorkspaces = openWorkspaces,
                ActiveDialog = rosterDialog
            };

            DialogCoordinationContext context = new(
                State: published,
                Publish: state => published = state,
                ImportAsync: static (_, _) => Task.CompletedTask,
                UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
                GetState: () => published);

            await coordinator.CoordinateAsync("open_watch_file", context, CancellationToken.None);

            Assert.IsNull(published.ActiveDialog);
            Assert.AreEqual(workspaceTwo, published.WorkspaceId);
            StringAssert.Contains(published.Notice ?? string.Empty, "SR6 setup tools:");
            StringAssert.Contains(published.Notice ?? string.Empty, "Watched runner 'APX' opened from roster watch folder.");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CoordinateAsync_open_roster_folder_character_roster_creates_folder_and_keeps_dialog_open()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-folder-{Guid.NewGuid():N}");
        CharacterWorkspaceId workspaceId = new("ghost-runner");
        OpenWorkspaceState[] openWorkspaces =
        [
            new OpenWorkspaceState(workspaceId, "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr5, true)
        ];
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterRosterPath = rosterPath
        };

        try
        {
            CharacterOverviewState published = CharacterOverviewState.Empty with
            {
                Profile = CreateProfile("Ghost Runner", "GST"),
                Preferences = preferences,
                WorkspaceId = workspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveDialog = factory.CreateCommandDialog(
                    "character_roster",
                    CreateProfile("Ghost Runner", "GST"),
                    preferences,
                    activeSectionJson: null,
                    currentWorkspace: workspaceId,
                    rulesetId: RulesetDefaults.Sr5,
                    openWorkspaces: openWorkspaces)
            };

            DialogCoordinationContext context = new(
                State: published,
                Publish: state => published = state,
                ImportAsync: static (_, _) => Task.CompletedTask,
                UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
                GetState: () => published);

            await coordinator.CoordinateAsync("open_roster_folder", context, CancellationToken.None);

            Assert.IsTrue(Directory.Exists(rosterPath));
            Assert.IsNotNull(published.ActiveDialog);
            StringAssert.Contains(published.Notice ?? string.Empty, "Roster folder created");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CoordinateAsync_open_portrait_character_roster_keeps_dialog_open_with_notice()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        string rosterPath = Path.Combine(Path.GetTempPath(), $"chummer-roster-portrait-{Guid.NewGuid():N}");
        string nestedPath = Path.Combine(rosterPath, "campaign-a");
        Directory.CreateDirectory(nestedPath);
        File.WriteAllText(Path.Combine(nestedPath, "ghost-runner.chum5"), "runner");
        File.WriteAllText(Path.Combine(nestedPath, "ghost-runner.png"), "portrait");
        CharacterWorkspaceId workspaceId = new("ghost-runner");
        OpenWorkspaceState[] openWorkspaces =
        [
            new OpenWorkspaceState(workspaceId, "Ghost Runner", "GST", DateTimeOffset.Parse("2026-04-21T08:00:00+00:00"), RulesetDefaults.Sr5, true)
        ];
        DesktopPreferenceState preferences = DesktopPreferenceState.Default with
        {
            CharacterRosterPath = rosterPath
        };

        try
        {
            CharacterOverviewState published = CharacterOverviewState.Empty with
            {
                Profile = CreateProfile("Ghost Runner", "GST"),
                Preferences = preferences,
                WorkspaceId = workspaceId,
                OpenWorkspaces = openWorkspaces,
                ActiveDialog = factory.CreateCommandDialog(
                    "character_roster",
                    CreateProfile("Ghost Runner", "GST"),
                    preferences,
                    activeSectionJson: null,
                    currentWorkspace: workspaceId,
                    rulesetId: RulesetDefaults.Sr5,
                    openWorkspaces: openWorkspaces)
            };

            DialogCoordinationContext context = new(
                State: published,
                Publish: state => published = state,
                ImportAsync: static (_, _) => Task.CompletedTask,
                UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
                GetState: () => published);

            await coordinator.CoordinateAsync("open_portrait", context, CancellationToken.None);

            Assert.IsNotNull(published.ActiveDialog);
            Assert.AreEqual("dialog.character_roster", published.ActiveDialog!.Id);
            StringAssert.Contains(published.Notice ?? string.Empty, "ghost-runner.png");
        }
        finally
        {
            if (Directory.Exists(rosterPath))
            {
                Directory.Delete(rosterPath, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task CoordinateAsync_open_source_master_index_keeps_dialog_open_with_notice()
    {
        DialogCoordinator coordinator = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState(
                Id: "dialog.master_index",
                Title: "Master Index",
                Message: null,
                Fields:
                [
                    new DesktopDialogField("masterIndexCurrentSourcebook", "Selected Book", "SR5 Core", "SR5 Core"),
                    new DesktopDialogField("masterIndexSelectedSource", "Source", "/tmp/core.pdf", "/tmp/core.pdf")
                ],
                Actions:
                [
                    new DesktopDialogAction("open_source", "Open Linked PDF", true)
                ])
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("open_source", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.master_index", published.ActiveDialog!.Id);
        StringAssert.Contains(published.Notice ?? string.Empty, "SR5 Core");
    }

    [TestMethod]
    public async Task CoordinateAsync_switch_sourcebook_master_index_keeps_dialog_open_with_notice()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = WithFieldValues(
                factory.CreateCommandDialog(
                    "master_index",
                    CreateProfile("Ghost Runner", "GST"),
                    DesktopPreferenceState.Default,
                    activeSectionJson: null,
                    currentWorkspace: null,
                    rulesetId: RulesetDefaults.Sr5,
                    masterIndex: CreateMasterIndexResponse()),
                ("masterIndexActiveFile", "weapons.xml"),
                ("masterIndexCurrentFile", "weapons.xml · 27 indexed entries"),
                ("masterIndexActiveResultKey", string.Empty))
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("switch_sourcebook", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.master_index", published.ActiveDialog!.Id);
        Assert.AreEqual("street-wyrd", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "masterIndexActiveSourcebookId"));
        Assert.AreEqual("armor.xml", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "masterIndexActiveFile"));
        Assert.AreEqual("SW · Street Wyrd", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "masterIndexCurrentSourcebook"));
        StringAssert.Contains(published.Notice ?? string.Empty, "Street Wyrd");
        StringAssert.Contains(published.Notice ?? string.Empty, "armor.xml");
    }

    [TestMethod]
    public async Task CoordinateAsync_switch_file_master_index_cycles_active_file_and_rebuilds_dialog()
    {
        DialogCoordinator coordinator = new();
        DesktopDialogFactory factory = new();
        CharacterOverviewState published = CharacterOverviewState.Empty with
        {
            ActiveDialog = factory.CreateCommandDialog(
                "master_index",
                CreateProfile("Ghost Runner", "GST"),
                DesktopPreferenceState.Default,
                activeSectionJson: null,
                currentWorkspace: null,
                rulesetId: RulesetDefaults.Sr5,
                masterIndex: CreateMasterIndexResponse())
        };

        DialogCoordinationContext context = new(
            State: published,
            Publish: state => published = state,
            ImportAsync: static (_, _) => Task.CompletedTask,
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => published);

        await coordinator.CoordinateAsync("switch_file", context, CancellationToken.None);

        Assert.IsNotNull(published.ActiveDialog);
        Assert.AreEqual("dialog.master_index", published.ActiveDialog!.Id);
        Assert.AreEqual("weapons.xml", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "masterIndexActiveFile"));
        Assert.AreEqual("weapons.xml", DesktopDialogFieldValueParser.GetValue(published.ActiveDialog, "masterIndexFileSelection"));
        StringAssert.Contains(published.Notice ?? string.Empty, "weapons.xml");
    }

    private static CharacterProfileSection CreateProfile(string name, string alias)
    {
        return new CharacterProfileSection(
            Name: name,
            Alias: alias,
            PlayerName: string.Empty,
            Metatype: "Human",
            Metavariant: string.Empty,
            Sex: string.Empty,
            Age: string.Empty,
            Height: string.Empty,
            Weight: string.Empty,
            Hair: string.Empty,
            Eyes: string.Empty,
            Skin: string.Empty,
            Concept: string.Empty,
            Description: string.Empty,
            Background: string.Empty,
            CreatedVersion: string.Empty,
            AppVersion: string.Empty,
            BuildMethod: string.Empty,
            GameplayOption: string.Empty,
            Created: true,
            Adept: false,
            Magician: false,
            Technomancer: false,
            AI: false,
            MainMugshotIndex: 0,
            MugshotCount: 1);
    }

    private static DesktopDialogState WithFieldValues(DesktopDialogState dialog, params (string FieldId, string Value)[] replacements)
    {
        return dialog with
        {
            Fields = dialog.Fields
                .Select(field =>
                {
                    foreach ((string fieldId, string value) in replacements)
                    {
                        if (string.Equals(field.Id, fieldId, StringComparison.Ordinal))
                        {
                            return field with
                            {
                                Value = value,
                                Placeholder = value
                            };
                        }
                    }

                    return field;
                })
                .ToArray()
        };
    }

    private static MasterIndexResponse CreateMasterIndexResponse()
    {
        return new MasterIndexResponse(
            Count: 4,
            GeneratedUtc: DateTimeOffset.UtcNow,
            Files:
            [
                new MasterIndexFileEntry("books.xml", "chummer", 42),
                new MasterIndexFileEntry("weapons.xml", "chummer", 27),
                new MasterIndexFileEntry("armor.xml", "chummer", 18)
            ],
            ReferenceLanePosture: "governed",
            SourcebookCount: 12,
            Sourcebooks:
            [
                new MasterIndexSourcebookEntry(
                    Id: "core-rulebook",
                    Code: "CRB",
                    Name: "Core Rulebook",
                    Permanent: true,
                    ReferencePosture: "governed",
                    RuleSnippetCount: 16,
                    RuleSnippets:
                    [
                        new MasterIndexRuleSnippetEntry(
                            Language: "en-us",
                            Page: 20,
                            Snippet: "Reference notes stay in this pane while the selected entry remains visible.",
                            Provenance: "books.xml"),
                        new MasterIndexRuleSnippetEntry(
                            Language: "en-us",
                            Page: 21,
                            Snippet: "Indexed source detail remains on the right, matching the legacy utility posture.",
                            Provenance: "books.xml")
                    ],
                    ReferenceSourcePosture: "governed",
                    LocalPdfPath: "/books/core-rulebook.pdf",
                    ReferenceUrl: "https://example.test/core-rulebook"),
                new MasterIndexSourcebookEntry(
                    Id: "street-wyrd",
                    Code: "SW",
                    Name: "Street Wyrd",
                    Permanent: false,
                    ReferencePosture: "stale",
                    RuleSnippetCount: 6,
                    RuleSnippets:
                    [
                        new MasterIndexRuleSnippetEntry(
                            Language: "en-us",
                            Page: 122,
                            Snippet: "Street Wyrd armor-side note keeps the preview populated after switching books.",
                            Provenance: "armor.xml")
                    ],
                    ReferenceSourcePosture: "stale",
                    ReferenceSnapshot: "https://example.test/snapshots/street-wyrd")
            ],
            ReferenceCoveragePercent: 67,
            SourcebooksWithSnippets: 8,
            ReferenceSourceLanePosture: "governed",
            SourcebooksWithGovernedReferenceSources: 9,
            SourcebooksWithStaleReferenceSources: 2,
            SourcebooksMissingReferenceSources: 1,
            ReferenceSourceLaneReceipt: "all sourcebooks expose governed PDF/URL/site-snapshot references.",
            SettingsLanePosture: "governed",
            SettingsProfileCount: 6,
            SettingsProfilesWithSourceToggles: 5,
            DistinctSourcebookToggles: 24,
            SourceToggleLanePosture: "governed",
            SourceSelectionLaneReceipt: "sourcebook selection is governed by 24 toggles across 12 sourcebooks (67% coverage).",
            SourcebookToggleCoveragePercent: 67,
            CustomDataLanePosture: "partial",
            CustomDataLaneReceipt: "custom-data lane is partial: 2 configured custom-data directories stay governed but not yet fully automated.",
            CustomDataAuthoringLaneReceipt: "custom-data authoring is partial: 2 configured custom-data directories with stale overlay bridge posture.",
            SettingsProfilesWithCustomDataDirectories: 2,
            DistinctCustomDataDirectoryCount: 2,
            XmlBridgePosture: "governed",
            XmlBridgeLaneReceipt: "xml bridge is governed: 2 enabled data overlays expose XML payloads.",
            EnabledDataOverlayCount: 2,
            TranslatorLanePosture: "governed",
            TranslatorLaneReceipt: "translator lane is governed: 6 translator corpus files and 3 enabled language overlays.",
            TranslatorBridgePosture: "governed",
            TranslatorLanguageCount: 6,
            EnabledLanguageOverlayCount: 3,
            OnlineStorageLanePosture: "partial",
            OnlineStorageReceiptPosture: "stale",
            OnlineStorageLaneReceipt: "online storage lane is partial: 1/2 continuity receipts are current with stale release proof on one required host lane.",
            OnlineStorageReceiptsCovered: 1,
            OnlineStorageReceiptsExpected: 2,
            OnlineStorageCoveragePercent: 50,
            ImportOracleLanePosture: "partial",
            ImportOracleReceiptPosture: "stale",
            LegacyChummer4FixtureCount: 18,
            LegacyChummer5FixtureCount: 31,
            HeroLabFixtureCount: 0,
            AdjacentSr6OracleReceiptPosture: "partial",
            AdjacentSr6OracleSourcesCovered: 1,
            AdjacentSr6OracleSourcesExpected: 2,
            ImportOracleSourcesCovered: 3,
            ImportOracleSourcesExpected: 4,
            ImportOracleCoveragePercent: 75,
            ImportOracleMissingSources: ["Hero Lab"],
            ImportOracleLaneReceipt: "import oracle is partial: 3/4 fixture families covered (missing: Hero Lab), adjacent SR6 oracle coverage 1/2.",
            AdjacentSr6OracleLaneReceipt: "adjacent SR6 oracle lane is partial: 1/2 covered with stale receipts for Genesis/CommLink.",
            Sr6SupplementLanePosture: "partial",
            Sr6DesignerToolsPosture: "partial",
            Sr6DesignerFamiliesAvailable: 4,
            Sr6DesignerFamiliesExpected: 5,
            HouseRuleLanePosture: "governed",
            HouseRuleOverlayCount: 3,
            Sr6SuccessorLaneReceipt: "sr6 successor lane is partial: supplement/governed designers/house-rule posture remains mixed.");
    }

    private sealed class SuccessfulDiceEvaluator : IEngineEvaluator
    {
        public ValueTask<RulesetCapabilityInvocationResult> InvokeAsync(
            RulesetCapabilityInvocationRequest request,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string expression = request.Arguments
                .FirstOrDefault(argument => string.Equals(argument.Name, "expression", StringComparison.Ordinal))
                ?.Value.StringValue
                ?? "1d6";

            return ValueTask.FromResult(new RulesetCapabilityInvocationResult(
                Success: true,
                Output: new RulesetCapabilityValue(
                    Kind: RulesetCapabilityValueKinds.String,
                    StringValue: $"{expression} => 13"),
                Diagnostics: []));
        }
    }
}
