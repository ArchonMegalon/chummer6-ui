using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public sealed record WorkspaceCollectionTextValueState(
    WorkspaceCollectionTextField Field,
    string Value,
    bool IsRequired = false,
    int MaximumLength = 65_536,
    bool IsEnabled = true);

public sealed record WorkspaceCollectionRatingState(
    int Value,
    int Minimum = 0,
    int Maximum = 1000);

public sealed record WorkspaceCollectionQuantityState(
    decimal Value,
    decimal MinimumExclusive = 0m,
    decimal Maximum = 1_000_000m);

public sealed record WorkspaceGearMergeCandidateState(
    Guid GearId,
    string Label,
    decimal Quantity);

public sealed record WorkspaceGearQuantityLifecycleState(
    Guid GearId,
    decimal Quantity,
    int DecimalPlaces,
    decimal MinimumIncrement,
    decimal PurchaseUnitCost,
    bool PurchaseUnitCostExact,
    IReadOnlyList<WorkspaceGearMergeCandidateState> MergeCandidates);

public sealed record WorkspaceArmorDamageAdjustmentState(
    Guid ArmorId,
    int Damage,
    int Maximum,
    bool CanRepair,
    bool CanDegrade);

public sealed record WorkspaceQualityLevelState(
    Guid QualityId,
    int Level,
    int MaximumLevel,
    bool CareerMode,
    string QualityType);

public sealed record WorkspaceCollectionToggleValueState(
    WorkspaceCollectionToggleField Field,
    bool Value,
    bool IsEnabled = true)
{
    public string? Label { get; init; }
}

public sealed record WorkspaceCollectionIntegerValueState(
    WorkspaceCollectionIntegerField Field,
    int Value,
    int Minimum = 0,
    int Maximum = int.MaxValue,
    bool IsEnabled = true)
{
    public string? Label { get; init; }
}

public sealed record WorkspaceContactEditorState(
    int Connection,
    int ConnectionMaximum,
    bool ConnectionEditable,
    int Loyalty,
    int LoyaltyMaximum,
    bool LoyaltyEditable,
    bool Exact);

public sealed record WorkspaceItemConditionMonitorState(
    string Label,
    int Filled,
    int Maximum,
    bool MaximumExact,
    bool Editable);

public sealed record WorkspaceLinkedCharacterState(
    bool IsLinked,
    bool IdentityResolved,
    string FileName,
    string RelativeFileName,
    string DisplayName,
    bool CanAttach,
    bool CanRemove);

public sealed record WorkspaceCollectionItemEditorState(
    WorkspaceCollectionItemTarget Target,
    int Index,
    string Label,
    IReadOnlyList<WorkspaceCollectionTextValueState> TextValues,
    WorkspaceCollectionRatingState? Rating,
    WorkspaceCollectionQuantityState? Quantity,
    IReadOnlyList<WorkspaceCollectionToggleValueState> ToggleValues,
    IReadOnlyList<WorkspaceNestedCollectionKind> AddableNestedKinds,
    bool CanDelete = true,
    bool CanMove = true,
    WorkspaceItemConditionMonitorState? PhysicalConditionMonitor = null,
    WorkspaceItemConditionMonitorState? MatrixConditionMonitor = null,
    WorkspaceContactEditorState? Contact = null,
    WorkspaceLinkedCharacterState? LinkedCharacter = null)
{
    public IReadOnlyList<WorkspaceCollectionIntegerValueState> IntegerValues { get; init; }
        = Array.Empty<WorkspaceCollectionIntegerValueState>();

    // Null means the active Core payload did not prove an exact nested-location
    // count/identity projection. An empty list is an exact, editable empty set.
    public IReadOnlyList<WorkspaceLocationItemState>? VehicleLocations { get; init; }

    // Null means Core did not supply an exact Boolean vehicle home-node value.
    public bool? VehicleHomeNode { get; init; }

    // Null means Core did not supply an exact Boolean armor home-node value.
    public bool? ArmorHomeNode { get; init; }

    // Null means Core could not prove the exact Chummer5 AI, Matrix-owner,
    // Device Rating, Program Limit, and DEP rule for this top-level weapon.
    public CharacterWeaponHomeNodeSemantics? WeaponHomeNode { get; init; }

    // Non-null means Core proved the exact Matrix-owner delegation and saved
    // character-wide active-device state for this stable top-level weapon.
    public CharacterWeaponActiveCommlinkSemantics? WeaponActiveCommlink { get; init; }

    // Null means Core did not prove this top-level armor is a persona-capable
    // commlink with an exact saved active Boolean.
    public bool? ArmorActiveCommlink { get; init; }

    // Non-null means Core proved this stable gear identity, persona eligibility,
    // and the exact character-wide saved active-device state.
    public CharacterGearActiveCommlinkSemantics? GearActiveCommlink { get; init; }

    // Null means Core did not prove exact Career-only armor degradation bounds.
    public WorkspaceArmorDamageAdjustmentState? ArmorDamageAdjustment { get; init; }

    // Null means Core could not prove exact, unique top-level armor equipment state.
    public CharacterArmorEquipmentState? ArmorEquipment { get; init; }

    // Non-null means Core proved the stable Lifestyle identity, saved interval count/unit,
    // mode, derived total interval cost, and (for Career purchases) current Nuyen authority.
    public CharacterLifestyleIncrementState? LifestyleIncrement { get; init; }

    // Null means Core did not supply an exact saved included Boolean for a
    // stable parent-weapon/accessory identity pair.
    public bool? WeaponAccessoryIncludedInWeapon { get; init; }

    // Null means Core could not prove one stable critter-power identity and
    // the exact legacy counttowardslimit Boolean (including its true default).
    public CharacterCritterPowerCountState? CritterPowerCount { get; init; }

    // Null means Core could not prove all shared SpiritControl Fettered/Pet rules from the
    // saved runner and its persisted active-settings shadow.
    public CharacterSpiritFetteringState? SpiritFettering { get; init; }

    // Null means the active Core payload did not prove the exact DropDownList choices for
    // SpiritControl.cboSpiritName from saved tradition/stream data and its source profile.
    public CharacterSpiritNameChoiceState? SpiritNameChoice { get; init; }

    // Null means Core did not prove exact Career quantity precision, cost, and merge identity.
    public WorkspaceGearQuantityLifecycleState? GearQuantityLifecycle { get; init; }

    public bool GearQuantityLifecycleRequired { get; init; }

    // Non-null means Core proved the exact Chummer5 duplicate-quality level
    // identity and a bounded side-effect-free source entry.
    public WorkspaceQualityLevelState? QualityLevel { get; init; }

    // Career Cyberware commerce is prepared from the native XML/source profile
    // on navigation; this flag only proves that the phone route is applicable.
    public bool CyberwareCommerceRequired { get; init; }
}

public sealed record WorkspaceCollectionEditorState(
    string SectionId,
    WorkspaceCollectionKind Kind,
    WorkspaceNestedCollectionKind? NestedKind,
    IReadOnlyList<WorkspaceCollectionItemEditorState> Items);
