using System.Text.RegularExpressions;
using Chummer.Presentation.Overview;

namespace Chummer.Presentation.Shell;

internal static partial class DesktopMouseFirstJourneyVisibleShellStateReader
{
    [GeneratedRegex(@"^(?:Workspace|Arbeitsbereich|Espace de travail|ワークスペース|工作区):\s*(?<workspaceId>.+?)\s+\((?:open|offen|ouverts|オープン|已打开|abertos):\s*(?<openCount>\d+),\s*(?<saveStatus>[^)]+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkspaceStripRegex();

    [GeneratedRegex(@"^(?:State|Status|Etat|状態|状态):\s*(?<readiness>.+?),\s*(?:workspace|arbeitsbereich|espace|workspace|工作区)=(?<workspaceId>.+?),\s*(?:open|offen|ouvert|open|aberto|open)=(?<openCount>\d+),\s*(?:saved|gespeichert|sauvegarde|saved|salvo|saved)=(?<saveStatus>.+?),\s*(?:last-command|letzter-befehl|derniere-commande|last-command|ultimo-comando|last-command)=(?<lastCommand>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolStripStatusRegex();

    public static DesktopMouseFirstJourneyVisibleShellState Read(
        string workspaceStripText,
        string toolStripStatusText,
        string characterStateText,
        string complianceStateText,
        string language)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        ParsedWorkspaceStripState workspaceState = ParseWorkspaceStripState(workspaceStripText, normalizedLanguage);
        ParsedWorkspaceStripState toolStripState = ParseToolStripStatusState(toolStripStatusText, normalizedLanguage);
        if (!workspaceState.HasActiveWorkspace && toolStripState.HasActiveWorkspace)
        {
            workspaceState = toolStripState;
        }

        bool characterLoaded = IsCharacterLoaded(characterStateText, normalizedLanguage);
        return new DesktopMouseFirstJourneyVisibleShellState(
            WorkspaceId: workspaceState.WorkspaceId,
            OpenCount: workspaceState.OpenCount,
            IsSaved: workspaceState.IsSaved,
            CharacterLoaded: characterLoaded,
            RulesetId: ParseRulesetId(complianceStateText),
            WorkspaceStripText: workspaceStripText?.Trim() ?? string.Empty,
            ToolStripStatusText: toolStripStatusText?.Trim() ?? string.Empty,
            CharacterStateText: characterStateText?.Trim() ?? string.Empty,
            ComplianceStateText: complianceStateText?.Trim() ?? string.Empty);
    }

    public static ParsedWorkspaceStripState ParseWorkspaceStripState(string? workspaceStripText, string language)
    {
        if (string.IsNullOrWhiteSpace(workspaceStripText))
        {
            return ParsedWorkspaceStripState.Empty;
        }

        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string normalizedText = workspaceStripText.Trim();
        Match match = WorkspaceStripRegex().Match(normalizedText);
        if (!match.Success)
        {
            return new ParsedWorkspaceStripState(null, 0, false, normalizedText);
        }

        string? workspaceId = match.Groups["workspaceId"].Value.Trim();
        string localizedNone = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", normalizedLanguage);
        if (string.Equals(workspaceId, localizedNone, StringComparison.OrdinalIgnoreCase))
        {
            workspaceId = null;
        }

        int openCount = int.TryParse(match.Groups["openCount"].Value, out int parsedOpenCount)
            ? parsedOpenCount
            : 0;
        string saveStatus = match.Groups["saveStatus"].Value.Trim();
        string localizedSaved = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.saved", normalizedLanguage);
        bool isSaved = string.Equals(saveStatus, localizedSaved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(saveStatus, "saved", StringComparison.OrdinalIgnoreCase);
        return new ParsedWorkspaceStripState(workspaceId, openCount, isSaved, normalizedText);
    }

    public static ParsedWorkspaceStripState ParseToolStripStatusState(string? toolStripStatusText, string language)
    {
        if (string.IsNullOrWhiteSpace(toolStripStatusText))
        {
            return ParsedWorkspaceStripState.Empty;
        }

        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string normalizedText = toolStripStatusText.Trim();
        Match match = ToolStripStatusRegex().Match(normalizedText);
        if (!match.Success)
        {
            return ParsedWorkspaceStripState.Empty;
        }

        string? workspaceId = match.Groups["workspaceId"].Value.Trim();
        string localizedNone = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", normalizedLanguage);
        if (string.Equals(workspaceId, localizedNone, StringComparison.OrdinalIgnoreCase))
        {
            workspaceId = null;
        }

        int openCount = int.TryParse(match.Groups["openCount"].Value, out int parsedOpenCount)
            ? parsedOpenCount
            : 0;
        string saveStatus = match.Groups["saveStatus"].Value.Trim();
        string localizedSaved = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.saved", normalizedLanguage);
        bool isSaved = string.Equals(saveStatus, localizedSaved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(saveStatus, "saved", StringComparison.OrdinalIgnoreCase);
        return new ParsedWorkspaceStripState(workspaceId, openCount, isSaved, normalizedText);
    }

    public static bool IsCharacterLoaded(string? characterStateText, string language)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(language);
        string localizedLoaded = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.loaded", normalizedLanguage);
        string expectedLoadedText = DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.status.character",
            normalizedLanguage,
            localizedLoaded);
        return string.Equals(characterStateText?.Trim(), expectedLoadedText, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ParseRulesetId(string? complianceStateText)
    {
        if (string.IsNullOrWhiteSpace(complianceStateText))
        {
            return null;
        }

        string normalized = complianceStateText.Trim();
        if (normalized.Contains(".chum4", StringComparison.OrdinalIgnoreCase))
        {
            return "sr4";
        }

        if (normalized.Contains(".chum5", StringComparison.OrdinalIgnoreCase))
        {
            return "sr5";
        }

        if (normalized.Contains(".chum6", StringComparison.OrdinalIgnoreCase))
        {
            return "sr6";
        }

        return null;
    }
}

internal readonly record struct ParsedWorkspaceStripState(
    string? WorkspaceId,
    int OpenCount,
    bool IsSaved,
    string RawText)
{
    public static ParsedWorkspaceStripState Empty => new(null, 0, false, string.Empty);

    public bool HasActiveWorkspace => !string.IsNullOrWhiteSpace(WorkspaceId);
}

internal readonly record struct DesktopMouseFirstJourneyVisibleShellState(
    string? WorkspaceId,
    int OpenCount,
    bool IsSaved,
    bool CharacterLoaded,
    string? RulesetId,
    string WorkspaceStripText,
    string ToolStripStatusText,
    string CharacterStateText,
    string ComplianceStateText)
{
    public bool HasActiveWorkspace => !string.IsNullOrWhiteSpace(WorkspaceId) || CharacterLoaded;
}
