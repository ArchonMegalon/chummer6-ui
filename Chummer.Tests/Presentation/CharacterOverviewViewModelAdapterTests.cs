using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Chummer.Avalonia;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class CharacterOverviewViewModelAdapterTests
{
    [TestMethod]
    public async Task InitializeAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.InitializeAsync(CancellationToken.None);

        Assert.AreEqual(1, presenter.InitializeCalls);
    }

    [TestMethod]
    public async Task LoadAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.LoadAsync(new CharacterWorkspaceId("ws-load"), CancellationToken.None);

        Assert.AreEqual("ws-load", presenter.LoadedWorkspaceId?.Value);
    }

    [TestMethod]
    public async Task SwitchWorkspaceAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.SwitchWorkspaceAsync(new CharacterWorkspaceId("ws-switch"), CancellationToken.None);

        Assert.AreEqual("ws-switch", presenter.SwitchedWorkspaceId?.Value);
    }

    [TestMethod]
    public async Task CloseWorkspaceAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.CloseWorkspaceAsync(new CharacterWorkspaceId("ws-close"), CancellationToken.None);

        Assert.AreEqual("ws-close", presenter.ClosedWorkspaceId?.Value);
    }

    [TestMethod]
    public async Task SelectTabAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.SelectTabAsync("tab-info", CancellationToken.None);

        Assert.AreEqual("tab-info", presenter.SelectedTabId);
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.ExecuteCommandAsync("save_character", CancellationToken.None);

        Assert.AreEqual("save_character", presenter.ExecutedCommandId);
    }

    [TestMethod]
    public async Task HandleUiControlAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.HandleUiControlAsync("create_entry", CancellationToken.None);

        Assert.AreEqual("create_entry", presenter.HandledUiControlId);
    }

    [TestMethod]
    public async Task ExecuteWorkspaceActionAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        WorkspaceSurfaceActionDefinition action = new(
            Id: "tab-info.summary",
            Label: "Summary",
            TabId: "tab-info",
            Kind: WorkspaceSurfaceActionKind.Summary,
            TargetId: "summary",
            RequiresOpenCharacter: true,
            EnabledByDefault: true,
            RulesetId: RulesetDefaults.Sr5);

        await adapter.ExecuteWorkspaceActionAsync(action, CancellationToken.None);

        Assert.AreEqual("tab-info.summary", presenter.ExecutedWorkspaceActionId);
    }

    [TestMethod]
    public async Task UpdateDialogFieldAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.UpdateDialogFieldAsync("diceCount", "8", CancellationToken.None);

        Assert.AreEqual("diceCount", presenter.UpdatedDialogFieldId);
        Assert.AreEqual("8", presenter.UpdatedDialogFieldValue);
    }

    [TestMethod]
    public async Task ExecuteDialogActionAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.ExecuteDialogActionAsync("close", CancellationToken.None);

        Assert.AreEqual("close", presenter.ExecutedDialogActionId);
    }

    [TestMethod]
    public async Task UpdateMetadataAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        UpdateWorkspaceMetadata command = new("Neo", "ONE", "Runner");
        await adapter.UpdateMetadataAsync(command, CancellationToken.None);

        Assert.AreEqual("Neo", presenter.UpdatedMetadata?.Name);
        Assert.AreEqual("ONE", presenter.UpdatedMetadata?.Alias);
        Assert.AreEqual("Runner", presenter.UpdatedMetadata?.Notes);
    }

    [TestMethod]
    public async Task SaveAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.SaveAsync(CancellationToken.None);

        Assert.AreEqual(1, presenter.SaveCalls);
    }

    [TestMethod]
    public async Task ExportAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.ExportAsync(CancellationToken.None);

        Assert.AreEqual(1, presenter.ExportCalls);
    }

    [TestMethod]
    public async Task PrintAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.PrintAsync(CancellationToken.None);

        Assert.AreEqual(1, presenter.PrintCalls);
    }

    [TestMethod]
    public async Task CloseDialogAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.CloseDialogAsync(CancellationToken.None);

        Assert.AreEqual(1, presenter.CloseDialogCalls);
    }

    [TestMethod]
    public async Task ImportAsync_delegates_to_presenter()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);

        await adapter.ImportAsync(Encoding.UTF8.GetBytes("<character />"), CancellationToken.None);

        Assert.AreEqual("<character />", presenter.ImportedContent);
        Assert.AreEqual(string.Empty, presenter.ImportedRulesetId);
    }

    [TestMethod]
    public void Updated_event_is_raised_when_presenter_state_changes()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        using var adapter = new CharacterOverviewViewModelAdapter(presenter);
        int updatedCount = 0;
        adapter.Updated += (_, _) => updatedCount++;

        presenter.Publish(CharacterOverviewState.Empty with { WorkspaceId = new CharacterWorkspaceId("ws-state") });

        Assert.AreEqual(1, updatedCount);
        Assert.AreEqual("ws-state", adapter.State.WorkspaceId?.Value);
    }

    [TestMethod]
    public void Dispose_unsubscribes_from_presenter_events()
    {
        var presenter = new FakeCharacterOverviewPresenter();
        var adapter = new CharacterOverviewViewModelAdapter(presenter);
        int updatedCount = 0;
        adapter.Updated += (_, _) => updatedCount++;
        adapter.Dispose();

        presenter.Publish(CharacterOverviewState.Empty with { WorkspaceId = new CharacterWorkspaceId("ws-after-dispose") });

        Assert.AreEqual(0, updatedCount);
    }
}
