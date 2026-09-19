using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class NewRunnerBuildMethodTests
{
    [TestMethod]
    [DataRow("Priority", CharacterCreationBuildMethods.Priority)]
    [DataRow("SumToTen", CharacterCreationBuildMethods.SumToTen)]
    [DataRow("SumtoTen", CharacterCreationBuildMethods.SumToTen)]
    [DataRow("Karma", CharacterCreationBuildMethods.Karma)]
    [DataRow("LifeModule", CharacterCreationBuildMethods.LifeModules)]
    [DataRow("sumtoten", null)]
    [DataRow("SUMTOTEN", null)]
    [DataRow("Sum-to-Ten", null)]
    [DataRow("unsupported", null)]
    public async Task Create_dispatches_only_the_canonical_core_method(string selected, string? canonical)
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
            Assert.IsTrue(CharacterCreationBootstrapProfiles.TryResolveCanonicalSettingsProfileId(canonical, out string profile));
            Assert.AreEqual(profile, captured.SettingsProfileId);
            StringAssert.Contains(state.Error ?? string.Empty, "test-core-response");
        }
        Assert.IsNull(state.WorkspaceId);
    }
}
