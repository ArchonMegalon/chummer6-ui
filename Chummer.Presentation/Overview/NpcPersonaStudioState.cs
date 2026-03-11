using System.Text.Json.Nodes;

namespace Chummer.Presentation.Overview;

public sealed record NpcPersonaStudioState(
    string DefaultPersonaId,
    string SelectedPersonaId,
    string PromptPolicy,
    IReadOnlyList<NpcPersonaDescriptorState> Personas,
    IReadOnlyList<NpcPersonaRoutePolicyState> Policies,
    IReadOnlyList<string> EvidenceLines,
    bool HasDraftPolicies,
    bool HasApprovedPolicies);

public sealed record NpcPersonaDescriptorState(
    string PersonaId,
    string Label,
    bool EvidenceFirst,
    string Summary,
    string Provenance,
    string ApprovalState,
    bool IsSelected);

public sealed record NpcPersonaRoutePolicyState(
    string RouteType,
    string RouteClassId,
    string PersonaId,
    string PrimaryProviderId,
    bool ToolingEnabled,
    string ApprovalState,
    IReadOnlyList<string> AllowedToolIds);

public static class NpcPersonaStudioProjector
{
    public static NpcPersonaStudioState? TryProject(JsonNode? node, NpcPersonaStudioState? previousState = null)
    {
        if (node is not JsonObject payload)
        {
            return null;
        }

        string defaultPersonaId = ReadString(payload, "defaultPersonaId");
        string promptPolicy = ReadString(payload, "promptPolicy");
        NpcPersonaDescriptorState[] personas = ProjectPersonas(payload, defaultPersonaId);
        NpcPersonaRoutePolicyState[] policies = ProjectPolicies(payload);

        if (personas.Length == 0 && policies.Length == 0)
        {
            return null;
        }

        string selectedPersonaId = ResolveSelectedPersonaId(personas, defaultPersonaId, previousState);
        if (string.IsNullOrWhiteSpace(defaultPersonaId))
        {
            defaultPersonaId = selectedPersonaId;
        }

        personas = personas
            .Select(persona => persona with
            {
                IsSelected = string.Equals(persona.PersonaId, selectedPersonaId, StringComparison.Ordinal)
            })
            .ToArray();

        string[] evidenceLines = BuildEvidenceLines(personas, policies, selectedPersonaId, promptPolicy);
        bool hasDraftPolicies = policies.Any(policy => string.Equals(policy.ApprovalState, "draft", StringComparison.OrdinalIgnoreCase));
        bool hasApprovedPolicies = policies.Any(policy => string.Equals(policy.ApprovalState, "approved", StringComparison.OrdinalIgnoreCase));

        return new NpcPersonaStudioState(
            DefaultPersonaId: defaultPersonaId,
            SelectedPersonaId: selectedPersonaId,
            PromptPolicy: promptPolicy,
            Personas: personas,
            Policies: policies,
            EvidenceLines: evidenceLines,
            HasDraftPolicies: hasDraftPolicies,
            HasApprovedPolicies: hasApprovedPolicies);
    }

    private static NpcPersonaDescriptorState[] ProjectPersonas(JsonObject payload, string defaultPersonaId)
    {
        JsonArray personas = ReadArray(payload, "personas");
        return personas
            .OfType<JsonObject>()
            .Select(persona =>
            {
                string personaId = ReadString(persona, "personaId");
                if (string.IsNullOrWhiteSpace(personaId))
                {
                    return null;
                }

                string label = FirstNonBlank(
                    ReadString(persona, "displayName"),
                    ReadString(persona, "label"),
                    personaId);
                bool evidenceFirst = ReadBool(persona, "evidenceFirst");
                string summary = FirstNonBlank(
                    ReadString(persona, "summary"),
                    ReadString(persona, "description"),
                    ReadString(persona, "notes"),
                    "No persona summary supplied.");
                string provenance = FirstNonBlank(
                    ReadString(persona, "provenance"),
                    ReadString(persona, "source"),
                    "persona-descriptor");
                string approvalState = ResolveApprovalState(persona);

                return new NpcPersonaDescriptorState(
                    PersonaId: personaId,
                    Label: label,
                    EvidenceFirst: evidenceFirst,
                    Summary: summary,
                    Provenance: provenance,
                    ApprovalState: approvalState,
                    IsSelected: string.Equals(personaId, defaultPersonaId, StringComparison.Ordinal));
            })
            .Where(persona => persona is not null)
            .Cast<NpcPersonaDescriptorState>()
            .ToArray();
    }

    private static NpcPersonaRoutePolicyState[] ProjectPolicies(JsonObject payload)
    {
        JsonArray policies = ReadArray(payload, "routePolicies");
        return policies
            .OfType<JsonObject>()
            .Select(policy =>
            {
                string routeType = ReadString(policy, "routeType");
                if (string.IsNullOrWhiteSpace(routeType))
                {
                    return null;
                }

                JsonArray allowedTools = ReadArray(policy, "allowedTools");
                string[] toolIds = allowedTools
                    .OfType<JsonObject>()
                    .Select(tool => ReadString(tool, "toolId"))
                    .Where(toolId => !string.IsNullOrWhiteSpace(toolId))
                    .ToArray();
                return new NpcPersonaRoutePolicyState(
                    RouteType: routeType,
                    RouteClassId: ReadString(policy, "routeClassId"),
                    PersonaId: ReadString(policy, "personaId"),
                    PrimaryProviderId: ReadString(policy, "primaryProviderId"),
                    ToolingEnabled: ReadBool(policy, "toolingEnabled"),
                    ApprovalState: ResolveApprovalState(policy),
                    AllowedToolIds: toolIds);
            })
            .Where(policy => policy is not null)
            .Cast<NpcPersonaRoutePolicyState>()
            .ToArray();
    }

    private static string ResolveSelectedPersonaId(
        IReadOnlyList<NpcPersonaDescriptorState> personas,
        string defaultPersonaId,
        NpcPersonaStudioState? previousState)
    {
        if (!string.IsNullOrWhiteSpace(previousState?.SelectedPersonaId)
            && personas.Any(persona => string.Equals(persona.PersonaId, previousState.SelectedPersonaId, StringComparison.Ordinal)))
        {
            return previousState.SelectedPersonaId!;
        }

        if (!string.IsNullOrWhiteSpace(defaultPersonaId)
            && personas.Any(persona => string.Equals(persona.PersonaId, defaultPersonaId, StringComparison.Ordinal)))
        {
            return defaultPersonaId;
        }

        return personas.FirstOrDefault()?.PersonaId ?? string.Empty;
    }

    private static string[] BuildEvidenceLines(
        IReadOnlyList<NpcPersonaDescriptorState> personas,
        IReadOnlyList<NpcPersonaRoutePolicyState> policies,
        string selectedPersonaId,
        string promptPolicy)
    {
        List<string> lines = [];
        if (!string.IsNullOrWhiteSpace(promptPolicy))
        {
            lines.Add($"Prompt policy: {promptPolicy}");
        }

        NpcPersonaDescriptorState? selectedPersona = personas.FirstOrDefault(persona =>
            string.Equals(persona.PersonaId, selectedPersonaId, StringComparison.Ordinal));
        if (selectedPersona is not null)
        {
            lines.Add($"Persona summary: {selectedPersona.Summary}");
            lines.Add($"Persona provenance: {selectedPersona.Provenance}");
        }

        foreach (NpcPersonaRoutePolicyState policy in policies
                     .Where(policy => string.IsNullOrWhiteSpace(policy.PersonaId)
                                      || string.Equals(policy.PersonaId, selectedPersonaId, StringComparison.Ordinal))
                     .Take(3))
        {
            string routeClass = string.IsNullOrWhiteSpace(policy.RouteClassId) ? "none" : policy.RouteClassId;
            string provider = string.IsNullOrWhiteSpace(policy.PrimaryProviderId) ? "none" : policy.PrimaryProviderId;
            lines.Add($"{policy.RouteType}: class {routeClass} · provider {provider}");
        }

        return lines.ToArray();
    }

    private static string ResolveApprovalState(JsonObject payload)
    {
        string explicitState = ReadString(payload, "approvalState");
        if (!string.IsNullOrWhiteSpace(explicitState))
        {
            return explicitState;
        }

        if (ReadBool(payload, "isApproved") || ReadBool(payload, "approved"))
        {
            return "approved";
        }

        if (ReadBool(payload, "isDraft") || ReadBool(payload, "draft"))
        {
            return "draft";
        }

        return "draft";
    }

    private static string ReadString(JsonObject payload, string propertyName)
    {
        foreach ((string key, JsonNode? value) in payload)
        {
            if (!string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return value?.GetValue<string>() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool ReadBool(JsonObject payload, string propertyName)
    {
        foreach ((string key, JsonNode? value) in payload)
        {
            if (!string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return value?.GetValue<bool>() ?? false;
        }

        return false;
    }

    private static JsonArray ReadArray(JsonObject payload, string propertyName)
    {
        foreach ((string key, JsonNode? value) in payload)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase) && value is JsonArray array)
            {
                return array;
            }
        }

        return [];
    }

    private static string FirstNonBlank(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }
}
