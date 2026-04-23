using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IDialogCoordinator
{
    Task CoordinateAsync(string actionId, DialogCoordinationContext context, CancellationToken ct);
}

public sealed record DialogCoordinationContext(
    CharacterOverviewState State,
    Action<CharacterOverviewState> Publish,
    Func<WorkspaceImportDocument, CancellationToken, Task> ImportAsync,
    Func<UpdateWorkspaceMetadata, CancellationToken, Task> UpdateMetadataAsync,
    Func<CharacterOverviewState> GetState,
    Func<CancellationToken, Task>? ExportAsync = null,
    Func<CancellationToken, Task>? PrintAsync = null,
    Func<string, CancellationToken, Task>? SetPreferredRulesetAsync = null,
    Func<WorkspaceQuickAddRequest, CancellationToken, Task>? ApplyQuickAddAsync = null);

public static class WorkspaceQuickAddKinds
{
    public const string Gear = "gear";
    public const string Weapon = "weapon";
    public const string Armor = "armor";
    public const string Skill = "skill";
    public const string Contact = "contact";
    public const string Vehicle = "vehicle";
    public const string Quality = "quality";
    public const string Drug = "drug";
    public const string Cyberware = "cyberware";
    public const string Spell = "spell";
    public const string Power = "power";
    public const string ComplexForm = "complex-form";
    public const string MatrixProgram = "matrix-program";
    public const string InitiationGrade = "initiation-grade";
    public const string Spirit = "spirit";
    public const string CritterPower = "critter-power";
}

public sealed record WorkspaceQuickAddRequest(
    string Kind,
    string Name,
    string? Category = null,
    string? Source = null,
    string? Cost = null,
    int Rating = 0,
    int Quantity = 1,
    int BaseValue = 1,
    bool IsKnowledge = false,
    string? Role = null,
    string? Location = null,
    int Connection = 0,
    int Loyalty = 0,
    int Karma = 0,
    string? Damage = null,
    string? ArmorValue = null,
    string? Accuracy = null,
    string? Mode = null,
    string? Ap = null,
    string? Handling = null,
    string? Speed = null,
    string? Body = null,
    string? Sensor = null,
    string? Seats = null,
    string? Type = null,
    string? Range = null,
    string? Duration = null,
    string? DrainValue = null,
    string? Target = null,
    string? FadingValue = null,
    string? Grade = null,
    string? Essence = null,
    string? Capacity = null,
    string? Slot = null,
    int Force = 0,
    int Services = 0,
    bool Bound = false,
    decimal PointsPerLevel = 0m,
    bool Res = false,
    bool Group = false,
    bool Ordeal = false,
    bool Schooling = false,
    string? Reward = null);
