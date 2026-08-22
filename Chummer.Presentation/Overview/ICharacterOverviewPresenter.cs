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

    Task<GroupNameEditorState?> PrepareGroupNameEditAsync(CancellationToken ct)
        => Task.FromResult<GroupNameEditorState?>(null);

    Task ApplyGroupNameEditAsync(GroupNameEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<TraditionNameEditorState?> PrepareTraditionNameEditAsync(CancellationToken ct)
        => Task.FromResult<TraditionNameEditorState?>(null);

    Task ApplyTraditionNameEditAsync(TraditionNameEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<TraditionDrainEditorState?> PrepareTraditionDrainEditAsync(CancellationToken ct)
        => Task.FromResult<TraditionDrainEditorState?>(null);

    Task ApplyTraditionDrainEditAsync(TraditionDrainEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<TraditionSpiritCategoryEditorState?> PrepareTraditionSpiritCategoryEditAsync(CancellationToken ct)
        => Task.FromResult<TraditionSpiritCategoryEditorState?>(null);

    Task ApplyTraditionSpiritCategoryEditAsync(
        TraditionSpiritCategoryEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<ArmorTreeFlagEditorState?> PrepareArmorTreeFlagEditAsync(
        Guid armorId,
        CancellationToken ct)
        => Task.FromResult<ArmorTreeFlagEditorState?>(null);

    Task ApplyArmorTreeFlagEditAsync(
        ArmorTreeFlagEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<GearStolenEditorState?> PrepareGearStolenEditAsync(
        Guid rootGearId,
        CancellationToken ct)
        => Task.FromResult<GearStolenEditorState?>(null);

    Task ApplyGearStolenEditAsync(
        GearStolenEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<WeaponStolenEditorState?> PrepareWeaponStolenEditAsync(
        Guid rootWeaponId,
        CancellationToken ct)
        => Task.FromResult<WeaponStolenEditorState?>(null);

    Task ApplyWeaponStolenEditAsync(
        WeaponStolenEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<GearEquipmentEditorState?> PrepareGearEquipmentEditAsync(
        Guid rootGearId,
        CancellationToken ct)
        => Task.FromResult<GearEquipmentEditorState?>(null);

    Task ApplyGearEquipmentEditAsync(
        GearEquipmentEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<VehicleEquipmentInstalledEditorState?> PrepareVehicleEquipmentInstalledEditAsync(
        Guid vehicleId,
        CancellationToken ct)
        => Task.FromResult<VehicleEquipmentInstalledEditorState?>(null);

    Task ApplyVehicleEquipmentInstalledEditAsync(
        VehicleEquipmentInstalledEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<GearOverclockerEditorState?> PrepareGearOverclockerEditAsync(
        Guid rootGearId,
        CancellationToken ct)
        => Task.FromResult<GearOverclockerEditorState?>(null);

    Task ApplyGearOverclockerEditAsync(
        GearOverclockerEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<GearAttackSwapEditorState?> PrepareGearAttackSwapEditAsync(
        Guid rootGearId,
        CancellationToken ct)
        => Task.FromResult<GearAttackSwapEditorState?>(null);

    Task ApplyGearAttackSwapEditAsync(
        GearAttackSwapEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<GearSleazeSwapEditorState?> PrepareGearSleazeSwapEditAsync(Guid rootGearId, CancellationToken ct)
        => Task.FromResult<GearSleazeSwapEditorState?>(null);

    Task ApplyGearSleazeSwapEditAsync(GearSleazeSwapEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<GearDataProcessingFirewallSwapEditorState?> PrepareGearDataProcessingFirewallSwapEditAsync(
        Guid rootGearId,
        CancellationToken ct)
        => Task.FromResult<GearDataProcessingFirewallSwapEditorState?>(null);

    Task ApplyGearDataProcessingFirewallSwapEditAsync(
        GearDataProcessingFirewallSwapEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<VehicleDataProcessingFirewallSwapEditorState?> PrepareVehicleDataProcessingFirewallSwapEditAsync(
        Guid vehicleId,
        CancellationToken ct)
        => Task.FromResult<VehicleDataProcessingFirewallSwapEditorState?>(null);

    Task ApplyVehicleDataProcessingFirewallSwapEditAsync(
        VehicleDataProcessingFirewallSwapEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<CyberwareMatrixSwapEditorState?> PrepareCyberwareMatrixSwapEditAsync(
        Guid cyberwareId,
        CancellationToken ct)
        => Task.FromResult<CyberwareMatrixSwapEditorState?>(null);

    Task ApplyCyberwareMatrixSwapEditAsync(
        CyberwareMatrixSwapEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<VehicleWeaponFiringModeEditorState?> PrepareVehicleWeaponFiringModeEditAsync(
        Guid vehicleId,
        CancellationToken ct)
        => Task.FromResult<VehicleWeaponFiringModeEditorState?>(null);

    Task ApplyVehicleWeaponFiringModeEditAsync(
        VehicleWeaponFiringModeEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<ImprovementActiveEditorState?> PrepareImprovementActiveEditAsync(CancellationToken ct)
        => Task.FromResult<ImprovementActiveEditorState?>(null);

    Task ApplyImprovementActiveEditAsync(
        ImprovementActiveEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<ImprovementNotesEditorState?> PrepareImprovementNotesEditAsync(CancellationToken ct)
        => Task.FromResult<ImprovementNotesEditorState?>(null);

    Task ApplyImprovementNotesEditAsync(
        ImprovementNotesEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<ImprovementGroupActiveEditorState?> PrepareImprovementGroupActiveEditAsync(
        CancellationToken ct)
        => Task.FromResult<ImprovementGroupActiveEditorState?>(null);

    Task ApplyImprovementGroupActiveEditAsync(
        ImprovementGroupActiveEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<ImprovementGroupAddEditorState?> PrepareImprovementGroupAddAsync(CancellationToken ct)
        => Task.FromResult<ImprovementGroupAddEditorState?>(null);

    Task ApplyImprovementGroupAddAsync(
        ImprovementGroupAddRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<FreeSpriteConversionEditorState?> PrepareFreeSpriteConversionAsync(CancellationToken ct)
        => Task.FromResult<FreeSpriteConversionEditorState?>(null);

    Task ApplyFreeSpriteConversionAsync(
        FreeSpriteConversionRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<MartialArtNotesEditorState?> PrepareMartialArtNotesEditAsync(CancellationToken ct)
        => Task.FromResult<MartialArtNotesEditorState?>(null);

    Task ApplyMartialArtNotesEditAsync(
        MartialArtNotesEditRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<MartialArtDeleteEditorState?> PrepareMartialArtDeleteAsync(CancellationToken ct)
        => Task.FromResult<MartialArtDeleteEditorState?>(null);

    Task ApplyMartialArtDeleteAsync(
        MartialArtDeleteRequest request,
        CancellationToken ct)
        => Task.CompletedTask;

    Task<CareerEdgeUseEditorState?> PrepareCareerEdgeUseEditAsync(CancellationToken ct)
        => Task.FromResult<CareerEdgeUseEditorState?>(null);

    Task ApplyCareerEdgeUseEditAsync(CareerEdgeUseEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<CareerManualKarmaEditorState?> PrepareCareerManualKarmaEditAsync(CancellationToken ct)
        => Task.FromResult<CareerManualKarmaEditorState?>(null);

    Task ApplyCareerManualKarmaEditAsync(CareerManualKarmaEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<CareerManualNuyenEditorState?> PrepareCareerManualNuyenEditAsync(CancellationToken ct)
        => Task.FromResult<CareerManualNuyenEditorState?>(null);

    Task ApplyCareerManualNuyenEditAsync(CareerManualNuyenEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<CareerNuyenExpenseEditorState?> PrepareCareerNuyenExpenseEditAsync(CancellationToken ct)
        => Task.FromResult<CareerNuyenExpenseEditorState?>(null);

    Task ApplyCareerNuyenExpenseEditAsync(CareerNuyenExpenseEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task<SustainedObjectsEditorState?> PrepareSustainedObjectsEditAsync(CancellationToken ct)
        => Task.FromResult<SustainedObjectsEditorState?>(null);

    Task ApplySustainedObjectEditAsync(SustainedObjectEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyPsycheActiveEditAsync(PsycheActiveEditRequest request, CancellationToken ct)
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

    Task ApplyGearActiveCommlinkEditAsync(GearActiveCommlinkEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyCyberwareActiveCommlinkEditAsync(CyberwareActiveCommlinkEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyPrototypeTranshumanEditAsync(PrototypeTranshumanEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyArmorDamageAdjustmentAsync(ArmorDamageAdjustmentRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyArmorEquipmentEditAsync(ArmorEquipmentEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyLifestyleIncrementEditAsync(LifestyleIncrementEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyWeaponAccessoryIncludedEditAsync(WeaponAccessoryIncludedEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplyCritterPowerCountEditAsync(CritterPowerCountEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplySpiritFetteredEditAsync(SpiritFetteredEditRequest request, CancellationToken ct)
        => Task.CompletedTask;

    Task ApplySpiritNameChoiceEditAsync(SpiritNameChoiceEditRequest request, CancellationToken ct)
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
