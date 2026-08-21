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

    // Null means Core did not prove this top-level armor is a persona-capable
    // commlink with an exact saved active Boolean.
    public bool? ArmorActiveCommlink { get; init; }
}

public sealed record WorkspaceCollectionEditorState(
    string SectionId,
    WorkspaceCollectionKind Kind,
    WorkspaceNestedCollectionKind? NestedKind,
    IReadOnlyList<WorkspaceCollectionItemEditorState> Items);
