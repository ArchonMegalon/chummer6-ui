using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public enum WorkspaceCollectionKind
{
    Gear,
    Weapon,
    Armor,
    Skill,
    Contact,
    Pet,
    Vehicle,
    Quality,
    Drug,
    Cyberware,
    Spell,
    Power,
    ComplexForm,
    MatrixProgram,
    InitiationGrade,
    Spirit,
    CritterPower
}

public enum WorkspaceNestedCollectionKind
{
    Gear,
    CyberwarePlugin,
    WeaponAccessory,
    ArmorMod,
    VehicleMod
}

public enum WorkspaceCollectionTextField
{
    Name,
    Category,
    Source,
    Notes,
    CustomName,
    Location,
    Role,
    Grade,
    Damage,
    ArmorValue,
    Accuracy,
    Mode,
    ArmorPenetration,
    Handling,
    Speed,
    Body,
    Sensor,
    Seats,
    Type,
    Range,
    Duration,
    DrainValue,
    Target,
    FadingValue,
    Capacity,
    Slot,
    Reward,
    Metatype,
    Gender,
    Age,
    ContactType,
    PreferredPayment,
    HobbiesVice,
    PersonalLife,
    GroupName
}

public enum WorkspaceCollectionToggleField
{
    Equipped,
    WirelessEnabled,
    HomeNode,
    Bound,
    Resonance,
    Group,
    Ordeal,
    Schooling,
    Free,
    Family,
    Blackmail
}

public enum WorkspaceCollectionIntegerField
{
    Services,
    Force
}

public sealed record WorkspaceCollectionItemTarget(
    WorkspaceCollectionKind Kind,
    string ItemId,
    WorkspaceNestedCollectionKind? NestedKind = null,
    string? NestedItemId = null);

public abstract record WorkspaceCollectionMutationRequest(WorkspaceCollectionItemTarget Target);

public sealed record WorkspaceSetCollectionTextRequest(
    WorkspaceCollectionItemTarget Target,
    WorkspaceCollectionTextField Field,
    string? Value)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceSetCollectionRatingRequest(
    WorkspaceCollectionItemTarget Target,
    int Value)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceSetCollectionQuantityRequest(
    WorkspaceCollectionItemTarget Target,
    decimal Value)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceSetCollectionToggleRequest(
    WorkspaceCollectionItemTarget Target,
    WorkspaceCollectionToggleField Field,
    bool Value)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceSetCollectionIntegerRequest(
    WorkspaceCollectionItemTarget Target,
    WorkspaceCollectionIntegerField Field,
    int Value)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspacePatchCollectionItemRequest(
    WorkspaceCollectionItemTarget Target,
    IReadOnlyDictionary<WorkspaceCollectionTextField, string?>? TextValues = null,
    int? Rating = null,
    decimal? Quantity = null,
    IReadOnlyDictionary<WorkspaceCollectionToggleField, bool>? ToggleValues = null,
    int? VehiclePhysicalDamage = null,
    int? VehicleMatrixDamage = null,
    int? GearMatrixDamage = null,
    int? ArmorMatrixDamage = null,
    int? WeaponMatrixDamage = null,
    int? CyberwareMatrixDamage = null,
    int? ContactConnection = null,
    int? ContactLoyalty = null,
    IReadOnlyDictionary<WorkspaceCollectionIntegerField, int>? IntegerValues = null)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceMoveCollectionItemRequest(
    WorkspaceCollectionItemTarget Target,
    int TargetIndex)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceDeleteCollectionItemRequest(WorkspaceCollectionItemTarget Target)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceSetLinkedCharacterRequest(
    WorkspaceCollectionItemTarget Target,
    string FileName,
    string RelativeFileName,
    string DisplayName,
    CharacterLinkedDocument Identity)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceRemoveLinkedCharacterRequest(WorkspaceCollectionItemTarget Target)
    : WorkspaceCollectionMutationRequest(Target);

public sealed record WorkspaceNestedItemDraft(
    string Name,
    string? Category = null,
    string? Source = null,
    string? Notes = null,
    string? CustomName = null,
    int Rating = 0,
    decimal Quantity = 1m,
    bool Equipped = true,
    bool WirelessEnabled = false);

public sealed record WorkspaceAddNestedCollectionItemRequest(
    WorkspaceCollectionItemTarget Target,
    WorkspaceNestedCollectionKind NestedKind,
    WorkspaceNestedItemDraft Item)
    : WorkspaceCollectionMutationRequest(Target);
