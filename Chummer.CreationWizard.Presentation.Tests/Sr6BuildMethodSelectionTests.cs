using System.Reflection;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
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
    [DataRow("Karma")]
    public void Selection_and_rebuild_preserve_exact_sr6_identity(string method)
    {
        DesktopDialogState dialog = Create(method);
        DesktopDialogField field = dialog.Fields.Single(item => item.Id == "newCharacterBuildMethod");
        string[] values = field.Options!.Select(option => option.Value).ToArray();
        CollectionAssert.AreEqual(Sr6CharacterCreationBuildMethods.All.ToArray(),
            values);
        Assert.AreEqual(method, field.Value);
        Assert.AreEqual(method, DesktopDialogFieldValueParser.GetValue(Rebuild(dialog), "newCharacterBuildMethod"));
        CollectionAssert.Contains(values, Sr6CharacterCreationBuildMethods.Karma);
        CollectionAssert.DoesNotContain(values, "LifeModule");
        StringAssert.Contains(dialog.Message!, "SR6 draft");
        StringAssert.Contains(dialog.Message!, "remaining SR6 wizard steps are not available");
    }

    [TestMethod]
    [DataRow("PointBuy")]
    [DataRow("LifePath")]
    [DataRow("Karma")]
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
    [DataRow("Karma")]
    public async Task Unavailable_sr6_bootstrap_keeps_selection_and_never_falls_back_to_sr5(string method)
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with { ActiveDialog = Create(method) };
        var context = new DialogCoordinationContext(state, next => state = next,
            ImportAsync: (_, _) => throw new InvalidOperationException("No generic import fallback."),
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => state,
            CreateCharacterBootstrapAsync: (request, _) =>
            {
                Assert.AreEqual(RulesetDefaults.Sr6, request.RulesetId);
                Assert.AreEqual(method, request.BuildMethod);
                Assert.IsTrue(Sr6CharacterCreationBootstrapProfiles.IsExactCanonicalTuple(method, request.SettingsProfileId));
                Assert.IsFalse(CharacterCreationBootstrapProfiles.IsExactCanonicalTuple(method, request.SettingsProfileId));
                return Task.FromResult(new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable, null, ["test-sr6-provider-unavailable"]));
            },
            LoadWorkspaceAsync: (_, _) => throw new InvalidOperationException("No workspace was created."));
        await new DialogCoordinator().CoordinateAsync("create_character", context, CancellationToken.None);
        Assert.IsNull(state.WorkspaceId);
        Assert.AreEqual(method, DesktopDialogFieldValueParser.GetValue(state.ActiveDialog!, "newCharacterBuildMethod"));
        StringAssert.Contains(state.Error!, "test-sr6-provider-unavailable");
    }

    [TestMethod]
    [DataRow("Priority", false)]
    [DataRow("SumtoTen", false)]
    [DataRow("PointBuy", false)]
    [DataRow("LifePath", false)]
    [DataRow("Karma", false)]
    [DataRow("Priority", true)]
    public async Task Valid_sr6_receipt_opens_the_selected_draft_without_claiming_completed_wizards(string method, bool activationPath)
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with { ActiveDialog = Create(method) };
        int creates = 0;
        int loads = 0;
        var workspaceId = new CharacterWorkspaceId("sr6-selected-draft");
        CharacterCreationBootstrapReceipt Produce(CharacterCreationBootstrapRequest request)
        {
            creates++;
            Assert.AreEqual(RulesetDefaults.Sr6, request.RulesetId);
            Assert.AreEqual(method, request.BuildMethod);
            return Receipt(request, workspaceId);
        }
        var context = new DialogCoordinationContext(state, next => state = next,
            ImportAsync: (_, _) => throw new InvalidOperationException("No generic import."),
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => state,
            CreateCharacterBootstrapAsync: (request, _) => Task.FromResult(
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Success, Produce(request), [])),
            LoadWorkspaceAsync: (id, _) =>
            {
                loads++;
                Assert.AreEqual(workspaceId, id);
                state = state with { WorkspaceId = id };
                return Task.CompletedTask;
            },
            CreateCharacterBootstrapActivationAsync: activationPath
                ? (request, _) => Task.FromResult(new CharacterCreationBootstrapActivationAttempt(
                    CharacterCreationBootstrapOutcomes.Success, Produce(request), null,
                    [CharacterCreationBootstrapBlockers.ActivationProjectionUnavailable]))
                : null,
            ActivateCharacterBootstrapAsync: activationPath
                ? (_, _) => throw new InvalidOperationException("SR6 must use its receipt, not an SR5 activation bundle.")
                : null);
        await new DialogCoordinator().CoordinateAsync("create_character", context, CancellationToken.None);
        Assert.AreEqual(1, creates);
        Assert.AreEqual(1, loads);
        Assert.AreEqual(workspaceId, state.WorkspaceId);
        Assert.IsNull(state.ActiveDialog);
        Assert.IsNull(state.Error);
        StringAssert.Contains(state.Notice!, method + " · SR6 draft");
        StringAssert.Contains(state.Notice!, "remaining SR6 wizard steps are not available");
    }

    [TestMethod]
    public async Task A_valid_sr5_receipt_cannot_satisfy_sr6_creation()
    {
        CharacterOverviewState state = CharacterOverviewState.Empty with { ActiveDialog = Create("Priority") };
        var context = new DialogCoordinationContext(state, next => state = next,
            ImportAsync: (_, _) => throw new InvalidOperationException("No generic import."),
            UpdateMetadataAsync: static (_, _) => Task.CompletedTask,
            GetState: () => state,
            CreateCharacterBootstrapAsync: (request, _) => Task.FromResult(
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(CharacterCreationBootstrapOutcomes.Success,
                    Receipt(request with { RulesetId = RulesetDefaults.Sr5,
                        SettingsProfileId = CharacterCreationBootstrapProfiles.PrioritySettingsProfileId }, new("foreign-sr5")), [])),
            LoadWorkspaceAsync: (_, _) => throw new InvalidOperationException("Foreign receipt must not open."));
        await new DialogCoordinator().CoordinateAsync("create_character", context, CancellationToken.None);
        Assert.IsNull(state.WorkspaceId);
        Assert.IsNotNull(state.ActiveDialog);
        StringAssert.Contains(state.Error!, CharacterCreationBootstrapBlockers.WorkspaceCreateFailed);
    }

    private static CharacterCreationBootstrapReceipt Receipt(CharacterCreationBootstrapRequest request, CharacterWorkspaceId id)
    {
        string digest = "sha256:" + new string('1', 64);
        var anchors = CharacterCreationBootstrapProfiles.ExpectedSourceAnchorIds(
            request.RulesetId, request.BuildMethod, request.SettingsProfileId);
        var binding = new CharacterCreationBootstrapBinding(CharacterCreationBootstrapSchemas.BindingV1,
            request.Stage, id, request.RulesetId, request.BuildMethod, request.SettingsProfileId, 1, 0,
            digest, digest, digest,
            request.BuildMethod is "Priority" or "SumtoTen" ? digest : string.Empty,
            CharacterCreationBootstrapProfiles.SettingsSourceAnchor(request.RulesetId, request.SettingsProfileId), anchors, string.Empty);
        binding = binding with { BindingDigest = CharacterCreationBootstrapBindingDigest.Compute(binding) };
        var receipt = new CharacterCreationBootstrapReceipt(CharacterCreationBootstrapSchemas.ReceiptV1,
            id, 1, 0, new(request.Name.Trim(), request.Alias.Trim(), string.Empty, request.BuildMethod,
                "test", "test", 0, 0, false), binding, anchors, string.Empty);
        receipt = receipt with { ReceiptDigest = CharacterCreationBootstrapReceiptDigest.Compute(receipt) };
        Assert.IsTrue(CharacterCreationBootstrapReceiptDigest.IsValid(receipt));
        return receipt;
    }

    private static DesktopDialogState Create(string method)
        => new DesktopDialogFactory().CreateCommandDialog("new_character", null,
            DesktopPreferenceState.Default with { CharacterPriority = method }, null, null, RulesetDefaults.Sr6);

    private static DesktopDialogState Rebuild(DesktopDialogState dialog)
        => (DesktopDialogState)typeof(DesktopDialogFactory)
            .GetMethod("RebuildDynamicDialog", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { dialog, DesktopPreferenceState.Default })!;
}
