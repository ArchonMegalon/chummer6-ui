using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class NewRunnerBuildMethodTests
{
    [TestMethod]
    [DataRow("Priority", CharacterCreationBuildMethods.Priority, NativeSr5CreationProfiles.Priority)]
    [DataRow("SumToTen", CharacterCreationBuildMethods.SumToTen, NativeSr5CreationProfiles.SumToTen)]
    [DataRow("SumtoTen", CharacterCreationBuildMethods.SumToTen, NativeSr5CreationProfiles.SumToTen)]
    [DataRow("Karma", CharacterCreationBuildMethods.Karma, NativeSr5CreationProfiles.KarmaQualityPrompts)]
    [DataRow("LifeModule", CharacterCreationBuildMethods.LifeModules, NativeSr5CreationProfiles.LifeModules)]
    [DataRow("sumtoten", null, null)]
    [DataRow("SUMTOTEN", null, null)]
    [DataRow("Sum-to-Ten", null, null)]
    [DataRow("unsupported", null, null)]
    public async Task Create_dispatches_only_the_canonical_core_method(string selected, string? canonical, string? nativeProfile)
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with
        {
            ActiveDialog = new DesktopDialogState("dialog.new_character", "New runner", null,
                [new DesktopDialogField("newCharacterBuildMethod", "Build method", selected, selected)],
                [new DesktopDialogAction("create_character", "Create", true)])
        };
        CharacterCreationBootstrapRequest? captured = null;
        var context = new DialogCoordinationContext(
            State: state,
            Publish: next => state = next,
            GetState: () => state,
            ImportAsync: (_, _) => throw new InvalidOperationException("Creation must not use generic import."),
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            CreateCharacterBootstrapAsync: (request, _) =>
            {
                captured = request;
                return Task.FromResult(new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable, null, ["test-core-response"]));
            },
            LoadWorkspaceAsync: static (_, _) => Task.CompletedTask);

        await new DialogCoordinator().CoordinateAsync("create_character", context, CancellationToken.None);

        if (canonical is null)
        {
            Assert.IsNull(captured);
            StringAssert.Contains(state.Error ?? string.Empty, CharacterCreationBootstrapBlockers.BuildMethodInvalid);
        }
        else
        {
            Assert.IsNotNull(captured);
            Assert.AreEqual(canonical, captured.BuildMethod);
            Assert.AreEqual(RulesetDefaults.Sr5, captured.RulesetId);
            Assert.AreEqual(nativeProfile, captured.SettingsProfileId);
            StringAssert.Contains(state.Error ?? string.Empty, "test-core-response");
        }
        Assert.IsNull(state.WorkspaceId);
    }
}
