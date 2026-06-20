using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Chummer.Tests.Presentation;

[TestClass]
public class OverviewCommandPolicyTests
{
    [TestMethod]
    public void Known_shared_command_policy_covers_all_app_catalog_commands()
    {
        string[] missing = AppCommandCatalog.All
            .Select(command => command.Id)
            .Where(commandId => !OverviewCommandPolicy.IsKnownSharedCommand(commandId))
            .OrderBy(commandId => commandId)
            .ToArray();

        Assert.IsEmpty(
            missing,
            "App commands missing shared presenter command policy coverage: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void Refresh_character_is_treated_as_known_shared_command()
    {
        Assert.IsTrue(OverviewCommandPolicy.IsKnownSharedCommand("refresh_character"));
    }

    [TestMethod]
    public void Switch_ruleset_is_treated_as_known_shared_command()
    {
        Assert.IsTrue(OverviewCommandPolicy.IsKnownSharedCommand("switch_ruleset"));
    }

    [TestMethod]
    public void Runtime_inspector_is_treated_as_known_shared_command()
    {
        Assert.IsTrue(OverviewCommandPolicy.IsKnownSharedCommand(OverviewCommandPolicy.RuntimeInspectorCommandId));
        Assert.IsTrue(OverviewCommandPolicy.IsRuntimeInspectorCommand(OverviewCommandPolicy.RuntimeInspectorCommandId));
    }

    [TestMethod]
    public void Auto_alice_is_treated_as_known_dialog_command()
    {
        Assert.IsTrue(OverviewCommandPolicy.IsKnownSharedCommand(DesktopAliceAssistant.CommandId));
        Assert.IsTrue(OverviewCommandPolicy.IsDialogCommand(DesktopAliceAssistant.CommandId));
        Assert.IsTrue(OverviewCommandPolicy.IsAiFeatureCommand(DesktopAliceAssistant.CommandId));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreference(
            DesktopAliceAssistant.CommandId,
            DesktopPreferenceState.Default with { DisableAiFeatures = true }));
    }

    [TestMethod]
    public void Origin_dossier_is_treated_as_known_dialog_command()
    {
        Assert.IsTrue(OverviewCommandPolicy.IsKnownSharedCommand("new_character_origin"));
        Assert.IsTrue(OverviewCommandPolicy.IsDialogCommand("new_character_origin"));
        Assert.IsTrue(OverviewCommandPolicy.IsAiFeatureCommand("new_character_origin"));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreference(
            "new_character_origin",
            DesktopPreferenceState.Default with { DisableAiFeatures = true }));
    }

    [TestMethod]
    public void Ai_feature_preference_blocks_ai_character_and_companion_options_without_blocking_critter_commands()
    {
        DesktopPreferenceState quiet = DesktopPreferenceState.Default with { DisableAiFeatures = true };

        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("A.I.", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("A.I. - 6 Depth", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("Metasapient A.I.", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("4e A.I.s", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("AI Companion", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("ai_companion", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("Artificial_Intelligence", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("E-Ghost", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("Xenosapients", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("Open Explain Companion", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("New Critter", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("Critter Powers", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("Human", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForCharacterOrCompanionOption("A.I.", DesktopPreferenceState.Default));
    }

    [TestMethod]
    public void Guided_horizon_policy_hides_only_assistant_and_local_automation_lanes()
    {
        DesktopPreferenceState quiet = DesktopPreferenceState.Default with { DisableAiFeatures = true };

        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon("alice", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon("local_co_processor", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon("knowledge_fabric", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon("quicksilver", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon("karma_forge", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon("alice", DesktopPreferenceState.Default));
    }

    [TestMethod]
    public void Guided_route_policy_hides_alice_and_local_coprocessor_routes()
    {
        DesktopPreferenceState quiet = DesktopPreferenceState.Default with { DisableAiFeatures = true };

        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/alice", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/account/alice/build-123", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/local-co-processor", quiet));
        Assert.IsTrue(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/account/local-co-processor?tab=policy", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/rules", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/account/quicksilver", quiet));
        Assert.IsFalse(OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForRoute("/account/alice", DesktopPreferenceState.Default));
    }

    [TestMethod]
    public void Show_login_video_is_treated_as_known_help_dialog_command_with_desktop_host_override()
    {
        Assert.IsTrue(OverviewCommandPolicy.IsKnownSharedCommand("show_login_video"));
        Assert.IsTrue(OverviewCommandPolicy.IsDialogCommand("show_login_video"));
    }

    [TestMethod]
    public void Unknown_command_is_not_marked_as_known()
    {
        Assert.IsFalse(OverviewCommandPolicy.IsKnownSharedCommand("totally_unknown_command"));
    }
}
