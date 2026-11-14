using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

internal sealed class MainWindowInteractionCoordinator
{
    private readonly ICharacterOverviewPresenter _presenter;
    private readonly IShellPresenter _shellPresenter;
    private readonly CharacterOverviewViewModelAdapter _adapter;

    public MainWindowInteractionCoordinator(
        ICharacterOverviewPresenter presenter,
        IShellPresenter shellPresenter,
        CharacterOverviewViewModelAdapter adapter)
    {
        _presenter = presenter;
        _shellPresenter = shellPresenter;
        _adapter = adapter;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        return _presenter.SaveAsync(ct);
    }

    public Task<CareerAttributeAdvanceEditorState?> PrepareCareerAttributeAdvanceAsync(CancellationToken ct)
    {
        return _presenter.PrepareCareerAttributeAdvanceAsync(ct);
    }

    public Task<Sr6CareerAttributeAdvanceEditorSnapshot?> PrepareSr6CareerAttributeAdvanceAsync(CancellationToken ct)
    {
        return _presenter.PrepareSr6CareerAttributeAdvanceAsync(ct);
    }

    public Task<Sr6CareerAttributeReceipt?> ApplySr6CareerAttributeAdvanceAsync(
        OwnerContextStamp owner,
        Sr6CareerAttributeCommand command,
        CancellationToken ct)
    {
        return _presenter.ApplySr6CareerAttributeAdvanceAsync(owner, command, ct);
    }

    public Task<CharacterCareerAttributeAdvanceReceipt?> ApplyCareerAttributeAdvanceAsync(
        CareerAttributeAdvanceRequest request,
        CancellationToken ct)
    {
        return _presenter.ApplyCareerAttributeAdvanceAsync(request, ct);
    }

    public CareerRewardApplyResult? GetPendingCareerReward(OwnerContextStamp owner, CharacterWorkspaceId workspaceId)
        => _presenter.GetPendingCareerReward(owner, workspaceId);

    public bool AcknowledgeCareerReward(OwnerContextStamp owner, CharacterWorkspaceId workspaceId, Guid operationId)
        => _presenter.AcknowledgeCareerReward(owner, workspaceId, operationId);

    public Task<CareerRewardApplyResult> ApplyCareerManualKarmaEditAsync(
        CareerManualKarmaEditRequest request, OwnerContextStamp owner, CancellationToken ct)
        => _presenter.ApplyCareerManualKarmaEditAsync(request, owner, ct);

    public Task<CareerRewardApplyResult> ApplyCareerManualNuyenEditAsync(
        CareerManualNuyenEditRequest request, OwnerContextStamp owner, CancellationToken ct)
        => _presenter.ApplyCareerManualNuyenEditAsync(request, owner, ct);

    public Task<CareerRewardApplyResult> ReconcileCareerRewardAsync(
        OwnerContextStamp owner, CharacterWorkspaceId workspaceId, Guid operationId, CancellationToken ct)
        => _presenter.ReconcileCareerRewardAsync(owner, workspaceId, operationId, ct);

    public Task<CareerManualKarmaEditorState?> PrepareCareerManualKarmaEditAsync(CancellationToken ct)
        => _presenter.PrepareCareerManualKarmaEditAsync(ct);

    public Task ApplyCareerManualKarmaEditAsync(CareerManualKarmaEditRequest request, CancellationToken ct)
        => _presenter.ApplyCareerManualKarmaEditAsync(request, ct);

    public Task<CareerManualNuyenEditorState?> PrepareCareerManualNuyenEditAsync(CancellationToken ct)
        => _presenter.PrepareCareerManualNuyenEditAsync(ct);

    public Task ApplyCareerManualNuyenEditAsync(CareerManualNuyenEditRequest request, CancellationToken ct)
        => _presenter.ApplyCareerManualNuyenEditAsync(request, ct);

    public Task<CareerActiveSkillAdvanceEditorState?> PrepareCareerActiveSkillAdvanceAsync(CancellationToken ct)
        => _presenter.PrepareCareerActiveSkillAdvanceAsync(ct);

    public Task<bool> TryApplyCareerActiveSkillAdvanceAsync(CareerActiveSkillAdvanceRequest request, CancellationToken ct)
        => _presenter.TryApplyCareerActiveSkillAdvanceAsync(request, ct);

    public Task ApplyCareerActiveSkillAdvanceAsync(CareerActiveSkillAdvanceRequest request, CancellationToken ct)
        => _presenter.ApplyCareerActiveSkillAdvanceAsync(request, ct);

    public Task<CareerKnowledgeSkillAdvanceEditorState?> PrepareCareerKnowledgeSkillAdvanceAsync(CancellationToken ct)
        => _presenter.PrepareCareerKnowledgeSkillAdvanceAsync(ct);

    public Task<CharacterCareerKnowledgeSkillAdvanceReceipt?> ApplyCareerKnowledgeSkillAdvanceAsync(
        CareerKnowledgeSkillAdvanceRequest request,
        CancellationToken ct)
        => _presenter.ApplyCareerKnowledgeSkillAdvanceAsync(request, ct);

    public Task<CareerSkillGroupAdvanceEditorState?> PrepareCareerSkillGroupAdvanceAsync(CancellationToken ct)
        => _presenter.PrepareCareerSkillGroupAdvanceAsync(ct);

    public Task<bool> TryApplyCareerSkillGroupAdvanceAsync(CareerSkillGroupAdvanceRequest request, CancellationToken ct)
        => _presenter.TryApplyCareerSkillGroupAdvanceAsync(request, ct);

    public Task ApplyCareerSkillGroupAdvanceAsync(CareerSkillGroupAdvanceRequest request, CancellationToken ct)
        => _presenter.ApplyCareerSkillGroupAdvanceAsync(request, ct);

    public Task<CareerSkillSpecializationEditorState?> PrepareCareerSkillSpecializationAsync(CancellationToken ct)
        => _presenter.PrepareCareerSkillSpecializationAsync(ct);

    public Task<CharacterCareerSkillSpecializationQuote?> PrepareCareerSkillSpecializationQuoteAsync(
        CareerSkillSpecializationQuoteRequest request,
        CancellationToken ct)
        => _presenter.PrepareCareerSkillSpecializationQuoteAsync(request, ct);

    public Task<bool> TryApplyCareerSkillSpecializationAsync(CareerSkillSpecializationRequest request, CancellationToken ct)
        => _presenter.TryApplyCareerSkillSpecializationAsync(request, ct);

    public Task ApplyCareerSkillSpecializationAsync(CareerSkillSpecializationRequest request, CancellationToken ct)
        => _presenter.ApplyCareerSkillSpecializationAsync(request, ct);

    public CharacterOverviewState PeekState() => _presenter.State;
    public Task<CharacterCareerAttributeCorrectionPlan?> CorrectCareerAttributeAdvanceAsync(
        CareerAttributeCorrectionRequest request,
        CancellationToken ct)
    {
        return _presenter.CorrectCareerAttributeAdvanceAsync(request, ct);
    }

    public Task ImportAsync(byte[] documentBytes, CancellationToken ct)
    {
        return _adapter.ImportAsync(documentBytes, ct);
    }

    public Task ExportAsync(CancellationToken ct)
    {
        return _presenter.ExportAsync(ct);
    }

    public Task PrintAsync(CancellationToken ct)
    {
        return _presenter.PrintAsync(ct);
    }

    public Task ToggleMenuAsync(string menuId, CancellationToken ct)
    {
        return _shellPresenter.ToggleMenuAsync(menuId, ct);
    }

    public async Task ExecuteCommandAsync(string commandId, CancellationToken ct)
    {
        await _shellPresenter.ExecuteCommandAsync(commandId, ct);
        await _adapter.ExecuteCommandAsync(commandId, ct);
    }

    public Task OpenRuntimeInspectorAsync(CancellationToken ct)
    {
        return _adapter.ExecuteCommandAsync(OverviewCommandPolicy.RuntimeInspectorCommandId, ct);
    }

    public Task SwitchWorkspaceAsync(string workspaceId, CancellationToken ct)
    {
        return _adapter.SwitchWorkspaceAsync(new CharacterWorkspaceId(workspaceId), ct);
    }

    public async Task SelectTabAsync(string tabId, CancellationToken ct)
    {
        await _shellPresenter.SelectTabAsync(tabId, ct);
        if (!string.Equals(_shellPresenter.State.ActiveTabId, tabId, StringComparison.Ordinal))
        {
            return;
        }

        await _adapter.SelectTabAsync(tabId, ct);
    }

    public Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        return _adapter.ExecuteWorkspaceActionAsync(action, ct);
    }

    public Task HandleUiControlAsync(string controlId, CancellationToken ct)
    {
        return _adapter.HandleUiControlAsync(controlId, ct);
    }

    public Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct)
    {
        return _adapter.ApplyAttributeEditAsync(request, ct);
    }

    public Task ApplyCollectionMutationAsync(WorkspaceCollectionMutationRequest request, CancellationToken ct)
    {
        return _adapter.ApplyCollectionMutationAsync(request, ct);
    }

    public Task ApplyConditionMonitorEditAsync(ConditionMonitorEditRequest request, CancellationToken ct)
    {
        return _adapter.ApplyConditionMonitorEditAsync(request, ct);
    }

    public Task ExecuteDialogActionAsync(string actionId, CancellationToken ct)
    {
        return _adapter.ExecuteDialogActionAsync(actionId, ct);
    }

    public bool TryGetActiveWorkspaceId(CharacterOverviewState state, out CharacterWorkspaceId activeWorkspaceId)
    {
        CharacterWorkspaceId? resolvedId = state.Session.ActiveWorkspaceId ?? state.WorkspaceId;
        if (resolvedId is null)
        {
            activeWorkspaceId = default;
            return false;
        }

        activeWorkspaceId = resolvedId.Value;
        return true;
    }

    public Task CloseWorkspaceAsync(CharacterWorkspaceId workspaceId, CancellationToken ct)
    {
        return _adapter.CloseWorkspaceAsync(workspaceId, ct);
    }
}
