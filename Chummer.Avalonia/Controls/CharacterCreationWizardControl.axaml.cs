using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;
using System.Globalization;

namespace Chummer.Avalonia.Controls;

public sealed record CharacterCreationWizardStepRequest(string StepId);

public sealed record CharacterCreationWizardBuildGhostQuestion(
    string Question,
    CharacterCreationWizardBuildGhostContext Context);

public sealed record CharacterCreationContactPreviewRequested(
    CharacterCreationContactEditInput Input);

public sealed record CharacterCreationContactConfirmRequested(
    string PreviewDigest);

public partial class CharacterCreationWizardControl : UserControl
{
    private static readonly string[] s_IdentityFieldIds =
    [
        CharacterCreationContactFieldIds.Name,
        CharacterCreationContactFieldIds.Role,
        CharacterCreationContactFieldIds.Location,
        CharacterCreationContactFieldIds.Notes,
        CharacterCreationContactFieldIds.CustomName,
        CharacterCreationContactFieldIds.Metatype,
        CharacterCreationContactFieldIds.Gender,
        CharacterCreationContactFieldIds.Age,
        CharacterCreationContactFieldIds.ContactType,
        CharacterCreationContactFieldIds.PreferredPayment,
        CharacterCreationContactFieldIds.HobbiesVice,
        CharacterCreationContactFieldIds.PersonalLife,
        CharacterCreationContactFieldIds.GroupName
    ];

    private CharacterCreationWizardDesktopState? _state;
    private readonly Dictionary<(Guid ContactId, string FieldId), Control> _contactEditors = [];
    private CharacterCreationContactPreparedPreview? _contactPreview;
    private string? _boundContactsSnapshotDigest;
    private string? _contactMutationStatus;
    private bool _contactMutationBusy;
    private bool _buildGhostPreferenceEnabled = true;
    private bool _buildGhostBusy;

    public CharacterCreationWizardControl()
    {
        InitializeComponent();
    }

    public event EventHandler<CharacterCreationWizardStepRequest>? StepRequested;
    public event EventHandler? ContinueRequested;
    public event EventHandler<CharacterCreationWizardBuildGhostQuestion>? BuildGhostQuestionSubmitted;
    public event EventHandler? RecoverCheckpointRequested;
    public event EventHandler? ExportCheckpointRequested;
    public event EventHandler<CharacterCreationContactPreviewRequested>? ContactPreviewRequested;
    public event EventHandler<CharacterCreationContactConfirmRequested>? ContactConfirmRequested;

    public void SetState(CharacterCreationWizardDesktopState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ContactsStep is { } contacts
            && !string.Equals(
                contacts.SnapshotDigest,
                _boundContactsSnapshotDigest,
                StringComparison.Ordinal))
        {
            _contactPreview = null;
            _contactMutationStatus = null;
            _boundContactsSnapshotDigest = contacts.SnapshotDigest;
        }
        _state = state;

        CharacterCreationWizardDesktopStep active = state.Steps.Single(step => step.IsSelected);
        ActiveStepTitle.Text = active.Label;
        ResumeStatusText.Text = state.Resume.Restored
            ? "Navigation restored for this exact workspace revision."
            : state.Resume.InvalidationReason is { Length: > 0 } reason
                ? $"Saved navigation was invalidated: {reason}."
                : $"Workspace revision {state.WorkspaceRevision}.";
        AuthorityStatusText.Text = BuildAuthorityStatus(active);
        CompletionBlockerText.Text = state.CompletionBlockers.Count == 0
            ? "No completion blockers are currently projected."
            : $"Completion blockers: {string.Join(", ", state.CompletionBlockers)}";
        ContinueButton.IsEnabled = state.CanContinue;
        EditorLockText.IsVisible = !state.AdvancedEditorUnlocked;
        BuildGhostAuthorityText.Text = state.BuildGhostAvailable
            ? "Bound to the current runtime, workspace revision, and wizard snapshot."
            : "Build Ghost is pending authoritative runtime and wizard context.";

        RenderSteps(state.Steps);
        RenderBudgets(state.Budgets);
        RenderLegalOptions(state.LegalOptions);
        RenderContacts(state.ContactsStep);
        RefreshBuildGhostAvailability();
    }

    public void SetContactPrepareResult(CharacterCreationContactsInteractionPrepareResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactPreview = result.PreparedPreview;
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactConfirmResult(CharacterCreationContactsInteractionConfirmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        if (result.Outcome is CharacterCreationContactOutcomes.Applied
            or CharacterCreationContactOutcomes.Replayed)
        {
            _contactPreview = null;
        }
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactReceiptLookupResult(
        CharacterCreationContactsInteractionReceiptLookupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _contactMutationStatus = BuildContactResultStatus(result.Outcome, result.Blockers);
        if (result.Receipt is not null)
            _contactPreview = null;
        RenderContacts(_state?.ContactsStep);
    }

    public void SetContactMutationBusy(bool busy)
    {
        _contactMutationBusy = busy;
        RenderContacts(_state?.ContactsStep);
    }

    public void AppendBuildGhostAnswer(string role, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        GhostConversation.Children.Add(new TextBlock
        {
            Text = $"{role}: {text.Trim()}",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            Classes = { "shell-caption" }
        });
    }

    public void SetBuildGhostBusy(bool busy)
    {
        _buildGhostBusy = busy;
        RefreshBuildGhostAvailability();
    }

    public void SetBuildGhostEnabled(bool enabled)
    {
        _buildGhostPreferenceEnabled = enabled;
        RefreshBuildGhostAvailability();
        if (!enabled)
            GhostQuestionTextBox.Text = string.Empty;
    }

    private void RefreshBuildGhostAvailability()
    {
        bool canSend = _state is not null
                       && CharacterCreationWizardBuildGhostPolicy.CanSend(
                           _state,
                           _buildGhostPreferenceEnabled)
                       && !_buildGhostBusy;
        AskGhostButton.IsEnabled = canSend;
        GhostQuestionTextBox.IsEnabled = canSend;
    }

    public void FocusBuildGhostQuestion()
        => GhostQuestionTextBox.Focus();

    private void RenderSteps(IReadOnlyList<CharacterCreationWizardDesktopStep> steps)
    {
        StepList.Children.Clear();
        foreach (CharacterCreationWizardDesktopStep step in steps)
        {
            Button button = new()
            {
                Content = $"{step.Label}\n{step.Status}",
                Tag = step.StepId,
                IsEnabled = step.CanEnter && !step.IsSelected,
                HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
            };
            button.Classes.Add("shell-action");
            if (!step.IsSelected)
                button.Classes.Add("quiet");
            AutomationProperties.SetAutomationId(button, $"creation-wizard-step-{step.StepId}");
            ToolTip.SetTip(button, BuildAuthorityStatus(step));
            button.Click += StepButton_OnClick;
            StepList.Children.Add(button);
        }
    }

    private void RenderBudgets(IReadOnlyList<CharacterCreationWizardDesktopBudget> budgets)
    {
        BudgetList.Children.Clear();
        if (budgets.Count == 0)
        {
            BudgetList.Children.Add(Caption("No authoritative budget is projected for this step."));
            return;
        }

        foreach (CharacterCreationWizardDesktopBudget budget in budgets)
        {
            string exactness = budget.IsExact ? "exact" : "authority pending";
            BudgetList.Children.Add(Caption(
                $"{budget.Label}: {budget.Remaining} / {budget.Total} {budget.Unit} remaining · {exactness}"));
        }
    }

    private void RenderLegalOptions(IReadOnlyList<CharacterCreationWizardDesktopOption> options)
    {
        LegalOptionList.Children.Clear();
        LegalOptionList.IsVisible = _state?.ContactsStep is null;
        StepContentHeading.Text = _state?.ContactsStep is null
            ? "Legal choices and prerequisites"
            : "Contacts and field authority";
        if (!LegalOptionList.IsVisible)
            return;
        if (options.Count == 0)
        {
            LegalOptionList.Children.Add(Caption(
                "No authoritative choices are available for this step. Check the blockers rather than guessing."));
            return;
        }

        foreach (CharacterCreationWizardDesktopOption option in options)
        {
            string source = option.SourceId is { Length: > 0 }
                ? option.SourcePage is int page
                    ? $"{option.SourceId} p. {page}"
                    : option.SourceId
                : "source unavailable";
            string costs = option.Costs.Count == 0
                ? "No projected cost"
                : string.Join(", ", option.Costs.Select(cost => $"{cost.Delta} {cost.Unit}"));
            string prerequisite = option.IsEnabled
                ? "Legal now"
                : $"Blocked: {option.DisableReasonKey ?? "authority unavailable"}";
            Border card = new() { Classes = { "shell-card", "subtle" }, Padding = new global::Avalonia.Thickness(9) };
            card.Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = option.Label, Classes = { "shell-section-title" } },
                    Caption($"{prerequisite} · {costs}"),
                    Caption($"Source: {source}")
                }
            };
            AutomationProperties.SetAutomationId(card, $"creation-wizard-option-{option.OptionId}");
            LegalOptionList.Children.Add(card);
        }
    }

    private void RenderContacts(CharacterCreationWizardDesktopContactsStep? contacts)
    {
        ContactsList.Children.Clear();
        _contactEditors.Clear();
        ContactsList.IsVisible = contacts is not null;
        if (contacts is null)
            return;

        string authority = contacts.CanEdit
            ? "Core can preview this exact revision; every change still requires explicit confirmation."
            : "Editing is blocked by Core authority for this exact revision.";
        ContactsList.Children.Add(Caption(authority));
        if (contacts.Blockers.Count > 0)
            ContactsList.Children.Add(Caption($"Blockers: {string.Join(", ", contacts.Blockers)}"));
        if (!string.IsNullOrWhiteSpace(_contactMutationStatus))
            ContactsList.Children.Add(Caption(_contactMutationStatus));
        if (contacts.Contacts.Count == 0)
        {
            ContactsList.Children.Add(Caption(
                "No existing contact is projected. Add-contact and Lifestyle authority are not available in this slice."));
            return;
        }

        foreach (CharacterCreationWizardDesktopContact contact in contacts.Contacts)
        {
            string title = string.IsNullOrWhiteSpace(contact.Name)
                ? contact.ContactId.ToString("D")
                : contact.Name;
            StackPanel fields = new() { Spacing = 3 };
            fields.Children.Add(new TextBlock
            {
                Text = title,
                Classes = { "shell-section-title" }
            });
            fields.Children.Add(Caption(
                $"{contact.Role} · {contact.ContactPointCost} contact points"));
            foreach (CharacterCreationWizardDesktopContactField field in contact.Fields)
            {
                StackPanel row = new() { Spacing = 2 };
                row.Children.Add(Caption(field.IsEditable
                    ? field.Label
                    : $"{field.Label} · locked"));
                Control editor = BuildContactEditor(contact.ContactId, field);
                row.Children.Add(editor);
                fields.Children.Add(row);
            }

            Button preview = new()
            {
                Content = "Preview changes",
                Tag = contact.ContactId,
                IsEnabled = contacts.CanEdit && !_contactMutationBusy,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
            };
            preview.Classes.Add("shell-action");
            AutomationProperties.SetAutomationId(
                preview,
                $"creation-wizard-contact-{contact.ContactId:D}-preview");
            preview.Click += ContactPreviewButton_OnClick;
            fields.Children.Add(preview);

            Border card = new()
            {
                Classes = { "shell-card", "subtle" },
                Padding = new global::Avalonia.Thickness(9),
                Child = fields
            };
            AutomationProperties.SetAutomationId(
                card,
                $"creation-wizard-contact-{contact.ContactId:D}");
            ContactsList.Children.Add(card);
        }

        RenderContactPreview();
    }

    private Control BuildContactEditor(
        Guid contactId,
        CharacterCreationWizardDesktopContactField field)
    {
        Control editor;
        if (string.Equals(field.ValueKind, CharacterCreationContactValueKinds.Text, StringComparison.Ordinal))
        {
            editor = new TextBox
            {
                Text = field.SerializedValue,
                IsReadOnly = !field.IsEditable,
                IsEnabled = !_contactMutationBusy,
                MaxLength = field.Maximum ?? int.MaxValue,
                AcceptsReturn = string.Equals(
                    field.FieldId,
                    CharacterCreationContactFieldIds.Notes,
                    StringComparison.Ordinal),
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
            };
        }
        else
        {
            List<ComboBoxItem> items = field.LegalOptions.Select(option => new ComboBoxItem
            {
                Content = option.Label,
                Tag = option.SerializedValue,
                IsEnabled = option.IsEnabled && field.IsEditable
            }).ToList();
            ComboBoxItem? selected = items.SingleOrDefault(item => string.Equals(
                item.Tag as string,
                field.SerializedValue,
                StringComparison.OrdinalIgnoreCase));
            editor = new ComboBox
            {
                ItemsSource = items,
                SelectedItem = selected,
                IsEnabled = field.IsEditable && !_contactMutationBusy,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
            };
        }

        AutomationProperties.SetAutomationId(
            editor,
            $"creation-wizard-contact-{contactId:D}-field-{field.FieldId}");
        _contactEditors[(contactId, field.FieldId)] = editor;
        return editor;
    }

    private void RenderContactPreview()
    {
        if (_contactPreview is not { } preview)
            return;

        StackPanel body = new() { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = "Review exact Core preview",
            Classes = { "shell-section-title" }
        });
        body.Children.Add(Caption(
            $"Contacts: {preview.ContactBudgetBefore.Remaining} → {preview.ContactBudgetAfter.Remaining} remaining"));
        body.Children.Add(Caption(
            $"Friends in High Places: {preview.HighPlacesBudgetBefore.Remaining} → {preview.HighPlacesBudgetAfter.Remaining} remaining"));
        foreach (CharacterCreationContactWriteOperation operation in preview.WritePlan.Operations
                     .OrderBy(static operation => operation.Order))
        {
            body.Children.Add(Caption(
                $"{operation.Order}. {operation.FieldId}: {operation.BeforeValue} → {operation.AfterValue}"));
        }
        if (preview.Blockers.Count > 0)
            body.Children.Add(Caption($"Blocked: {string.Join(", ", preview.Blockers)}"));

        Button confirm = new()
        {
            Content = "Confirm and apply",
            IsEnabled = preview.RequiresExplicitConfirmation
                        && preview.CanConfirm
                        && preview.Blockers.Count == 0
                        && !_contactMutationBusy,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        confirm.Classes.Add("shell-action");
        AutomationProperties.SetAutomationId(
            confirm,
            $"creation-wizard-contact-{preview.Edit.ContactId:D}-confirm");
        confirm.Click += ContactConfirmButton_OnClick;
        body.Children.Add(confirm);

        Border card = new()
        {
            Classes = { "shell-card" },
            Padding = new global::Avalonia.Thickness(9),
            Child = body
        };
        AutomationProperties.SetAutomationId(
            card,
            $"creation-wizard-contact-{preview.Edit.ContactId:D}-write-plan");
        ContactsList.Children.Add(card);
    }

    private CharacterCreationContactEditInput? BuildContactInput(
        CharacterCreationWizardDesktopContact contact,
        out string? error)
    {
        string? localError = null;
        Dictionary<string, string> values = contact.Fields.ToDictionary(
            static field => field.FieldId,
            field => ReadContactEditor(contact.ContactId, field.FieldId),
            StringComparer.Ordinal);
        bool identityChanged = s_IdentityFieldIds.Any(fieldId =>
            !string.Equals(
                values[fieldId],
                contact.Fields.Single(field => field.FieldId == fieldId).SerializedValue,
                StringComparison.Ordinal));
        CharacterCreationContactIdentity? identity = identityChanged
            ? new CharacterCreationContactIdentity(
                values[CharacterCreationContactFieldIds.Name],
                values[CharacterCreationContactFieldIds.Role],
                values[CharacterCreationContactFieldIds.Location],
                values[CharacterCreationContactFieldIds.Notes],
                values[CharacterCreationContactFieldIds.CustomName],
                values[CharacterCreationContactFieldIds.Metatype],
                values[CharacterCreationContactFieldIds.Gender],
                values[CharacterCreationContactFieldIds.Age],
                values[CharacterCreationContactFieldIds.ContactType],
                values[CharacterCreationContactFieldIds.PreferredPayment],
                values[CharacterCreationContactFieldIds.HobbiesVice],
                values[CharacterCreationContactFieldIds.PersonalLife],
                values[CharacterCreationContactFieldIds.GroupName])
            : null;

        int? connection = ChangedInt(CharacterCreationContactFieldIds.Connection);
        int? loyalty = ChangedInt(CharacterCreationContactFieldIds.Loyalty);
        bool? group = ChangedBool(CharacterCreationContactFieldIds.Group);
        bool? free = ChangedBool(CharacterCreationContactFieldIds.Free);
        bool? family = ChangedBool(CharacterCreationContactFieldIds.Family);
        bool? blackmail = ChangedBool(CharacterCreationContactFieldIds.Blackmail);
        if (localError is not null)
        {
            error = localError;
            return null;
        }
        error = null;
        return new CharacterCreationContactEditInput(
            contact.ContactId,
            identity,
            connection,
            loyalty,
            group,
            free,
            family,
            blackmail);

        int? ChangedInt(string fieldId)
        {
            CharacterCreationWizardDesktopContactField field = contact.Fields.Single(item => item.FieldId == fieldId);
            string value = values[fieldId];
            if (string.Equals(value, field.SerializedValue, StringComparison.Ordinal))
                return null;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                return parsed;
            localError = $"{field.Label} is not a valid integer choice.";
            return null;
        }

        bool? ChangedBool(string fieldId)
        {
            CharacterCreationWizardDesktopContactField field = contact.Fields.Single(item => item.FieldId == fieldId);
            string value = values[fieldId];
            if (string.Equals(value, field.SerializedValue, StringComparison.OrdinalIgnoreCase))
                return null;
            if (bool.TryParse(value, out bool parsed))
                return parsed;
            localError = $"{field.Label} is not a valid yes/no choice.";
            return null;
        }
    }

    private string ReadContactEditor(Guid contactId, string fieldId)
    {
        if (!_contactEditors.TryGetValue((contactId, fieldId), out Control? editor))
            return string.Empty;
        return editor switch
        {
            TextBox text => text.Text ?? string.Empty,
            ComboBox { SelectedItem: ComboBoxItem { Tag: string value } } => value,
            _ => string.Empty
        };
    }

    private static string BuildContactResultStatus(
        string outcome,
        IReadOnlyList<string> blockers)
        => blockers.Count == 0
            ? $"Contact operation: {outcome}."
            : $"Contact operation: {outcome} · {string.Join(", ", blockers)}";

    private void ContactPreviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_state?.ContactsStep is not { } contacts
            || sender is not Button { Tag: Guid contactId }
            || contacts.Contacts.SingleOrDefault(contact => contact.ContactId == contactId)
               is not { } contact)
        {
            return;
        }

        CharacterCreationContactEditInput? input = BuildContactInput(contact, out string? error);
        if (input is null)
        {
            _contactMutationStatus = error ?? "The contact edit could not be projected.";
            RenderContacts(contacts);
            return;
        }
        ContactPreviewRequested?.Invoke(this, new CharacterCreationContactPreviewRequested(input));
    }

    private void ContactConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_contactPreview is not { } preview
            || !preview.RequiresExplicitConfirmation
            || !preview.CanConfirm
            || preview.Blockers.Count != 0)
        {
            return;
        }
        ContactConfirmRequested?.Invoke(
            this,
            new CharacterCreationContactConfirmRequested(preview.PreviewDigest));
    }

    private static TextBlock Caption(string text)
    {
        TextBlock block = new()
        {
            Text = text,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        };
        block.Classes.Add("shell-caption");
        return block;
    }

    private static string BuildAuthorityStatus(CharacterCreationWizardDesktopStep step)
    {
        List<string> status = [$"Status: {step.Status}"];
        if (step.Blockers.Count > 0)
            status.Add($"Blockers: {string.Join(", ", step.Blockers)}");
        if (step.Warnings.Count > 0)
            status.Add($"Warnings: {string.Join(", ", step.Warnings)}");
        return string.Join(" · ", status);
    }

    private void StepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string stepId })
            StepRequested?.Invoke(this, new CharacterCreationWizardStepRequest(stepId));
    }

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
        => ContinueRequested?.Invoke(this, EventArgs.Empty);

    private void RecoverButton_OnClick(object? sender, RoutedEventArgs e)
        => RecoverCheckpointRequested?.Invoke(this, EventArgs.Empty);

    private void ExportButton_OnClick(object? sender, RoutedEventArgs e)
        => ExportCheckpointRequested?.Invoke(this, EventArgs.Empty);

    private void AskGhostButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_state is null
            || !CharacterCreationWizardBuildGhostPolicy.CanSend(
                _state,
                _buildGhostPreferenceEnabled)
            || string.IsNullOrWhiteSpace(GhostQuestionTextBox.Text))
            return;

        string question = GhostQuestionTextBox.Text.Trim();
        GhostQuestionTextBox.Text = string.Empty;
        AppendBuildGhostAnswer("You", question);
        BuildGhostQuestionSubmitted?.Invoke(
            this,
            new CharacterCreationWizardBuildGhostQuestion(question, _state.BuildGhostContext));
    }

    private void WizardRoot_OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool commandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                               || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (commandModifier
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && e.Key == Key.G)
        {
            FocusBuildGhostQuestion();
            e.Handled = true;
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt) || _state is null)
            return;

        int selected = _state.Steps.ToList().FindIndex(static step => step.IsSelected);
        int delta = e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0;
        if (delta == 0)
            return;

        int candidate = selected + delta;
        while (candidate >= 0 && candidate < _state.Steps.Count)
        {
            CharacterCreationWizardDesktopStep target = _state.Steps[candidate];
            if (target.CanEnter)
            {
                StepRequested?.Invoke(this, new CharacterCreationWizardStepRequest(target.StepId));
                e.Handled = true;
                return;
            }

            candidate += delta;
        }
    }
}
