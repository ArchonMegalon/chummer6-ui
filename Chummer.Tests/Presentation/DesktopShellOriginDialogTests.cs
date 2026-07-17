#nullable enable annotations

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Chummer.Blazor;
using Chummer.Blazor.Components.Layout;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Rulesets.Sr5;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BunitContext = Bunit.BunitContext;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopShellOriginDialogTests
{
    [TestMethod]
    public async Task DesktopShell_keeps_origin_advanced_story_controls_open_across_transient_null_select_refreshes()
    {
        using var context = CreateContext();

        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        TransientNullOriginDialogOverviewPresenter presenter = new(CreateOverviewState(originWizard));
        PassiveShellPresenter shellPresenter = new(CreateShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.Find("[data-origin-advanced-toggle]").Click();
        Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
        Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));

        await cut.Find("select[data-field-id='newCharacterOriginBuildPreference']")
            .ChangeAsync(new ChangeEventArgs { Value = "LifeModule" });

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
            Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
            Assert.AreEqual(
                "LifeModule",
                cut.Find("select[data-field-id='newCharacterOriginBuildPreference']").GetAttribute("value"));
        });

        Assert.AreEqual("newCharacterOriginBuildPreference", presenter.UpdatedDialogFieldId);
        Assert.AreEqual("LifeModule", presenter.UpdatedDialogFieldValue);
        Assert.IsTrue(
            context.JSInterop.Invocations.Any(invocation => string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal)),
            "The live DesktopShell select path should capture dialog scroll before the origin dialog refreshes.");
        Assert.IsTrue(
            context.JSInterop.Invocations.Any(invocation => string.Equals(invocation.Identifier, "chummerDialogs.restoreDialogScroll", StringComparison.Ordinal)),
            "The live DesktopShell select path should restore dialog scroll after the origin dialog rerenders.");
    }

    [TestMethod]
    public async Task DesktopShell_keeps_origin_advanced_story_controls_open_after_switching_another_combo_value()
    {
        using var context = CreateContext();

        DesktopDialogState originWizard = DesktopDialogFactory.BuildNewCharacterOriginWizardDialog(RulesetDefaults.Sr4, "Nova", "Cipher");
        string[] sequentialFieldIds =
        [
            "newCharacterOriginMetatypePreference",
            "newCharacterOriginArchetypeIntent",
            "newCharacterRulesetId",
            "newCharacterOriginBuildPreference",
            "newCharacterOriginBackground",
            "newCharacterOriginTurningPoint",
            "newCharacterOriginTrainingPath",
            "newCharacterOriginUpgradeExposure",
            "newCharacterOriginPressureCost",
            "newCharacterOriginMotivation",
            "newCharacterOriginTone",
            "newCharacterOriginGmConstraintPreset"
        ];
        Dictionary<string, string> expectedValues = sequentialFieldIds.ToDictionary(
            fieldId => fieldId,
            fieldId =>
            {
                DesktopDialogField field = originWizard.Fields.Single(candidate => string.Equals(candidate.Id, fieldId, StringComparison.Ordinal));
                return (field.Options ?? [])
                    .First(option => !string.Equals(option.Value, field.Value, StringComparison.Ordinal))
                    .Value;
            },
            StringComparer.Ordinal);

        TransientNullOriginDialogOverviewPresenter presenter = new(CreateOverviewState(originWizard));
        PassiveShellPresenter shellPresenter = new(CreateShellState());
        RegisterDesktopShellServices(context, presenter, shellPresenter);

        IRenderedComponent<DesktopShell> cut = context.Render<DesktopShell>();

        cut.Find("[data-origin-advanced-toggle]").Click();
        Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
        Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
        HashSet<string> appliedFieldIds = new(StringComparer.Ordinal);

        foreach (string fieldId in sequentialFieldIds)
        {
            await cut.Find($"select[data-field-id='{fieldId}']")
                .ChangeAsync(new ChangeEventArgs { Value = expectedValues[fieldId] });
            appliedFieldIds.Add(fieldId);

            cut.WaitForAssertion(() =>
            {
                Assert.AreEqual("true", cut.Find("[data-origin-advanced-toggle]").GetAttribute("aria-expanded"));
                Assert.IsFalse(cut.Find("[data-origin-advanced-content]").HasAttribute("hidden"));
                foreach ((string expectedFieldId, string expectedValue) in expectedValues.Where(entry => appliedFieldIds.Contains(entry.Key)))
                {
                    Assert.AreEqual(
                        expectedValue,
                        cut.Find($"select[data-field-id='{expectedFieldId}']").GetAttribute("value"));
                }
            });
        }

        string finalFieldId = sequentialFieldIds[^1];
        Assert.AreEqual(finalFieldId, presenter.UpdatedDialogFieldId);
        Assert.AreEqual(expectedValues[finalFieldId], presenter.UpdatedDialogFieldValue);
        Assert.IsTrue(
            context.JSInterop.Invocations.Count(invocation => string.Equals(invocation.Identifier, "chummerDialogs.captureDialogScroll", StringComparison.Ordinal)) >= sequentialFieldIds.Length,
            "Sequential live DesktopShell combo changes should capture dialog scroll before each origin dialog refresh.");
        Assert.IsTrue(
            context.JSInterop.Invocations.Count(invocation => string.Equals(invocation.Identifier, "chummerDialogs.restoreDialogScroll", StringComparison.Ordinal)) >= sequentialFieldIds.Length,
            "Sequential live DesktopShell combo changes should restore dialog scroll after each origin dialog refresh.");
    }

    private static BunitContext CreateContext()
    {
        BunitContext context = new();
        context.JSInterop.Setup<bool>("chummerDialogs.isSameDialogRefresh", _ => true).SetResult(false);
        context.JSInterop.SetupVoid("chummerDialogs.revealActiveDialog").SetVoidResult();
        context.JSInterop.Setup<double[]>("chummerDialogs.captureDialogScroll", _ => true).SetResult([180d, 0d]);
        context.JSInterop.SetupVoid("chummerDialogs.restoreDialogScroll", _ => true).SetVoidResult();
        context.JSInterop.Setup<bool>("chummerDialogs.restorePendingDialogScroll", _ => true).SetResult(false);
        context.Services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        return context;
    }

    private static void RegisterDesktopShellServices(
        BunitContext context,
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter)
    {
        context.Services.AddSingleton(presenter);
        context.Services.AddSingleton(shellPresenter);
        context.Services.AddSingleton<ICommandAvailabilityEvaluator, DefaultCommandAvailabilityEvaluator>();
        context.Services.AddSingleton<IWorkbenchCoachApiClient>(FakeWorkbenchCoachApiClient.CreateDefault());
        context.Services.AddSingleton<IRulesetPlugin, Sr5RulesetPlugin>();
        context.Services.AddSingleton<IRulesetPluginRegistry, RulesetPluginRegistry>();
        context.Services.AddSingleton<IRulesetShellCatalogResolver, RulesetShellCatalogResolverService>();
        context.Services.AddSingleton<IShellSurfaceResolver, ShellSurfaceResolver>();
    }

    private static CharacterOverviewState CreateOverviewState(DesktopDialogState dialog)
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);

        return CharacterOverviewState.Empty with
        {
            Session = new WorkspaceSessionState(
                ActiveWorkspaceId: null,
                OpenWorkspaces: [],
                RecentWorkspaceIds: []),
            Commands = [menuRoot],
            ActiveDialog = dialog
        };
    }

    private static ShellState CreateShellState()
    {
        AppCommandDefinition menuRoot = new("file", "menu.file", "menu", false, true, RulesetDefaults.Sr5);
        NavigationTabDefinition infoTab = new("tab-info", "Info", "profile", "character", true, true, RulesetDefaults.Sr5);

        return ShellState.Empty with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = [],
            ActiveRulesetId = RulesetDefaults.Sr5,
            Commands = [menuRoot],
            MenuRoots = [menuRoot],
            NavigationTabs = [infoTab],
            ActiveTabId = infoTab.Id
        };
    }

    private sealed class PassiveShellPresenter : IShellPresenter
    {
        public PassiveShellPresenter(ShellState state)
        {
            State = state;
        }

        public ShellState State { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

        public Task ToggleMenuAsync(string menuId, CancellationToken ct) => Task.CompletedTask;

        public Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct) => Task.CompletedTask;

        public Task SyncWorkspaceContextAsync(CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct)
        {
            State = State with { ActiveWorkspaceId = activeWorkspaceId };
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class TransientNullOriginDialogOverviewPresenter : ICharacterOverviewPresenter
    {
        public TransientNullOriginDialogOverviewPresenter(CharacterOverviewState state)
        {
            State = state;
        }

        public CharacterOverviewState State { get; private set; }

        public string? UpdatedDialogFieldId { get; private set; }

        public string? UpdatedDialogFieldValue { get; private set; }

        public event EventHandler? StateChanged;

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct) => Task.CompletedTask;

        public Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteCommandAsync(string commandId, CancellationToken ct) => Task.CompletedTask;

        public Task HandleUiControlAsync(string controlId, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct) => Task.CompletedTask;

        public async Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct)
        {
            UpdatedDialogFieldId = fieldId;
            UpdatedDialogFieldValue = value;

            if (State.ActiveDialog is not { } activeDialog)
            {
                return;
            }

            DesktopDialogState updatedDialog = activeDialog with
            {
                Fields = activeDialog.Fields
                    .Select(field => string.Equals(field.Id, fieldId, StringComparison.Ordinal)
                        ? field with { Value = value ?? string.Empty }
                        : field)
                    .ToArray()
            };

            State = State with { ActiveDialog = null };
            StateChanged?.Invoke(this, EventArgs.Empty);

            await Task.Yield();

            State = State with { ActiveDialog = updatedDialog };
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct) => Task.CompletedTask;

        public Task CloseDialogAsync(CancellationToken ct) => Task.CompletedTask;

        public Task SelectTabAsync(string tabId, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExportAsync(CancellationToken ct) => Task.CompletedTask;

        public Task PrintAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
