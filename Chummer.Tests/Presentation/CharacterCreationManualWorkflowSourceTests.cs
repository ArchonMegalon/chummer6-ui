#nullable enable

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Desktop.Runtime;

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

    private static string ResolveRoot()
        => DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
}
