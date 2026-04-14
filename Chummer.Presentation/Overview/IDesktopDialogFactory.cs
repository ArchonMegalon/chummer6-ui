using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IDesktopDialogFactory
{
    DesktopDialogState CreateMetadataDialog(
        CharacterProfileSection? profile,
        DesktopPreferenceState preferences);

    DesktopDialogState CreateCommandDialog(
        string commandId,
        CharacterProfileSection? profile,
        DesktopPreferenceState preferences,
        string? activeSectionJson,
        CharacterWorkspaceId? currentWorkspace,
        string? rulesetId,
        RuntimeInspectorProjection? runtimeInspector = null,
        MasterIndexResponse? masterIndex = null,
        TranslatorLanguagesResponse? translatorLanguages = null,
        IReadOnlyList<OpenWorkspaceState>? openWorkspaces = null);

    DesktopDialogState CreateUiControlDialog(
        string controlId,
        DesktopPreferenceState preferences);
}
