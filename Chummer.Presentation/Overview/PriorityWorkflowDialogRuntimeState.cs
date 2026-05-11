using System.Text.Json;

namespace Chummer.Presentation.Overview;

public sealed record PriorityWorkflowDialogRuntimeState(
    string Mode,
    string SumToTenLabel,
    IReadOnlyList<DesktopDialogFieldOption> MetavariantOptions,
    string SelectedMetavariant,
    string MetatypeKarma,
    string SpecialAttributes,
    string Source,
    IReadOnlyList<PriorityWorkflowInspectAttributeState> InspectAttributes,
    IReadOnlyList<string> Qualities,
    bool ForceVisible,
    int Force,
    bool PossessionVisible,
    bool PossessionBased,
    IReadOnlyList<DesktopDialogFieldOption> PossessionMethodOptions,
    string SelectedPossessionMethod,
    string SkillSelectionLabel,
    PriorityWorkflowChoiceState SkillChoice1,
    PriorityWorkflowChoiceState SkillChoice2,
    PriorityWorkflowChoiceState SkillChoice3,
    bool CanCommit)
{
    public static PriorityWorkflowDialogRuntimeState Empty { get; } = new(
        Mode: "Priority",
        SumToTenLabel: string.Empty,
        MetavariantOptions: [],
        SelectedMetavariant: string.Empty,
        MetatypeKarma: "0",
        SpecialAttributes: "0",
        Source: string.Empty,
        InspectAttributes: [],
        Qualities: [],
        ForceVisible: false,
        Force: 1,
        PossessionVisible: false,
        PossessionBased: false,
        PossessionMethodOptions: [],
        SelectedPossessionMethod: string.Empty,
        SkillSelectionLabel: string.Empty,
        SkillChoice1: PriorityWorkflowChoiceState.Hidden,
        SkillChoice2: PriorityWorkflowChoiceState.Hidden,
        SkillChoice3: PriorityWorkflowChoiceState.Hidden,
        CanCommit: true);
}

public sealed record PriorityWorkflowInspectAttributeState(
    string Label,
    string Value);

public sealed record PriorityWorkflowChoiceState(
    bool Visible,
    string Value,
    IReadOnlyList<DesktopDialogFieldOption> Options)
{
    public static PriorityWorkflowChoiceState Hidden { get; } = new(false, string.Empty, []);
}

public static class PriorityWorkflowDialogRuntimeStateSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(PriorityWorkflowDialogRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, SerializerOptions);
    }

    public static PriorityWorkflowDialogRuntimeState Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PriorityWorkflowDialogRuntimeState.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<PriorityWorkflowDialogRuntimeState>(value, SerializerOptions)
                ?? PriorityWorkflowDialogRuntimeState.Empty;
        }
        catch
        {
            return PriorityWorkflowDialogRuntimeState.Empty;
        }
    }
}
