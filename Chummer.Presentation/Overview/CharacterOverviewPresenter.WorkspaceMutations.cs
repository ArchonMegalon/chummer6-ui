using System.Text;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private async Task ApplyQuickAddAsync(WorkspaceQuickAddRequest request, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with { Error = "No workspace loaded." });
            return;
        }

        CommandResult<WorkspaceDownloadReceipt> download = await _client.DownloadAsync(currentWorkspace.Value, ct);
        if (!download.Success || download.Value is null)
        {
            Publish(State with { Error = download.Error ?? "Workspace download failed." });
            return;
        }

        string xml = Encoding.UTF8.GetString(Convert.FromBase64String(download.Value.ContentBase64));
        string mutatedXml;
        try
        {
            mutatedXml = WorkspaceXmlMutationCatalog.ApplyQuickAdd(xml, request);
        }
        catch (Exception ex)
        {
            Publish(State with { Error = ex.Message });
            return;
        }

        CharacterWorkspaceId previousWorkspaceId = currentWorkspace.Value;
        string? returnTabId = State.ActiveTabId;
        string? returnActionId = State.ActiveActionId;
        string? returnSectionId = State.ActiveSectionId;
        string rulesetId = RulesetDefaults.NormalizeOptional(download.Value.RulesetId)
            ?? ResolveWorkspaceRulesetId(previousWorkspaceId)
            ?? RulesetDefaults.Sr5;

        await ImportAsync(new WorkspaceImportDocument(mutatedXml, rulesetId, WorkspaceDocumentFormat.NativeXml), ct);
        if (!string.IsNullOrWhiteSpace(State.Error) || State.WorkspaceId is null)
        {
            return;
        }

        if (!string.Equals(State.WorkspaceId.Value.Value, previousWorkspaceId.Value, StringComparison.Ordinal))
        {
            await CloseWorkspaceAsync(previousWorkspaceId, ct);
            if (!string.IsNullOrWhiteSpace(State.Error))
            {
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(returnSectionId)
            && !string.Equals(returnSectionId, "summary", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(returnSectionId, "validate", StringComparison.OrdinalIgnoreCase))
        {
            await LoadSectionAsync(returnSectionId, returnTabId, returnActionId, ct);
        }
    }
}
