#nullable enable

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Desktop.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Fonts.Inter;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using Chummer.Tests.Presentation;

namespace Chummer.Tests;

/// <summary>
/// Regression tests for the R47 manual-creation scope (contacts add/remove +
/// lifestyles create/edit/delete). These are source-contract tests: they pin the
/// wiring between the unpinned session/control layers and the pinned
/// presenters (ChangeKind.Add/Remove, MutationKinds, idempotency preservation,
/// reload binding) without touching any oracle-pinned file.
/// </summary>
[TestClass]
public sealed class CharacterCreationManualWorkflowSourceTests
{
    [TestMethod]
    public void Contacts_add_flow_projects_template_and_builds_typed_add_input()
    {
        string root = ResolveRoot();
        string session = File.ReadAllText(Path.Combine(
            root, "Chummer.Presentation", "Overview", "CharacterCreationWizardDesktopSession.cs"));
        string control = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "Controls", "CharacterCreationWizardControl.axaml.cs"));

        // Session projects the core-owned add template into the desktop step.
        Assert.Contains("NewContactTemplate", session, StringComparison.Ordinal);
        Assert.Contains(
            "public CharacterCreationContactProjection? NewContactTemplate { get; init; }",
            session,
            StringComparison.Ordinal);

        // Control renders an add form over the template and emits a typed Add input.
        Assert.Contains("Guid.NewGuid()", control, StringComparison.Ordinal);
        Assert.Contains("ChangeKind.Add", control, StringComparison.Ordinal);
        Assert.Contains("BuildAddContactInput", control, StringComparison.Ordinal);
        Assert.Contains("ContactAddPreviewButton_OnClick", control, StringComparison.Ordinal);
        Assert.Contains("creation-wizard-contact-add-preview", control, StringComparison.Ordinal);
        // Add inputs must never reuse the template's empty id.
        Assert.DoesNotContain("ContactId = Guid.Empty,", control, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Contacts_remove_flow_is_delete_gated_and_typed()
    {
        string root = ResolveRoot();
        string session = File.ReadAllText(Path.Combine(
            root, "Chummer.Presentation", "Overview", "CharacterCreationWizardDesktopSession.cs"));
        string control = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "Controls", "CharacterCreationWizardControl.axaml.cs"));

        // Session projects the core CanDelete authority per contact (positional record parameter).
        Assert.Contains("bool CanDelete = false)", session, StringComparison.Ordinal);
        Assert.Contains("contact.CanDelete", session, StringComparison.Ordinal);

        // Control gates the remove button on CanDelete + wizard editability + busy state.
        Assert.Contains(
            "IsEnabled = contact.CanDelete && contacts.CanEdit && !_contactMutationBusy",
            control,
            StringComparison.Ordinal);
        Assert.Contains("ChangeKind.Remove", control, StringComparison.Ordinal);
        Assert.Contains("ContactRemoveButton_OnClick", control, StringComparison.Ordinal);
        Assert.Contains("-remove\"", control, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Stale_contact_authority_warning_is_filtered_only_when_add_is_available()
    {
        string root = ResolveRoot();
        string session = File.ReadAllText(Path.Combine(
            root, "Chummer.Presentation", "Overview", "CharacterCreationWizardDesktopSession.cs"));

        // The pinned projector emits ContactCreateDeleteAuthorityUnavailable
        // unconditionally; the session filters it from displayed warnings only
        // when the add path actually exists (template guard drives the filter).
        Assert.Contains("ContactCreateDeleteAuthorityUnavailable", session, StringComparison.Ordinal);
        Assert.Contains("NewContactTemplate is not null", session, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Lifestyles_flow_projects_step_and_emits_typed_mutation_inputs()
    {
        string root = ResolveRoot();
        string session = File.ReadAllText(Path.Combine(
            root, "Chummer.Presentation", "Overview", "CharacterCreationWizardDesktopSession.cs"));
        string control = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "Controls", "CharacterCreationWizardControl.axaml.cs"));
        string integration = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "MainWindow.CreationWizard.cs"));
        string app = File.ReadAllText(Path.Combine(root, "Chummer.Avalonia", "App.axaml.cs"));

        // Session: snapshot-bound lifestyle projection (positional Bind parameter).
        Assert.Contains("LifestylesMatchSnapshot", session, StringComparison.Ordinal);
        Assert.Contains("ProjectLifestyles", session, StringComparison.Ordinal);
        Assert.Contains("CharacterCreationWizardDesktopLifestylesStep", session, StringComparison.Ordinal);
        Assert.Contains("CharacterCreationLifestylesState? lifestyles = null", session, StringComparison.Ordinal);

        // Control: create form + per-card edit/delete via typed mutation inputs.
        Assert.Contains("RenderLifestyles", control, StringComparison.Ordinal);
        Assert.Contains("SetLifestylePrepareResult", control, StringComparison.Ordinal);
        Assert.Contains("LifestylePreviewRequested", control, StringComparison.Ordinal);
        Assert.Contains("LifestyleConfirmRequested", control, StringComparison.Ordinal);
        Assert.Contains("BuildConfigurationFromForm", control, StringComparison.Ordinal);
        Assert.Contains("creation-wizard-lifestyle-create-preview", control, StringComparison.Ordinal);
        Assert.Contains("LifestyleCreatePreviewButton_OnClick", control, StringComparison.Ordinal);

        // MainWindow: DI + prepare/confirm handlers with idempotency preservation.
        Assert.Contains("LifestylePreviewRequested", integration, StringComparison.Ordinal);
        Assert.Contains("LifestyleConfirmRequested", integration, StringComparison.Ordinal);
        Assert.Contains("ExplicitlyConfirmed: true", integration, StringComparison.Ordinal);
        Assert.Contains("previous.IdempotencyKey", integration, StringComparison.Ordinal);
        Assert.Contains("prepared.IdempotencyKey", integration, StringComparison.Ordinal);
        Assert.Contains("_creationLifestylesInteractionPresenter", integration, StringComparison.Ordinal);

        // App registers the lifestyles interaction presenter in DI.
        Assert.Contains("CharacterCreationLifestylesInteractionPresenter", app, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Lifestyle_mutation_inputs_match_pinned_presenter_contract()
    {
        string root = ResolveRoot();
        string control = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "Controls", "CharacterCreationWizardControl.axaml.cs"));
        string presenter = File.ReadAllText(Path.Combine(
            root, "Chummer.Presentation", "Overview", "CharacterCreationLifestylesInteractionPresenter.cs"));

        // Presenter contract: create/edit need a configuration, delete forbids one.
        Assert.Contains("record CharacterCreationLifestyleMutationInput(", presenter, StringComparison.Ordinal);
        Assert.Contains("bool ExplicitlyConfirmed", presenter, StringComparison.Ordinal);
        Assert.Contains(
            "CharacterCreationLifestyleMutationKinds.Delete => mutation.Configuration is null",
            presenter,
            StringComparison.Ordinal);

        // Control emits the mutation kinds via the core constants, not ad-hoc strings.
        Assert.Contains("CharacterCreationLifestyleMutationKinds.Create", control, StringComparison.Ordinal);
        Assert.Contains("CharacterCreationLifestyleMutationKinds.Edit", control, StringComparison.Ordinal);
        Assert.Contains("CharacterCreationLifestyleMutationKinds.Delete", control, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Wizard_integration_binds_lifestyles_snapshot_with_contacts()
    {
        string root = ResolveRoot();
        string integration = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "MainWindow.CreationWizard.cs"));

        // Both Bind sites must pass a lifestyles state: the fresh path uses
        // overview.CreationLifestyles, the recovery path _adapter.State.CreationLifestyles.
        Assert.IsGreaterThanOrEqualTo(
            0,
            integration.IndexOf("overview.CreationLifestyles", StringComparison.Ordinal),
            "fresh Bind site must pass the lifestyles state");
        Assert.IsGreaterThanOrEqualTo(
            0,
            integration.IndexOf("_adapter.State.CreationLifestyles", StringComparison.Ordinal),
            "recovery Bind site must pass the lifestyles state");
        Assert.IsGreaterThanOrEqualTo(
            0,
            integration.IndexOf("_creationLifestylesInteractionPresenter.Prepare(", StringComparison.Ordinal)
                + integration.IndexOf("_creationLifestylesInteractionPresenter.Confirm(", StringComparison.Ordinal)
                + 1,
            "lifestyles prepare and confirm seams must exist");
    }

    [TestMethod]
    public void Uncertain_save_recovery_is_receipt_lookup_not_a_second_mutation()
    {
        string root = ResolveRoot();
        string integration = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "MainWindow.CreationWizard.cs"));

        // Audit win008 finding 5: confirm dispatches the mutation exactly once,
        // then every recovery path is a read-only receipt lookup. The confirm
        // handler must hand a PENDING preview to the recovery routine before
        // and after dispatch, and the recovery routine must never call Confirm.
        Assert.Contains("_pendingCreationLifestyles[workspace] = prepared;", integration, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(
            0,
            integration.IndexOf("await RecoverAmbiguousCreationLifestyleResultAsync(pending, _creationWizardLifetime.Token);", StringComparison.Ordinal),
            "a re-click while a pending operation exists must recover, not re-dispatch");
        Assert.Contains(
            "lookup = _creationLifestylesInteractionPresenter.LookupReceipt(_adapter.State, prepared.IdempotencyKey);",
            integration,
            StringComparison.Ordinal);
        // The recovery routine itself (from its method definition to EOF) must
        // never confirm again — it is a read-only receipt lookup only.
        int recoveryDef = integration.IndexOf(
            "private async Task RecoverAmbiguousCreationLifestyleResultAsync(",
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, recoveryDef, "recovery routine must exist");
        Assert.DoesNotContain(
            "_creationLifestylesInteractionPresenter.Confirm(",
            integration.Substring(recoveryDef),
            StringComparison.Ordinal);
        // The single first-dispatch Confirm call is preceded by the pending
        // registration, so a lost response can be recovered by lookup.
        int confirmSite = integration.IndexOf(
            "_creationLifestylesInteractionPresenter.Confirm(", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, confirmSite, "first dispatch must exist");
        int pendingSite = integration.IndexOf(
            "_pendingCreationLifestyles[workspace] = prepared;", StringComparison.Ordinal);
        Assert.IsGreaterThan(pendingSite, confirmSite,
            "the operation must be pending BEFORE the mutation is dispatched");
        // The receipt must be bound to the exact reviewed operation.
        Assert.Contains("receipt.WritePlan.PlanDigest != prepared.WritePlan.PlanDigest", integration, StringComparison.Ordinal);
        Assert.Contains("receipt.PreviousWorkspaceRevision != prepared.Binding.WorkspaceRevision", integration, StringComparison.Ordinal);
        Assert.Contains("receipt.MutationKind != prepared.Mutation.MutationKind", integration, StringComparison.Ordinal);
        // A verified receipt clears the pending state and reloads the character.
        Assert.Contains("_pendingCreationLifestyles.Remove(prepared.Binding.WorkspaceId.Value);", integration, StringComparison.Ordinal);
        // A definite rejection re-opens review; an uncertain write stays pending.
        Assert.Contains("_pendingCreationLifestyles.Remove(workspace);", integration, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Rook_context_receives_the_lifestyle_projection()
    {
        string root = ResolveRoot();
        string session = File.ReadAllText(Path.Combine(
            root, "Chummer.Presentation", "Overview", "CharacterCreationWizardDesktopSession.cs"));

        // Audit win008 finding 4: the serialized Build Ghost context must carry
        // the same lifestyle projection the player sees, revision-bound.
        Assert.Contains("LifestylesStep: lifestylesStep", session, StringComparison.Ordinal);
        Assert.Contains(
            "CharacterCreationWizardDesktopLifestylesStep? LifestylesStep = null",
            session,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void Draft_and_review_lifecycle_wiring_is_present()
    {
        string root = ResolveRoot();
        string control = File.ReadAllText(Path.Combine(
            root, "Chummer.Avalonia", "Controls", "CharacterCreationWizardControl.axaml.cs"));

        // Audit win008 findings 2+3: drafts survive previews (valid, blocked or
        // failed) and confirmation is bound to the visible reviewed values.
        Assert.Contains("_contactAddDraft", control, StringComparison.Ordinal);
        Assert.Contains("_contactAddDraftIdentity", control, StringComparison.Ordinal);
        Assert.Contains("_contactEditDrafts", control, StringComparison.Ordinal);
        Assert.Contains("_lifestyleCreateDraft", control, StringComparison.Ordinal);
        Assert.Contains("_lifestyleCreateDraftIdentity", control, StringComparison.Ordinal);
        Assert.Contains("_lifestyleEditDrafts", control, StringComparison.Ordinal);
        Assert.Contains("_contactPreviewBaseline", control, StringComparison.Ordinal);
        Assert.Contains("_lifestylePreviewBaseline", control, StringComparison.Ordinal);
        Assert.Contains("CaptureContactEditorValues", control, StringComparison.Ordinal);
        Assert.Contains("ContactPreviewMatchesVisibleEditors", control, StringComparison.Ordinal);
        Assert.Contains("LifestylePreviewMatchesVisibleEditors", control, StringComparison.Ordinal);
        Assert.Contains(
            "Values changed since this preview",
            control,
            StringComparison.Ordinal);
        // The minted create identity must be reused, not re-minted per preview.
        Assert.Contains("repeated previews describe the same new lifestyle", control, StringComparison.Ordinal);
    }

    private static string ResolveRoot()
        => DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
}

// Exercise the production controls, including the values sent to Core. These
// headless interactions supplement wiring checks; they are not native Windows evidence.
[TestClass]
public sealed class CharacterCreationManualWorkflowAvaloniaTests
{
    [TestMethod]
    public void Lifestyle_edit_sends_changed_values_and_preserves_unoffered_settings()
        => WithUi((control, _) =>
        {
            var original = Configuration();
            control.SetState(State(original));
            string prefix = $"creation-wizard-lifestyle-{original.LifestyleId:D}";
            Find<TextBox>(control, prefix + "-name").Text = "New home";
            Find<NumericUpDown>(control, prefix + "-increments").Value = 3;
            Find<NumericUpDown>(control, prefix + "-percentage").Value = 62.5m;
            Find<NumericUpDown>(control, prefix + "-roommates").Value = 2;
            Find<CheckBox>(control, prefix + "-split").IsChecked = true;
            CharacterCreationLifestyleMutationInput? input = null;
            int confirmations = 0;
            control.LifestylePreviewRequested += (_, request) => input = request.Input;
            control.LifestyleConfirmRequested += (_, _) => confirmations++;
            Click(Find<Button>(control, prefix + "-preview"));
            Assert.IsNotNull(input);
            Assert.AreEqual(CharacterCreationLifestyleMutationKinds.Edit, input.MutationKind);
            Assert.AreEqual(original with { Name = "New home", Increments = 3, Percentage = 62.5m,
                Roommates = 2, SplitCostWithRoommates = true }, input.Configuration);
            Assert.AreEqual(0, confirmations);
        });

    [TestMethod]
    public void Lifestyle_create_preserves_fractional_percentage_and_uses_new_identity()
        => WithUi((control, _) =>
        {
            control.SetState(State(Configuration()));
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Studio";
            Find<NumericUpDown>(control, "creation-wizard-lifestyle-create-percentage").Value = 37.5m;
            CharacterCreationLifestyleMutationInput? input = null;
            control.LifestylePreviewRequested += (_, request) => input = request.Input;
            Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
            Assert.IsNotNull(input);
            Assert.AreEqual(CharacterCreationLifestyleMutationKinds.Create, input.MutationKind);
            Assert.AreNotEqual(Guid.Empty, input.LifestyleId);
            Assert.AreEqual(input.LifestyleId, input.Configuration!.LifestyleId);
            Assert.AreEqual(37.5m, input.Configuration.Percentage);
            Assert.AreEqual("Studio", input.Configuration.Name);
        });

    [TestMethod]
    public void Delete_is_reviewed_before_confirmation_and_names_the_removed_lifestyle()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            control.SetState(State(config));
            CharacterCreationLifestyleMutationInput? input = null;
            int confirms = 0;
            control.LifestylePreviewRequested += (_, request) => input = request.Input;
            control.LifestyleConfirmRequested += (_, _) => confirms++;
            Click(Find<Button>(control, $"creation-wizard-lifestyle-{config.LifestyleId:D}-delete"));
            Assert.IsNotNull(input);
            Assert.AreEqual(CharacterCreationLifestyleMutationKinds.Delete, input.MutationKind);
            Assert.IsNull(input.Configuration);
            Assert.AreEqual(0, confirms);
            control.SetLifestylePrepareResult(new("available", null, Preview(config), []));
            string text = string.Join("\n", control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            StringAssert.Contains(text, "Before: Old home");
            StringAssert.Contains(text, "Budget remaining: 900 → 1000");
            Click(Find<Button>(control, "creation-wizard-lifestyle-confirm"));
            Assert.AreEqual(1, confirms);
        });

    [TestMethod]
    public void Busy_and_unavailable_authority_reject_even_previously_captured_buttons()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            var edit = Find<Button>(control, $"creation-wizard-lifestyle-{config.LifestyleId:D}-preview");
            var delete = Find<Button>(control, $"creation-wizard-lifestyle-{config.LifestyleId:D}-delete");
            var create = Find<Button>(control, "creation-wizard-lifestyle-create-preview");
            int previews = 0;
            control.LifestylePreviewRequested += (_, _) => previews++;
            control.SetLifestyleMutationBusy(true);
            Click(edit); Click(delete); Click(create);
            Assert.AreEqual(0, previews);
            control.SetLifestyleMutationBusy(false);
            control.SetState(state with { LifestylesStep = state.LifestylesStep! with { CanEdit = false } });
            Click(edit); Click(delete); Click(create);
            Assert.AreEqual(0, previews);
        });

    [TestMethod]
    public void Missing_authority_and_context_reset_remove_old_confirmation()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            control.SetLifestylePrepareResult(new("available", null, Preview(config), []));
            var oldConfirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            int confirms = 0;
            control.LifestyleConfirmRequested += (_, _) => confirms++;
            control.SetState(state with { LifestylesStep = null });
            Click(oldConfirm);
            Assert.AreEqual(0, confirms);
            control.SetState(state);
            control.SetLifestylePrepareResult(new("available", null, Preview(config), []));
            oldConfirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            control.ClearContactLifestylePreviews();
            Click(oldConfirm);
            Assert.AreEqual(0, confirms);
        });

    [TestMethod]
    public void Lifestyle_edit_draft_survives_blocked_preview_result()
        => WithUi((control, _) =>
        {
            var original = Configuration();
            control.SetState(State(original));
            string prefix = $"creation-wizard-lifestyle-{original.LifestyleId:D}";

            // Enter non-default values and request a preview.
            Find<TextBox>(control, prefix + "-name").Text = "New home";
            Find<NumericUpDown>(control, prefix + "-increments").Value = 3;
            Click(Find<Button>(control, prefix + "-preview"));

            // A BLOCKED preview rebuilds the surface; the typed values must
            // still be in the controls (draft preservation), not the saved ones.
            control.SetLifestylePrepareResult(new(
                CharacterCreationLifestyleOutcomes.Blocked, null, null,
                ["This Lifestyle exceeds your budget."]));
            Assert.AreEqual("New home", Find<TextBox>(control, prefix + "-name").Text);
            Assert.AreEqual(3, ReadNumeric(control, prefix + "-increments"));

            // Re-preview without re-entering: the emitted input still carries
            // the typed values.
            CharacterCreationLifestyleMutationInput? blockedInput = null;
            control.LifestylePreviewRequested += (_, request) => blockedInput = request.Input;
            Click(Find<Button>(control, prefix + "-preview"));
            Assert.IsNotNull(blockedInput);
            Assert.AreEqual("New home", blockedInput.Configuration!.Name);
            Assert.AreEqual(3, blockedInput.Configuration.Increments);
        });

    [TestMethod]
    public void Lifestyle_create_preview_reuses_the_minted_identity()
        => WithUi((control, _) =>
        {
            control.SetState(State(Configuration()));
            Guid? firstId = null;
            control.LifestylePreviewRequested += (_, request) => firstId = request.Input.LifestyleId;
            Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
            Assert.IsNotNull(firstId);
            // A preview result rebuilds the surface; a second preview must keep
            // the identity minted for this create cycle, not mint a new one.
            control.SetLifestylePrepareResult(new("available", null, EditPreview(Configuration()), []));
            Guid? secondId = null;
            control.LifestylePreviewRequested += (_, request) => secondId = request.Input.LifestyleId;
            Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
            Assert.IsNotNull(secondId);
            Assert.AreEqual(firstId, secondId);
        });

    [TestMethod]
    public void Lifestyle_edit_after_preview_disables_confirmation_until_new_preview()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            control.SetState(State(config));
            string prefix = $"creation-wizard-lifestyle-{config.LifestyleId:D}";

            // Real flow: edit, preview (captures the baseline), result arrives.
            Find<TextBox>(control, prefix + "-name").Text = "Reviewed name";
            Click(Find<Button>(control, prefix + "-preview"));
            control.SetLifestylePrepareResult(new(
                "available", null, EditPreview(config with { Name = "Reviewed name" }), []));
            Button confirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            Assert.IsTrue(confirm.IsEnabled);

            // Changing the visible field retires the reviewed proposal:
            // confirmation is unavailable until the new proposal is reviewed.
            Find<TextBox>(control, prefix + "-name").Text = "Changed after preview";
            control.SetLifestyleMutationBusy(false);
            confirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            Assert.IsFalse(confirm.IsEnabled);
            string text = string.Join("\n",
                control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            StringAssert.Contains(text, "Values changed since this preview");

            // A fresh preview of the changed values restores confirmation.
            Click(Find<Button>(control, prefix + "-preview"));
            control.SetLifestylePrepareResult(new(
                "available", null, EditPreview(config with { Name = "Changed after preview" }), []));
            confirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            Assert.IsTrue(confirm.IsEnabled);
        });

    [TestMethod]
    public void Uncertain_save_has_visible_read_only_recovery_feedback()
        => WithUi((control, _) =>
        {
            control.SetState(State(Configuration()));
            int mutations = 0;
            control.LifestyleConfirmRequested += (_, _) => mutations++;
            control.SetLifestyleReceiptLookupResult(new("not-found", null, null, []));
            string text = string.Join("\n", control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
            StringAssert.Contains(text, "outcome is still unknown");
            StringAssert.Contains(text, "no new change will be sent");
            Assert.AreEqual(0, mutations);
        });

    private static CharacterCreationLifestyleConfiguration Configuration()
        => new(Guid.Parse("5ee27326-0912-4dad-b65b-9ad4c01a8cbe"), "low", "Old home", "advanced", "week",
            2, 75.25m, 1, false, true, 2, 3, 4, 5, "Seattle", "Redmond", "Barrens", []);

    private static CharacterCreationLifestyleBinding Binding()
        => new(new("manual-workspace"), 1, 1, 1, "content", "aux", "source", "rules", "runtime");

    private static CharacterCreationLifestyleProjection Projection(CharacterCreationLifestyleConfiguration config)
        => new(config, Guid.Empty, "Low", "SR5", "10", new(50, 100, 5, 2, true, false, []), [], "lifestyle");

    private static CharacterCreationLifestylePreparedPreview EditPreview(
        CharacterCreationLifestyleConfiguration config)
        => new("lifestyles", Binding(), new("edit", config.LifestyleId, config), [Projection(config)], Projection(config), null,
            new(1000, 100, 900, 0, true, [], []), new(1000, 0, 1000, 0, true, [], []), null!, [], true, true, "operation", "preview");

    private static int ReadNumeric(Control control, string id)
        => (int)Find<NumericUpDown>(control, id).Value!.Value;

    private static CharacterCreationLifestylePreparedPreview Preview(CharacterCreationLifestyleConfiguration config)
        => new("lifestyles", Binding(), new("delete", config.LifestyleId, null), [Projection(config)], Projection(config), null,
            new(1000, 100, 900, 0, true, [], []), new(1000, 0, 1000, 0, true, [], []), null!, [], true, true, "operation", "preview");

    private static CharacterCreationWizardDesktopState State(CharacterCreationLifestyleConfiguration config)
    {
        var step = CharacterCreationWizardStepIds.ContactsLifestyles;
        var option = new CharacterCreationLifestyleCatalogOption("low", Guid.Empty, "Low", 50, 1, 100, 5,
            1, 1, 1, 0, 10, 0, 10, 0, 10, true, "month", "SR5", "10", [], true, true, [], [], "option");
        var authority = new CharacterCreationLifestylesAuthority("schema", "sr5", "settings", [option], [],
            1, false, [], [], true, "source", "profile", "policy", "runtime", "authority");
        var projection = Projection(config);
        var lifestyle = new CharacterCreationWizardDesktopLifestyle(config, Guid.Empty, "Low", "SR5", "10", projection.Economics, [], "lifestyle");
        return new("manual-workspace", 1, "snapshot", step,
            [new(step, step, "available", true, true, false, true, [], [], [], [])], [], [], [], [], false, false, false, false,
            new(false, null), new(CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
                "manual-workspace", 1, "snapshot", step, "sr5", "runtime", "Priority", [], [], [], []),
            LifestylesStep: new(Binding(), authority, [lifestyle], new(1000, 100, 900, 0, true, [], []), [], true, "lifestyles"));
    }

    private static T Find<T>(Control control, string id) where T : Control
        => control.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == id);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void WithUi(Action<CharacterCreationWizardControl, Window> action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Bootstrap));
            session.Dispatch(() =>
            {
                var control = new CharacterCreationWizardControl();
                var host = new Window { Content = control };
                host.Show();
                try { action(control, host); } finally { host.Close(); }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
    private sealed class Bootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>().UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .ConfigureFonts(static manager => manager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter",
                    FontFallbacks = [new FontFallback { FontFamily = new FontFamily("fonts:Inter#Inter") }],
                    FontFamilyMappings = new Dictionary<string, FontFamily> { ["Segoe UI"] = new("fonts:Inter#Inter"), ["Verdana"] = new("fonts:Inter#Inter"), ["Arial"] = new("fonts:Inter#Inter") }
                }).WithInterFont();
    }
}
