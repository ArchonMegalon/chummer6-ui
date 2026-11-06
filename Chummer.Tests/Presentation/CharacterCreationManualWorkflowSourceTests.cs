#nullable enable

using System;
using System.IO;
using System.Net.Http;
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
using System.Threading.Tasks;
using Avalonia.Threading;
using Chummer.Contracts.AI;
using Chummer.Contracts.Workspaces;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Rulesets.Hosting;
using Chummer.Rulesets.Sr5;
using Chummer.Presentation.Shell;
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

    [TestMethod]
    public void Lifestyle_create_keeps_selected_base_and_all_reviewed_fields()
        => WithUi((control, _) =>
        {
            var state = State(Configuration());
            var low = state.LifestylesStep!.Authority.LifestyleOptions.Single();
            state = state with { LifestylesStep = state.LifestylesStep with {
                Authority = state.LifestylesStep.Authority with {
                    LifestyleOptions = [low, low with { OptionId = "high", Name = "High" }] } } };
            control.SetState(state);
            var basePicker = Find<ComboBox>(control, "creation-wizard-lifestyle-create-base");
            basePicker.SelectedItem = basePicker.Items.OfType<ComboBoxItem>().Single(item =>
                item.Tag is CharacterCreationLifestyleCatalogOption { OptionId: "high" });
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Penthouse";
            CharacterCreationLifestyleMutationInput? input = null;
            control.LifestylePreviewRequested += (_, request) => input = request.Input;
            Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
            Assert.IsNotNull(input);
            var prepared = EditPreview(input.Configuration!) with { Mutation = new(input.MutationKind, input.LifestyleId, input.Configuration) };
            control.SetLifestyleMutationBusy(true);
            control.SetLifestylePrepareResult(new("available", null, prepared, []));
            control.SetLifestyleMutationBusy(false);
            Assert.AreEqual("high", ((CharacterCreationLifestyleCatalogOption)((ComboBoxItem)
                Find<ComboBox>(control, "creation-wizard-lifestyle-create-base").SelectedItem!).Tag!).OptionId);
            Assert.IsTrue(Find<Button>(control, "creation-wizard-lifestyle-confirm").IsEnabled);
            int confirms = 0;
            control.LifestyleConfirmRequested += (_, _) => confirms++;
            foreach (string field in new[] { "name", "base", "style", "increment" })
            {
                if (field == "name")
                    Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Changed";
                else
                {
                    var picker = Find<ComboBox>(control, "creation-wizard-lifestyle-create-" + field);
                    picker.SelectedIndex = (picker.SelectedIndex + 1) % picker.Items.Count;
                }
                // A routed click must also be rejected, even before a UI repaint.
                Click(Find<Button>(control, "creation-wizard-lifestyle-confirm"));
                Assert.AreEqual(0, confirms, field);
                control.SetLifestyleMutationBusy(false);
                Assert.IsFalse(Find<Button>(control, "creation-wizard-lifestyle-confirm").IsEnabled, field);
                Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
                prepared = EditPreview(input!.Configuration!) with { Mutation = new(input.MutationKind, input.LifestyleId, input.Configuration) };
                control.SetLifestylePrepareResult(new("available", null, prepared, []));
                Assert.IsTrue(Find<Button>(control, "creation-wizard-lifestyle-confirm").IsEnabled, field);
            }
        });

    [TestMethod]
    public void Lifestyle_drafts_do_not_cross_snapshot_workspace_or_owner_reset()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string name = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, name).Text = "Private old draft";
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Private new home";
            control.SetState(state with { WorkspaceId = "other-workspace" });
            Assert.AreEqual(config.Name, Find<TextBox>(control, name).Text);
            Assert.AreEqual(string.Empty, Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text);
            Find<TextBox>(control, name).Text = "Another draft";
            control.ClearContactLifestylePreviews();
            Assert.IsFalse(control.GetVisualDescendants().OfType<TextBox>().Any(editor =>
                AutomationProperties.GetAutomationId(editor) == name));
            control.SetState(state with { WorkspaceId = "other-workspace" });
            Assert.AreEqual(config.Name, Find<TextBox>(control, name).Text);
            Find<TextBox>(control, name).Text = "Old revision";
            control.SetState(state with { LifestylesStep = state.LifestylesStep! with { SnapshotDigest = "new-revision" } });
            Assert.AreEqual(config.Name, Find<TextBox>(control, name).Text);
        });

    [TestMethod]
    public void Busy_render_preserves_unsubmitted_lifestyle_typing()
        => WithUi((control, _) =>
        {
            control.SetState(State(Configuration()));
            string name = "creation-wizard-lifestyle-create-name";
            Find<TextBox>(control, name).Text = "Not previewed yet";
            control.SetLifestyleMutationBusy(true);
            control.SetLifestyleMutationBusy(false);
            Assert.AreEqual("Not previewed yet", Find<TextBox>(control, name).Text);
        });


    [TestMethod]
    public void Lifestyle_draft_survives_step_navigation_within_the_same_character()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string name = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, name).Text = "Unsaved navigation draft";

            // Navigate away: the step projection becomes null (digest changes),
            // which must retire previews but NOT discard the unsaved input.
            // Away from the step: no editor is rendered, but the draft must be
            // retained in the control (it is not cleared by navigation).
            control.SetState(state with { LifestylesStep = null });

            // Returning to the step restores the editor with the draft values.
            control.SetState(state);
            Assert.AreEqual("Unsaved navigation draft",
                Find<TextBox>(control, name).Text);
        });

    [TestMethod]
    public void Saving_one_lifestyle_keeps_the_other_entrys_unsaved_input()
        => WithUi((control, _) =>
        {
            var first = Configuration();
            var second = first with { LifestyleId = Guid.Parse("a1111111-1111-1111-1111-111111111111") };
            var state = State(first) with { LifestylesStep = State(first).LifestylesStep! with {
                Lifestyles = [DesktopLifestyle(first), DesktopLifestyle(second)] } };
            control.SetState(state);
            string firstId = $"creation-wizard-lifestyle-{first.LifestyleId:D}-name";
            string secondId = $"creation-wizard-lifestyle-{second.LifestyleId:D}-name";
            Find<TextBox>(control, firstId).Text = "First unsaved";
            Find<TextBox>(control, secondId).Text = "Second unsaved";

            // Preview and confirm the FIRST entry; the second keeps its input.
            Click(Find<Button>(control, $"creation-wizard-lifestyle-{first.LifestyleId:D}-preview"));
            var prepared = EditPreview(first with { Name = "First unsaved" });
            control.SetLifestylePrepareResult(new("available", null, prepared, []));
            int confirms = 0;
            control.LifestyleConfirmRequested += (_, _) => confirms++;
            Click(Find<Button>(control, "creation-wizard-lifestyle-confirm"));
            Assert.AreEqual(1, confirms);
            control.SetLifestyleConfirmResult(new(
                CharacterCreationLifestyleOutcomes.Applied,
                prepared,
                Receipt(first.LifestyleId),
                null, []));

            Assert.AreEqual("Second unsaved", Find<TextBox>(control, secondId).Text);
        });

    [TestMethod]
    public void Saving_a_contact_keeps_unsaved_lifestyle_input()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string lifestyleName = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, lifestyleName).Text = "Lifestyle unsaved";
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Lifestyle new unsaved";

            // A contact save advances the character revision: the refresh may
            // retire the contact preview, but the lifestyle input must survive
            // the state refresh with the same character identity.
            control.SetState(state with {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2",
                LifestylesStep = state.LifestylesStep! with { SnapshotDigest = "lifestyles-2" } });
            Assert.AreEqual("Lifestyle unsaved", Find<TextBox>(control, lifestyleName).Text);
            Assert.AreEqual("Lifestyle new unsaved",
                Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text);
        });

    [TestMethod]
    public void Drafts_do_not_leak_into_another_owner_or_character_context()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string name = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, name).Text = "Owner A draft";
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Owner A new";

            // A different workspace identity is a different character context:
            // drafts must not transplant into it.
            control.SetState(state with { WorkspaceId = "other-character" });
            Assert.AreEqual(config.Name, Find<TextBox>(control, name).Text);
            Assert.AreEqual(string.Empty, Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text);
        });

    [TestMethod]
    public void Untouched_lifestyle_uses_refreshed_authoritative_values()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            control.SetState(State(config));
            control.SetLifestyleMutationBusy(true);
            control.SetLifestyleMutationBusy(false);
            control.SetState(State(config with { Name = "New saved name" }) with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "new-saved-snapshot"
            });
            Assert.AreEqual("New saved name",
                Find<TextBox>(control, $"creation-wizard-lifestyle-{config.LifestyleId:D}-name").Text);
        });

    [TestMethod]
    public void Owner_reset_uses_the_next_authoritative_values_for_the_same_workspace()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            control.SetState(State(config));
            string name = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, name).Text = "Private previous-owner draft";
            control.ClearContactLifestylePreviews();
            var next = State(config with { Name = "Current owner home" }) with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "current-owner-snapshot"
            };
            control.SetState(next);
            Assert.AreEqual("Current owner home", Find<TextBox>(control, name).Text);
        });

    [TestMethod]
    public void Retained_draft_after_revision_change_requires_a_fresh_preview()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string name = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            string previewButtonId = $"creation-wizard-lifestyle-{config.LifestyleId:D}-preview";
            Find<TextBox>(control, name).Text = "Reviewed draft";
            Click(Find<Button>(control, previewButtonId));
            control.SetLifestylePrepareResult(new(
                "available", null, EditPreview(config with { Name = "Reviewed draft" }), []));
            Button confirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            Assert.IsTrue(confirm.IsEnabled);

            // The character revision advances (another save elsewhere). The old
            // confirmation must be unusable even though the draft is retained.
            control.SetState(state with {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2",
                LifestylesStep = state.LifestylesStep! with { SnapshotDigest = "lifestyles-2" } });
            control.SetLifestyleMutationBusy(false);
            // The retired preview means no confirmation is offered at all: the
            // old review cannot be applied against the advanced revision.
            Assert.IsFalse(FindOptional<Button>(control, "creation-wizard-lifestyle-confirm") is { });

            // A fresh Core preview re-enables confirmation for the retained input.
            Click(Find<Button>(control, previewButtonId));
            control.SetLifestylePrepareResult(new(
                "available", null, EditPreview(config with { Name = "Reviewed draft" }), []));
            confirm = Find<Button>(control, "creation-wizard-lifestyle-confirm");
            Assert.IsTrue(confirm.IsEnabled);
        });

    [TestMethod]
    public void Partly_edited_lifestyle_keeps_local_edit_and_accepts_refreshed_values()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string nameId = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            string percentageId = $"creation-wizard-lifestyle-{config.LifestyleId:D}-percentage";

            // The player changes ONLY the name.
            Find<TextBox>(control, nameId).Text = "My unsaved rename";
            // The saved projection of the same lifestyle advances: percentage
            // changed from 75.25 to 50 elsewhere, name stayed "Old home". The
            // refreshed state carries its OWN lifestyle step (new saved values).
            control.SetState(State(config with { Percentage = 50m }) with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2"
            });
            // Field-level merge: local edit retained, untouched field refreshed.
            Assert.AreEqual("My unsaved rename", Find<TextBox>(control, nameId).Text);
            Assert.AreEqual(50m, Find<NumericUpDown>(control, percentageId).Value);
        });

    [TestMethod]
    public void Refreshed_lifestyle_conflicting_with_local_edit_is_reported_and_keeps_the_edit()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);
            string nameId = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, nameId).Text = "My unsaved rename";
            // Same field changed differently elsewhere: keep the local edit,
            // surface the conflict instead of silently choosing a side.
            control.SetState(State(config with { Name = "Saved elsewhere" }) with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2"
            });
            Assert.AreEqual("My unsaved rename", Find<TextBox>(control, nameId).Text);
            string rendered = string.Join("\n",
                control.GetVisualDescendants().OfType<TextBlock>().Select(b => b.Text));
            StringAssert.Contains(rendered, "changed elsewhere");
            StringAssert.Contains(rendered, "saved 'Saved elsewhere' vs your edit 'My unsaved rename'");
        });

    [TestMethod]
    public void Refreshed_contact_untouched_fields_accept_saved_changes_and_dirty_fields_win()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            Guid contactId = Guid.Parse("3f7f5c1e-2f04-4c53-9db1-6f0f5f3e1a01");
            System.Collections.Generic.List<CharacterCreationWizardDesktopContactField> fields =
            [
                new(CharacterCreationContactFieldIds.Name, "Name", "text", true,
                    "Old name", null, null, [], [], []),
                new(CharacterCreationContactFieldIds.Notes, "Notes", "text", true,
                    "Old notes", null, null, [], [], [])
            ];
            var contact = new CharacterCreationWizardDesktopContact(
                contactId, "Old name", "Broker", 3, true, false, fields, [], "digest-1");
            var contactBinding = new CharacterCreationContactBinding(
                new("manual-workspace"), 1, 1, 1, "content", "aux", "source", "rules", "runtime");
            var contactsStep = new CharacterCreationWizardDesktopContactsStep(
                contactBinding, [contact], null, null, [], CanEdit: true, SnapshotDigest: "contacts-1");
            control.SetState(state with { ContactsStep = contactsStep });
            string nameId = $"creation-wizard-contact-{contactId:D}-field-"
                + CharacterCreationContactFieldIds.Name;
            string notesId = $"creation-wizard-contact-{contactId:D}-field-"
                + CharacterCreationContactFieldIds.Notes;

            // The player edits only the contact name.
            Find<TextBox>(control, nameId).Text = "Local rename";
            // A save elsewhere changes the saved notes value.
            var refreshed = contact with
            {
                Fields = fields.Select(f => f.FieldId == CharacterCreationContactFieldIds.Notes
                    ? f with { SerializedValue = "Refreshed notes" } : f).ToArray()
            };
            control.SetState(state with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2",
                ContactsStep = contactsStep with { Contacts = [refreshed], SnapshotDigest = "contacts-2" }
            });
            Assert.AreEqual("Local rename", Find<TextBox>(control, nameId).Text);
            Assert.AreEqual("Refreshed notes", Find<TextBox>(control, notesId).Text);
        });

    [TestMethod]
    public void Refreshed_contact_conflict_is_visible_on_the_first_refreshed_render()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            Guid contactId = Guid.Parse("3f7f5c1e-2f04-4c53-9db1-6f0f5f3e1a01");
            System.Collections.Generic.List<CharacterCreationWizardDesktopContactField> fields =
            [
                new(CharacterCreationContactFieldIds.Name, "Name", "text", true,
                    "Old name", null, null, [], [], []),
                new(CharacterCreationContactFieldIds.Notes, "Notes", "text", true,
                    "Old notes", null, null, [], [], [])
            ];
            var contact = new CharacterCreationWizardDesktopContact(
                contactId, "Old name", "Broker", 3, true, false, fields, [], "digest-1");
            var contactBinding = new CharacterCreationContactBinding(
                new("manual-workspace"), 1, 1, 1, "content", "aux", "source", "rules", "runtime");
            var contactsStep = new CharacterCreationWizardDesktopContactsStep(
                contactBinding, [contact], null, null, [], CanEdit: true, SnapshotDigest: "contacts-1");
            control.SetState(state with { ContactsStep = contactsStep });
            string nameId = $"creation-wizard-contact-{contactId:D}-field-"
                + CharacterCreationContactFieldIds.Name;
            string notesId = $"creation-wizard-contact-{contactId:D}-field-"
                + CharacterCreationContactFieldIds.Notes;

            // The player edits only the contact name.
            Find<TextBox>(control, nameId).Text = "Local rename";
            // A save elsewhere changes the saved notes value.
            var refreshed = contact with
            {
                Fields = fields.Select(f => f.FieldId == CharacterCreationContactFieldIds.Notes
                    ? f with { SerializedValue = "Refreshed notes" } : f with { SerializedValue = "Saved elsewhere" }).ToArray()
            };
            control.SetState(state with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2",
                ContactsStep = contactsStep with { Contacts = [refreshed], SnapshotDigest = "contacts-2" }
            });
            Assert.AreEqual("Local rename", Find<TextBox>(control, nameId).Text);
            Assert.AreEqual("Refreshed notes", Find<TextBox>(control, notesId).Text);
            string rendered = string.Join("\n", control.GetVisualDescendants().OfType<TextBlock>().Select(b => b.Text));
            StringAssert.Contains(rendered, "changed elsewhere");
            StringAssert.Contains(rendered, "Saved elsewhere");
        });

    [TestMethod]
    public void Lifestyle_refresh_matching_the_local_edit_does_not_report_a_conflict()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            control.SetState(State(config));
            string nameId = $"creation-wizard-lifestyle-{config.LifestyleId:D}-name";
            Find<TextBox>(control, nameId).Text = "Shared new name";
            control.SetState(State(config with { Name = "Shared new name" }) with
            {
                WorkspaceRevision = 2,
                SnapshotDigest = "snapshot-2"
            });
            Assert.AreEqual("Shared new name", Find<TextBox>(control, nameId).Text);
            string rendered = string.Join("\n", control.GetVisualDescendants().OfType<TextBlock>().Select(b => b.Text));
            Assert.IsFalse(rendered.Contains("changed elsewhere", StringComparison.Ordinal));
        });

    [TestMethod]
    public void Verified_receipt_recovery_retires_the_create_cycle_and_next_create_gets_a_new_identity()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);

            // Start a create cycle: mint an identity through a preview.
            CharacterCreationLifestyleMutationInput? createInput = null;
            control.LifestylePreviewRequested += (_, request) => createInput = request.Input;
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Lost response flat";
            Find<ComboBox>(control, "creation-wizard-lifestyle-create-base").SelectedIndex = 0;
            Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
            Assert.IsNotNull(createInput);
            Guid lostIdentity = createInput.LifestyleId;

            // The response is lost; a later receipt lookup finds the save.
            control.SetLifestyleReceiptLookupResult(new(
                CharacterCreationLifestyleOutcomes.Applied,
                Receipt(lostIdentity),
                null,
                []));

            // The completed create cycle must be retired: a distinct new create
            // operation mints a DIFFERENT identity instead of reusing the saved
            // one. An unrelated dirty edit survives.
            Find<TextBox>(control, "creation-wizard-lifestyle-create-name").Text = "Second flat";
            createInput = null;
            Click(Find<Button>(control, "creation-wizard-lifestyle-create-preview"));
            Assert.IsNotNull(createInput);
            Assert.AreNotEqual(lostIdentity, createInput.LifestyleId);
        });

    [TestMethod]
    public void Window_recovery_executes_a_committed_lost_response_operation_through_the_real_handler()
    {
        WithMainWindow((window, overviewPresenter, lifestyles, pump) =>
        {
            var binding = Binding();

            // ---- State 1: character with NO lifestyles, revision 1 -------------
            overviewPresenter.Publish(OverviewState(binding, [], revision: 1));
            pump();

            // ---- 1. start a create cycle: preview mints identity X -------------
            lifestyles.PreparePreview = CreatePreview(binding, "Lost response flat", revision: 1);
            EmitLifestylePreview(window, new CharacterCreationLifestyleMutationInput(
                CharacterCreationLifestyleMutationKinds.Create, Guid.NewGuid(), null));
            pump();
            var prepared = lifestyles.LastPreparedPreview;
            Assert.IsNotNull(prepared, "The prepare call must reach the lifestyles presenter.");
            Guid identityX = prepared.Mutation.LifestyleId;
            Assert.AreNotEqual(Guid.Empty, identityX);

            // ---- 2. confirm: the write commits but the response is LOST --------
            lifestyles.ConfirmThrows = new IOException("lost response");
            lifestyles.LookupReceiptValue = CreateReceipt(binding, identityX, prepared.IdempotencyKey, prepared.WritePlan);
            EmitLifestyleConfirm(window, prepared.PreviewDigest);
            pump();

            // ---- 3. recovery: receipt lookup resolves the EXACT operation ------
            Assert.IsTrue(lifestyles.LookupCalls >= 1,
                "The window must resolve the ambiguous result via receipt lookup.");
            Assert.AreEqual(prepared.IdempotencyKey, lifestyles.LastLookupKey);
            Assert.AreEqual(identityX, lifestyles.LastLookupReceipt?.LifestyleId,
                "The receipt must belong to the lost create operation.");

            // ---- 4. the character refreshes: now contains the saved lifestyle --
            Assert.AreEqual(1, overviewPresenter.LoadCalls,
                "A verified receipt must reload the character authority.");
            var savedConfig = Configuration() with { Name = "Lost response flat", LifestyleId = identityX };
            overviewPresenter.Publish(OverviewState(binding, [Projection(savedConfig)], revision: 2));
            pump();

            // ---- 5. a DISTINCT new create operation must NOT replay ------------
            lifestyles.PreparePreview = CreatePreview(binding, "Second flat", revision: 2);
            Assert.AreEqual(1, lifestyles.WriteAttempts,
                "The main recovery test: exactly the lost-response write attempt ran.");
            int writesBefore = lifestyles.WriteAttempts;
            EmitLifestylePreview(window, new CharacterCreationLifestyleMutationInput(
                CharacterCreationLifestyleMutationKinds.Create, Guid.NewGuid(), null));
            pump();
            var second = lifestyles.LastPreparedPreview;
            Assert.IsNotNull(second);
            Assert.AreNotEqual(identityX, second.Mutation.LifestyleId,
                "The next create cycle must receive a fresh identity, not the completed one.");
            Assert.AreEqual(writesBefore, lifestyles.WriteAttempts,
                "Recovery must not replay the original write.");
        });
    }

    [TestMethod]
    public void Window_recovery_rejects_a_receipt_bound_to_a_different_operation()
    {
        // A receipt whose PreviousWorkspaceRevision does not bind it to the
        // exact reviewed operation must be rejected: no authority reload, no
        // retirement of the pending identity, no confirmation of an unrelated
        // write. The window stays in the ambiguous state.
        WithMainWindow((window, overviewPresenter, lifestyles, pump) =>
        {
            var binding = Binding();
            overviewPresenter.Publish(OverviewState(binding, [], revision: 1));
            pump();
            lifestyles.PreparePreview = CreatePreview(binding, "Foreign receipt", revision: 1);
            EmitLifestylePreview(window, new CharacterCreationLifestyleMutationInput(
                CharacterCreationLifestyleMutationKinds.Create, Guid.NewGuid(), null));
            pump();
            var prepared = lifestyles.LastPreparedPreview;
            Assert.IsNotNull(prepared);
            lifestyles.ConfirmThrows = new IOException("lost response");
            lifestyles.LookupReceiptValue = CreateReceipt(
                binding, prepared.Mutation.LifestyleId, prepared.IdempotencyKey,
                prepared.WritePlan) with { PreviousWorkspaceRevision = binding.WorkspaceRevision + 5 };
            EmitLifestyleConfirm(window, prepared.PreviewDigest);
            pump();
            Assert.IsTrue(lifestyles.LookupCalls >= 1,
                "The window must attempt the receipt lookup.");
            Assert.AreEqual(0, overviewPresenter.LoadCalls,
                "A receipt from another operation must NOT trigger the authority reload.");
            Assert.AreEqual(1, lifestyles.ConfirmCalls,
                "Only the original lost-response confirm may have run.");
            Assert.AreEqual(1, lifestyles.WriteAttempts,
                "Recovery must not issue a second (replay) confirm: the only write attempt is the original lost response.");
        });
    }

    [TestMethod]
    public void Window_build_ghost_question_sends_the_bound_wizard_context_to_the_sidecar()
    {
        // Production chain under test: the overview state binds the wizard
        // snapshot (via ApplyCreationWizardState -> Bind/Project), the player
        // asks the Build Ghost a question on the Contacts & Lifestyles step,
        // and the window handler serializes the CURRENT bound context --
        // including the lifestyle projection the player sees -- into the
        // sidecar message. Rook must never answer from a context that omits
        // the visible entries.
        WithMainWindow((window, overviewPresenter, lifestyles, pump) =>
        {
            var stub = FindCoachStub(window);
            var binding = Binding();
            overviewPresenter.Publish(OverviewState(binding,
                [Projection(Configuration())], revision: 1));
            pump();

            var session = (CharacterCreationWizardDesktopSession)typeof(MainWindow)
                .GetField("_creationWizardSession", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(window)!;
            CharacterCreationWizardDesktopState state = session.State;
            Assert.IsNotNull(state.LifestylesStep,
                "The bound desktop state must carry the lifestyle projection.");
            Assert.AreEqual(1, state.LifestylesStep.Lifestyles.Count);
            Assert.IsTrue(state.BuildGhostAvailable,
                "An authorized runtime with no blockers must make the Build Ghost available.");

            RaiseEvent(WizardControl(window), "BuildGhostQuestionSubmitted",
                new CharacterCreationWizardBuildGhostQuestion(
                    "Which lifestyle fits my remaining nuyen?", state.BuildGhostContext));
            pump();

            Assert.HasCount(1, stub.BuildTurns);
            AiConversationTurnRequest request = stub.BuildTurns[0];
            Assert.AreEqual(state.BuildGhostContext.WorkspaceId, request.WorkspaceId);
            Assert.AreEqual(state.BuildGhostContext.RuntimeFingerprint, request.RuntimeFingerprint);
            Assert.AreEqual("creation-wizard:" + state.BuildGhostContext.WorkspaceId, request.ConversationId);
            // The serialized context is embedded verbatim in the message.
            StringAssert.Contains(request.Message, "\"LifestylesStep\"");
            StringAssert.Contains(request.Message, Configuration().LifestyleId.ToString("D"));
            StringAssert.Contains(request.Message, Configuration().Name);
            StringAssert.Contains(request.Message, "\"WorkspaceRevision\":" + state.WorkspaceRevision);
            StringAssert.Contains(request.Message, "User question: Which lifestyle fits my remaining nuyen?");
            // Read-only advice: the production preamble must explicitly bind
            // Rook to reviewable-only advice (no mutation claims).
            StringAssert.Contains(request.Message,
                "Advice and reviewable suggestions only: do not claim to apply, confirm, finalize, or mutate anything.");
        });
    }

    [TestMethod]
    public void Window_build_ghost_answer_bound_to_a_stale_context_is_rejected()
    {
        // A late answer that no longer matches the bound context (revision
        // moved on) must be replaced by the stale-context notice -- the
        // sidecar text must never reach the transcript unverified.
        WithMainWindow((window, overviewPresenter, lifestyles, pump) =>
        {
            var stub = FindCoachStub(window);
            var binding = Binding();
            overviewPresenter.Publish(OverviewState(binding,
                [Projection(Configuration())], revision: 1));
            pump();
            var session = (CharacterCreationWizardDesktopSession)typeof(MainWindow)
                .GetField("_creationWizardSession", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(window)!;
            CharacterCreationWizardDesktopState state = session.State;

            stub.NextBuildAnswer = "STALE ADVICE";
            stub.HoldBuildResponses = true;
            RaiseEvent(WizardControl(window), "BuildGhostQuestionSubmitted",
                new CharacterCreationWizardBuildGhostQuestion("?", state.BuildGhostContext));
            pump();

            // The answer raced a revision bump: publish a new revision while
            // the sidecar response is still in flight, then let it arrive.
            overviewPresenter.Publish(OverviewState(binding,
                [Projection(Configuration())], revision: 2));
            pump();
            stub.ReleaseBuildResponse();
            pump();
            pump();

            Assert.HasCount(1, stub.BuildTurns,
                "Exactly one sidecar turn must have been sent.");
            string transcript = GetGhostTranscriptText(window);
            Assert.IsFalse(transcript.Contains("STALE ADVICE"),
                "A stale answer must never reach the transcript. Transcript: " + transcript);
            Assert.IsTrue(transcript.Contains("changed while I was answering"),
                "The stale-context notice must be shown instead. Transcript: " + transcript);
        });
    }

    private static StubCoachSidecarClientForLifestyleTests FindCoachStub(MainWindow window)
        => (StubCoachSidecarClientForLifestyleTests)typeof(MainWindow)
            .GetField("_coachSidecarClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static CharacterCreationWizardControl WizardControl(MainWindow window)
        => window.GetVisualDescendants().OfType<CharacterCreationWizardControl>().First();

    private static string GetGhostTranscriptText(MainWindow window)
    {
        var control = WizardControl(window);
        var panel = control.FindControl<global::Avalonia.Controls.StackPanel>("GhostConversation")!;
        var sb = new System.Text.StringBuilder();
        foreach (var child in panel.Children)
            if (child is global::Avalonia.Controls.TextBlock block)
                sb.Append(block.Text).Append('\n');
        return sb.ToString();
    }

    private static void EmitLifestylePreview(MainWindow window, CharacterCreationLifestyleMutationInput input)
    {
        var control = window.GetVisualDescendants().OfType<CharacterCreationWizardControl>().First();
        RaiseEvent(control, nameof(CharacterCreationWizardControl.LifestylePreviewRequested),
            new CharacterCreationLifestylePreviewRequested(input));
    }

    private static void EmitLifestyleConfirm(MainWindow window, string previewDigest)
    {
        var control = window.GetVisualDescendants().OfType<CharacterCreationWizardControl>().First();
        RaiseEvent(control, nameof(CharacterCreationWizardControl.LifestyleConfirmRequested),
            new CharacterCreationLifestyleConfirmRequested(previewDigest));
    }

    private static string Sha256(string input)
        => "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static void RaiseEvent(Control control, string eventName, object args)
    {
        var field = typeof(CharacterCreationWizardControl)
            .GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing event backing field '" + eventName + "'.");
        var handler = (System.Delegate?)field.GetValue(control);
        handler?.DynamicInvoke(control, args);
    }

    private static CharacterCreationLifestylePreparedPreview CreatePreview(
        CharacterCreationLifestyleBinding binding, string name, long revision)
    {
        var config = Configuration() with { Name = name, LifestyleId = Guid.NewGuid() };
        var after = Projection(config);
        var bindingAtRevision = binding with { WorkspaceRevision = revision, ContentRevision = revision,
            SavedRevision = revision };
        var plan = new CharacterCreationLifestyleAtomicWritePlan(
            "schema", "lifestyles", CharacterCreationLifestyleMutationKinds.Create, config.LifestyleId,
            null, after, [], "before-digest", "after-digest", "sib-b", "sib-a", "nest-b", "nest-a",
            true, true, "plan");
        return new(
            "lifestyles", bindingAtRevision,
            new(CharacterCreationLifestyleMutationKinds.Create, config.LifestyleId, config),
            [], null, after,
            new(1000, 100, 900, 0, true, [], []), new(1000, 100, 900, 0, true, [], []),
            plan, [], true, true,
            "creation-lifestyle-" + Guid.NewGuid().ToString("N"),
            "preview-digest-" + Guid.NewGuid().ToString("N"));
    }

    private static CharacterOverviewState OverviewState(
        CharacterCreationLifestyleBinding binding,
        IReadOnlyList<CharacterCreationLifestyleProjection> lifestyles,
        long revision)
    {
        var authority = new CharacterCreationLifestylesAuthority("schema", "sr5", "settings", [], [],
            1, false, [], [], true, "source", "profile", "policy", "runtime", "authority");
        var step = CharacterCreationWizardStepIds.ContactsLifestyles;
        var desktopLifestyles = lifestyles.Select(p => new CharacterCreationWizardDesktopLifestyle(
            p.Configuration, Guid.Empty, p.BaseLifestyleName, p.SourceBook, p.Page,
            p.Economics, [], "lifestyle")).ToArray();
        var lifestylesStep = new CharacterCreationWizardDesktopLifestylesStep(
            binding with { WorkspaceRevision = revision, ContentRevision = revision, SavedRevision = revision },
            authority, desktopLifestyles, new(1000, 100, 900, 0, true, [], []), [], true, "lifestyles");
        var snapshot = new CharacterCreationWizardSnapshot(
            Schema: CharacterCreationWizardSchemas.SnapshotV1,
            WorkspaceId: binding.WorkspaceId.Value,
            WorkspaceRevision: revision,
            ContentDigest: "content",
            SourceDigest: "source",
            RulesetId: "sr5",
            RuntimeFingerprint: "runtime",
            BuildMethod: "Priority",
            CharacterCreated: false,
            ActiveStepId: step,
            Steps: [new(step, step, "available", true, true, false, [], [], [], [])],
            Budgets: [],
            LegalOptionsByStep: new Dictionary<string, IReadOnlyList<CharacterCreationLegalOption>>(),
            CompletionBlockers: [],
            Warnings: [],
            CanFinalize: false,
            SnapshotDigest: string.Empty);
        snapshot = snapshot with { SnapshotDigest = Sha256("snapshot-" + revision) };
        var desktopState = new CharacterCreationWizardDesktopState(
            "manual-workspace", revision, Sha256("snapshot-" + revision), step,
            [new(step, step, "available", true, true, false, true, [], [], [], [])],
            [], [], [], [], false, false, false, false,
            new(false, null),
            new(CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
                "manual-workspace", revision, Sha256("snapshot-" + revision), step, "sr5",
                "runtime", "Priority", [], [], [], [], LifestylesStep: lifestylesStep),
            LifestylesStep: lifestylesStep);
        var contractState = new CharacterCreationLifestylesState(
            CharacterCreationLifestylesSchemas.StateV1, step,
            binding with { WorkspaceRevision = revision, ContentRevision = revision, SavedRevision = revision },
            authority, false, lifestyles, new(1000, 100, 900, 0, true, [], []), [], true,
            Sha256("snapshot-" + revision));
        return CharacterOverviewState.Empty with
        {
            WorkspaceId = binding.WorkspaceId,
            CreationWizard = snapshot,
            CreationLifestyles = contractState
        };
    }

    private sealed class RecordingLifestylesPresenter : ICharacterCreationLifestylesInteractionPresenter
    {
        public CharacterCreationLifestylePreparedPreview? PreparePreview;
        public Exception? ConfirmThrows;
        public CharacterCreationLifestyleReceipt? LookupReceiptValue;
        public int LookupCalls;
        public string? LastLookupKey;
        public CharacterCreationLifestyleReceipt? LastLookupReceipt;
        public int WriteAttempts;
        public int ConfirmCalls;
        public CharacterCreationLifestylePreparedPreview? LastPreparedPreview;
        public (CharacterOverviewState State, CharacterCreationLifestyleMutationInput Input)? LastPrepare;

        public CharacterCreationLifestylesInteractionLoadResult Load(CharacterOverviewState overview)
            => new(CharacterCreationLifestyleOutcomes.Available, null, []);

        public CharacterCreationLifestylesInteractionPrepareResult Prepare(
            CharacterOverviewState overview, CharacterCreationLifestyleMutationInput input)
        {
            LastPrepare = (overview, input);
            if (PreparePreview is null)
            {
                LastPreparedPreview = null;
                return new(CharacterCreationLifestyleOutcomes.Unavailable, null, null, []);
            }
            var prepared = PreparePreview with { DisplayOwnerContext = overview.DisplayOwnerContext };
            LastPreparedPreview = prepared;
            return new(CharacterCreationLifestyleOutcomes.Available, null, prepared, []);
        }

        public CharacterCreationLifestylesInteractionConfirmResult Confirm(
            CharacterOverviewState overview, CharacterCreationLifestyleConfirmation confirmation)
        {
            WriteAttempts++;
            ConfirmCalls++;
            if (ConfirmThrows is { } error)
                throw error;
            return new(CharacterCreationLifestyleOutcomes.Applied, confirmation.PreparedPreview,
                LookupReceiptValue, null, []);
        }

        public CharacterCreationLifestylesInteractionReceiptLookupResult LookupReceipt(
            CharacterOverviewState overview, string idempotencyKey)
        {
            LookupCalls++;
            LastLookupKey = idempotencyKey;
            LastLookupReceipt = LookupReceiptValue;
            if (LookupReceiptValue is { } receipt)
                return new(CharacterCreationLifestyleOutcomes.Applied, receipt, null, []);
            return new(CharacterCreationLifestyleOutcomes.Unavailable, null, null,
                [CharacterCreationLifestylesInteractionBlockers.ReceiptMismatch]);
        }
    }

    private static void WithMainWindow(
        Action<MainWindow, FakeCharacterOverviewPresenter, RecordingLifestylesPresenter, Action> action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Bootstrap));
            session.Dispatch(() =>
            {
                var overviewPresenter = new FakeCharacterOverviewPresenter();
                using var adapter = new CharacterOverviewViewModelAdapter(overviewPresenter);
                var lifestyles = new RecordingLifestylesPresenter();
                var availabilityEvaluator = new DefaultCommandAvailabilityEvaluator();
                var shellCatalogResolver = new RulesetShellCatalogResolverService(
                    new RulesetPluginRegistry([new Sr5RulesetPlugin()]));
                var window = new MainWindow(
                    overviewPresenter,
                    new RecordingShellPresenterForLifestyleTests(),
                    availabilityEvaluator,
                    new ShellSurfaceResolver(shellCatalogResolver, availabilityEvaluator),
                    new StubCoachSidecarClientForLifestyleTests(),
                    new DesktopAnalyticsClient(new HttpClient { BaseAddress = new Uri("http://localhost/") }),
                    adapter,
                    creationContactsInteractionPresenter: null,
                    creationFoundationInteractionPresenter: null,
                    creationResourcesInteractionPresenter: null,
                    creationFinalizationService: null,
                    creationPrerequisiteService: null,
                    creationAiFinishService: null,
                    creationLifestylesInteractionPresenter: lifestyles);
                window.Show();
                void Pump()
                {
                    Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                    Dispatcher.UIThread.RunJobs();
                }
                Pump();
                try { action(window, overviewPresenter, lifestyles, Pump); }
                finally { window.Close(); Pump(); }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    private sealed class RecordingShellPresenterForLifestyleTests : IShellPresenter
    {
        public ShellState State { get; private set; } = ShellState.Empty with
        {
            ActiveRulesetId = RulesetDefaults.Sr5,
            PreferredRulesetId = RulesetDefaults.Sr5,
            Commands = [],
            MenuRoots = [],
            NavigationTabs = [],
            ActiveTabId = "tab-info",
            Notice = "Ready."
        };
        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;
        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;
        public Task ToggleMenuAsync(string menuId, CancellationToken ct) => Task.CompletedTask;
        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;
        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class StubCoachSidecarClientForLifestyleTests : IAvaloniaCoachSidecarClient
    {
        public List<AiConversationTurnRequest> BuildTurns = [];
        public string? NextBuildAnswer;
        public bool HoldBuildResponses;
        private TaskCompletionSource<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>>? _heldBuild;
        private AiConversationTurnRequest? _heldRequest;
        private string? _heldAnswer;
        public void ReleaseBuildResponse()
        {
            TaskCompletionSource<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>>? held = _heldBuild;
            AiConversationTurnRequest? request = _heldRequest;
            _heldBuild = null;
            _heldRequest = null;
            held?.TrySetResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Success(
                200, NewBuildResponse(request!, _heldAnswer ?? "Build Ghost stub answer.")));
        }
        public Task<AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>> GetStatusAsync(CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection>.Failure(0, "disabled"));
        public Task<AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>> ListProviderHealthAsync(string? routeType = null, CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]>.Failure(0, "disabled"));
        public Task<AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>> ListConversationAuditsAsync(
            string routeType,
            string? runtimeFingerprint = null,
            int maxCount = 3,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage>.Failure(0, "disabled"));
        public Task<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>> SendCoachTurnAsync(
            AiConversationTurnRequest request,
            CancellationToken ct = default)
            => Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Failure(0, "disabled"));
        public Task<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>> SendBuildTurnAsync(
            AiConversationTurnRequest request,
            CancellationToken ct = default)
        {
            BuildTurns.Add(request);
            string answer = NextBuildAnswer ?? "Build Ghost stub answer.";
            if (HoldBuildResponses)
            {
                _heldRequest = request;
                _heldAnswer = answer;
                _heldBuild = new TaskCompletionSource<AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _heldBuild.Task;
            }
            return Task.FromResult(AvaloniaCoachSidecarCallResult<AiConversationTurnResponse>.Success(200,
                NewBuildResponse(request, answer)));
        }

        private static AiConversationTurnResponse NewBuildResponse(
            AiConversationTurnRequest request,
            string answer)
            =>
                new AiConversationTurnResponse(
                    ConversationId: request.ConversationId,
                    RouteType: AiRouteTypes.Build,
                    ProviderId: AiProviderIds.AiMagicx,
                    Answer: answer,
                    RouteDecision: new AiProviderRouteDecision(
                        RouteType: AiRouteTypes.Build,
                        ProviderId: AiProviderIds.AiMagicx,
                        Reason: "stub provider adapter registered",
                        BudgetUnit: AiBudgetUnits.ChummerAiUnits,
                        CredentialTier: AiProviderCredentialTiers.Primary,
                        CredentialSlotIndex: 0),
                    Grounding: new AiGroundingBundle(
                        RouteType: AiRouteTypes.Build,
                        RuntimeFingerprint: request.RuntimeFingerprint,
                        CharacterId: request.CharacterId,
                        ConversationId: request.ConversationId,
                        WorkspaceId: request.WorkspaceId,
                        RuntimeFacts: new Dictionary<string, string>(),
                        CharacterFacts: new Dictionary<string, string>(),
                        Constraints: [],
                        RetrievedItems: [],
                        AllowedTools: []),
                    Budget: new AiBudgetSnapshot(AiBudgetUnits.ChummerAiUnits, 10, 1, 4, 1, true),
                    Citations: [],
                    SuggestedActions: [],
                    ToolInvocations: [],
                    FlavorLine: "Signal's clean.",
                    StructuredAnswer: null,
                    Cache: null);
    }

    private static CharacterCreationLifestyleReceipt CreateReceipt(
        CharacterCreationLifestyleBinding binding, Guid lifestyleId, string idempotencyKeyDigest,
        CharacterCreationLifestyleAtomicWritePlan writePlan)
        => new("schema", "receipt-" + Guid.NewGuid().ToString("N"), "lifestyles",
            binding.WorkspaceId, CharacterCreationLifestyleMutationKinds.Create, lifestyleId,
            idempotencyKeyDigest, "cmd", binding.WorkspaceRevision, binding.WorkspaceRevision + 1,
            binding.ContentRevision, binding.ContentRevision + 1, binding.SavedRevision,
            binding.SavedRevision + 1, "before", "after", "source", "rules", "runtime",
            0m, 100m, 900m, writePlan, "receipt-digest");

    private static CharacterCreationLifestyleConfiguration Configuration()
        => new(Guid.Parse("5ee27326-0912-4dad-b65b-9ad4c01a8cbe"), "low", "Old home", "advanced", "week",
            2, 75.25m, 1, false, true, 2, 3, 4, 5, "Seattle", "Redmond", "Barrens", []);

    private static CharacterCreationLifestyleBinding Binding()
        => new(new("manual-workspace"), 1, 1, 1, "content", "aux", "source", "rules", "runtime");

    private static CharacterCreationWizardDesktopLifestyle DesktopLifestyle(
        CharacterCreationLifestyleConfiguration config)
    {
        var projection = Projection(config);
        return new(config, Guid.Empty, "Low", "SR5", "10", projection.Economics, [], "lifestyle");
    }

    private static CharacterCreationLifestyleProjection Projection(CharacterCreationLifestyleConfiguration config)
        => new(config, Guid.Empty, "Low", "SR5", "10", new(50, 100, 5, 2, true, false, []), [], "lifestyle");

    private static CharacterCreationLifestylePreparedPreview EditPreview(
        CharacterCreationLifestyleConfiguration config)
        => new("lifestyles", Binding(), new("edit", config.LifestyleId, config), [Projection(config)], Projection(config), null,
            new(1000, 100, 900, 0, true, [], []), new(1000, 0, 1000, 0, true, [], []), null!, [], true, true, "operation", "preview");

    [TestMethod]
    public void BuildGhost_context_serialization_carries_the_lifestyle_projection()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config);
            control.SetState(state);

            // The serialized outgoing context must carry the same lifestyle
            // projection the player sees: identity, current values, revision.
            CharacterCreationWizardBuildGhostContext context = state.BuildGhostContext;
            Assert.IsNotNull(context.LifestylesStep, "The wizard context must include the lifestyle step.");
            Assert.HasCount(1, context.LifestylesStep.Lifestyles);
            CharacterCreationWizardDesktopLifestyle projected = context.LifestylesStep.Lifestyles[0];
            Assert.AreEqual(config, projected.Configuration);
            Assert.AreEqual(config.Name, projected.Configuration.Name);
            Assert.AreEqual(state.WorkspaceId, context.WorkspaceId);
            Assert.AreEqual(state.WorkspaceRevision, context.WorkspaceRevision);

            string json = CharacterCreationWizardDesktopSession.SerializeBuildGhostContext(context);
            StringAssert.Contains(json, config.LifestyleId.ToString("D"));
            StringAssert.Contains(json, config.Name);
            StringAssert.Contains(json, "\"WorkspaceRevision\":" + state.WorkspaceRevision);
            StringAssert.Contains(json, "\"LifestylesStep\"");
        });

    [TestMethod]
    public void BuildGhost_context_serialization_omits_null_lifestyle_step()
        => WithUi((control, _) =>
        {
            var config = Configuration();
            var state = State(config) with { LifestylesStep = null };
            control.SetState(state);

            CharacterCreationWizardBuildGhostContext context = state.BuildGhostContext with {
                LifestylesStep = null };
            string json = CharacterCreationWizardDesktopSession.SerializeBuildGhostContext(context);
            // Read-only advice payload: a step without projection must not
            // fabricate lifestyle data.
            Assert.IsFalse(json.Contains("\"Lifestyles\""),
                "A context without a lifestyle step must not fabricate lifestyle data.");
        });

    private static CharacterCreationLifestyleReceipt Receipt(Guid lifestyleId)
        => new("schema", "receipt-1", "lifestyles", new("manual-workspace"), "edit",
            lifestyleId, "idem", "cmd", 1, 2, 1, 2, 1, 2,
            "before", "after", "source", "rules", "runtime",
            100m, 75.25m, 925m, null!, "digest");

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
        var lifestylesStep = new CharacterCreationWizardDesktopLifestylesStep(Binding(), authority, [lifestyle],
            new(1000, 100, 900, 0, true, [], []), [], true, "lifestyles");
        return new("manual-workspace", 1, "snapshot", step,
            [new(step, step, "available", true, true, false, true, [], [], [], [])], [], [], [], [], false, false, false, false,
            new(false, null), new(CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
                "manual-workspace", 1, "snapshot", step, "sr5", "runtime", "Priority", [], [], [], [],
                LifestylesStep: lifestylesStep),
            LifestylesStep: lifestylesStep);
    }

    private static T Find<T>(Control control, string id) where T : Control
        => FindOptional<T>(control, id)
           ?? throw new InvalidOperationException($"No {typeof(T).Name} with id '{id}'.");
    private static T? FindOptional<T>(Control control, string id) where T : Control
        => control.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id);
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
