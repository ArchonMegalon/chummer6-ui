using System.Reflection;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.CreationWizard.Presentation.Tests;

[TestClass]
public sealed class Sr6BuildMethodSelectionTests
{
    [TestMethod]
    [DataRow("Priority")]
    [DataRow("SumtoTen")]
    [DataRow("PointBuy")]
    [DataRow("LifePath")]
    public void Selection_and_rebuild_preserve_exact_sr6_identity(string method)
    {
        DesktopDialogState dialog = Create(method);
        DesktopDialogField field = dialog.Fields.Single(item => item.Id == "newCharacterBuildMethod");
        string[] values = field.Options!.Select(option => option.Value).ToArray();
        CollectionAssert.AreEqual(Sr6CharacterCreationBuildMethods.All.ToArray(),
            values);
        Assert.AreEqual(method, field.Value);
        Assert.AreEqual(method, DesktopDialogFieldValueParser.GetValue(Rebuild(dialog), "newCharacterBuildMethod"));
        CollectionAssert.DoesNotContain(values, "Karma");
        CollectionAssert.DoesNotContain(values, "LifeModule");
        StringAssert.Contains(dialog.Message!, "not connected");
    }

    [TestMethod]
    [DataRow("PointBuy")]
    [DataRow("LifePath")]
    public void Alternative_never_substitutes_sr5_karma(string method)
    {
        MethodInfo continuation = typeof(DesktopDialogFactory)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(info => info.Name == "BuildNewCharacterContinuationDialog" && info.GetParameters().Length == 5);
        DesktopDialogState dialog = (DesktopDialogState)continuation.Invoke(null,
            new object[] { RulesetDefaults.Sr6, method, false, "Six", "VI" })!;
        Assert.AreEqual("dialog.new_character.sr6_wizard_unavailable", dialog.Id);
        Assert.AreEqual(method, DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowBuildMethod"));
        Assert.AreEqual(RulesetDefaults.Sr6,
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowRulesetId"));
        Assert.IsFalse(dialog.Fields.Any(field => field.Id.Contains("Karma", StringComparison.OrdinalIgnoreCase)));
        CollectionAssert.AreEqual(new[] { "cancel" }, dialog.Actions.Select(action => action.Id).ToArray());
    }

    [TestMethod]
    public void Sr6_choice_does_not_leak_into_sr5_after_edition_change()
    {
        DesktopDialogState dialog = Create("LifePath");
        DesktopDialogState sr5 = Rebuild(dialog with
        {
            Fields = dialog.Fields.Select(field => field.Id == "newCharacterRulesetId"
                ? field with { Value = RulesetDefaults.Sr5 } : field).ToArray()
        });
        DesktopDialogField method = sr5.Fields.Single(field => field.Id == "newCharacterBuildMethod");
        Assert.AreEqual("Priority", method.Value);
        CollectionAssert.AreEqual(new[] { "Priority", "SumToTen", "Karma", "LifeModule" },
            method.Options!.Select(option => option.Value).ToArray());
    }

    [TestMethod]
    public void Canonical_sr6_sum_to_ten_retains_the_priority_preview_not_karma()
    {
        MethodInfo continuation = typeof(DesktopDialogFactory)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(info => info.Name == "BuildNewCharacterContinuationDialog" && info.GetParameters().Length == 5);
        DesktopDialogState dialog = (DesktopDialogState)continuation.Invoke(null,
            new object[] { RulesetDefaults.Sr6, "SumToTen", false, "Six", "VI" })!;
        Assert.AreEqual("dialog.new_character.priority_workflow", dialog.Id);
        Assert.AreEqual(Sr6CharacterCreationBuildMethods.SumToTen,
            DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterWorkflowBuildMethod"));
        StringAssert.Contains(DesktopDialogFieldValueParser.GetValue(dialog, "newCharacterPriorityWorkflowSummary")!,
            "Sum-to-Ten Total | 10");
    }

    [TestMethod]
    [DataRow("Priority")]
    [DataRow("SumtoTen")]
    [DataRow("PointBuy")]
    [DataRow("LifePath")]
    public async Task Unavailable_sr6_bootstrap_never_creates_an_sr5_runner(string method)
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with { ActiveDialog = Create(method) };
        var context = new DialogCoordinationContext(state, next => state = next,
            ImportAsync: (_, _) => throw new InvalidOperationException("No generic import fallback."),
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => state,
            CreateCharacterBootstrapAsync: (_, _) => throw new InvalidOperationException("No SR5 bootstrap fallback."),
            LoadWorkspaceAsync: (_, _) => throw new InvalidOperationException("No workspace was created."));
        await new DialogCoordinator().CoordinateAsync("create_character", context, CancellationToken.None);
        Assert.IsNull(state.WorkspaceId);
        Assert.AreEqual(method, DesktopDialogFieldValueParser.GetValue(state.ActiveDialog!, "newCharacterBuildMethod"));
        StringAssert.Contains(state.Error!, CharacterCreationBootstrapBlockers.RulesetSr5Required);
    }

    private static DesktopDialogState Create(string method)
        => new DesktopDialogFactory().CreateCommandDialog("new_character", null,
            DesktopPreferenceState.Default with { CharacterPriority = method }, null, null, RulesetDefaults.Sr6);

    private static DesktopDialogState Rebuild(DesktopDialogState dialog)
        => (DesktopDialogState)typeof(DesktopDialogFactory)
            .GetMethod("RebuildDynamicDialog", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { dialog, DesktopPreferenceState.Default })!;
}
