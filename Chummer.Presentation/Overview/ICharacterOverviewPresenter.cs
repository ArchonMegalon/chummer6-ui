using Chummer.Contracts.Workspaces;
using Chummer.Contracts.Presentation;

namespace Chummer.Presentation.Overview;

public interface ICharacterOverviewPresenter
{
    CharacterOverviewState State { get; }

    event EventHandler? StateChanged;

    Task InitializeAsync(CancellationToken ct);

    Task ImportAsync(WorkspaceImportDocument document, CancellationToken ct);

    Task LoadAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task SwitchWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task DeleteWorkspaceAsync(CharacterWorkspaceId id, bool confirmed, CancellationToken ct)
        => Task.CompletedTask;

    Task ExecuteCommandAsync(string commandId, CancellationToken ct);

    Task HandleUiControlAsync(string controlId, CancellationToken ct);

    Task ExecuteWorkspaceActionAsync(WorkspaceSurfaceActionDefinition action, CancellationToken ct);

    Task UpdateDialogFieldAsync(string fieldId, string? value, CancellationToken ct);

    Task ApplyAttributeEditAsync(AttributeEditRequest request, CancellationToken ct);

    Task ApplyOriginDossierEditAsync(OriginDossierEditRequest request, CancellationToken ct);

    Task ApplyCollectionMutationAsync(WorkspaceCollectionMutationRequest request, CancellationToken ct);

    Task ApplyConditionMonitorEditAsync(ConditionMonitorEditRequest request, CancellationToken ct);

    Task<CareerReputationEditorState?> PrepareCareerReputationEditAsync(CancellationToken ct)
        => Task.FromResult<CareerReputationEditorState?>(null);

    Task ApplyCareerReputationEditAsync(CareerReputationEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyBurnStreetCredAsync(BurnStreetCredRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<SituationalModifiersEditorState?> PrepareSituationalModifiersEditAsync(CancellationToken ct)
        => Task.FromResult<SituationalModifiersEditorState?>(null);

    Task ApplySituationalModifiersEditAsync(SituationalModifiersEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<PrimaryArmEditorState?> PreparePrimaryArmEditAsync(CancellationToken ct)
        => Task.FromResult<PrimaryArmEditorState?>(null);

    Task ApplyPrimaryArmEditAsync(PrimaryArmEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<GroupMembershipEditorState?> PrepareGroupMembershipEditAsync(CancellationToken ct)
        => Task.FromResult<GroupMembershipEditorState?>(null);

    Task ApplyGroupMembershipEditAsync(GroupMembershipEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyGearLocationAddAsync(GearLocationAddRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyWeaponLocationAddAsync(WeaponLocationAddRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyVehicleLocationAddAsync(VehicleLocationAddRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyVehicleHomeNodeEditAsync(VehicleHomeNodeEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyArmorHomeNodeEditAsync(ArmorHomeNodeEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyWeaponHomeNodeEditAsync(WeaponHomeNodeEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyWeaponActiveCommlinkEditAsync(WeaponActiveCommlinkEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyArmorActiveCommlinkEditAsync(ArmorActiveCommlinkEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyArmorDamageAdjustmentAsync(ArmorDamageAdjustmentRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyArmorEquipmentEditAsync(ArmorEquipmentEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyWeaponAccessoryIncludedEditAsync(WeaponAccessoryIncludedEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyCritterPowerCountEditAsync(CritterPowerCountEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplySpiritFetteredEditAsync(SpiritFetteredEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyGearQuantityEditAsync(GearQuantityEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyQualityLevelEditAsync(QualityLevelEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<CyberwareCommerceEditorState?> PrepareCyberwareCommerceEditAsync(
        Guid cyberwareId,
        CancellationToken ct)
        => Task.FromResult<CyberwareCommerceEditorState?>(null);

    Task ApplyCyberwareCommerceEditAsync(CyberwareCommerceRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyLocationRenameAsync(LocationRenameRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ExecuteDialogActionAsync(string actionId, CancellationToken ct);

    Task CloseDialogAsync(CancellationToken ct);

    Task SelectTabAsync(string tabId, CancellationToken ct);

    Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);

    Task ExportAsync(CancellationToken ct);

    Task PrintAsync(CancellationToken ct);
}
