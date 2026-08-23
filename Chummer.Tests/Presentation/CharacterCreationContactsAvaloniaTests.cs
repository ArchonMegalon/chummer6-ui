using System;
using System.Globalization;
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CharacterCreationContactsAvaloniaTests
{
    [TestMethod]
    public void Contact_editor_projects_typed_complete_identity_and_explicit_confirmation()
    {
        WithHeadlessUi(() =>
        {
            Fixture fixture = CreateFixture();
            CharacterCreationWizardControl control = new();
            Window host = new() { Content = control };
            host.Show();
            control.SetState(fixture.DesktopState);

            TextBox nameEditor = FindByAutomationId<TextBox>(
                control,
                FieldAutomationId(fixture.Contact.ContactId, CharacterCreationContactFieldIds.Name));
            ComboBox freeEditor = FindByAutomationId<ComboBox>(
                control,
                FieldAutomationId(fixture.Contact.ContactId, CharacterCreationContactFieldIds.Free));
            ComboBox connectionEditor = Assert.IsInstanceOfType<ComboBox>(FindByAutomationId<Control>(
                control,
                FieldAutomationId(fixture.Contact.ContactId, CharacterCreationContactFieldIds.Connection)));
            ComboBox blackmailEditor = FindByAutomationId<ComboBox>(
                control,
                FieldAutomationId(fixture.Contact.ContactId, CharacterCreationContactFieldIds.Blackmail));
            Assert.AreEqual(32_767, nameEditor.MaxLength);
            Assert.HasCount(6, connectionEditor.Items);
            Assert.HasCount(2, freeEditor.Items);
            Assert.IsFalse(blackmailEditor.IsEnabled);
            nameEditor.Text = "Nova";
            freeEditor.SelectedItem = freeEditor.Items
                .OfType<ComboBoxItem>()
                .Single(item => string.Equals(item.Tag as string, "True", StringComparison.Ordinal));

            CharacterCreationContactEditInput? requestedEdit = null;
            control.ContactPreviewRequested += (_, request) => requestedEdit = request.Input;
            RaiseClick(FindByAutomationId<Button>(
                control,
                $"creation-wizard-contact-{fixture.Contact.ContactId:D}-preview"));

            Assert.IsNotNull(requestedEdit);
            Assert.AreEqual(fixture.Contact.ContactId, requestedEdit.ContactId);
            Assert.IsNotNull(requestedEdit.Identity, "Changing one identity field must emit the complete 13-field identity.");
            Assert.AreEqual("Nova", requestedEdit.Identity.Name);
            Assert.AreEqual(fixture.Contact.Identity.Role, requestedEdit.Identity.Role);
            Assert.AreEqual(fixture.Contact.Identity.PersonalLife, requestedEdit.Identity.PersonalLife);
            Assert.AreEqual(true, requestedEdit.Free);
            Assert.IsNull(requestedEdit.Connection);
            Assert.IsNull(requestedEdit.Loyalty);

            control.SetContactPrepareResult(new CharacterCreationContactsInteractionPrepareResult(
                CharacterCreationContactOutcomes.Available,
                null,
                fixture.PreparedPreview,
                []));
            string renderedText = string.Join(
                "\n",
                control.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text));
            StringAssert.Contains(renderedText, "Contacts: 9 → 12 remaining");
            StringAssert.Contains(renderedText, "1. free: False → True");

            string? confirmedDigest = null;
            control.ContactConfirmRequested += (_, request) => confirmedDigest = request.PreviewDigest;
            Button confirm = FindByAutomationId<Button>(
                control,
                $"creation-wizard-contact-{fixture.Contact.ContactId:D}-confirm");
            Assert.IsTrue(confirm.IsEnabled);
            RaiseClick(confirm);
            Assert.AreEqual(fixture.PreparedPreview.PreviewDigest, confirmedDigest);

            host.Close();
        });
    }

    private static Fixture CreateFixture()
    {
        Guid contactId = Guid.Parse("0147fd6c-518c-47df-8906-814f9c3615f7");
        CharacterCreationContactIdentity identity = new(
            "Fixer", "Broker", "Seattle", "Trusted", "", "Human", "F", "38",
            "Fixer", "Credstick", "Urban brawl", "Partner", "");
        CharacterCreationContactProjection before = Contact(contactId, identity, free: false, cost: 3, Digest('a'));
        CharacterCreationContactProjection after = Contact(contactId, identity, free: true, cost: 0, Digest('b'));
        CharacterCreationContactBinding binding = new(
            new("avalonia-contact-workspace"),
            WorkspaceRevision: 7,
            ContentRevision: 7,
            SavedRevision: 0,
            ContentDigest: Digest('c'),
            AuxiliaryStateDigest: RawDigest('d'),
            SourceDigest: Digest('e'),
            RulesDigest: Digest('f'),
            RuntimeDigest: Digest('0'));
        CharacterCreationContactBudget contactBefore = Budget(
            CharacterCreationContactBudgetIds.Contacts,
            total: 12,
            used: 3);
        CharacterCreationContactBudget contactAfter = Budget(
            CharacterCreationContactBudgetIds.Contacts,
            total: 12,
            used: 0);
        CharacterCreationContactBudget highPlaces = Budget(
            CharacterCreationContactBudgetIds.FriendsInHighPlaces,
            total: 0,
            used: 0);
        CharacterCreationContactEdit edit = new(contactId, identity, Free: true);
        CharacterCreationContactAtomicWritePlan plan = new(
            CharacterCreationContactsSchemas.WritePlanV1,
            CharacterCreationWizardStepIds.ContactsLifestyles,
            contactId,
            [new CharacterCreationContactWriteOperation(
                1,
                CharacterCreationContactFieldIds.Free,
                "False",
                "True",
                CharacterCreationContactSourceAnchors.All)],
            binding.ContentDigest,
            Digest('1'),
            Digest('2'),
            Digest('2'),
            Digest('3'),
            Digest('3'),
            PreservesUntouchedSiblingState: true,
            PreservesNestedState: true,
            PlanDigest: Digest('4'));
        CharacterCreationContactPreparedPreview prepared = new(
            ContactsSnapshotDigest: Digest('5'),
            Binding: binding,
            Edit: edit,
            ContactsBefore: [before],
            ContactBefore: before,
            ContactAfter: after,
            ContactBudgetBefore: contactBefore,
            ContactBudgetAfter: contactAfter,
            HighPlacesBudgetBefore: highPlaces,
            HighPlacesBudgetAfter: highPlaces,
            WritePlan: plan,
            Blockers: [],
            RequiresExplicitConfirmation: true,
            CanConfirm: true,
            IdempotencyKey: "creation-contact-headless-test",
            PreviewDigest: Digest('6'));

        CharacterCreationWizardDesktopContact desktopContact = new(
            before.ContactId,
            before.Identity.Name,
            before.Identity.Role,
            before.ContactPointCost,
            before.CountsAgainstContactBudget,
            before.CountsAgainstHighPlacesBudget,
            before.Fields.Select(static field => new CharacterCreationWizardDesktopContactField(
                field.FieldId,
                field.Label,
                field.ValueKind,
                field.IsEditable
                && field.FieldId != CharacterCreationContactFieldIds.Blackmail,
                field.SerializedValue,
                field.Minimum,
                field.Maximum,
                field.LegalOptions,
                field.Blockers,
                field.SourceAnchorIds)).ToArray(),
            before.SourceAnchorIds,
            before.ContactDigest);
        CharacterCreationWizardDesktopContactsStep contactsStep = new(
            binding,
            [desktopContact],
            contactBefore,
            highPlaces,
            [],
            CanEdit: true,
            SnapshotDigest: Digest('5'));
        CharacterCreationWizardDesktopStep step = new(
            CharacterCreationWizardStepIds.ContactsLifestyles,
            "Contacts and lifestyles",
            CharacterCreationWizardStepStatuses.InProgress,
            IsRequired: true,
            CanEnter: true,
            IsComplete: false,
            IsSelected: true,
            BudgetIds: [CharacterCreationContactBudgetIds.Contacts],
            Blockers:
            [
                CharacterCreationWizardProjector.ContactCreateDeleteAuthorityUnavailable,
                CharacterCreationWizardProjector.ContactPetsAuthorityUnavailable,
                CharacterCreationWizardProjector.LifestylesAuthorityUnavailable
            ],
            Warnings: [],
            LegalNextStepIds: []);
        CharacterCreationWizardBuildGhostContext ghost = new(
            CharacterCreationWizardDesktopSchemas.BuildGhostContextV1,
            binding.WorkspaceId.Value,
            binding.WorkspaceRevision,
            Digest('7'),
            step.StepId,
            "sr5",
            "runtime",
            "priority",
            [],
            [],
            step.Blockers,
            [],
            contactsStep);
        CharacterCreationWizardDesktopState desktop = new(
            binding.WorkspaceId.Value,
            binding.WorkspaceRevision,
            Digest('7'),
            step.StepId,
            [step],
            [],
            [],
            step.Blockers,
            [],
            CanContinue: false,
            CanFinalize: false,
            AdvancedEditorUnlocked: false,
            BuildGhostAvailable: true,
            new CharacterCreationWizardDesktopResume(false, null),
            ghost,
            contactsStep);
        return new Fixture(before, prepared, desktop);
    }

    private static CharacterCreationContactProjection Contact(
        Guid id,
        CharacterCreationContactIdentity identity,
        bool free,
        int cost,
        string digest)
        => new(
            id,
            identity,
            Connection: 2,
            Loyalty: 1,
            IsGroup: false,
            Free: free,
            Family: false,
            Blackmail: false,
            ContactPointCost: cost,
            CountsAgainstContactBudget: !free,
            CountsAgainstHighPlacesBudget: false,
            Fields: CharacterCreationContactFieldIds.All.Select(fieldId => Field(fieldId, identity, free)).ToArray(),
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All,
            ContactDigest: digest);

    private static CharacterCreationContactFieldAuthority Field(
        string fieldId,
        CharacterCreationContactIdentity identity,
        bool free)
    {
        bool integer = fieldId is CharacterCreationContactFieldIds.Connection or CharacterCreationContactFieldIds.Loyalty;
        bool boolean = fieldId is CharacterCreationContactFieldIds.Group
            or CharacterCreationContactFieldIds.Free
            or CharacterCreationContactFieldIds.Family
            or CharacterCreationContactFieldIds.Blackmail;
        string value = fieldId switch
        {
            CharacterCreationContactFieldIds.Name => identity.Name,
            CharacterCreationContactFieldIds.Role => identity.Role,
            CharacterCreationContactFieldIds.Location => identity.Location,
            CharacterCreationContactFieldIds.Notes => identity.Notes,
            CharacterCreationContactFieldIds.CustomName => identity.CustomName,
            CharacterCreationContactFieldIds.Metatype => identity.Metatype,
            CharacterCreationContactFieldIds.Gender => identity.Gender,
            CharacterCreationContactFieldIds.Age => identity.Age,
            CharacterCreationContactFieldIds.ContactType => identity.ContactType,
            CharacterCreationContactFieldIds.PreferredPayment => identity.PreferredPayment,
            CharacterCreationContactFieldIds.HobbiesVice => identity.HobbiesVice,
            CharacterCreationContactFieldIds.PersonalLife => identity.PersonalLife,
            CharacterCreationContactFieldIds.GroupName => identity.GroupName,
            CharacterCreationContactFieldIds.Connection => "2",
            CharacterCreationContactFieldIds.Loyalty => "1",
            CharacterCreationContactFieldIds.Free => free.ToString(CultureInfo.InvariantCulture),
            _ when boolean => false.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };
        CharacterCreationContactOption[] options = boolean
            ? [BooleanOption(false), BooleanOption(true)]
            : integer
                ? Enumerable.Range(1, 6).Select(IntegerOption).ToArray()
                : [];
        return new CharacterCreationContactFieldAuthority(
            fieldId,
            fieldId,
            boolean
                ? CharacterCreationContactValueKinds.Boolean
                : integer
                    ? CharacterCreationContactValueKinds.Integer
                    : CharacterCreationContactValueKinds.Text,
            IsEditable: true,
            SerializedValue: value,
            Minimum: integer ? 1 : boolean ? null : 0,
            Maximum: integer ? 6 : boolean ? null : 32_767,
            LegalOptions: options,
            Blockers: [],
            SourceAnchorIds: CharacterCreationContactSourceAnchors.All);
    }

    private static CharacterCreationContactOption BooleanOption(bool value)
        => new(
            value ? "true" : "false",
            value ? "Yes" : "No",
            value.ToString(CultureInfo.InvariantCulture),
            true,
            [],
            CharacterCreationContactSourceAnchors.All);

    private static CharacterCreationContactOption IntegerOption(int value)
        => new(
            value.ToString(CultureInfo.InvariantCulture),
            value.ToString(CultureInfo.InvariantCulture),
            value.ToString(CultureInfo.InvariantCulture),
            true,
            [],
            CharacterCreationContactSourceAnchors.All);

    private static CharacterCreationContactBudget Budget(string id, int total, int used)
        => new(
            id,
            total,
            used,
            Math.Max(0, total - used),
            Math.Max(0, used - total),
            true,
            [],
            CharacterCreationContactSourceAnchors.All);

    private static string Digest(char value) => "sha256:" + new string(value, 64);

    private static string RawDigest(char value) => new(value, 64);

    private static string FieldAutomationId(Guid contactId, string fieldId)
        => $"creation-wizard-contact-{contactId:D}-field-{fieldId}";

    private static T FindByAutomationId<T>(Control root, string automationId)
        where T : Control
        => root.GetVisualDescendants()
            .Prepend(root)
            .OfType<T>()
            .Single(control => string.Equals(
                control.GetValue(AutomationProperties.AutomationIdProperty),
                automationId,
                StringComparison.Ordinal));

    private static void RaiseClick(Button button)
        => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void WithHeadlessUi(Action action)
    {
        lock (AvaloniaHeadlessSessionGate.SyncRoot)
        {
            HeadlessUnitTestSession? session = null;
            try
            {
                session = HeadlessUnitTestSession.StartNew(typeof(ContactsHeadlessAppBootstrap));
                session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                try
                {
                    session?.Dispose();
                }
                catch (NullReferenceException)
                {
                    // Avalonia headless teardown can intermittently throw after successful dispatch.
                }
            }
        }
    }

    private sealed class ContactsHeadlessAppBootstrap
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .ConfigureFonts(static manager => manager.AddFontCollection(new InterFontCollection()))
                .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
                .WithInterFont();
    }

    private sealed record Fixture(
        CharacterCreationContactProjection Contact,
        CharacterCreationContactPreparedPreview PreparedPreview,
        CharacterCreationWizardDesktopState DesktopState);
}
